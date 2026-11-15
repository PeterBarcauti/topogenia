// ITrashPurge.cs
// Port of the physical purge of the trash and of the expired deletion batches, run in short transactions (spec 4.1, 4.5, 4.6).

using Topogenia.Domain.Trash;

namespace Topogenia.Application.Ports;

/// <summary>What a purge deleted: the records of the plan, the entries of the operation log that referred to them, the transactions.</summary>
public sealed record PurgeOutcome(int Records, int LogEntries, int Transactions);

/// <summary>
/// The physical side of the purge (spec 4.1: the 1.x procedures `purge_*` become C#, in batches, in short transactions).
/// The choices stay with the Application and the domain: the limit of the retention (`TrashRules.Limit`), the plan of a
/// project (`PurgePlanner.Expired` or `PurgePlanner.WholeProject`), the right of the administrator, the daily schedule and
/// the logging of a failure without blocking (annex A 2.15). This port finds the projects that may have something to
/// purge and deletes exactly the records of a plan, in its order, a few hundred per transaction, together with the
/// entries of the operation log that refer to what is purged (spec 4.6). It never updates a row: the histories of the
/// data are only deleted with their object (ADR 032), the audit stays (spec 5.5).
/// </summary>
public interface ITrashPurge
{
    /// <summary>
    /// The projects in the trash since before <paramref name="limit"/>, and those that hold a deletion batch deleted before
    /// it, in the order of their identifiers: the candidates of the daily purge, to plan one by one.
    /// </summary>
    Task<IReadOnlyList<Guid>> ProjectsToPurgeAsync(DateTime limit, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the records of <paramref name="plan"/>, computed on a workspace of the project read before. The operation
    /// log entries that refer to the purged batches or maps go first; then the records, in the order of the plan, in
    /// transactions of a bounded size, each under the lock of the project. Every transaction first checks that the project
    /// (for a whole purge) and the purged batches are still those of the plan: a project restored or a batch changed since
    /// refuses the rest of the purge with `CONFLIT_VERSION`, what was already purged staying purged (a new plan finishes it).
    /// </summary>
    Task<PurgeOutcome> PurgeAsync(Guid projectId, PurgePlan plan, CancellationToken cancellationToken);
}
