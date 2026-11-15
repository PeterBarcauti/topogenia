// PurgeGuard.cs
// What a purge plan must still match in the database when each of its transactions starts: the trashed project, the purged batches.

using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Domain.Trash;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Purge;

/// <summary>
/// A plan is computed on a workspace read before the purge; the purge then runs in several transactions. Between them, a
/// facilitator may restore the project from the trash, an undo may restore a batch. Each transaction therefore checks,
/// under the lock of the project, that what is left of the plan still holds: the project of a whole purge still has the
/// version and the deletion of the plan (still in the trash), and each batch still to purge is still the row of the plan.
/// Otherwise the purge stops with `CONFLIT_VERSION`; what an earlier transaction purged stays purged (the plan is in the
/// order of the references, so the database stays consistent) and a new plan, read again, finishes it.
/// </summary>
internal sealed class PurgeGuard
{
    private readonly Guid projectId;

    private PurgeGuard(Guid projectId) => this.projectId = projectId;

    /// <summary>The guard of a plan of this project; a plan that names another project, or a whole purge of a live one, is a bug of the caller.</summary>
    public static PurgeGuard Of(Guid projectId, PurgePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var projects = plan.Records.OfType<Project>().ToList();
        if (plan.PurgesProject != (projects.Count == 1) || projects.Count > 1 || projects.Any(project => project.Id != projectId || !project.IsInTrash))
            throw new ArgumentException("A whole purge holds its trashed project once, a purge of batches holds none.", nameof(plan));
        var batches = plan.Records.OfType<DeletionBatch>().ToList();
        if (batches.Any(batch => batch.ProjectId != projectId)
            || !batches.Select(batch => batch.Id).Order().SequenceEqual(plan.PurgedBatchIds.Order()))
            throw new ArgumentException("The purged batches of a plan are batches of its project, each listed once.", nameof(plan));
        return new PurgeGuard(projectId);
    }

    /// <summary>Refuses with `CONFLIT_VERSION` when the project or a batch among the records still to purge changed since the plan.</summary>
    public async Task EnsureAsync(AppDbContext context, IReadOnlyCollection<IRecord> remaining, CancellationToken cancellationToken)
    {
        if (remaining.OfType<Project>().SingleOrDefault() is { } planned)
        {
            var row = await context.Set<Project>().AsNoTracking().SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);
            if (row is null || row.Version != planned.Version || row.DeletedAt != planned.DeletedAt)
                throw StoreRefusals.VersionConflict(planned.Key.Type, row?.UpdatedBy, row?.UpdatedAt);
        }
        var batches = remaining.OfType<DeletionBatch>().ToList();
        foreach (var chunk in Chunks.Of(batches.Select(batch => batch.Id)))
        {
            var rows = await context.Set<DeletionBatch>().AsNoTracking()
                .Where(batch => batch.ProjectId == projectId && chunk.Contains(batch.Id))
                .ToDictionaryAsync(batch => batch.Id, cancellationToken);
            if (batches.Where(batch => chunk.Contains(batch.Id)).FirstOrDefault(batch => !rows.TryGetValue(batch.Id, out var row) || row != batch) is { } changed)
                throw StoreRefusals.VersionConflict(changed.Key.Type, rows.GetValueOrDefault(changed.Id)?.RestoredBy, rows.GetValueOrDefault(changed.Id)?.RestoredAt);
        }
    }
}
