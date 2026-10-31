// ExportScope.cs
// What a package carries of a workspace: the live structure and the open frames, never the trash (spec 7.2, ADR 051).

using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The records of a project that go into its package, and the readable codes by which the package names them (it
/// carries no identifier of the database: an import gives new ones, spec 7.3, so the package names a step, a lane or a
/// pain point by its code, a map by the code of its parent step, a flow by its two ends; ADR 051).
/// Exported: the live maps under the root map (empty sub-maps included, AR-09), their live lanes, steps and flows, and
/// on each of them the open frame of a dissolution with its dissolved step (deleted, but read by the frame and its
/// remainder, spec 7.2). Left out: the trash (deleted maps, lanes, steps and flows, and whatever hangs on them),
/// the closed frames, the deletion batches and the operation log (spec 7.2).
/// </summary>
internal sealed class ExportScope
{
    private readonly Dictionary<Guid, string> stepCodes = [];
    private readonly Dictionary<Guid, string> laneCodes = [];
    private readonly Dictionary<Guid, ProcessMap> maps = [];
    private readonly Dictionary<Guid, Flow> flows = [];

    public ExportScope(ProjectWorkspace workspace)
    {
        Workspace = workspace;
        var root = workspace.RootMap;
        foreach (var id in workspace.Subtree(root.Id))
            maps[id] = workspace.LiveMap(id);
        foreach (var map in maps.Values)
        {
            foreach (var lane in workspace.LanesOf(map.Id))
                laneCodes[lane.Id] = lane.Code;
            foreach (var step in workspace.ReadingOrder(map.Id))
                stepCodes[step.Id] = step.Code;
        }
        foreach (var map in maps.Values)
        {
            foreach (var flow in workspace.FlowsOf(map.Id).Where(flow => stepCodes.ContainsKey(flow.SourceStepId) && stepCodes.ContainsKey(flow.TargetStepId)))
                flows[flow.Id] = flow;
        }
        Frames = workspace.All<Dissolution>()
            .Where(dissolution => dissolution.IsOpen && maps.ContainsKey(dissolution.MapId) && workspace.Step(dissolution.StepId) is not null)
            .ToDictionary(dissolution => dissolution.MapId);
        foreach (var frame in Frames.Values)
            stepCodes.TryAdd(frame.StepId, workspace.Step(frame.StepId)!.Code);
        Maps = [root, .. maps.Values.Where(map => !map.IsRoot).OrderBy(map => ParentCode(map), StringComparer.Ordinal)];
    }

    public ProjectWorkspace Workspace { get; }

    /// <summary>The exported maps: the root map first, then the sub-maps by the code of their parent step.</summary>
    public IReadOnlyList<ProcessMap> Maps { get; }

    /// <summary>The open frame of each exported map that has one (one at most, annex A 2.9).</summary>
    public IReadOnlyDictionary<Guid, Dissolution> Frames { get; }

    /// <summary>The code of an exported step: live on an exported map, or the dissolved step of an open frame.</summary>
    public string? StepCode(Guid? stepId) => stepId is { } id && stepCodes.TryGetValue(id, out var code) ? code : null;

    public bool HasStep(Guid? stepId) => StepCode(stepId) is not null;

    /// <summary>The code of an exported (live) lane.</summary>
    public string? LaneCode(Guid? laneId) => laneId is { } id && laneCodes.TryGetValue(id, out var code) ? code : null;

    public bool HasMap(Guid mapId) => maps.ContainsKey(mapId);

    /// <summary>The code of the parent step of a sub-map, null for the root map.</summary>
    public string? ParentCode(ProcessMap map) => map.ParentStepId is { } parent ? StepCode(parent) : null;

    /// <summary>An exported flow: live, between two live steps of an exported map.</summary>
    public Flow? ExportedFlow(Guid flowId) => flows.GetValueOrDefault(flowId);

    /// <summary>The steps of an exported map in reading order (annex A 2.4).</summary>
    public IReadOnlyList<Step> StepsOf(ProcessMap map) => Workspace.ReadingOrder(map.Id);

    /// <summary>The flows of an exported map, by the reading place of their source, then of their target.</summary>
    public IReadOnlyList<Flow> FlowsOf(ProcessMap map)
    {
        var place = StepsOf(map).Select((step, index) => (step.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
        return flows.Values.Where(flow => flow.MapId == map.Id)
            .OrderBy(flow => place[flow.SourceStepId])
            .ThenBy(flow => place[flow.TargetStepId])
            .ToList();
    }
}
