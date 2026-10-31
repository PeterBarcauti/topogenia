// ExpiredPurge.cs
// The daily purge of the trash: every project in the trash past the retention and every expired batch, one project at a time (annex A 2.15).

using Topogenia.Application.Audit;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Trash;

namespace Topogenia.Application.Maintenance;

/// <summary>What one project gave to the purge.</summary>
public sealed record PurgedProject(Guid ProjectId, bool Whole, int Batches, PurgeOutcome Outcome);

/// <summary>A project whose purge failed, with the failure for the log of the host (identifiers and codes only, spec 5.2).</summary>
public sealed record PurgeFailure(Guid ProjectId, Exception Failure);

/// <summary>What a run of the purge did: the projects purged and those that failed; the others had nothing expired.</summary>
public sealed record PurgeReport(DateTime Limit, IReadOnlyList<PurgedProject> Purged, IReadOnlyList<PurgeFailure> Failures);

/// <summary>
/// The daily purge (annex A 2.15, `ServiceCorbeille.cs:108`, the 1.x `purge_trash`): the candidates of <see cref="ITrashPurge"/>
/// before the limit of the retention (`TrashRules.Limit`, A-2.15-20), then, one project at a time, its workspace read in
/// one consistent read, the plan of the domain (`PurgePlanner.Expired`: the whole project in the trash past the retention,
/// A-2.15-21, else its expired batches except the batch of an open frame, A-2.15-22) and its deletion in short
/// transactions. A failure is kept in the report and never stops the other projects (A-2.15-24): the next run takes it
/// again. It is a job of the host, at the start then every 24 hours (A-2.15-19), not a request: no user and no right, so it
/// does not go through <see cref="Commands.UserRunner"/>. A whole project purged leaves an audit event without user
/// (spec 5.5); an expired batch does not, being part of the life of a project.
/// </summary>
public sealed class ExpiredPurge
{
    private readonly IClock clock;
    private readonly ITrashPurge purge;
    private readonly IWorkspaceStore store;
    private readonly AppSettings settings;
    private readonly IAuditStore audit;

    public ExpiredPurge(IClock clock, ITrashPurge purge, IWorkspaceStore store, AppSettings settings, IAuditStore audit)
    {
        this.audit = audit;
        this.clock = clock;
        this.purge = purge;
        this.store = store;
        this.settings = settings;
    }

    public async Task<PurgeReport> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var limit = TrashRules.Limit(now, settings);
        var purged = new List<PurgedProject>();
        var failures = new List<PurgeFailure>();
        foreach (var projectId in await purge.ProjectsToPurgeAsync(limit, cancellationToken))
        {
            try
            {
                if (await PurgeAsync(projectId, now, cancellationToken) is { } done)
                    purged.Add(done);
            }
            catch (Exception failure) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add(new PurgeFailure(projectId, failure));
            }
        }
        return new PurgeReport(limit, purged, failures);
    }

    private async Task<PurgedProject?> PurgeAsync(Guid projectId, DateTime now, CancellationToken cancellationToken)
    {
        var workspace = await store.LoadAsync(projectId, WorkspaceContent.All, cancellationToken);
        if (workspace is null)
            return null;
        var plan = PurgePlanner.Expired(workspace, now, settings);
        if (plan.IsEmpty)
            return null;
        var outcome = await purge.PurgeAsync(projectId, plan, cancellationToken);
        if (plan.PurgesProject)
            await audit.AppendAsync(AuditTrail.OfJob(now, AuditKinds.ProjectPurged, projectId), cancellationToken);
        return new PurgedProject(projectId, plan.PurgesProject, plan.PurgedBatchIds.Count, outcome);
    }
}
