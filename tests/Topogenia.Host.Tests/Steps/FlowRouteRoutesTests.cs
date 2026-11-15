// FlowRouteRoutesTests.cs
// The route chosen for a flow through the whole host on both databases: written by its route, in the view of the map, the middle of a column and of a row too, one operation undone and redone, its refusals, released when a step of the flow moves, the sides of a pasted copy (ADR 091).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

public sealed class FlowRouteRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static int VersionOf(JsonElement map, Guid flowId) => StructureCalls.FlowOf(map, flowId).GetProperty("version").GetInt32();

    private static JsonElement RouteOf(JsonElement map, Guid flowId) => StructureCalls.FlowOf(map, flowId).GetProperty("route");

    private static object Band(Guid lane, int row, string edge) => new { aisle = (int?)null, lane_id = lane, row, edge };

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_route_written_by_its_route_is_in_the_view_and_is_one_operation_undone_and_done_again(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var a = await project.AddStepAsync(0, "Recevoir");
        var b = await project.AddStepAsync(2, "Traiter");
        var flow = await project.AddFlowAsync(a.Id, b.Id);
        Assert.Equal(JsonValueKind.Null, RouteOf(await project.MapAsync(), flow).ValueKind);

        var map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{flow}/route", new { source_side = "top", target_side = "bottom", via = new { aisle = -1 } }, VersionOf(map, flow))).Ok();
        var route = RouteOf(await project.MapAsync(), flow);
        Assert.Equal(("top", "bottom", -1, JsonValueKind.Null), (route.GetProperty("source_side").GetString(), route.GetProperty("target_side").GetString(),
            route.GetProperty("via").GetProperty("aisle").GetInt32(), route.GetProperty("via").GetProperty("lane_id").ValueKind));

        map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{flow}/route", new { source_side = (string?)null, target_side = "left", via = Band(project.FirstLaneId, 0, "bottom") },
            VersionOf(map, flow))).Ok();
        route = RouteOf(await project.MapAsync(), flow);
        Assert.Equal((JsonValueKind.Null, project.FirstLaneId, 0, "bottom"), (route.GetProperty("source_side").ValueKind,
            route.GetProperty("via").GetProperty("lane_id").GetGuid(), route.GetProperty("via").GetProperty("row").GetInt32(), route.GetProperty("via").GetProperty("edge").GetString()));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal("tracer_lien", undone.GetProperty("code").GetString());
        Assert.Equal(-1, RouteOf(await project.MapAsync(), flow).GetProperty("via").GetProperty("aisle").GetInt32());
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(JsonValueKind.Null, RouteOf(await project.MapAsync(), flow).ValueKind);
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Ok();
        Assert.Equal("top", RouteOf(await project.MapAsync(), flow).GetProperty("source_side").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_middle_of_a_column_and_the_middle_of_a_row_are_written_kept_by_the_database_and_given_back_in_the_view(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var a = await project.AddStepAsync(0, "Recevoir");
        var b = await project.AddStepAsync(2, "Traiter");
        var flow = await project.AddFlowAsync(a.Id, b.Id);
        var map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{flow}/route", new { via = new { column = 1 } }, VersionOf(map, flow))).Ok();
        var via = RouteOf(await project.MapAsync(), flow).GetProperty("via");
        Assert.Equal((1, JsonValueKind.Null, JsonValueKind.Null), (via.GetProperty("column").GetInt32(), via.GetProperty("aisle").ValueKind, via.GetProperty("lane_id").ValueKind));

        map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{flow}/route", new { via = new { lane_id = project.FirstLaneId, row = 0 } }, VersionOf(map, flow))).Ok();
        via = RouteOf(await project.MapAsync(), flow).GetProperty("via");
        Assert.Equal((project.FirstLaneId, 0, JsonValueKind.Null, JsonValueKind.Null), (via.GetProperty("lane_id").GetGuid(), via.GetProperty("row").GetInt32(),
            via.GetProperty("edge").ValueKind, via.GetProperty("column").ValueKind));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_route_out_of_its_rules_by_whom_may_not_change_the_map_or_on_an_old_version_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var gateway = await project.AddStepAsync(0, "Conforme ?", type: "decision");
        var yes = await project.AddStepAsync(1, "Payer");
        var no = await project.AddStepAsync(2, "Refuser");
        var first = await project.AddFlowAsync(gateway.Id, yes.Id, "oui");
        var second = await project.AddFlowAsync(gateway.Id, no.Id, "non");
        var map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{first}/route", new { source_side = "top" }, VersionOf(map, first))).Ok();

        map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{second}/route", new { source_side = "top" }, VersionOf(map, second))).Refused(409, "POINT_PRIS");
        Assert.Equal("via", (await facilitator.PutAsync($"/api/flows/{second}/route", new { via = new { aisle = -2 } }, VersionOf(map, second)))
            .Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("via", (await facilitator.PutAsync($"/api/flows/{second}/route", new { via = new { lane_id = project.FirstLaneId } }, VersionOf(map, second)))
            .Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("via", (await facilitator.PutAsync($"/api/flows/{second}/route", new { via = new { aisle = 1, column = 1 } }, VersionOf(map, second)))
            .Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("via", (await facilitator.PutAsync($"/api/flows/{second}/route", new { via = new { column = -1 } }, VersionOf(map, second)))
            .Refused(400, "VALEUR_INVALIDE").Field());
        (await facilitator.PutAsync($"/api/flows/{second}/route", new { source_side = "middle" }, VersionOf(map, second))).Refused(400, "REQUETE_INVALIDE");
        (await contributor.PutAsync($"/api/flows/{second}/route", new { source_side = "bottom" }, VersionOf(map, second))).Refused(403, "ACCES_REFUSE");
        (await reader.PutAsync($"/api/flows/{second}/route", new { source_side = "bottom" }, VersionOf(map, second))).Refused(403, "ACCES_REFUSE");
        (await facilitator.PutAsync($"/api/flows/{Guid.NewGuid()}/route", new { source_side = "bottom" }, 1)).Refused(404, "INTROUVABLE");
        (await facilitator.PutAsync($"/api/flows/{first}/route", new { source_side = "bottom" }, VersionOf(map, first) - 1)).Refused(409, "CONFLIT_VERSION");
        Assert.Equal(JsonValueKind.Null, RouteOf(await project.MapAsync(), second).ValueKind);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_of_the_flow_moved_releases_its_passage_and_keeps_its_sides(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var a = await project.AddStepAsync(0, "Recevoir");
        var b = await project.AddStepAsync(2, "Traiter");
        var flow = await project.AddFlowAsync(a.Id, b.Id);
        var map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{flow}/route", new { source_side = "bottom", target_side = "top", via = new { aisle = 1 } }, VersionOf(map, flow))).Ok();

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/move",
            new { steps = new[] { new { id = b.Id, lane_id = project.FirstLaneId, column = 4, row = 0 } } })).Ok();
        var route = RouteOf(await project.MapAsync(), flow);
        Assert.Equal(("bottom", "top", JsonValueKind.Null), (route.GetProperty("source_side").GetString(), route.GetProperty("target_side").GetString(),
            route.GetProperty("via").ValueKind));
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(1, RouteOf(await project.MapAsync(), flow).GetProperty("via").GetProperty("aisle").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_pasted_flow_takes_the_sides_given_for_the_flow_copied(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var pasted = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/paste", new
        {
            steps = new[]
            {
                new { key = "k1", label = "Un", type = (string?)null, lane_id = project.FirstLaneId, column = 5, row = 0 },
                new { key = "k2", label = "Deux", type = (string?)null, lane_id = project.FirstLaneId, column = 6, row = 0 },
            },
            flows = new[] { new { source = "k1", target = "k2", label = (string?)null, source_side = "top", target_side = "bottom" } },
            anchor = (object?)null,
        })).Ok();
        var map = await project.MapAsync();
        var ids = pasted.Ids();
        var flow = map.GetProperty("flows").EnumerateArray().Single(item => item.GetProperty("source_step_id").GetGuid() == ids[0]);
        Assert.Equal(("top", "bottom"), (flow.GetProperty("route").GetProperty("source_side").GetString(), flow.GetProperty("route").GetProperty("target_side").GetString()));
    }
}
