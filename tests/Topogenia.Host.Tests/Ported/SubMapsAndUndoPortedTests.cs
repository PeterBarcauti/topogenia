// SubMapsAndUndoPortedTests.cs
// The 1.x sub-map tests (SousCartesEtAnnulationTests.SousCartesTests and SousCarteVideTests, T-SCAN, T-SCVI) ported to the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.SubMapsAndUndoPortedCalls;

namespace Topogenia.Host.Tests.Ported;

public sealed class SubMapsAndUndoPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static IReadOnlyList<string?> Warnings(JsonElement reply) => [.. reply.GetProperty("warnings").EnumerateArray().Select(code => code.GetString())];

    private static async Task<JsonElement> RootStepAsync(ApiProject project, Guid stepId) => StructureCalls.StepOf(await project.MapAsync(), stepId);

    /// <summary>T-SCAN-06.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Detailing_copies_the_lane_of_the_step_and_builds_the_breadcrumb_and_the_paths(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var root = await project.MapAsync();
        var lane = root.GetProperty("lanes")[0];
        (await facilitator.PatchAsync($"/api/lanes/{project.FirstLaneId}", new
        {
            name = "CDG Lisbonne",
            color = "#E0F7FA",
            translations = new Dictionary<string, string?> { ["en"] = "Controlling Lisbon" },
        }, lane.GetProperty("version").GetInt32())).Ok();
        await project.AddStepAsync(0, "Avant");
        var macro = await project.AddStepAsync(1, "Clôture mensuelle");
        var (subMap, subLane) = await project.DetailAsync(macro.Id);

        Assert.Equal(subMap, (await RootStepAsync(project, macro.Id)).GetProperty("sub_map_id").GetGuid());
        var detail = await project.MapAsync(subMap);
        Assert.Equal(("Clôture mensuelle", macro.Id, 2), (detail.GetProperty("name").GetString(), detail.GetProperty("parent_step_id").GetGuid(),
            detail.GetProperty("depth").GetInt32()));
        var copy = Assert.Single(detail.GetProperty("lanes").EnumerateArray());
        Assert.Equal(("CDG Lisbonne", "#E0F7FA", "Controlling Lisbon"), (copy.GetProperty("name").GetString(), copy.GetProperty("color").GetString(),
            copy.GetProperty("translations").GetProperty("en").GetString()));
        Assert.NotEqual(lane.GetProperty("code").GetString(), copy.GetProperty("code").GetString());
        Assert.Equal([root.GetProperty("name").GetString(), "Clôture mensuelle"],
            detail.GetProperty("breadcrumb").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.Equal([project.RootMapId, subMap], detail.GetProperty("breadcrumb").EnumerateArray().Select(item => item.GetProperty("map_id").GetGuid()));

        // Paths: the macro step is the second step of the root map, its steps are 2.1 and 2.2.
        await project.AddStepAsync(0, "Sous 1", subLane, subMap);
        await project.AddStepAsync(1, "Sous 2", subLane, subMap);
        Assert.Equal(["2.1", "2.2"], (await project.MapAsync(subMap)).GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("path").GetString()));
    }

    /// <summary>T-SCAN-08: the warning comes with the fifth level exactly, the detail is not refused.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Detailing_beyond_four_levels_warns_without_refusing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (mapId, laneId) = (project.RootMapId, project.FirstLaneId);
        for (var level = 2; level <= 5; level++)
        {
            var step = await project.AddStepAsync(0, $"Niveau {level}", laneId, mapId);
            var reply = (await facilitator.PostAsync($"/api/steps/{step.Id}/detail", null, await project.StepVersionAsync(step.Id, mapId))).Ok();
            Assert.Equal(level > 4, Warnings(reply).Contains("PROFONDEUR_DEPASSEE"));
            mapId = reply.GetProperty("id").GetGuid();
            laneId = (await project.MapAsync(mapId)).GetProperty("lanes")[0].GetProperty("id").GetGuid();
        }
        var deepest = await project.MapAsync(mapId);
        Assert.Equal((5, 4, 5), (deepest.GetProperty("depth").GetInt32(), deepest.GetProperty("recommended_depth").GetInt32(),
            deepest.GetProperty("breadcrumb").GetArrayLength()));
    }

    /// <summary>T-SCAN-10: three levels, every level below the macro step goes.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Deleting_a_macro_step_with_the_delete_choice_takes_its_whole_descent(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var macro = await project.AddStepAsync(0, "Macro");
        var (level2, lane2) = await project.DetailAsync(macro.Id);
        var below = await project.AddStepAsync(0, "Sous", lane2, level2);
        var (level3, lane3) = await project.DetailAsync(below.Id, level2);
        await project.AddStepAsync(0, "Plus bas", lane3, level3);

        (await facilitator.DeleteAsync($"/api/steps/{macro.Id}?sub_map=delete", await project.StepVersionAsync(macro.Id))).Ok();
        Assert.Equal(0, (await project.MapAsync()).GetProperty("steps").GetArrayLength());
        (await facilitator.GetAsync($"/api/maps/{level2}")).Refused(404, "INTROUVABLE");
        (await facilitator.GetAsync($"/api/maps/{level3}")).Refused(404, "INTROUVABLE");
        (await facilitator.GetAsync($"/api/steps/{below.Id}/sheet")).Refused(404, "INTROUVABLE");
    }

    /// <summary>T-SCAN-12.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_sub_map_is_not_reattached_inside_its_own_descent_the_root_map_never_and_never_to_another_project(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var macro = await project.AddStepAsync(0, "Macro");
        var (subMap, subLane) = await project.DetailAsync(macro.Id);
        var inside = await project.AddStepAsync(0, "Dedans", subLane, subMap);
        (await facilitator.PostAsync($"/api/maps/{subMap}/reattach", new { step_id = inside.Id }, await MapVersionAsync(project, subMap)))
            .Refused(400, "RATTACHEMENT_INVALIDE");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/reattach", new { step_id = inside.Id }, await MapVersionAsync(project, project.RootMapId)))
            .Refused(400, "CARTE_RACINE");
        // The facilitator is a member of the other project too: the step is refused for its project, not for his rights.
        var other = await ApiProject.CreateAsync(project.Host);
        var elsewhere = await other.AddStepAsync(0, "Ailleurs");
        (await facilitator.PostAsync($"/api/maps/{subMap}/reattach", new { step_id = elsewhere.Id }, await MapVersionAsync(project, subMap)))
            .Refused(400, "RATTACHEMENT_INVALIDE");
        Assert.Equal(macro.Id, (await project.MapAsync(subMap)).GetProperty("parent_step_id").GetGuid());
        Assert.Equal(JsonValueKind.Null, StructureCalls.StepOf(await other.MapAsync(), elsewhere.Id).GetProperty("sub_map_id").ValueKind);
    }

    /// <summary>T-SCAN-13: a facilitator of another project, no member of this one, does not find its maps.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_map_of_another_project_is_not_found_for_a_facilitator_who_is_no_member_of_it(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var macro = await project.AddStepAsync(0, "Macro");
        var (subMap, _) = await project.DetailAsync(macro.Id);
        await ProjectLedByTheStrangerAsync(project.Host);
        using var stranger = project.Stranger;
        (await stranger.DeleteAsync($"/api/maps/{subMap}", await MapVersionAsync(project, subMap))).Refused(404, "INTROUVABLE");
        (await stranger.DeleteAsync($"/api/maps/{project.RootMapId}", await MapVersionAsync(project, project.RootMapId))).Refused(404, "INTROUVABLE");
        Assert.Equal(subMap, (await RootStepAsync(project, macro.Id)).GetProperty("sub_map_id").GetGuid());
    }

    /// <summary>T-SCAN-14.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_root_map_of_its_project_is_not_deleted(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        (await facilitator.DeleteAsync($"/api/maps/{project.RootMapId}", await MapVersionAsync(project, project.RootMapId))).Refused(400, "CARTE_RACINE");
        Assert.Equal(project.RootMapId, (await project.MapAsync()).GetProperty("id").GetGuid());
    }

    /// <summary>T-SCVI-01.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_sub_map_stays_empty_until_a_step_is_added_a_lane_is_not_enough(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = await project.AddStepAsync(0, "B");
        var (subMap, subLane) = await project.DetailAsync(step.Id);
        var read = await RootStepAsync(project, step.Id);
        Assert.Equal((subMap, true), (read.GetProperty("sub_map_id").GetGuid(), read.GetProperty("sub_map_empty").GetBoolean()));

        // A lane added is not enough: only the steps count.
        await project.AddLaneAsync("Autre", subMap);
        Assert.True((await RootStepAsync(project, step.Id)).GetProperty("sub_map_empty").GetBoolean());

        var inside = await project.AddStepAsync(0, "Dedans", subLane, subMap);
        Assert.False((await RootStepAsync(project, step.Id)).GetProperty("sub_map_empty").GetBoolean());
        (await facilitator.DeleteAsync($"/api/steps/{inside.Id}", await project.StepVersionAsync(inside.Id, subMap))).Ok();
        Assert.True((await RootStepAsync(project, step.Id)).GetProperty("sub_map_empty").GetBoolean());
    }

    /// <summary>T-SCVI-03: no choice is asked, and the server undo gives back each step with its empty sub-map, the last deletion first.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_with_an_empty_sub_map_is_deleted_without_question_and_the_undo_gives_it_back_with_its_sub_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (b, c) = (await project.AddStepAsync(0, "B"), await project.AddStepAsync(1, "C"));
        var (subB, _) = await project.DetailAsync(b.Id);
        var (subC, _) = await project.DetailAsync(c.Id);

        (await facilitator.DeleteAsync($"/api/steps/{b.Id}", await project.StepVersionAsync(b.Id))).Ok();
        (await facilitator.GetAsync($"/api/maps/{subB}")).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/deletion", new { steps = new[] { c.Id } })).Ok();
        Assert.Equal(0, (await project.MapAsync()).GetProperty("steps").GetArrayLength());

        var first = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok().GetProperty("map");
        Assert.Equal((subC, true), (StructureCalls.StepOf(first, c.Id).GetProperty("sub_map_id").GetGuid(), StructureCalls.StepOf(first, c.Id).GetProperty("sub_map_empty").GetBoolean()));
        var second = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok().GetProperty("map");
        Assert.Equal((subB, true), (StructureCalls.StepOf(second, b.Id).GetProperty("sub_map_id").GetGuid(), StructureCalls.StepOf(second, b.Id).GetProperty("sub_map_empty").GetBoolean()));
        Assert.Equal(b.Id, (await project.MapAsync(subB)).GetProperty("parent_step_id").GetGuid());
    }

    /// <summary>T-SCVI-04.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Becoming_a_start_or_an_end_removes_an_empty_sub_map_but_is_refused_with_a_filled_one(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (b, c) = (await project.AddStepAsync(0, "B"), await project.AddStepAsync(1, "C"));
        var (subB, _) = await project.DetailAsync(b.Id);
        var (subC, laneC) = await project.DetailAsync(c.Id);
        await project.AddStepAsync(0, "Dedans", laneC, subC);

        var ended = StructureCalls.StepOf((await facilitator.PatchAsync($"/api/steps/{b.Id}", new { type = "end" }, await project.StepVersionAsync(b.Id))).Ok()
            .GetProperty("map"), b.Id);
        Assert.Equal(("end", JsonValueKind.Null), (ended.GetProperty("type").GetString(), ended.GetProperty("sub_map_id").ValueKind));
        (await facilitator.GetAsync($"/api/maps/{subB}")).Refused(404, "INTROUVABLE");

        (await facilitator.PatchAsync($"/api/steps/{c.Id}", new { type = "start" }, await project.StepVersionAsync(c.Id))).Refused(400, "ETAPE_NON_DETAILLABLE");
        // The other types stay allowed on a detailed step.
        (await facilitator.PatchAsync($"/api/steps/{c.Id}", new { type = "decision" }, await project.StepVersionAsync(c.Id))).Ok();
        var decision = await RootStepAsync(project, c.Id);
        Assert.Equal(("decision", subC), (decision.GetProperty("type").GetString(), decision.GetProperty("sub_map_id").GetGuid()));
    }
}
