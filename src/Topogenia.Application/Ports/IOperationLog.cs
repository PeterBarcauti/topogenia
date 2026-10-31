// IOperationLog.cs
// Port of the operation log read and written by the command template, undo and redo (spec 4.6, D05, ADR 042).

using Topogenia.Domain.Model;

namespace Topogenia.Application.Ports;

/// <summary>
/// The operation log of the maps (table `operation_log`, spec 4.6). Every write happens inside the transaction of the
/// current unit of work, under the lock of the project; <see cref="EntriesAsync"/> also reads outside a unit, in a read
/// transaction of its own, for the history returned with the read of a map. The rules of the stack (chains of an operation and its redos, depth of 50 per
/// map and user, redo stack cleared by a new operation, ADR 042) belong to the Application (`OperationLog/`); this port
/// only reads and writes entries.
/// </summary>
public interface IOperationLog
{
    /// <summary>The next sequence of a map: one more than the highest sequence of its entries, 0 for the first.</summary>
    Task<int> NextSequenceAsync(Guid mapId, CancellationToken cancellationToken);

    /// <summary>Every entry of a user on a map, highest sequence first (the stack is kept short by the depth).</summary>
    Task<IReadOnlyList<OperationLogEntry>> EntriesAsync(Guid mapId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Adds the entry of an operation that has just been written.</summary>
    Task AppendAsync(OperationLogEntry entry, CancellationToken cancellationToken);

    /// <summary>Replaces an entry (its undo time, the changes of its undo); the entry keeps its identifier and its sequence.</summary>
    Task UpdateAsync(OperationLogEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Removes entries, whole chains at a time (an entry and every entry redone from it), so that no remaining entry
    /// refers to a removed one; returns how many were removed.
    /// </summary>
    Task<int> RemoveAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken);
}
