// MaintenanceStartTests.cs
// The daily service is a hosted service of the host and runs at its start, unasked: trash and audit events past their retention go, the retention configured (A-2.15-19, ADR 046, spec 5.5).

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Topogenia.Application.Audit;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Host.Composition;
using Topogenia.Host.Maintenance;
using Topogenia.Host.Tests.Desktop;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Logging;
using Topogenia.Host.Tests.Server;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Maintenance;

/// <summary>
/// The start is the one of a variant of the host on the same base (<see cref="ApiHost.WithServicesAsync"/>), whose clock
/// is set past the retention of the trash: no request asks for the purge, no test calls
/// <see cref="MaintenanceService.RunOnceAsync"/>; the state of the base is read through the first host, whose own run at
/// its start came before the records of the test.
/// </summary>
public sealed class MaintenanceStartTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_daily_service_is_a_hosted_service_of_the_host_run_every_24_hours(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var service = host.Services.GetRequiredService<MaintenanceService>();
        Assert.Contains(service, host.Services.GetServices<IHostedService>());
        Assert.Equal(TimeSpan.FromHours(24), MaintenanceService.Period);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_host_that_starts_purges_the_expired_trash_and_audit_events_without_being_asked(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var options = host.Services.GetRequiredService<IOptions<ProductOptions>>().Value;
        var project = await ApiProject.CreateAsync(host);
        using (var facilitator = project.Facilitator)
        {
            var version = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("version").GetInt32();
            (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", version)).Ok(204);
        }
        var later = DateTime.UtcNow.AddDays(options.TrashRetentionDays + 1);
        var kindOf = $"test.{Guid.CreateVersion7():N}";
        var (old, recent) = (later.AddDays(-options.AuditRetentionDays - 1), later.AddDays(-options.AuditRetentionDays + 1));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IAuditStore>();
            await store.AppendAsync(new AuditEvent(Guid.CreateVersion7(), old, null, kindOf, null, null), Token);
            await store.AppendAsync(new AuditEvent(Guid.CreateVersion7(), recent, null, kindOf, null, null), Token);
        }
        Assert.Empty(await EventsAsync(host.Services, project.Id, AuditKinds.ProjectPurged));

        await host.WithServicesAsync(services => services.AddSingleton<IClock>(new SettableClock(later)), Logins.Admin,
            async _ => Assert.True(await EventuallyAsync(async () => (await EventsAsync(host.Services, project.Id, AuditKinds.ProjectPurged)).Count == 1
                && (await EventsAsync(host.Services, null, kindOf)).Count == 1), $"The start of the host did not run the daily jobs within {Patience}."));

        Assert.Equal([recent.Date], (await EventsAsync(host.Services, null, kindOf)).Select(audit => audit.At.Date));
        using var admin = project.Admin;
        var trash = (await admin.GetAsync("/api/trash")).Ok().GetProperty("projects").EnumerateArray();
        Assert.DoesNotContain(project.Id, trash.Select(item => item.GetProperty("id").GetGuid()));
        (await admin.PostAsync($"/api/trash/{project.Id}/restore")).Refused(404, "INTROUVABLE");
    }

    /// <summary>
    /// The retention of the audit trail is a setting (spec 5.5, `AuditRetentionDays`): a host started for real, its
    /// configuration giving 90 days, removes by its daily job the event of 91 days and keeps the one of 89, which the year
    /// by default would both keep. SQLite in desktop mode, SQL Server in server mode.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_configured_audit_retention_removes_the_events_past_it_and_keeps_the_younger(EngineKind kind)
    {
        var settings = new Dictionary<string, string?> { [$"{ProductOptions.Section}:AuditRetentionDays"] = "90" };
        if (kind == EngineKind.Sqlite)
        {
            await using var desktop = await DesktopRun.StartAsync(settings);
            await AssertRetentionOf90DaysAsync(desktop.Services);
        }
        else
        {
            await using var server = await ServerRun.StartAsync(settings);
            await AssertRetentionOf90DaysAsync(server.Services);
        }
    }

    private static async Task AssertRetentionOf90DaysAsync(IServiceProvider services)
    {
        Assert.Equal(90, services.GetRequiredService<IOptions<ProductOptions>>().Value.AuditRetentionDays);
        var now = services.GetRequiredService<IClock>().UtcNow;
        var kindOf = $"test.{Guid.CreateVersion7():N}";
        var (old, young) = (new AuditEvent(Guid.CreateVersion7(), now.AddDays(-91), null, kindOf, null, null),
            new AuditEvent(Guid.CreateVersion7(), now.AddDays(-89), null, kindOf, null, null));
        await using (var scope = services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IAuditStore>();
            await store.AppendAsync(old, Token);
            await store.AppendAsync(young, Token);
        }
        // The run of the start may meet these events too: it applies the same setting.
        await services.GetRequiredService<MaintenanceService>().RunOnceAsync(Token);

        Assert.Equal([young.Id], (await EventsAsync(services, null, kindOf)).Select(audit => audit.Id));
    }

    /// <summary>Waits until <paramref name="done"/>, read in the base, as long as <see cref="Patience"/>.</summary>
    private static async Task<bool> EventuallyAsync(Func<Task<bool>> done)
    {
        var until = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < until)
        {
            if (await done())
                return true;
            await Task.Delay(TimeSpan.FromMilliseconds(100), Token);
        }
        return false;
    }

    private static async Task<IReadOnlyList<AuditEvent>> EventsAsync(IServiceProvider services, Guid? projectId, string kind)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAuditStore>().ReadAsync(new AuditQuery(null, null, projectId, null, kind, 10), Token);
    }
}
