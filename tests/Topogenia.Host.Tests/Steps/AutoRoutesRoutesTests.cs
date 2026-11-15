// AutoRoutesRoutesTests.cs
// The drawing given back to several flows at once through the whole host on both databases (plan 3.1, L2 C): their sides and passages left to it in one operation, undone and done again by one undo and one redo; its refusals.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

public sealed class AutoRoutesRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static int VersionOf(JsonElement map, Guid flowId) => StructureCalls.FlowOf(map, flowId).GetProperty("version").GetInt32();

    private static JsonValueKind RouteKind(JsonElement map, Guid flowId) => StructureCalls.FlowOf(map, flowId).GetProperty("route").ValueKind;

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Several_flows_given_back_to_the_drawing_are_one_operation_undone_and_done_again(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var a = await project.AddStepAsync(0, "Recevoir");
        var b = await project.AddStepAsync(1, "Traiter");
        var c = await project.AddStepAsync(2, "Classer");
        var first = await project.AddFlowAsync(a.Id, b.Id);
        var second = await project.AddFlowAsync(b.Id, c.Id);
        var plain = await project.AddFlowAsync(a.Id, c.Id);
        var map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{first}/route", new { source_side = "top" }, VersionOf(map, first))).Ok();
        map = await project.MapAsync();
        (await facilitator.PutAsync($"/api/flows/{second}/route", new { via = new { aisle = 1 } }, VersionOf(map, second))).Ok();

        var reply = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/routes/auto", new { flows = new[] { first, second, plain, first } })).Ok();
        Assert.Equal([first, second, plain], reply.GetProperty("ids").EnumerateArray().Select(id => id.GetGuid()));
        map = await project.MapAsync();
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null), (RouteKind(map, first), RouteKind(map, second), RouteKind(map, plain)));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal("retracer_liens", undone.GetProperty("code").GetString());
        map = await project.MapAsync();
        Assert.Equal("top", StructureCalls.FlowOf(map, first).GetProperty("route").GetProperty("source_side").GetString());
        Assert.Equal(1, StructureCalls.FlowOf(map, second).GetProperty("route").GetProperty("via").GetProperty("aisle").GetInt32());
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Ok();
        map = await project.MapAsync();
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (RouteKind(map, first), RouteKind(map, second)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task No_flow_a_flow_not_of_the_map_or_whom_may_not_change_the_map_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var a = await project.AddStepAsync(0, "Recevoir");
        var b = await project.AddStepAsync(1, "Traiter");
        var flow = await project.AddFlowAsync(a.Id, b.Id);
        var path = $"/api/maps/{project.RootMapId}/routes/auto";

        Assert.Equal("flows", (await facilitator.PostAsync(path, new { flows = Array.Empty<Guid>() })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("flows", (await facilitator.PostAsync(path, new { })).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        (await facilitator.PostAsync(path, new { flows = new[] { flow, Guid.NewGuid() } })).Refused(404, "INTROUVABLE");
        (await contributor.PostAsync(path, new { flows = new[] { flow } })).Refused(403, "ACCES_REFUSE");
        (await reader.PostAsync(path, new { flows = new[] { flow } })).Refused(403, "ACCES_REFUSE");
        (await facilitator.PostAsync($"/api/maps/{Guid.NewGuid()}/routes/auto", new { flows = new[] { flow } })).Refused(404, "INTROUVABLE");
    }
}
