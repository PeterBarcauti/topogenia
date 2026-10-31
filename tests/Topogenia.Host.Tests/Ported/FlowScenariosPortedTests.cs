// FlowScenariosPortedTests.cs
// The 1.x flow scenarios (T-SCFL): « Detail the selection » then « Dissolve the detail » through the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// Each test carries the intention of one 1.x test of `ScenariosFluxTests` (T-SCFL-nn of `docs/portage-tests.md`): the block is
/// grouped by `POST /api/maps/{id}/group`, the maps are read, then the detail is dissolved by `POST /api/steps/{id}/dissolve`.
/// The routes keep the origin operation of the detail (spec 4.6); the same round trips without it, which no route can produce,
/// are proved by the domain (`Topogenia.Domain.Tests.Dissolutions.FlowScenarioTests`).
/// </summary>
public sealed class FlowScenariosPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string TaskType = "task";

    private static readonly string[] ThreeLanes = ["A", "B", "C"];

    private static readonly string[] HomonymousLanes = ["Equipe", "Equipe", "C"];

    private static readonly string[] BlockOverThreeLanes = ["a1", "b2", "c3"];

    /// <summary>The six steps of scenarios 1 and 2: a0 (A) -> a1 (A) -> b2 (B) -> c3 (C) -> a4 (A) -> b5 (B).</summary>
    private static readonly (string Name, int Lane, int Column, int Row, string Type)[] SixSteps =
        [("a0", 0, 0, 0, TaskType), ("a1", 0, 1, 0, TaskType), ("b2", 1, 2, 0, TaskType), ("c3", 2, 3, 0, TaskType), ("a4", 0, 4, 0, TaskType), ("b5", 1, 5, 0, TaskType)];

    private static readonly (string Source, string Target, string? Label)[] SixFlows =
        [("a0", "a1", null), ("a1", "b2", null), ("b2", "c3", null), ("c3", "a4", null), ("a4", "b5", null)];

    /// <summary>The maps read along a round trip: before, after grouping (root and detail), after dissolving.</summary>
    private sealed record RoundTrip(JsonElement Before, JsonElement Grouped, JsonElement Detail, JsonElement After);

    /// <summary>
    /// Draws the lanes (the first one renamed), the steps (lane index, column, row, type) and the labelled flows on the root map,
    /// groups the selection into « Bloc », reads the maps, then dissolves « Bloc » with the version read.
    /// </summary>
    private async Task<RoundTrip> RoundTripAsync(EngineKind kind, string[] lanes, (string Name, int Lane, int Column, int Row, string Type)[] steps,
        (string Source, string Target, string? Label)[] flows, params string[] selection)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var firstVersion = (await project.MapAsync()).GetProperty("lanes")[0].GetProperty("version").GetInt32();
        (await facilitator.PatchAsync($"/api/lanes/{project.FirstLaneId}", new { name = lanes[0] }, firstVersion)).Ok();
        var laneIds = new List<Guid> { project.FirstLaneId };
        foreach (var name in lanes.Skip(1))
            laneIds.Add(await project.AddLaneAsync(name));
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var (name, lane, column, row, type) in steps)
            ids[name] = (await project.AddStepAsync(column, name, laneIds[lane], type: type, row: row)).Id;
        foreach (var (source, target, label) in flows)
            await project.AddFlowAsync(ids[source], ids[target], label);

        var before = await project.MapAsync();
        var grouped = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = selection.Select(name => ids[name]).ToArray(), label = "Bloc" })).Ok();
        var (macro, subMap) = (grouped.GetProperty("id").GetGuid(), grouped.GetProperty("ids")[0].GetGuid());
        var afterGrouping = await project.MapAsync();
        var detail = await project.MapAsync(subMap);
        (await facilitator.PostAsync($"/api/steps/{macro}/dissolve", null, await project.StepVersionAsync(macro))).Ok();
        return new RoundTrip(before, afterGrouping, detail, await project.MapAsync());
    }

    /// <summary>Every step of a map as "label@lane/column/row", the lane by its identifier.</summary>
    private static IReadOnlyList<string> Cells(JsonElement map) =>
        [.. map.GetProperty("steps").EnumerateArray()
            .Select(step => $"{step.GetProperty("label").GetString()}@{step.GetProperty("lane_id").GetGuid()}/{step.GetProperty("column").GetInt32()}/{step.GetProperty("row").GetInt32()}")
            .Order(StringComparer.Ordinal)];

    /// <summary>The cell of each step of a map by its label: the name of its lane, its column and its row.</summary>
    private static Dictionary<string, (string? Lane, int Column, int Row)> NamedCells(JsonElement map)
    {
        var names = map.GetProperty("lanes").EnumerateArray().ToDictionary(lane => lane.GetProperty("id").GetGuid(), lane => lane.GetProperty("name").GetString());
        return map.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetProperty("label").GetString()!,
            step => (names[step.GetProperty("lane_id").GetGuid()], step.GetProperty("column").GetInt32(), step.GetProperty("row").GetInt32()), StringComparer.Ordinal);
    }

    /// <summary>The flows of a map as "source->target(label)", or "source->target", with the labels of the steps.</summary>
    private static IReadOnlyList<string> Flows(JsonElement map, bool withLabels = true)
    {
        var labels = map.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetProperty("id").GetGuid(), step => step.GetProperty("label").GetString());
        return [.. map.GetProperty("flows").EnumerateArray()
            .Select(flow => $"{labels[flow.GetProperty("source_step_id").GetGuid()]}->{labels[flow.GetProperty("target_step_id").GetGuid()]}"
                + (withLabels ? $"({flow.GetProperty("label").GetString()})" : ""))
            .Order(StringComparer.Ordinal)];
    }

    private static IReadOnlyList<string?> LaneNames(JsonElement map) =>
        [.. map.GetProperty("lanes").EnumerateArray().OrderBy(lane => lane.GetProperty("sort_order").GetInt32()).Select(lane => lane.GetProperty("name").GetString())];

    private static IReadOnlyList<Guid> LaneIds(JsonElement map) =>
        [.. map.GetProperty("lanes").EnumerateArray().OrderBy(lane => lane.GetProperty("sort_order").GetInt32()).Select(lane => lane.GetProperty("id").GetGuid())];

    /// <summary>The round trip is exact: the same lanes (none created), every step in its cell, the same flows with their labels.</summary>
    private static void AssertExact(RoundTrip run)
    {
        Assert.Equal(LaneIds(run.Before), LaneIds(run.After));
        Assert.Equal(Cells(run.Before), Cells(run.After));
        Assert.Equal(Flows(run.Before), Flows(run.After));
    }

    /// <summary>T-SCFL-01: a block over three lanes keeps its lanes, their order and the reading order in the detail, and comes back exactly.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_block_over_three_lanes_keeps_its_lanes_and_reading_order_in_the_detail_and_comes_back_exactly(EngineKind kind)
    {
        var run = await RoundTripAsync(kind, ThreeLanes, SixSteps, SixFlows, BlockOverThreeLanes);
        Assert.Equal(ThreeLanes, LaneNames(run.Detail));
        var detail = NamedCells(run.Detail);
        Assert.Equal([("A", 0), ("B", 1), ("C", 2)], BlockOverThreeLanes.Select(name => (detail[name].Lane, detail[name].Column)));
        Assert.Equal(ThreeLanes, LaneNames(run.After));
        AssertExact(run);
    }

    /// <summary>T-SCFL-02: the flows that enter or leave the middle of the block go through « Bloc » during the detail and come back exactly.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Flows_into_and_out_of_the_middle_of_the_block_pass_through_the_detailed_step_and_come_back_exactly(EngineKind kind)
    {
        var run = await RoundTripAsync(kind, ThreeLanes, SixSteps, [.. SixFlows, ("a0", "c3", "saut"), ("b2", "b5", "raccourci")], BlockOverThreeLanes);
        Assert.Contains("a0->Bloc", Flows(run.Grouped, withLabels: false));
        Assert.Contains("Bloc->b5", Flows(run.Grouped, withLabels: false));
        AssertExact(run);
    }

    /// <summary>T-SCFL-03: a loop inside the block survives the round trip.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_loop_inside_the_block_survives_the_round_trip(EngineKind kind)
    {
        var run = await RoundTripAsync(kind, ThreeLanes, SixSteps[..5], [.. SixFlows[..4], ("c3", "a1", "non conforme")], BlockOverThreeLanes);
        Assert.Contains("c3->a1(non conforme)", Flows(run.After));
        AssertExact(run);
    }

    /// <summary>T-SCFL-04: a selection on lanes A and C with a parallel branch copies only the lanes used and gives back rows, lanes and flows.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_selection_skipping_a_lane_with_a_parallel_branch_copies_only_the_used_lanes_and_comes_back(EngineKind kind)
    {
        var run = await RoundTripAsync(kind, ThreeLanes,
            [("a0", 0, 0, 0, TaskType), ("a1", 0, 1, 0, TaskType), ("a1bis", 0, 1, 1, TaskType), ("b1", 1, 1, 0, TaskType), ("c2", 2, 2, 0, TaskType), ("a3", 0, 3, 0, TaskType)],
            [("a0", "a1", null), ("a0", "a1bis", null), ("a1", "c2", null), ("a1bis", "c2", null), ("c2", "a3", null), ("a0", "b1", null), ("b1", "a3", null)],
            "a1", "a1bis", "c2");
        Assert.Equal(["A", "C"], LaneNames(run.Detail));
        var after = NamedCells(run.After);
        Assert.Equal((after["a1"].Column, 0, 1), (after["a1bis"].Column, after["a1"].Row, after["a1bis"].Row));
        Assert.Equal(ThreeLanes, LaneNames(run.After));
        AssertExact(run);
    }

    /// <summary>T-SCFL-05: with two lanes of the same name, the step comes back to its own lane and not to the homonym.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task With_two_lanes_of_the_same_name_the_step_comes_back_to_its_own_lane(EngineKind kind)
    {
        var run = await RoundTripAsync(kind, HomonymousLanes,
            [("x0", 2, 0, 0, TaskType), ("x1", 2, 1, 0, TaskType), ("x2", 2, 2, 0, TaskType), ("e1", 1, 1, 0, TaskType)],
            [("x0", "x1", null), ("x1", "x2", null), ("x0", "e1", null)], "e1");
        var second = LaneIds(run.Before)[1];
        Assert.Equal(second, run.After.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("label").GetString() == "e1").GetProperty("lane_id").GetGuid());
        Assert.Equal(HomonymousLanes, LaneNames(run.After));
        AssertExact(run);
    }

    /// <summary>T-SCFL-06: both labelled exits of a decision that leave the block start from « Bloc », then come back with their labels and cells.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Both_labelled_exits_of_a_decision_leaving_the_block_come_back_with_their_labels(EngineKind kind)
    {
        var run = await RoundTripAsync(kind, ThreeLanes,
            [("a0", 0, 0, 0, TaskType), ("a1", 0, 1, 0, TaskType), ("a3", 0, 3, 0, TaskType), ("c3", 2, 3, 0, TaskType), ("b1", 1, 2, 0, "decision")],
            [("a0", "a1", null), ("a1", "b1", null), ("b1", "a3", "oui"), ("b1", "c3", "non")], "a1", "b1");
        Assert.Contains("Bloc->a3", Flows(run.Grouped, withLabels: false));
        Assert.Contains("Bloc->c3", Flows(run.Grouped, withLabels: false));
        Assert.Equal(["non", "oui"], run.After.GetProperty("flows").EnumerateArray().Select(flow => flow.GetProperty("label").GetString()).OfType<string>().Order(StringComparer.Ordinal));
        AssertExact(run);
    }

    /// <summary>T-SCFL-07: a block on a parallel row comes back to its cells.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_block_on_a_parallel_row_comes_back_exactly(EngineKind kind)
    {
        var run = await RoundTripAsync(kind, ["A", "B"],
            [("a0", 0, 0, 0, TaskType), ("a1", 0, 1, 0, TaskType), ("p1", 0, 1, 1, TaskType), ("p2", 0, 2, 1, TaskType), ("a3", 0, 3, 0, TaskType), ("b2", 1, 2, 0, TaskType)],
            [("a0", "a1", null), ("a0", "p1", null), ("p1", "p2", null), ("p2", "a3", null), ("a1", "b2", null), ("b2", "a3", null)], "p1", "p2");
        Assert.Equal(("A", 1, 1), NamedCells(run.After)["p1"]);
        AssertExact(run);
    }
}
