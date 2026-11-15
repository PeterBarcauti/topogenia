// CommandCatalog.SelectionAndDissolutions.cs
// Rights of the commands of the selection, the sub-maps, the dissolutions and the RACI (annex A 1).

using Topogenia.Application.Dissolutions;
using Topogenia.Application.Raci;
using Topogenia.Application.Selection;
using Topogenia.Application.SubMaps;

namespace Topogenia.Application.Tests.Commands;

internal static partial class CommandCatalog
{
    internal static readonly Dictionary<Type, string> SelectionAndDissolutions = new()
    {
        [typeof(DeleteSelectionCommand)] = "facilitator",
        [typeof(PasteCommand)] = "facilitator",
        [typeof(TransferBlockCommand)] = "facilitator",
        [typeof(GroupSelectionCommand)] = "facilitator",
        [typeof(DetailStepCommand)] = "facilitator",
        [typeof(DeleteSubMapCommand)] = "facilitator",
        [typeof(ReattachSubMapCommand)] = "facilitator",
        [typeof(DissolveCommand)] = "facilitator",
        [typeof(AssignRemainderCommand)] = "facilitator",
        [typeof(RemoveRemainderCommand)] = "facilitator",
        [typeof(FinishDissolutionCommand)] = "facilitator",
        [typeof(SetRaciCommand)] = "facilitator",
    };
}
