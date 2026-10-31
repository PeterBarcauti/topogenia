// ProjectsAndSecurityModelPortedTests.cs
// The 1.x integration tests of the rules of the model ported to the whole host on both databases, the undo of the server in place of the restore of a batch (T-MODE).

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

public sealed class ProjectsAndSecurityModelPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static IReadOnlyList<(Guid Step, Guid Lane, string? Role)> Raci(JsonElement map) =>
        [.. map.GetProperty("raci").EnumerateArray().Select(role => (role.GetProperty("step_id").GetGuid(), role.GetProperty("lane_id").GetGuid(), role.GetProperty("role").GetString()))];

    private static List<Guid> StepIds(JsonElement map) => [.. map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetGuid())];

    private static async Task SetRoleAsync(ApiProject project, Guid stepId, Guid laneId, string role, Guid? mapId = null)
    {
        using var facilitator = project.Facilitator;
        (await facilitator.PutAsync($"/api/steps/{stepId}/raci", new { lane_id = laneId, role }, await project.StepVersionAsync(stepId, mapId))).Ok();
    }

    /// <summary>The flows of a map as « source>target:label », by the labels of the steps, sorted.</summary>
    private static List<string> Arrows(JsonElement map)
    {
        string Label(Guid id) => StructureCalls.StepOf(map, id).GetProperty("label").GetString()!;
        return [.. map.GetProperty("flows").EnumerateArray()
            .Select(flow => $"{Label(flow.GetProperty("source_step_id").GetGuid())}>{Label(flow.GetProperty("target_step_id").GetGuid())}:{flow.GetProperty("label").GetString()}")
            .Order(StringComparer.Ordinal)];
    }

    // T-MODE-01
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_deleted_step_comes_back_with_its_flow_by_one_undo_and_is_not_restored_a_second_time(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (a, b) = (await project.AddStepAsync(0, "A"), await project.AddStepAsync(1, "B"));
        await project.AddFlowAsync(a.Id, b.Id);
        var deleted = (await facilitator.DeleteAsync($"/api/steps/{b.Id}", await project.StepVersionAsync(b.Id))).Ok();
        var deletion = deleted.GetProperty("operation_id").GetGuid();
        Assert.Equal([a.Id], StepIds(deleted.GetProperty("map")));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(deletion, undone.GetProperty("id").GetGuid());
        Assert.Equal(["A>B:"], Arrows(undone.GetProperty("map")));
        var history = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Ok();
        Assert.DoesNotContain(deletion, history.GetProperty("undo").EnumerateArray().Select(item => item.GetProperty("operation_id").GetGuid()));

        var next = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.NotEqual(deletion, next.GetProperty("id").GetGuid());
        Assert.Equal([a.Id, b.Id], StepIds(next.GetProperty("map")).Order());
    }

    // T-MODE-03
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Detailing_a_selection_keeps_the_raci_roles_on_the_copied_lanes_and_drops_those_of_the_lanes_the_block_does_not_occupy(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var accounting = await project.AddLaneAsync("Comptabilité");
        var management = await project.AddLaneAsync("Direction");
        var b = await project.AddStepAsync(0, "B");
        var c = await project.AddStepAsync(1, "C", accounting);
        await SetRoleAsync(project, b.Id, accounting, "C");
        await SetRoleAsync(project, b.Id, management, "A");

        var grouped = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = new[] { b.Id, c.Id }, label = "Facturer" })).Ok();
        var detail = await project.MapAsync(grouped.GetProperty("ids")[0].GetGuid());
        var copy = detail.GetProperty("lanes").EnumerateArray().Single(lane => lane.GetProperty("name").GetString() == "Comptabilité").GetProperty("id").GetGuid();
        Assert.Equal([(b.Id, copy, "C")], Raci(detail));
    }

    // T-MODE-04
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_start_an_end_and_a_parallel_gate_take_neither_a_raci_role_nor_a_sheet(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var other = await project.AddLaneAsync("Direction");
        var steps = new[]
        {
            await project.AddStepAsync(0, "Début", type: "start"), await project.AddStepAsync(1, "Et", type: "parallel"), await project.AddStepAsync(2, "Fin", type: "end"),
        };
        foreach (var step in steps)
        {
            (await facilitator.PutAsync($"/api/steps/{step.Id}/raci", new { lane_id = other, role = "I" }, await project.StepVersionAsync(step.Id))).Refused(400, "ETAPE_SANS_CONTENU");
            (await facilitator.PutAsync($"/api/steps/{step.Id}/sheet", StepSheets.Purpose("Lancer"), await StepSheets.ReadVersionAsync(facilitator, step.Id)))
                .Refused(400, "ETAPE_SANS_CONTENU");
        }
        var map = await project.MapAsync();
        Assert.Empty(Raci(map));
        Assert.All(steps, step => Assert.Equal("empty", StructureCalls.StepOf(map, step.Id).GetProperty("sheet").GetProperty("status").GetString()));
    }

    // T-MODE-05
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_login_in_the_upn_form_is_refused_and_no_user_is_created(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using var admin = host.As(Logins.Admin);
        (await admin.GetAsync("/api/me")).Ok();
        var before = (await admin.GetAsync($"/api/admin/audit?kind={AuditKinds.UserCreated}&limit=1000")).Ok().GetArrayLength();
        using var upn = host.As("isabelle@exemple.fr");
        (await upn.GetAsync("/api/me")).Refused(403, "ACCES_REFUSE");
        (await upn.GetAsync("/api/projects")).Refused(403, "ACCES_REFUSE");
        Assert.Equal(before, (await admin.GetAsync($"/api/admin/audit?kind={AuditKinds.UserCreated}&limit=1000")).Ok().GetArrayLength());
    }

    // T-MODE-06
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_of_a_detail_is_refused_once_the_detailed_map_was_filled_and_nothing_is_lost(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var e = await project.AddStepAsync(0, "E");
        var (subMap, subLane) = await project.DetailAsync(e.Id);
        var inside = await project.AddStepAsync(0, "Dans le détail", subLane, subMap);

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Refused(409, "CARTE_MODIFIEE");
        Assert.Equal([inside.Id], StepIds(await project.MapAsync(subMap)));
        Assert.Equal(subMap, StructureCalls.StepOf(await project.MapAsync(), e.Id).GetProperty("sub_map_id").GetGuid());
    }

    // T-MODE-07
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_cut_and_paste_just_before_the_successor_keeps_the_flow_and_its_undo_leaves_no_extra_flow(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var other = await project.AddLaneAsync("Comptabilité");
        var (a, x, b) = (await project.AddStepAsync(0, "A"), await project.AddStepAsync(1, "X"), await project.AddStepAsync(2, "B"));
        await project.AddFlowAsync(a.Id, x.Id);
        await project.AddFlowAsync(x.Id, b.Id);

        var moved = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/transfer", new
        {
            steps = new[] { new { id = x.Id, lane_id = other, column = 1 } },
            cut = true,
            anchor = new { step_id = b.Id, position = "before" },
        })).Ok();
        Assert.Equal(["A>X:", "X>B:"], Arrows(moved.GetProperty("map")));
        Assert.Equal(other, StructureCalls.StepOf(moved.GetProperty("map"), x.Id).GetProperty("lane_id").GetGuid());

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(moved.GetProperty("operation_id").GetGuid(), undone.GetProperty("id").GetGuid());
        Assert.Equal(["A>X:", "X>B:"], Arrows(undone.GetProperty("map")));
        Assert.Equal(project.FirstLaneId, StructureCalls.StepOf(undone.GetProperty("map"), x.Id).GetProperty("lane_id").GetGuid());
    }

    // T-MODE-08
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_cut_and_paste_after_a_decision_takes_the_label_of_the_branch_and_replaces_the_stitched_flow(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var d = await project.AddStepAsync(0, "D", type: "decision");
        var (x, b, c) = (await project.AddStepAsync(1, "X"), await project.AddStepAsync(2, "B"), await project.AddStepAsync(3, "C"));
        await project.AddFlowAsync(d.Id, x.Id, "oui");
        await project.AddFlowAsync(x.Id, b.Id);
        await project.AddFlowAsync(d.Id, c.Id, "non");

        var moved = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/transfer", new
        {
            steps = new[] { new { id = x.Id, lane_id = project.FirstLaneId, column = 5 } },
            cut = true,
            anchor = new { step_id = d.Id, position = "after" },
        })).Ok();
        Assert.Equal(["D>C:non", "D>X:oui", "X>B:"], Arrows(moved.GetProperty("map")));
        Assert.Equal(["D>C:non", "D>X:oui", "X>B:"], Arrows(await project.MapAsync()));
    }

    // T-MODE-09
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_lanes_of_one_name_do_not_block_a_move_to_another_map_the_roles_stay_hidden_and_nothing_is_lost(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (first, second) = (await project.AddLaneAsync("Compta"), await project.AddLaneAsync("Compta"));
        var e = await project.AddStepAsync(0, "E");
        var macro = await project.AddStepAsync(1, "M");
        await SetRoleAsync(project, e.Id, first, "C");
        await SetRoleAsync(project, e.Id, second, "I");
        var (subMap, subLane) = await project.DetailAsync(macro.Id);
        await project.AddLaneAsync("Compta", subMap);

        var moved = (await facilitator.PostAsync($"/api/maps/{subMap}/transfer", new { steps = new[] { new { id = e.Id, lane_id = subLane, column = 0 } }, cut = true })).Ok();
        Assert.Contains(e.Id, StepIds(moved.GetProperty("map")));
        Assert.Empty(Raci(moved.GetProperty("map")));

        (await facilitator.PostAsync($"/api/maps/{subMap}/undo")).Ok();
        Assert.Equal(new (Guid, Guid, string?)[] { (e.Id, first, "C"), (e.Id, second, "I") }.Order(), Raci(await project.MapAsync()).Order());
    }

    // T-MODE-10
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_raci_roles_come_back_when_the_block_is_moved_back_to_its_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var accounting = await project.AddLaneAsync("Comptabilité");
        var b = await project.AddStepAsync(0, "B");
        var c = await project.AddStepAsync(1, "C", accounting);
        await SetRoleAsync(project, b.Id, accounting, "C");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = new[] { b.Id, c.Id }, label = "Facturer" })).Ok();

        var back = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/transfer", new
        {
            steps = new[] { new { id = b.Id, lane_id = project.FirstLaneId, column = 0 }, new { id = c.Id, lane_id = accounting, column = 1 } },
        })).Ok();
        Assert.Equal([(b.Id, accounting, "C")], Raci(back.GetProperty("map")));
    }

    // T-MODE-10
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_raci_roles_come_back_when_the_grouping_is_undone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var accounting = await project.AddLaneAsync("Comptabilité");
        var b = await project.AddStepAsync(0, "B");
        var c = await project.AddStepAsync(1, "C", accounting);
        await SetRoleAsync(project, b.Id, accounting, "C");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = new[] { b.Id, c.Id }, label = "Facturer" })).Ok();
        Assert.Empty(Raci(await project.MapAsync()));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal("regrouper", undone.GetProperty("code").GetString());
        Assert.Equal([(b.Id, accounting, "C")], Raci(undone.GetProperty("map")));
    }

    // T-MODE-11
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Reattaching_a_map_to_a_step_whose_detailed_map_is_empty_replaces_the_empty_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (a, b) = (await project.AddStepAsync(0, "A"), await project.AddStepAsync(1, "B"));
        var (full, fullLane) = await project.DetailAsync(a.Id);
        await project.AddStepAsync(0, "Dedans", fullLane, full);
        var (empty, _) = await project.DetailAsync(b.Id);

        var version = (await project.MapAsync(full)).GetProperty("version").GetInt32();
        (await facilitator.PostAsync($"/api/maps/{full}/reattach", new { step_id = b.Id }, version)).Ok();
        Assert.Equal(b.Id, (await project.MapAsync(full)).GetProperty("parent_step_id").GetGuid());
        var root = await project.MapAsync();
        Assert.Equal(full, StructureCalls.StepOf(root, b.Id).GetProperty("sub_map_id").GetGuid());
        Assert.Equal(JsonValueKind.Null, StructureCalls.StepOf(root, a.Id).GetProperty("sub_map_id").ValueKind);
        (await facilitator.GetAsync($"/api/maps/{empty}")).Refused(404, "INTROUVABLE");
    }

    // T-MODE-12
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_does_not_come_back_on_a_map_deleted_since(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var e = await project.AddStepAsync(0, "E");
        var (subMap, _) = await project.DetailAsync(e.Id);
        var lane = await project.AddLaneAsync("Logistique", subMap);
        (await facilitator.DeleteAsync($"/api/lanes/{lane}", await project.LaneVersionAsync(lane, subMap))).Ok();
        (await facilitator.DeleteAsync($"/api/maps/{subMap}", (await project.MapAsync(subMap)).GetProperty("version").GetInt32())).Ok();

        (await facilitator.PostAsync($"/api/maps/{subMap}/undo")).Refused(409, "CARTE_MODIFIEE");
        (await facilitator.GetAsync($"/api/maps/{subMap}")).Refused(404, "INTROUVABLE");
    }
}
