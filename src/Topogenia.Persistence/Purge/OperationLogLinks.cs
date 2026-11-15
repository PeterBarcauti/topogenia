// OperationLogLinks.cs
// The entries of the operation log that refer to what a purge deletes, deleted with it (spec 4.6: the purge of the batches purges their operations).

using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Purge;

/// <summary>
/// An entry of `operation_log` refers to a purged batch when it is the operation that filled the batch
/// (`deletion_batch.operation_id`) or a redo of that operation (`redone_from_id`): undoing or redoing it would bring back
/// what no longer exists, so it goes with the batch (spec 4.6). An entry of a purged map goes with the map (its foreign
/// key), and every entry of a project purged whole goes with the project. Each deletion is one statement per packet of
/// 500 identifiers (spec 4.3-2), always limited to the project; the log is never updated.
/// </summary>
internal static class OperationLogLinks
{
    /// <summary>Deletes the entries of the operations of the batches and of their redos; returns how many were deleted.</summary>
    public static async Task<int> DeleteOfOperationsAsync(AppDbContext context, Guid projectId, IEnumerable<Guid> operationIds, CancellationToken cancellationToken)
    {
        var deleted = 0;
        foreach (var chunk in Chunks.Of(operationIds))
        {
            deleted += await Entries(context, projectId)
                .Where(entry => chunk.Contains(entry.Id) || (entry.RedoneFromId != null && chunk.Contains(entry.RedoneFromId.Value)))
                .ExecuteDeleteAsync(cancellationToken);
        }
        return deleted;
    }

    /// <summary>Deletes the entries written on these maps; returns how many were deleted.</summary>
    public static async Task<int> DeleteOfMapsAsync(AppDbContext context, Guid projectId, IEnumerable<Guid> mapIds, CancellationToken cancellationToken)
    {
        var deleted = 0;
        foreach (var chunk in Chunks.Of(mapIds))
            deleted += await Entries(context, projectId).Where(entry => chunk.Contains(entry.MapId)).ExecuteDeleteAsync(cancellationToken);
        return deleted;
    }

    /// <summary>Deletes every entry of the project; returns how many were deleted.</summary>
    public static Task<int> DeleteOfProjectAsync(AppDbContext context, Guid projectId, CancellationToken cancellationToken) =>
        Entries(context, projectId).ExecuteDeleteAsync(cancellationToken);

    private static IQueryable<OperationLogEntry> Entries(AppDbContext context, Guid projectId) =>
        context.Set<OperationLogEntry>().Where(entry => entry.ProjectId == projectId);
}
