// Chains.cs
// A chain of sub-maps built by the domain: the root map, then each level detailed from the only step of the level above.

using Topogenia.Domain.Steps;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Workspace;

namespace Topogenia.Persistence.Tests.Queries;

internal static class Chains
{
    /// <summary>The maps of the chain from the root down: the root map, then <paramref name="levels"/> sub-maps, one step each.</summary>
    public static List<Guid> Build(ProjectWorkspace workspace, OperationContext context, int levels)
    {
        var root = workspace.RootMap.Id;
        var maps = new List<Guid> { root };
        var step = StepOperations.Create(workspace, context, new CreateStepRequest(root, workspace.LanesOf(root)[0].Id, 0, "Level 0")).Id;
        for (var level = 1; level <= levels; level++)
        {
            var detail = SubMapRules.Detail(workspace, context, step);
            maps.Add(detail.MapId);
            step = StepOperations.Create(workspace, context, new CreateStepRequest(detail.MapId, detail.FirstLaneId!.Value, 0, $"Level {level}")).Id;
        }
        return maps;
    }
}
