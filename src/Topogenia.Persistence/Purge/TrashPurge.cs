// TrashPurge.cs
// Adapter of the purge port: the records of a purge plan deleted in their order, in short transactions, with their log entries (spec 4.1, 4.6).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Trash;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Purge;

/// <summary>
/// <see cref="ITrashPurge"/> on a <see cref="StoreSession"/>. The 1.x procedures `purge_project` and `purge_trash` ran one
/// long transaction with temporary tables (annex A 5, `db/006`, `db/007`); here the plan of the domain is deleted in
/// short units of work (<see cref="UnitOfWork.RunAsync{T}"/>: retried whole on a deadlock or a busy database), each under
/// the lock of the project (spec 4.7) and after the check of <see cref="PurgeGuard"/>:
/// 1. the log entries that refer to the purged batches or maps, and every entry of a project purged whole;
/// 2. the records of the plan, in its order (dependents before what they reference), at most
///    <see cref="RecordsPerTransaction"/> per transaction, each written as a removal of a change set
///    (<see cref="ChangeSetWriter"/>: version checked, record without version compared, refusal of a guard translated),
///    after the log entries of the maps it deletes.
/// Every committed prefix of the plan leaves a consistent database. Only deletes are sent: the histories of the data go
/// with their object and are never updated (ADR 032), the audit has no foreign key and stays (spec 5.5).
/// </summary>
public sealed class TrashPurge : ITrashPurge
{
    /// <summary>Records deleted by one transaction at most: a short transaction (spec 4.1), like a packet of identifiers (spec 4.3-2).</summary>
    public const int DefaultRecordsPerTransaction = Chunks.Size;

    private readonly StoreSession session;

    public TrashPurge(StoreSession session, int recordsPerTransaction = DefaultRecordsPerTransaction)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThan(recordsPerTransaction, 1);
        this.session = session;
        RecordsPerTransaction = recordsPerTransaction;
    }

    public int RecordsPerTransaction { get; }

    public Task<IReadOnlyList<Guid>> ProjectsToPurgeAsync(DateTime limit, CancellationToken cancellationToken)
    {
        if (limit.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The limit of the retention is a UTC moment (D08).", nameof(limit));
        return ConsistentRead.RunAsync<IReadOnlyList<Guid>>(session, async context =>
        {
            var trashed = context.Set<Project>().AsNoTracking().Where(project => project.DeletedAt != null && project.DeletedAt < limit).Select(project => project.Id);
            var holding = context.Set<DeletionBatch>().AsNoTracking().Where(batch => batch.DeletedAt < limit).Select(batch => batch.ProjectId);
            var projects = await trashed.Union(holding).ToListAsync(cancellationToken);
            return [.. projects.Distinct().Order()];
        }, cancellationToken);
    }

    public async Task<PurgeOutcome> PurgeAsync(Guid projectId, PurgePlan plan, CancellationToken cancellationToken)
    {
        var guard = PurgeGuard.Of(projectId, plan);
        if (plan.IsEmpty)
            return new PurgeOutcome(0, 0, 0);
        var unit = new UnitOfWork(session);
        var store = new WorkspaceStore(session);
        var logEntries = await unit.RunAsync(async token =>
        {
            var context = await LockedAsync(unit, guard, projectId, plan.Records, token);
            if (plan.PurgesProject)
                return await OperationLogLinks.DeleteOfProjectAsync(context, projectId, token);
            var operations = plan.Records.OfType<DeletionBatch>().Where(batch => batch.OperationId is not null).Select(batch => batch.OperationId!.Value);
            return await OperationLogLinks.DeleteOfOperationsAsync(context, projectId, operations, token)
                + await OperationLogLinks.DeleteOfMapsAsync(context, projectId, MapsOf(plan.Records), token);
        }, cancellationToken);
        var records = plan.Records.ToArray();
        var transactions = 1;
        for (var start = 0; start < records.Length; start += RecordsPerTransaction)
        {
            var remaining = new ArraySegment<IRecord>(records, start, records.Length - start);
            var chunk = remaining.Take(RecordsPerTransaction).ToList();
            logEntries += await unit.RunAsync(async token =>
            {
                var context = await LockedAsync(unit, guard, projectId, remaining, token);
                var removed = await OperationLogLinks.DeleteOfMapsAsync(context, projectId, MapsOf(chunk), token);
                await store.SaveAsync(new ChangeSet(chunk.Select(record => new RecordChange(ChangeKind.Removed, record, null)).ToList()), token);
                return removed;
            }, cancellationToken);
            transactions++;
        }
        return new PurgeOutcome(plan.Records.Count, logEntries, transactions);
    }

    /// <summary>Takes the lock of the project, then checks what remains of the plan; returns the context of the unit.</summary>
    private async Task<AppDbContext> LockedAsync(UnitOfWork unit, PurgeGuard guard, Guid projectId, IReadOnlyList<IRecord> remaining, CancellationToken cancellationToken)
    {
        await unit.LockProjectAsync(projectId, cancellationToken);
        await guard.EnsureAsync(session.Context, remaining, cancellationToken);
        return session.Context;
    }

    private static IEnumerable<Guid> MapsOf(IEnumerable<IRecord> records) => records.OfType<ProcessMap>().Select(map => map.Id);
}
