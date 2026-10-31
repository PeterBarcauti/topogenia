// StructureLanePortedTests.cs
// The 1.x lane tests (StructureTests.CouloirsTests, T-STRU-01 to 12) ported to the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.StructurePortedCalls;

namespace Topogenia.Host.Tests.Ported;

public sealed class StructureLanePortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static IReadOnlyList<string?> Colors(JsonElement map) =>
        [.. map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("color").GetString())];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_added_at_the_end_then_at_the_head_gets_the_next_code_renumbered_orders_and_the_next_tint(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var sap = await AddLaneAsync(project, "SAP");
        var head = await AddLaneAsync(project, "Responsable", sortOrder: 0);
        Assert.Equal(($"{project.Code}-C02", $"{project.Code}-C03"), (sap.GetProperty("code").GetString(), head.GetProperty("code").GetString()));

        var map = await project.MapAsync();
        Assert.Equal(["Responsable", "Acteur 1", "SAP"], LaneNames(map));
        Assert.Equal([0, 1, 2], LaneOrders(map));
        // The tint follows the number of lanes of the map at the addition (spec 9.2, AR-45): second tint, then third.
        Assert.Equal("#E5E0F8", LaneOf(map, sap.GetProperty("id").GetGuid()).GetProperty("color").GetString());
        Assert.Equal("#D9E9FA", LaneOf(map, head.GetProperty("id").GetGuid()).GetProperty("color").GetString());
        Assert.Equal(3, Colors(map).Distinct().Count());

        // The addition is an operation of the log (4.6): its undo takes the head lane away and renumbers the others.
        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal("creer_couloir", undone.GetProperty("code").GetString());
        Assert.Equal(["Acteur 1", "SAP"], LaneNames(undone.GetProperty("map")));
        Assert.Equal([0, 1], LaneOrders(undone.GetProperty("map")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_added_in_the_middle_takes_its_order_and_an_order_out_of_bounds_puts_it_last(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        await AddLaneAsync(project, "B");
        await AddLaneAsync(project, "Milieu", sortOrder: 1);
        await AddLaneAsync(project, "Fin", sortOrder: 99);
        var map = await project.MapAsync();
        Assert.Equal(["Acteur 1", "Milieu", "B", "Fin"], LaneNames(map));
        Assert.Equal([0, 1, 2, 3], LaneOrders(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_type_and_the_colour_of_a_lane_are_checked_and_a_lower_case_colour_is_kept_in_capitals(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = LanesUrl(project);
        Assert.Equal("type", (await facilitator.PostAsync(url, new { name = "X", type = "robot" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("color", (await facilitator.PostAsync(url, new { name = "X", type = "system", color = "rouge" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("color", (await facilitator.PostAsync(url, new { name = "X", type = "system", color = "#C8E6C" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Single(LaneNames(await project.MapAsync()));

        var upper = (await facilitator.PostAsync(url, new { name = "X", type = "system", color = "#C8E6C9" })).Ok().GetProperty("id").GetGuid();
        var lower = (await facilitator.PostAsync(url, new { name = "Y", type = "department", color = "#ffe0b2" })).Ok().GetProperty("id").GetGuid();
        var map = await project.MapAsync();
        Assert.Equal(("system", "#C8E6C9"), (LaneOf(map, upper).GetProperty("type").GetString(), LaneOf(map, upper).GetProperty("color").GetString()));
        Assert.Equal(("department", "#FFE0B2"), (LaneOf(map, lower).GetProperty("type").GetString(), LaneOf(map, lower).GetProperty("color").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_renamed_with_its_version_keeps_its_code_records_its_translation_and_an_empty_translation_removes_it(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var before = LaneOf(await project.MapAsync(), project.FirstLaneId);
        (await facilitator.PatchAsync($"/api/lanes/{project.FirstLaneId}",
            new { name = "Contrôle de gestion", translations = new Dictionary<string, string> { ["en"] = "Controlling" } }, before.GetProperty("version").GetInt32())).Ok();
        var renamed = LaneOf(await project.MapAsync(), project.FirstLaneId);
        Assert.Equal(("Contrôle de gestion", before.GetProperty("code").GetString()), (renamed.GetProperty("name").GetString(), renamed.GetProperty("code").GetString()));
        Assert.Equal("Controlling", renamed.GetProperty("translations").GetProperty("en").GetString());
        Assert.NotEqual(before.GetProperty("version").GetInt32(), renamed.GetProperty("version").GetInt32());

        (await facilitator.PatchAsync($"/api/lanes/{project.FirstLaneId}", new { translations = new Dictionary<string, string> { ["en"] = "" } },
            renamed.GetProperty("version").GetInt32())).Ok();
        var cleared = LaneOf(await project.MapAsync(), project.FirstLaneId);
        Assert.Empty(cleared.GetProperty("translations").EnumerateObject());
        Assert.Equal("Contrôle de gestion", cleared.GetProperty("name").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_actor_of_a_lane_must_be_a_member_of_the_project_and_can_be_removed(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var contributor = await MemberIdAsync(project, Logins.Contributor);
        (await PatchLaneAsync(project, facilitator, project.FirstLaneId, new { actor_user_id = contributor, type = "person" })).Ok();
        Assert.Equal(contributor, LaneOf(await project.MapAsync(), project.FirstLaneId).GetProperty("actor_user_id").GetGuid());

        Assert.Equal("actor_user_id", (await PatchLaneAsync(project, facilitator, project.FirstLaneId, new { actor_user_id = Guid.NewGuid() }))
            .Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal(contributor, LaneOf(await project.MapAsync(), project.FirstLaneId).GetProperty("actor_user_id").GetGuid());

        (await PatchLaneAsync(project, facilitator, project.FirstLaneId, new { actor_user_id = (Guid?)null })).Ok();
        Assert.Equal(JsonValueKind.Null, LaneOf(await project.MapAsync(), project.FirstLaneId).GetProperty("actor_user_id").ValueKind);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_changed_since_its_reading_is_a_conflict_naming_the_author_and_how_long_ago_and_writes_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        using var facilitator = project.Facilitator;
        var read = LaneOf(await project.MapAsync(), project.FirstLaneId).GetProperty("version").GetInt32();
        (await admin.PatchAsync($"/api/lanes/{project.FirstLaneId}", new { name = "Par l'administrateur" }, read)).Ok();
        var conflict = await facilitator.PatchAsync($"/api/lanes/{project.FirstLaneId}", new { name = "Trop tard" }, read);
        var details = conflict.Refused(409, "CONFLIT_VERSION");
        Assert.Equal(("admin", "à l'instant"), (details.GetProperty("by").GetString(), details.GetProperty("when").GetString()));
        var message = conflict.Json.GetProperty("message").GetString();
        Assert.Contains("admin", message, StringComparison.Ordinal);
        Assert.Contains("instant", message, StringComparison.Ordinal);
        Assert.Equal("Par l'administrateur", LaneOf(await project.MapAsync(), project.FirstLaneId).GetProperty("name").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_last_lane_of_a_map_is_not_deleted(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var version = await project.LaneVersionAsync(project.FirstLaneId);
        (await facilitator.DeleteAsync($"/api/lanes/{project.FirstLaneId}", version)).Refused(409, "COULOIR_DERNIER");
        (await facilitator.DeleteAsync($"/api/lanes/{project.FirstLaneId}?steps=delete", version)).Refused(409, "COULOIR_DERNIER");
        Assert.Equal(["Acteur 1"], LaneNames(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_empty_lane_is_deleted_in_one_operation_and_the_order_is_renumbered(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        await AddLaneAsync(project, "C");
        var deleted = (await facilitator.DeleteAsync($"/api/lanes/{b}", await project.LaneVersionAsync(b))).Ok();
        Assert.NotEqual(JsonValueKind.Null, deleted.GetProperty("operation_id").ValueKind);
        var map = await project.MapAsync();
        Assert.Equal(["Acteur 1", "C"], LaneNames(map));
        Assert.Equal([0, 1], LaneOrders(map));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(deleted.GetProperty("operation_id").GetGuid(), undone.GetProperty("id").GetGuid());
        Assert.Equal(["Acteur 1", "B", "C"], LaneNames(undone.GetProperty("map")));
        Assert.Equal([0, 1, 2], LaneOrders(undone.GetProperty("map")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_with_steps_needs_a_choice_and_a_move_needs_another_lane_of_the_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        var step = await project.AddStepAsync(0, "Etape", b);
        var (_, subLane) = await project.DetailAsync((await project.AddStepAsync(1, "Macro")).Id);

        var version = await project.LaneVersionAsync(b);
        Assert.Equal(1, (await facilitator.DeleteAsync($"/api/lanes/{b}", version)).Refused(409, "COULOIR_NON_VIDE").GetProperty("step_count").GetInt32());
        (await facilitator.DeleteAsync($"/api/lanes/{b}?steps=move", version)).Refused(400, "COULOIR_CIBLE_INVALIDE");
        (await facilitator.DeleteAsync($"/api/lanes/{b}?steps=move&to={b}", version)).Refused(400, "COULOIR_CIBLE_INVALIDE");
        (await facilitator.DeleteAsync($"/api/lanes/{b}?steps=move&to={subLane}", version)).Refused(400, "COULOIR_CIBLE_INVALIDE");
        (await facilitator.DeleteAsync($"/api/lanes/{b}?steps=move&to={Guid.NewGuid()}", version)).Refused(400, "COULOIR_CIBLE_INVALIDE");

        var map = await project.MapAsync();
        Assert.Equal(2, LaneNames(map).Count);
        Assert.Equal((b, 0, 0), CellOf(map, step.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_deleted_with_a_move_puts_its_steps_in_the_target_with_the_rg_c04_shift(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var a = project.FirstLaneId;
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        var x = await project.AddStepAsync(1, "X", a);
        var e1 = await project.AddStepAsync(1, "E1", b);
        var e2 = await project.AddStepAsync(2, "E2", b);
        (await facilitator.DeleteAsync($"/api/lanes/{b}?steps=move&to={a}", await project.LaneVersionAsync(b))).Ok();

        var map = await project.MapAsync();
        Assert.Equal([a], LaneIds(map));
        // E1 takes column 1 and pushes X; E2 takes column 2 and pushes X again.
        Assert.Equal((a, 1, 0), CellOf(map, e1.Id));
        Assert.Equal((a, 2, 0), CellOf(map, e2.Id));
        Assert.Equal((a, 3, 0), CellOf(map, x.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_deleted_with_its_steps_takes_their_flows(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        var x = await project.AddStepAsync(0, "X");
        var y = await project.AddStepAsync(1, "Y", b);
        await project.AddFlowAsync(x.Id, y.Id);
        (await facilitator.DeleteAsync($"/api/lanes/{b}?steps=delete", await project.LaneVersionAsync(b))).Ok();
        var map = await project.MapAsync();
        Assert.Equal(["X"], Labels(map));
        Assert.Empty(map.GetProperty("flows").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_lanes_are_reordered_only_by_their_complete_list_and_the_new_order_is_undone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var a = project.FirstLaneId;
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        var c = (await AddLaneAsync(project, "C")).GetProperty("id").GetGuid();
        var url = $"/api/maps/{project.RootMapId}/lane-order";
        (await facilitator.PutAsync(url, new[] { c, a, b })).Ok();
        Assert.Equal(["C", "Acteur 1", "B"], LaneNames(await project.MapAsync()));

        (await facilitator.PutAsync(url, new[] { c, a })).Refused(400, "ORDRE_COULOIRS_INVALIDE");
        (await facilitator.PutAsync(url, new[] { c, a, a })).Refused(400, "ORDRE_COULOIRS_INVALIDE");
        (await facilitator.PutAsync(url, new[] { c, a, Guid.NewGuid() })).Refused(400, "ORDRE_COULOIRS_INVALIDE");
        Assert.Equal(["C", "Acteur 1", "B"], LaneNames(await project.MapAsync()));

        // The order of the lanes is an operation of the log (4.6).
        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(["Acteur 1", "B", "C"], LaneNames(undone.GetProperty("map")));
    }
}
