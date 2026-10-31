// MaintenanceService.cs
// The daily jobs of the host: the purge of the trash past its retention and the removal of the audit events past theirs (annex A 2.15, spec 5.5).

using Microsoft.Extensions.Options;
using Topogenia.Application.Audit;
using Topogenia.Application.Maintenance;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;

namespace Topogenia.Host.Maintenance;

/// <summary>
/// Runs once the host has started, then every <see cref="Period"/> (A-2.15-19: at the start then every 24 hours, as the
/// 1.x): the purge of the trash (<see cref="ExpiredPurge"/>), then the removal of the audit events older than the
/// retention of the configuration (`AuditRetentionDays`, a year by default, spec 5.5). Each run has its own scope, so its
/// own session of the database. A failure is logged by its type and the project concerned, never by its message (ADR
/// 036), and never stops the host nor the next run.
/// </summary>
public sealed partial class MaintenanceService(IServiceScopeFactory scopes, IClock clock, IOptions<ProductOptions> options, ILogger<MaintenanceService> logger)
    : BackgroundService
{
    public static readonly TimeSpan Period = TimeSpan.FromHours(24);

    /// <summary>Runs the jobs once; the tests call it directly.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        try
        {
            var report = await services.GetRequiredService<ExpiredPurge>().RunAsync(cancellationToken);
            foreach (var purged in report.Purged)
                LogPurged(logger, purged.ProjectId, purged.Whole, purged.Batches, purged.Outcome.Records);
            foreach (var failure in report.Failures)
                LogPurgeFailed(logger, failure.ProjectId, failure.Failure.GetType().FullName ?? failure.Failure.GetType().Name);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            LogJobFailed(logger, "trash", failure.GetType().FullName ?? failure.GetType().Name);
        }
        try
        {
            var limit = clock.UtcNow.AddDays(-AuditTrail.RetentionDays(options.Value.AuditRetentionDays));
            var removed = await services.GetRequiredService<IAuditStore>().RemoveBeforeAsync(limit, cancellationToken);
            if (removed > 0)
                LogAuditRemoved(logger, removed);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            LogJobFailed(logger, "audit", failure.GetType().FullName ?? failure.GetType().Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Period);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Trash purged: project {ProjectId} (whole {Whole}, {Batches} batches, {Records} records).")]
    private static partial void LogPurged(ILogger logger, Guid projectId, bool whole, int batches, int records);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Purge of the project {ProjectId} failed: {Type}; the next run takes it again.")]
    private static partial void LogPurgeFailed(ILogger logger, Guid projectId, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Audit events past the retention removed: {Count}.")]
    private static partial void LogAuditRemoved(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Daily job {Job} failed: {Type}.")]
    private static partial void LogJobFailed(ILogger logger, string job, string type);
}

/// <summary>Registration of the daily jobs.</summary>
public static class MaintenanceServices
{
    public static IServiceCollection AddMaintenance(this IServiceCollection services)
    {
        services.AddScoped(provider => new ExpiredPurge(provider.GetRequiredService<IClock>(), provider.GetRequiredService<ITrashPurge>(),
            provider.GetRequiredService<IWorkspaceStore>(), provider.GetRequiredService<Domain.Model.AppSettings>(), provider.GetRequiredService<IAuditStore>()));
        services.AddSingleton<MaintenanceService>();
        services.AddHostedService(provider => provider.GetRequiredService<MaintenanceService>());
        return services;
    }
}
