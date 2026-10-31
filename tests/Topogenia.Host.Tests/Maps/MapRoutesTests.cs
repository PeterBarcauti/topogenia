// MapRoutesTests.cs
// The routes of a map and of its lanes through the whole host on both databases: read, lanes, undo, redo, history (annex A 1, spec 4.6).

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Maps;

public sealed class MapRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static IEnumerable<string?> LaneNames(JsonElement map) => map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("name").GetString());

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_read_of_a_map_gives_the_map_and_the_history_of_the_reader(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        var read = (await reader.GetAsync($"/api/maps/{project.RootMapId}")).Ok();
        Assert.Equal((project.RootMapId, project.Code), (read.GetProperty("map").GetProperty("id").GetGuid(), read.GetProperty("map").GetProperty("project_code").GetString()));
        Assert.Equal(["Acteur 1"], LaneNames(read.GetProperty("map")));
        Assert.Equal((0, 0), (read.GetProperty("history").GetProperty("undo").GetArrayLength(), read.GetProperty("history").GetProperty("redo").GetArrayLength()));
        Assert.False(read.GetProperty("map").GetProperty("rights").GetProperty("structure").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_added_is_answered_with_its_code_the_operation_and_the_map_then_undone_and_redone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var added = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Ok();
        Assert.Equal($"{project.Code}-C02", added.GetProperty("code").GetString());
        var operation = added.GetProperty("operation_id").GetGuid();
        Assert.Equal(["Acteur 1", "Achats"], LaneNames(added.GetProperty("map")));
        Assert.Equal(operation, added.GetProperty("history").GetProperty("undo")[0].GetProperty("operation_id").GetGuid());

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal((operation, "creer_couloir"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        Assert.Equal(["Acteur 1"], LaneNames(undone.GetProperty("map")));
        var history = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Ok();
        Assert.Equal((0, 1), (history.GetProperty("undo").GetArrayLength(), history.GetProperty("redo").GetArrayLength()));

        var redone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Ok();
        Assert.Equal(["Acteur 1", "Achats"], LaneNames(redone.GetProperty("map")));
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_created_with_translations_records_them_as_manual_and_ignores_the_source_language(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        // The lane takes the language of its author (French): its French translation is the name itself and is not written.
        var added = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes",
            new { name = "Achats", translations = new Dictionary<string, string> { ["en"] = "Purchasing", ["fr"] = "Approvisionnement" } })).Ok();
        var id = added.GetProperty("id").GetGuid();
        var lane = (await project.MapAsync()).GetProperty("lanes").EnumerateArray().Single(candidate => candidate.GetProperty("id").GetGuid() == id);
        Assert.Equal("Achats", lane.GetProperty("name").GetString());
        Assert.Equal([("en", "Purchasing")], lane.GetProperty("translations").EnumerateObject().Select(entry => (entry.Name, entry.Value.GetString())));

        var text = (await facilitator.GetAsync($"/api/projects/{project.Id}/translations?scope=all")).Ok().GetProperty("texts").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetGuid() == id);
        Assert.Equal(("lane", "fr", "Achats"), (text.GetProperty("type").GetString(), text.GetProperty("language").GetString(), text.GetProperty("original").GetString()));
        Assert.Equal([("en", "manual")], text.GetProperty("sources").EnumerateObject().Select(entry => (entry.Name, entry.Value.GetString())));
        Assert.Equal([("en", "Purchasing")], text.GetProperty("translations").EnumerateObject().Select(entry => (entry.Name, entry.Value.GetString())));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_is_renamed_with_its_version_and_a_null_actor_removes_the_actor(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = (await project.MapAsync()).GetProperty("lanes")[0];
        var patched = (await facilitator.PatchAsync($"/api/lanes/{project.FirstLaneId}", new { name = "Ventes", actor_user_id = (Guid?)null },
            lane.GetProperty("version").GetInt32())).Ok();
        var after = patched.GetProperty("map").GetProperty("lanes")[0];
        Assert.Equal(("Ventes", JsonValueKind.Null), (after.GetProperty("name").GetString(), after.GetProperty("actor_user_id").ValueKind));
        Assert.Equal(lane.GetProperty("type").GetString(), after.GetProperty("type").GetString());
        (await facilitator.PatchAsync($"/api/lanes/{project.FirstLaneId}", new { color = "rouge" }, after.GetProperty("version").GetInt32()))
            .Refused(400, "VALEUR_INVALIDE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_lanes_are_reordered_by_the_complete_list_of_their_identifiers(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var second = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Ok().GetProperty("id").GetGuid();
        var reordered = (await facilitator.PutAsync($"/api/maps/{project.RootMapId}/lane-order", new[] { second, project.FirstLaneId })).Ok();
        Assert.Equal(["Achats", "Acteur 1"], LaneNames(reordered.GetProperty("map")));
        Assert.Equal([second, project.FirstLaneId], reordered.GetProperty("ids").EnumerateArray().Select(id => id.GetGuid()));
        (await facilitator.PutAsync($"/api/maps/{project.RootMapId}/lane-order", new[] { second })).Refused(400, "ORDRE_COULOIRS_INVALIDE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_with_steps_needs_a_choice_and_is_deleted_with_them(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Ok().GetProperty("id").GetGuid();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = lane, column = 0, label = "Commander" })).Ok();
        var version = await project.LaneVersionAsync(lane);
        Assert.Equal(1, (await facilitator.DeleteAsync($"/api/lanes/{lane}", version)).Refused(409, "COULOIR_NON_VIDE").GetProperty("step_count").GetInt32());
        (await facilitator.DeleteAsync($"/api/lanes/{lane}?steps=burn", version)).Refused(400, "VALEUR_INVALIDE");
        var deleted = (await facilitator.DeleteAsync($"/api/lanes/{lane}?steps=delete", version)).Ok();
        Assert.Equal(["Acteur 1"], LaneNames(deleted.GetProperty("map")));
        Assert.Equal(0, deleted.GetProperty("map").GetProperty("steps").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_needs_its_lane_and_its_column_never_an_empty_identifier_nor_zero(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/steps";
        Assert.Equal("lane_id", (await facilitator.PostAsync(url, new { column = 0, label = "Commander" })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        Assert.Equal("column", (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, label = "Commander" })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        var created = (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column = 2, label = "Commander" })).Ok();
        var step = created.GetProperty("map").GetProperty("steps")[0];
        Assert.Equal((created.GetProperty("id").GetGuid(), 2, "task"), (step.GetProperty("id").GetGuid(), step.GetProperty("column").GetInt32(), step.GetProperty("type").GetString()));
    }
}
