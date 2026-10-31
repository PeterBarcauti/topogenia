// LayoutRoutesTests.cs
// The grouped layout of a map and the grouped move of dragged steps through the whole host on both databases (annex A 1.3, A 2.4, AR-44).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

public sealed class LayoutRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static (Guid Lane, int Column, int Row) CellOf(JsonElement map, Guid stepId)
    {
        var step = StructureCalls.StepOf(map, stepId);
        return (step.GetProperty("lane_id").GetGuid(), step.GetProperty("column").GetInt32(), step.GetProperty("row").GetInt32());
    }

    private static IEnumerable<Guid> LaneIds(JsonElement map) => map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("id").GetGuid());

    private static IEnumerable<string?> UndoKinds(JsonElement reply) =>
        reply.GetProperty("history").GetProperty("undo").EnumerateArray().Select(item => item.GetProperty("kind").GetString());

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_layout_puts_the_steps_in_their_exact_cells_orders_the_lanes_and_is_logged_as_a_move(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = await project.AddLaneAsync("Achats");
        var (first, second) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var laidOut = (await facilitator.PutAsync($"/api/maps/{project.RootMapId}/layout", new
        {
            lane_order = new[] { lane, project.FirstLaneId },
            steps = new object[] { new { id = first.Id, lane_id = lane, column = 5, row = 1 }, new { id = second.Id, lane_id = lane, column = 5 } },
        })).Ok();
        Assert.Equal([first.Id, second.Id], laidOut.Ids());
        Assert.Equal([lane, project.FirstLaneId], LaneIds(laidOut.GetProperty("map")));
        var map = await project.MapAsync();
        Assert.Equal((lane, 5, 1), CellOf(map, first.Id));
        Assert.Equal((lane, 5, 0), CellOf(map, second.Id));
        Assert.Equal("deplacer", UndoKinds(laidOut).First());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_layout_of_the_align_wand_is_logged_as_an_alignment_and_an_unknown_purpose_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = await project.AddStepAsync(0, "Commander");
        var url = $"/api/maps/{project.RootMapId}/layout";
        Assert.Equal("purpose", (await facilitator.PutAsync(url, new { steps = new[] { new { id = step.Id, lane_id = project.FirstLaneId, column = 2 } }, purpose = "shuffle" }))
            .Refused(400, "VALEUR_INVALIDE").Field());
        var aligned = (await facilitator.PutAsync(url, new { steps = new[] { new { id = step.Id, lane_id = project.FirstLaneId, column = 2 } }, purpose = "align" })).Ok();
        Assert.Equal("aligner", UndoKinds(aligned).First());
        Assert.Equal((project.FirstLaneId, 2, 0), CellOf(aligned.GetProperty("map"), step.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_layout_refuses_a_step_of_another_map_a_taken_cell_and_a_cell_without_column(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (first, second) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var (subMap, subLane) = await project.DetailAsync(first.Id);
        var inner = await project.AddStepAsync(0, "Saisir", subLane, subMap);
        var url = $"/api/maps/{project.RootMapId}/layout";
        var lane = project.FirstLaneId;
        (await facilitator.PutAsync(url, new { steps = new[] { new { id = inner.Id, lane_id = lane, column = 4 } } })).Refused(400, "ETAPE_HORS_CARTE");
        (await facilitator.PutAsync(url, new { steps = new[] { new { id = first.Id, lane_id = lane, column = 1 } } })).Refused(400, "DISPOSITION_CASE_OCCUPEE");
        Assert.Equal("column", (await facilitator.PutAsync(url, new { steps = new[] { new { id = first.Id, lane_id = lane } } })).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        Assert.Equal("lane_id", (await facilitator.PutAsync(url, new { steps = new[] { new { id = first.Id, lane_id = subLane, column = 4 } } }))
            .Refused(400, "VALEUR_INVALIDE").Field());
        var map = await project.MapAsync();
        Assert.Equal(((lane, 0, 0), (lane, 1, 0)), (CellOf(map, first.Id), CellOf(map, second.Id)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Dragged_steps_are_moved_in_one_operation_taken_back_by_one_undo(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (first, second) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var lane = project.FirstLaneId;
        var moved = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/move", new
        {
            steps = new[] { new { id = first.Id, lane_id = lane, column = 4 }, new { id = second.Id, lane_id = lane, column = 5 } },
        })).Ok();
        Assert.Equal([first.Id, second.Id], moved.Ids().Order());
        Assert.Equal(((lane, 4, 0), (lane, 5, 0)), (CellOf(moved.GetProperty("map"), first.Id), CellOf(moved.GetProperty("map"), second.Id)));
        Assert.Equal("deplacer", UndoKinds(moved).First());

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(((lane, 0, 0), (lane, 1, 0)), (CellOf(undone.GetProperty("map"), first.Id), CellOf(undone.GetProperty("map"), second.Id)));
        Assert.Equal("steps", (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/move", new { })).Refused(400, "CHAMP_OBLIGATOIRE").Field());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_contributor_neither_lays_out_nor_moves_and_a_stranger_is_told_not_found(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        var step = await project.AddStepAsync(0, "Commander");
        var cells = new { steps = new[] { new { id = step.Id, lane_id = project.FirstLaneId, column = 3 } } };
        (await contributor.PutAsync($"/api/maps/{project.RootMapId}/layout", cells)).Refused(403, "ACCES_REFUSE");
        (await stranger.PutAsync($"/api/maps/{project.RootMapId}/layout", cells)).Refused(404, "INTROUVABLE");
        (await contributor.PostAsync($"/api/maps/{project.RootMapId}/move", cells)).Refused(403, "ACCES_REFUSE");
        (await stranger.PostAsync($"/api/maps/{project.RootMapId}/move", cells)).Refused(404, "INTROUVABLE");
        Assert.Equal((project.FirstLaneId, 0, 0), CellOf(await project.MapAsync(), step.Id));
    }
}
