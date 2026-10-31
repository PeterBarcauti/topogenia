// StructureReply.cs
// The reply of a structure command: what was created or touched, the operation, the warnings and the map up to date (AR-50, A-1.1-16).

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;

namespace Topogenia.Host.Api;

/// <summary>
/// The reply of every command on a map (1.x `ResultatStructure` without `lot`, `liens_crees`, `liens_coupes`, AR-50):
/// `id`, `code` and `ids` of the objects created or touched (the client selects the new steps with them), `operation_id`
/// (what undo will take back; null when the command cannot be undone or changed nothing), `warnings` of the operation,
/// `structure_version` of the project, the `map` up to date with its layout warnings, and the `history` of the user on it.
/// </summary>
public sealed record StructureReply(
    Guid? Id,
    string? Code,
    IReadOnlyList<Guid> Ids,
    Guid? OperationId,
    IReadOnlyList<string> Warnings,
    int StructureVersion,
    MapView? Map,
    HistoryView? History)
{
    /// <summary>The reply of an outcome, with what the endpoint reads of its result.</summary>
    public static StructureReply Of<TResult>(CommandOutcome<TResult> outcome, Guid? id = null, string? code = null, IReadOnlyList<Guid>? ids = null)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new StructureReply(id, code, ids ?? [], outcome.OperationId, outcome.Warnings, outcome.StructureVersion, outcome.View, outcome.History);
    }
}
