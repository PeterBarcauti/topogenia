// CommandCatalog.StepsAndFlows.cs
// Rights of the commands of the steps, the flows, the layout and the command bar (annex A 1).

using Topogenia.Application.Flows;
using Topogenia.Application.Layout;
using Topogenia.Application.Steps;

namespace Topogenia.Application.Tests.Commands;

internal static partial class CommandCatalog
{
    internal static readonly Dictionary<Type, string> StepsAndFlows = new()
    {
        [typeof(CreateStepCommand)] = "facilitator",
        [typeof(UpdateStepCommand)] = "facilitator",
        [typeof(InsertStepCommand)] = "facilitator",
        [typeof(DeleteStepCommand)] = "facilitator",
        [typeof(ApplyLayoutCommand)] = "facilitator",
        [typeof(MoveStepsCommand)] = "facilitator",
        [typeof(CreateFlowCommand)] = "facilitator",
        [typeof(RelabelFlowCommand)] = "facilitator",
        [typeof(ShareFlowCommand)] = "facilitator",
        [typeof(ReverseFlowCommand)] = "facilitator",
        [typeof(DeleteFlowCommand)] = "facilitator",
        [typeof(ApplyCommandBarCommand)] = "facilitator",
    };
}
