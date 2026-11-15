// CommandOutcome.cs
// What the template returns for a command: the result of the domain, the logged operation, the warnings and the view (AR-50).

using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;

namespace Topogenia.Application.Commands;

/// <summary>
/// The reply of a command (spec 3.4 step 6, AR-50): the result of the domain (from which the host takes `id`, `code` and
/// `ids`), the identifier of the logged operation (null when the command cannot be undone or changed nothing), the new
/// structure version of the project, the warnings of the operation (PROFONDEUR_DEPASSEE, SOUS_CARTE_EXISTANTE; the
/// warnings of the layout are in the view, annex A 2.5) and the view of the map up to date (null for a command that touches
/// no map), with the history of the user on that map (what he can undo and redo, annex B 2.6).
/// </summary>
public sealed record CommandOutcome<TResult>(
    TResult Result,
    Guid? OperationId,
    int StructureVersion,
    IReadOnlyList<string> Warnings,
    MapView? View,
    HistoryView? History);
