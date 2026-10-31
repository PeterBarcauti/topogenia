// OperationStack.cs
// The undo and redo stacks of a user on a map, read from the entries of the log as chains (spec 4.6, ADR 042).

using Topogenia.Domain.Model;

namespace Topogenia.Application.OperationLog;

/// <summary>
/// One logical operation of the stack: the entry written by the command and the entries of its redos, which point to it by
/// `redone_from_id` (ADR 042). Only the latest entry of a chain matters: live, the operation can be undone; undone, it can
/// be redone. The older entries of a chain stay until the chain leaves the stack, so that the purge of a batch reaches
/// every redo of its operation (spec 4.6) and no entry ever refers to a removed one.
/// </summary>
public sealed record OperationChain(Guid RootId, IReadOnlyList<OperationLogEntry> Entries)
{
    /// <summary>The entry that gives the state of the operation: the highest sequence of the chain.</summary>
    public OperationLogEntry Latest => Entries[0];

    /// <summary>
    /// The place of the operation in the stack: the sequence of the entry written by the command (the root, the lowest
    /// sequence of the chain). A redo writes a newer entry but puts the operation back at its own place.
    /// </summary>
    public int Place => Entries[^1].Sequence;

    public bool IsUndone => Latest.UndoneAt is not null;
}

/// <summary>An operation of the history: the chain, its kind and when its latest entry was written.</summary>
public sealed record HistoryItem(Guid OperationId, string Kind, DateTime At);

/// <summary>What the user can undo and redo on a map, the next one first (annex B 2.6: it survives a reload).</summary>
public sealed record HistoryView(IReadOnlyList<HistoryItem> Undo, IReadOnlyList<HistoryItem> Redo);

/// <summary>Pure reading of the stacks of a user on a map: depth, undo order, redo order, what a new operation clears.</summary>
public static class OperationStack
{
    /// <summary>Operations kept per map and per user (spec 4.6, as the 1.x).</summary>
    public const int Depth = 50;

    /// <summary>The chains of the entries, the latest place in the stack first (<see cref="OperationChain.Place"/>).</summary>
    public static IReadOnlyList<OperationChain> Chains(IEnumerable<OperationLogEntry> entries) =>
        entries.GroupBy(entry => entry.RedoneFromId ?? entry.Id)
            .Select(chain => new OperationChain(chain.Key, chain.OrderByDescending(entry => entry.Sequence).ToList()))
            .OrderByDescending(chain => chain.Place)
            .ToList();

    /// <summary>The operations that can be undone, the next one to undo first: the live one of the latest place.</summary>
    public static IReadOnlyList<OperationChain> Undoable(IEnumerable<OperationLogEntry> entries) =>
        Chains(entries).Where(chain => !chain.IsUndone).ToList();

    /// <summary>
    /// The operations that can be redone, the next one to redo first: the one undone last. The live operations are the
    /// bottom of the stack and the undone ones its top, both by place (an undo takes the live one of the latest place, a
    /// redo the undone one of the earliest, a new operation clears the undone ones): the one undone last is the undone
    /// operation of the earliest place, whatever the sequence of its latest entry (a redo undone again keeps its place).
    /// </summary>
    public static IReadOnlyList<OperationChain> Redoable(IEnumerable<OperationLogEntry> entries) =>
        Chains(entries).Where(chain => chain.IsUndone).OrderBy(chain => chain.Place).ToList();

    /// <summary>The history of a user on a map, read from his entries.</summary>
    public static HistoryView History(IReadOnlyCollection<OperationLogEntry> entries) =>
        new(Undoable(entries).Select(Item).ToList(), Redoable(entries).Select(Item).ToList());

    private static HistoryItem Item(OperationChain chain) => new(chain.RootId, chain.Latest.Kind, chain.Latest.UndoneAt ?? chain.Latest.CreatedAt);

    /// <summary>
    /// The entries to remove before a new operation is logged: the whole redo stack (spec 4.6: a new operation clears it)
    /// and the oldest operations beyond the depth, so that the stack holds the depth once the new one is added.
    /// </summary>
    public static IReadOnlyList<Guid> ClearedByNewOperation(IReadOnlyCollection<OperationLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var beyondDepth = Undoable(entries).Skip(Depth - 1);
        return Redoable(entries).Concat(beyondDepth).SelectMany(chain => chain.Entries).Select(entry => entry.Id).ToList();
    }
}
