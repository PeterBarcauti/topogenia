// CommandCatalog.Lanes.cs
// Rights of the lane commands, of undo and redo, and of the read of a map (annex A 1).

using Topogenia.Application.Lanes;
using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;

namespace Topogenia.Application.Tests.Commands;

internal static partial class CommandCatalog
{
    internal static readonly Dictionary<Type, string> LanesAndMaps = new()
    {
        [typeof(AddLaneCommand)] = "facilitator",
        [typeof(UpdateLaneCommand)] = "facilitator",
        [typeof(ReorderLanesCommand)] = "facilitator",
        [typeof(DeleteLaneCommand)] = "facilitator",
        [typeof(UndoCommand)] = "facilitator",
        [typeof(RedoCommand)] = "facilitator",
        [typeof(GetMapQuery)] = "reader",
        [typeof(GetHistoryQuery)] = "reader",
    };
}
