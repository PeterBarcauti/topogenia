// OperationRecorder.cs
// Logs a structure operation that has just been written: redo stack cleared, depth kept, entry appended (spec 4.6, ADR 042).

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.OperationLog;

/// <summary>
/// Writes the entry of a new operation of a user on a map, under the lock of the project: the redo stack of the user on
/// the map is cleared and the oldest operations beyond the depth are removed (spec 4.6), then the entry is appended with
/// the next sequence of the map. Its identifier is the identifier of the operation, which the deletion batches it created
/// carry (`deletion_batch.operation_id`): the purge of a batch finds its operation by it (spec 4.6).
/// </summary>
public static class OperationRecorder
{
    /// <summary>
    /// The inverse of an operation that has not been undone yet. The undo writes in `inverse_json` the change set it really
    /// applied, repairs included, which the redo needs (`UndoReplay.Redo`, ADR 042).
    /// </summary>
    public const string NotUndoneYet = "[]";

    public static async Task<Guid> RecordAsync(
        IOperationLog log, OperationContext context, Guid projectId, Guid mapId, string kind, ChangeSet changes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(context);
        var entries = await log.EntriesAsync(mapId, context.ActorId, cancellationToken);
        var cleared = OperationStack.ClearedByNewOperation(entries);
        if (cleared.Count > 0)
            await log.RemoveAsync(cleared, cancellationToken);
        var sequence = await log.NextSequenceAsync(mapId, cancellationToken);
        var entry = new OperationLogEntry(context.OperationId, projectId, mapId, context.ActorId, kind, sequence,
            ChangeSetJson.Write(changes), NotUndoneYet, ChangeSetJson.WriteTouched(changes), context.Now, null, null);
        await log.AppendAsync(entry, cancellationToken);
        return entry.Id;
    }
}
