// UndoHistoryPortedTests.cs
// The 1.x client tests of the snapshots of positions (T-WGEO-27 to 29) and of the undo stack (T-WHIS-01, 04) ported to the server undo through the whole host on both databases (AR-48).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.SubMapsAndUndoPortedCalls;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// The 1.x kept in the browser a snapshot of the positions before a change (`instantane`) and the layout that brings it
/// back (`dispositionVers`), and a stack of undo entries (`Historique`). The 2.0 keeps both on the server: the layout is
/// an operation of the journal and its undo puts back the lane order and the cells it changed (AR-48, spec 4.6); the stack
/// is per map and per user. The scenario of each 1.x test is played through `PUT /api/maps/{id}/layout`, `undo` and `redo`.
/// </summary>
public sealed class UndoHistoryPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static string Layout(ApiProject project) => $"/api/maps/{project.RootMapId}/layout";

    private static object Cell(Guid step, Guid lane, int column) => new { id = step, lane_id = lane, column, row = 0 };

    private static IReadOnlyList<Guid> LaneOrder(JsonElement map) => [.. map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("id").GetGuid())];

    private static (Guid Lane, int Column) CellOf(JsonElement map, Guid step)
    {
        var read = StructureCalls.StepOf(map, step);
        return (read.GetProperty("lane_id").GetGuid(), read.GetProperty("column").GetInt32());
    }

    /// <summary>The lanes, steps and flows of a map with their versions; the structure version moves with each locked request (spec 4.7).</summary>
    private static string Content(JsonElement map) =>
        string.Join('\n', new[] { "lanes", "steps", "flows" }.Select(name => map.GetProperty(name).GetRawText()));

    private static async Task<JsonElement> HistoryAsync(ApiClient client, ApiProject project) =>
        (await client.GetAsync($"/api/maps/{project.RootMapId}/history")).Ok();

    /// <summary>
    /// T-WGEO-27, nothing to lay out again when nothing moved: a layout to the positions already held is no operation;
    /// the lanes, steps and flows keep their versions.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_layout_to_the_positions_already_held_writes_no_operation_and_leaves_nothing_new_to_undo(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (a, b) = (project.FirstLaneId, await project.AddLaneAsync("B"));
        var (first, second) = (await project.AddStepAsync(0, "Un"), await project.AddStepAsync(1, "Deux"));
        var before = await project.MapAsync();
        var history = await HistoryAsync(facilitator, project);

        (await facilitator.PutAsync(Layout(project), new { lane_order = new[] { a, b }, steps = new[] { Cell(first.Id, a, 0), Cell(second.Id, a, 1) } })).Ok();

        Assert.Equal(Content(before), Content(await project.MapAsync()));
        Assert.Equal(history.GetRawText(), (await HistoryAsync(facilitator, project)).GetRawText());
        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal((UndoOperations(history)[0], "creer_etape"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
    }

    /// <summary>T-WGEO-28, the columns and the order of the lanes found again.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_of_a_layout_brings_back_the_columns_and_the_order_of_the_lanes_and_the_redo_takes_them_again(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (a, b, c) = (project.FirstLaneId, await project.AddLaneAsync("B"), await project.AddLaneAsync("C"));
        var (first, second) = (await project.AddStepAsync(0, "Un"), await project.AddStepAsync(1, "Deux"));

        var laidOut = (await facilitator.PutAsync(Layout(project), new { lane_order = new[] { c, b, a }, steps = new[] { Cell(first.Id, b, 2), Cell(second.Id, a, 0) } })).Ok();
        Assert.Equal([c, b, a], LaneOrder(laidOut.GetProperty("map")));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal((laidOut.GetProperty("operation_id").GetGuid(), "deplacer"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        var back = await project.MapAsync();
        Assert.Equal([a, b, c], LaneOrder(back));
        Assert.Equal(((a, 0), (a, 1)), (CellOf(back, first.Id), CellOf(back, second.Id)));

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Ok();
        var again = await project.MapAsync();
        Assert.Equal([c, b, a], LaneOrder(again));
        Assert.Equal(((b, 2), (a, 0)), (CellOf(again, first.Id), CellOf(again, second.Id)));
    }

    /// <summary>
    /// T-WGEO-29, the vanished steps and lanes are ignored: a step and a lane that the layout did not move,
    /// removed since by another facilitator, are left removed by its undo.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_of_a_layout_leaves_removed_the_steps_and_lanes_removed_since_and_puts_back_the_others(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var (a, b, c) = (project.FirstLaneId, await project.AddLaneAsync("B"), await project.AddLaneAsync("C"));
        var (first, second) = (await project.AddStepAsync(0, "Un"), await project.AddStepAsync(0, "Deux", b));
        (await facilitator.PutAsync(Layout(project), new { lane_order = new[] { b, a, c }, steps = new[] { Cell(first.Id, a, 3) } })).Ok();

        (await admin.DeleteAsync($"/api/steps/{second.Id}", await project.StepVersionAsync(second.Id))).Ok();
        var laneVersion = (await project.MapAsync()).GetProperty("lanes").EnumerateArray().Single(lane => lane.GetProperty("id").GetGuid() == c).GetProperty("version").GetInt32();
        (await admin.DeleteAsync($"/api/lanes/{c}?steps=delete", laneVersion)).Ok();

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        var map = await project.MapAsync();
        Assert.Equal([a, b], LaneOrder(map));
        Assert.Equal([first.Id], map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetGuid()));
        Assert.Equal((a, 0), CellOf(map, first.Id));
    }

    /// <summary>T-WHIS-01, undone in reverse order then redone: the journal reads `-b`, `-a`, `+a`, one undo and one redo left.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_takes_back_the_operations_in_reverse_order_and_the_redo_replays_the_last_undone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var undo = $"/api/maps/{project.RootMapId}/undo";
        var operationA = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "A" })).Ok().GetProperty("operation_id").GetGuid();
        var operationB = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column = 0, label = "B" }))
            .Ok().GetProperty("operation_id").GetGuid();

        var journal = new List<string>();
        foreach (var (route, sign) in new[] { (undo, "-"), (undo, "-"), ($"/api/maps/{project.RootMapId}/redo", "+") })
        {
            var id = (await facilitator.PostAsync(route)).Ok().GetProperty("id").GetGuid();
            journal.Add(sign + (id == operationA ? "a" : id == operationB ? "b" : "?"));
        }

        Assert.Equal(["-b", "-a", "+a"], journal);
        var history = await HistoryAsync(facilitator, project);
        Assert.Equal([operationA], UndoOperations(history));
        Assert.Equal([operationB], RedoOperations(history));
        var map = await project.MapAsync();
        Assert.Equal((2, 0), (map.GetProperty("lanes").GetArrayLength(), map.GetProperty("steps").GetArrayLength()));
    }

    /// <summary>T-WHIS-04, nothing happens on an empty stack: the 2.0 answers INTROUVABLE (ADR 042) and changes nothing.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_undo_or_a_redo_on_an_empty_stack_is_not_found_and_changes_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var before = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}")).Ok().GetRawText();

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Refused(404, "INTROUVABLE");

        Assert.Equal(before, (await facilitator.GetAsync($"/api/maps/{project.RootMapId}")).Ok().GetRawText());
        var history = await HistoryAsync(facilitator, project);
        Assert.Equal((0, 0), (history.GetProperty("undo").GetArrayLength(), history.GetProperty("redo").GetArrayLength()));
    }
}
