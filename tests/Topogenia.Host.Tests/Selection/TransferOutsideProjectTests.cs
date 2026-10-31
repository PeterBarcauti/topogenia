// TransferOutsideProjectTests.cs
// A cut step of another project pasted on a map: TRANSFERT_HORS_PROJET for who may read that project, INTROUVABLE for anyone else (AR-71).

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Selection;

public sealed class TransferOutsideProjectTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static async Task<Guid> StepAsync(ApiClient client, Guid mapId, Guid laneId) =>
        (await client.PostAsync($"/api/maps/{mapId}/steps", new { lane_id = laneId, column = 0, label = "Commander" })).Ok().GetProperty("id").GetGuid();

    private static object Transfer(Guid stepId, Guid laneId) => new { steps = new[] { new { id = stepId, lane_id = laneId, column = 3, row = 0 } }, cut = true };

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_cut_in_another_project_of_the_user_is_refused_as_outside_the_project_and_nothing_moves(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var origin = await ApiProject.CreateAsync(host);
        var target = await ApiProject.CreateAsync(host);
        using var facilitator = target.Facilitator;
        var step = await StepAsync(facilitator, origin.RootMapId, origin.FirstLaneId);
        (await facilitator.PostAsync($"/api/maps/{target.RootMapId}/transfer", Transfer(step, target.FirstLaneId))).Refused(400, "TRANSFERT_HORS_PROJET");
        Assert.Equal(step, (await origin.MapAsync()).GetProperty("steps")[0].GetProperty("id").GetGuid());
        Assert.Equal(0, (await target.MapAsync()).GetProperty("steps").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_of_a_project_the_user_may_not_read_stays_not_found(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var target = await ApiProject.CreateAsync(host);
        using var admin = target.Admin;
        var created = (await admin.PostAsync("/api/projects", new
        {
            code = ApiProject.NewCode(),
            name = "Projet d'un autre service",
            members = new[] { new { login = Logins.Stranger, role = "facilitator", is_sponsor = false } },
        })).Ok(201);
        var hidden = (await admin.GetAsync($"/api/maps/{created.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map");
        var step = await StepAsync(admin, hidden.GetProperty("id").GetGuid(), hidden.GetProperty("lanes")[0].GetProperty("id").GetGuid());
        using var facilitator = target.Facilitator;
        (await facilitator.PostAsync($"/api/maps/{target.RootMapId}/transfer", Transfer(step, target.FirstLaneId))).Refused(404, "INTROUVABLE");
    }
}
