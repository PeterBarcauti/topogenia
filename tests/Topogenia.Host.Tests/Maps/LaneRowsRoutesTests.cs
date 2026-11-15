// LaneRowsRoutesTests.cs
// Rows inserted into a lane and an empty row taken out through the whole host on both databases (3.1, lot G, ADR 096): the steps and a flow pinned to a row move together, one operation undone and redone, a row that holds something refused, the values checked.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Maps;

public sealed class LaneRowsRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static int RowOf(JsonElement map, Guid stepId) => StructureCalls.StepOf(map, stepId).GetProperty("row").GetInt32();

    private static int PinnedRowOf(JsonElement map, Guid flowId) => StructureCalls.FlowOf(map, flowId).GetProperty("route").GetProperty("via").GetProperty("row").GetInt32();

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_row_inserted_then_taken_out_moves_the_steps_and_the_pinned_flow_each_one_operation_undone_and_redone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var a = await project.AddStepAsync(0, "Recevoir");
        var b = await project.AddStepAsync(2, "Traiter");
        var flow = await project.AddFlowAsync(a.Id, b.Id);
        var version = StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("version").GetInt32();
        (await facilitator.PutAsync($"/api/flows/{flow}/route", new { via = new { lane_id = project.FirstLaneId, row = 1 } }, version)).Ok();

        var inserted = (await facilitator.PostAsync($"/api/lanes/{project.FirstLaneId}/rows", new { row = 0 })).Ok();
        Assert.Equal(2, inserted.GetProperty("ids").GetArrayLength());
        var map = inserted.GetProperty("map");
        Assert.Equal((1, 1, 2), (RowOf(map, a.Id), RowOf(map, b.Id), PinnedRowOf(map, flow)));

        Assert.Equal("inserer_rangees", (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok().GetProperty("code").GetString());
        map = await project.MapAsync();
        Assert.Equal((0, 1), (RowOf(map, a.Id), PinnedRowOf(map, flow)));
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Ok();

        map = (await facilitator.DeleteAsync($"/api/lanes/{project.FirstLaneId}/rows/0")).Ok().GetProperty("map");
        Assert.Equal((0, 0, 1), (RowOf(map, a.Id), RowOf(map, b.Id), PinnedRowOf(map, flow)));
        Assert.Equal("retirer_rangee", (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok().GetProperty("code").GetString());
        Assert.Equal(1, RowOf(await project.MapAsync(), a.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_row_inserted_at_the_top_of_a_lane_added_to_the_map_takes_its_steps_down_only(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var second = await project.AddLaneAsync("Direction");
        var a = await project.AddStepAsync(0, "Recevoir");
        var c = await project.AddStepAsync(2, "Valider", second);
        var map = (await facilitator.PostAsync($"/api/lanes/{second}/rows", new { row = 0, count = 1 })).Ok().GetProperty("map");
        Assert.Equal((0, 1), (RowOf(map, a.Id), RowOf(map, c.Id)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_row_that_holds_a_step_or_a_passage_is_refused_and_the_values_are_checked(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        await project.AddStepAsync(0, "Recevoir");
        var logged = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Ok().GetProperty("undo").GetArrayLength();
        (await facilitator.DeleteAsync($"/api/lanes/{project.FirstLaneId}/rows/0")).Refused(409, "RANGEE_NON_VIDE");
        (await facilitator.PostAsync($"/api/lanes/{project.FirstLaneId}/rows", new { row = 0, count = 0 })).Refused(400, "VALEUR_INVALIDE");
        (await facilitator.PostAsync($"/api/lanes/{project.FirstLaneId}/rows", new { row = 51 })).Refused(400, "VALEUR_INVALIDE");
        (await facilitator.PostAsync($"/api/lanes/{project.FirstLaneId}/rows", new { count = 1 })).Refused(400, "CHAMP_OBLIGATOIRE");
        (await facilitator.PostAsync($"/api/lanes/{Guid.NewGuid()}/rows", new { row = 0 })).Refused(404, "INTROUVABLE");
        Assert.Equal(logged, (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Ok().GetProperty("undo").GetArrayLength());
    }
}
