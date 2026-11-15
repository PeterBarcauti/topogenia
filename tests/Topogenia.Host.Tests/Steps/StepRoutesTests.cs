// StepRoutesTests.cs
// The creation, update and deletion of a step through the whole host on both databases: POST, PATCH with If-Match, DELETE with its sub-map choice (annex A 1.3, A 1.4).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

public sealed class StepRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static async Task<Guid> UserIdAsync(ApiProject project, string login)
    {
        using var facilitator = project.Facilitator;
        var detail = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok();
        return detail.GetProperty("members").EnumerateArray().Single(member => member.GetProperty("login").GetString() == login).GetProperty("user_id").GetGuid();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_patch_changes_only_the_fields_sent_with_the_version_read_and_a_null_assignee_removes_him(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = await project.AddStepAsync(0, "Commander");
        var paul = await UserIdAsync(project, Logins.Contributor);
        var url = $"/api/steps/{step.Id}";
        var assigned = (await facilitator.PatchAsync(url, new { assignee_user_id = paul, automation_status = "automated" }, await project.StepVersionAsync(step.Id))).Ok();
        Assert.Equal((step.Id, step.Code), (assigned.GetProperty("id").GetGuid(), assigned.GetProperty("code").GetString()));
        Assert.NotEqual(JsonValueKind.Null, assigned.GetProperty("operation_id").ValueKind);
        var read = StructureCalls.StepOf(assigned.GetProperty("map"), step.Id);
        Assert.Equal((paul, "automated", "Commander"), (read.GetProperty("assignee_user_id").GetGuid(), read.GetProperty("automation_status").GetString(), read.GetProperty("label").GetString()));

        var version = read.GetProperty("version").GetInt32();
        var patched = (await facilitator.PatchAsync(url, new { label = "Facturer", assignee_user_id = (Guid?)null }, version)).Ok();
        var after = StructureCalls.StepOf(patched.GetProperty("map"), step.Id);
        Assert.Equal(("Facturer", JsonValueKind.Null), (after.GetProperty("label").GetString(), after.GetProperty("assignee_user_id").ValueKind));
        Assert.Equal(("automated", "task", 0), (after.GetProperty("automation_status").GetString(), after.GetProperty("type").GetString(), after.GetProperty("column").GetInt32()));
        Assert.Equal("Facturer", StructureCalls.StepOf(await project.MapAsync(), step.Id).GetProperty("label").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_patch_with_a_stale_version_is_a_conflict_that_names_the_author_and_writes_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = await project.AddStepAsync(0, "Commander");
        var version = await project.StepVersionAsync(step.Id);
        (await facilitator.PatchAsync($"/api/steps/{step.Id}", new { label = "Facturer" }, version)).Ok();
        var details = (await facilitator.PatchAsync($"/api/steps/{step.Id}", new { label = "Livrer" }, version)).Refused(409, "CONFLIT_VERSION");
        Assert.Equal(("anne", "à l'instant"), (details.GetProperty("by").GetString(), details.GetProperty("when").GetString()));
        (await facilitator.SendAsync(HttpMethod.Patch, $"/api/steps/{step.Id}", new { label = "Livrer" }, null, ("If-Match", "\"v1\""))).Refused(400, "VERSION_INVALIDE");
        Assert.Equal("Facturer", StructureCalls.StepOf(await project.MapAsync(), step.Id).GetProperty("label").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_is_created_in_a_lane_of_its_map_with_a_label_and_answered_with_its_code(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        var macro = await project.AddStepAsync(0, "Commander");
        var (_, subLane) = await project.DetailAsync(macro.Id);
        var url = $"/api/maps/{project.RootMapId}/steps";
        var created = (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column = 1, label = "Payer", type = "decision", row = 0 })).Ok();
        Assert.Equal($"{project.Code}-E002", created.GetProperty("code").GetString());
        Assert.Equal("creer_etape", created.GetProperty("history").GetProperty("undo")[0].GetProperty("kind").GetString());
        var step = StructureCalls.StepOf(await project.MapAsync(), created.GetProperty("id").GetGuid());
        Assert.Equal(("Payer", "decision", 1), (step.GetProperty("label").GetString(), step.GetProperty("type").GetString(), step.GetProperty("column").GetInt32()));

        Assert.Equal("lane_id", (await facilitator.PostAsync(url, new { lane_id = subLane, column = 2, label = "Livrer" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("label", (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column = 2, label = " " })).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        (await contributor.PostAsync(url, new { lane_id = project.FirstLaneId, column = 2, label = "Livrer" })).Refused(403, "ACCES_REFUSE");
        (await stranger.PostAsync(url, new { lane_id = project.FirstLaneId, column = 2, label = "Livrer" })).Refused(404, "INTROUVABLE");
        Assert.Equal(2, (await project.MapAsync()).GetProperty("steps").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_patch_moves_a_step_to_its_cell_but_refuses_a_null_lane_or_column_and_an_unknown_type(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = await project.AddLaneAsync("Achats");
        var step = await project.AddStepAsync(0, "Commander");
        var url = $"/api/steps/{step.Id}";
        var version = await project.StepVersionAsync(step.Id);
        Assert.Equal("lane_id", (await facilitator.PatchAsync(url, new { lane_id = (Guid?)null }, version)).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        Assert.Equal("column", (await facilitator.PatchAsync(url, new { column = (int?)null }, version)).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        Assert.Equal("row", (await facilitator.PatchAsync(url, new { row = (int?)null }, version)).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        Assert.Equal("type", (await facilitator.PatchAsync(url, new { type = "robot" }, version)).Refused(400, "VALEUR_INVALIDE").Field());
        var moved = StructureCalls.StepOf((await facilitator.PatchAsync(url, new { lane_id = lane, column = 3, row = 1 }, version)).Ok().GetProperty("map"), step.Id);
        Assert.Equal((lane, 3, 1, "task"), (moved.GetProperty("lane_id").GetGuid(), moved.GetProperty("column").GetInt32(), moved.GetProperty("row").GetInt32(),
            moved.GetProperty("type").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_contributor_neither_patches_nor_deletes_a_step_and_a_stranger_is_told_not_found(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        var step = await project.AddStepAsync(0, "Commander");
        (await contributor.PatchAsync($"/api/steps/{step.Id}", new { label = "Facturer" })).Refused(403, "ACCES_REFUSE");
        (await stranger.PatchAsync($"/api/steps/{step.Id}", new { label = "Facturer" })).Refused(404, "INTROUVABLE");
        (await contributor.DeleteAsync($"/api/steps/{step.Id}")).Refused(403, "ACCES_REFUSE");
        (await stranger.DeleteAsync($"/api/steps/{step.Id}")).Refused(404, "INTROUVABLE");
        Assert.Equal("Commander", StructureCalls.StepOf(await project.MapAsync(), step.Id).GetProperty("label").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_deleted_with_stitching_leaves_its_neighbours_linked_and_an_unknown_sub_map_choice_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (before, middle, after) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Valider"), await project.AddStepAsync(2, "Payer"));
        await project.AddFlowAsync(before.Id, middle.Id);
        await project.AddFlowAsync(middle.Id, after.Id);
        Assert.Equal("sub_map", (await facilitator.DeleteAsync($"/api/steps/{middle.Id}?sub_map=burn")).Refused(400, "VALEUR_INVALIDE").Field());
        var deleted = (await facilitator.DeleteAsync($"/api/steps/{middle.Id}?stitch=true", await project.StepVersionAsync(middle.Id))).Ok();
        Assert.Equal(middle.Id, deleted.GetProperty("id").GetGuid());
        Assert.NotEqual(JsonValueKind.Null, deleted.GetProperty("operation_id").ValueKind);
        var map = deleted.GetProperty("map");
        Assert.Equal([before.Id, after.Id], map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetGuid()).Order());
        Assert.Equal([(before.Id, after.Id)], StructureCalls.FlowPairs(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_deleted_without_stitching_takes_its_flows(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (before, middle) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Valider"));
        await project.AddFlowAsync(before.Id, middle.Id);
        var map = (await facilitator.DeleteAsync($"/api/steps/{middle.Id}", await project.StepVersionAsync(middle.Id))).Ok().GetProperty("map");
        Assert.Equal(before.Id, map.GetProperty("steps").EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Empty(StructureCalls.FlowPairs(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_macro_step_needs_a_choice_and_its_sub_map_is_reattached_to_the_step_named_by_to(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (macro, heir) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var (subMap, subLane) = await project.DetailAsync(macro.Id);
        await project.AddStepAsync(0, "Saisir", subLane, subMap);
        (await facilitator.PatchAsync($"/api/steps/{macro.Id}", new { type = "end" }, await project.StepVersionAsync(macro.Id))).Refused(400, "ETAPE_NON_DETAILLABLE");
        var version = await project.StepVersionAsync(macro.Id);
        (await facilitator.DeleteAsync($"/api/steps/{macro.Id}", version)).Refused(409, "MACRO_CHOIX_REQUIS");
        (await facilitator.DeleteAsync($"/api/steps/{macro.Id}?sub_map=reattach", version)).Refused(400, "RATTACHEMENT_INVALIDE");
        var deleted = (await facilitator.DeleteAsync($"/api/steps/{macro.Id}?sub_map=reattach&to={heir.Id}", version)).Ok();
        Assert.Equal(subMap, StructureCalls.StepOf(deleted.GetProperty("map"), heir.Id).GetProperty("sub_map_id").GetGuid());
        Assert.Equal(heir.Id, (await project.MapAsync(subMap)).GetProperty("parent_step_id").GetGuid());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_macro_step_deleted_with_the_delete_choice_takes_its_sub_map_with_it(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var macro = await project.AddStepAsync(0, "Commander");
        var (subMap, subLane) = await project.DetailAsync(macro.Id);
        await project.AddStepAsync(0, "Saisir", subLane, subMap);
        var map = (await facilitator.DeleteAsync($"/api/steps/{macro.Id}?sub_map=delete", await project.StepVersionAsync(macro.Id))).Ok().GetProperty("map");
        Assert.Equal(0, map.GetProperty("steps").GetArrayLength());
        (await facilitator.GetAsync($"/api/maps/{subMap}")).Refused(404, "INTROUVABLE");
    }
}
