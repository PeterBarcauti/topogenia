// RedoRefusalRoutesTests.cs
// A refused redo through the whole host on both databases: 409 OPERATION_DEPASSEE naming its author, then nothing left to undo or to redo (annex B 2.6-48, AR-05).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

/// <summary>
/// The 1.x cleared the history of the map when `retablir` failed (`magasin.ts`, `vider()`). Here the refusal of
/// `POST /api/maps/{id}/redo` clears, in a unit of its own, the undo and the redo stacks of the user on the map: the reply
/// is the refusal, the history read next is empty, and the change of the other member stays.
/// </summary>
public sealed class RedoRefusalRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static IEnumerable<string?> LaneNames(JsonElement map) => map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("name").GetString());

    private static (int Undo, int Redo) Stacks(JsonElement history) =>
        (history.GetProperty("undo").GetArrayLength(), history.GetProperty("redo").GetArrayLength());

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_redo_outdated_by_another_member_is_refused_409_with_its_author_and_clears_the_undo_and_the_redo_stacks_of_the_user(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var map = $"/api/maps/{project.RootMapId}";
        var lane = $"/api/lanes/{project.FirstLaneId}";
        (await facilitator.PostAsync($"{map}/lanes", new { name = "Ventes" })).Ok();
        (await facilitator.PatchAsync(lane, new { name = "Achats" }, await project.LaneVersionAsync(project.FirstLaneId))).Ok();
        Assert.Equal((1, 1), Stacks((await facilitator.PostAsync($"{map}/undo")).Ok().GetProperty("history")));
        (await admin.PatchAsync(lane, new { name = "Comptabilité" }, await project.LaneVersionAsync(project.FirstLaneId))).Ok();
        var adminId = (await admin.GetAsync("/api/me")).Ok().GetProperty("id").GetGuid();

        var refusal = (await facilitator.PostAsync($"{map}/redo")).Refused(409, "OPERATION_DEPASSEE");

        Assert.Equal(adminId, refusal.GetProperty("updated_by").GetGuid());
        Assert.Equal((0, 0), Stacks((await facilitator.GetAsync($"{map}/history")).Ok()));
        (await facilitator.PostAsync($"{map}/redo")).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync($"{map}/undo")).Refused(404, "INTROUVABLE");
        Assert.Equal(["Comptabilité", "Ventes"], LaneNames(await project.MapAsync()));
        Assert.Equal((1, 0), Stacks((await admin.GetAsync($"{map}/history")).Ok()));
    }
}
