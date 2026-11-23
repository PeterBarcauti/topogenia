// AssistantCommentDraftTargetsTests.cs
// The bound of the list the practice `comments` sends (ADR 107, point 7; audit of the copy 65, NEW-P3-01): the maps of the tree taken whole while they fit in 300 entries, the last ones left out entire, never the flows of a map listed; the map open always listed, its first entries only when it alone passes the bound.

using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantCommentDraftTargetsTests
{
    /// <summary>A chain of steps on a map, column after column, with a flow between each step and the next.</summary>
    private static (List<Guid> Steps, List<Guid> Flows) Chain(TestProject project, int count, Guid? mapId = null, Guid? laneId = null)
    {
        var steps = Enumerable.Range(0, count).Select(column => project.Step(column, laneId: laneId, mapId: mapId)).ToList();
        var flows = steps.Zip(steps.Skip(1)).Select(pair => project.Flow(pair.First, pair.Second, mapId)).ToList();
        return (steps, flows);
    }

    /// <summary>The list of the practice for the tree of a map, as the context of the assistant builds it.</summary>
    private static IReadOnlyList<AssistantDraftTarget> Listed(TestProject project, Guid mapId)
    {
        var workspace = project.Database.Load(project.ProjectId, WorkspaceContent.None)!;
        return AssistantCommentDraftTargets.Of(PresentationCommentRules.TreeSteps(workspace, mapId), [], FlowPresentationCommentRules.TreeFlows(workspace, mapId), [],
            map => map == project.RootMapId ? "Carte" : "Détail");
    }

    [Fact]
    public void A_detailed_map_that_would_pass_the_bound_is_left_out_entire_and_the_map_open_keeps_all_its_flows()
    {
        var project = new TestProject();
        // The map open: 150 steps and their 149 flows, 299 entries; its detailed map: 2 steps and a flow, 3 more.
        var (steps, flows) = Chain(project, 150);
        var (subMap, subLane) = project.SubMap(steps[0]);
        var (inner, innerFlows) = Chain(project, 2, subMap, subLane);

        var listed = Listed(project, project.RootMapId);

        Assert.Equal(steps.Concat(flows), listed.Select(target => target.SubjectId));
        Assert.All(listed, target => Assert.Equal("Carte", target.Map));
        // Asked from the detailed map, it is listed whole.
        Assert.Equal(inner.Concat(innerFlows), Listed(project, subMap).Select(target => target.SubjectId));
    }

    [Fact]
    public void Maps_that_fit_are_listed_whole_one_after_the_other()
    {
        var project = new TestProject();
        var (steps, flows) = Chain(project, 100);
        var (subMap, subLane) = project.SubMap(steps[0]);
        var (inner, innerFlows) = Chain(project, 50, subMap, subLane);

        var listed = Listed(project, project.RootMapId);

        Assert.Equal(steps.Concat(flows).Concat(inner).Concat(innerFlows), listed.Select(target => target.SubjectId));
        Assert.Equal(298, listed.Count);
    }

    [Fact]
    public void The_map_open_alone_past_the_bound_gives_its_first_entries_its_steps_before_its_flows()
    {
        var project = new TestProject();
        // 160 steps and 159 flows: 319 entries, the 300 first are the steps and 140 flows; its detailed map is left out.
        var (steps, flows) = Chain(project, 160);
        var (subMap, subLane) = project.SubMap(steps[0]);
        Chain(project, 2, subMap, subLane);

        var listed = Listed(project, project.RootMapId);

        Assert.Equal(AssistantCommentTargets.CommentsMax, listed.Count);
        Assert.Equal(steps.Concat(flows.Take(140)), listed.Select(target => target.SubjectId));
    }
}
