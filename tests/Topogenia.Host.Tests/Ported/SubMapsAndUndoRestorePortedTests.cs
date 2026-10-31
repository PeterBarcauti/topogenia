// SubMapsAndUndoRestorePortedTests.cs
// The 1.x batch restore tests (SousCartesEtAnnulationTests.RestaurationTests, T-SCAN-15 to 24) ported to the server undo through the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.SubMapsAndUndoPortedCalls;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// The 1.x restored a deletion batch by `POST /api/suppressions/{lot}/restaurer`; the 2.0 undoes the last operation of the
/// user on the map (spec 4.6, AR-43). Where the 1.x scenario put a more recent change between the deletion and its
/// restoration, that change is made by a second facilitator (the administrator, a member of every project of the tests),
/// so that the undo of the first one still takes back the deletion.
/// </summary>
public sealed class SubMapsAndUndoRestorePortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static Task<ApiResponse> UndoAsync(ApiClient client, Guid mapId) => client.PostAsync($"/api/maps/{mapId}/undo");

    private static IReadOnlyList<(Guid Id, string? Code, int Column)> Cells(JsonElement map) =>
        [.. map.GetProperty("steps").EnumerateArray()
            .Select(step => (step.GetProperty("id").GetGuid(), step.GetProperty("code").GetString(), step.GetProperty("column").GetInt32()))
            .OrderBy(cell => cell.Item3)];

    /// <summary>T-SCAN-15; « once »: the deletion undone leaves the undo stack for the redo stack.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_deleted_step_comes_back_with_its_identifier_its_code_its_column_and_its_labelled_flow_once(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var x = await project.AddStepAsync(0, "X");
        var y = await project.AddStepAsync(1, "Y");
        await project.AddFlowAsync(x.Id, y.Id, "suite");
        var before = Cells(await project.MapAsync());
        var deleted = (await facilitator.DeleteAsync($"/api/steps/{y.Id}", await project.StepVersionAsync(y.Id))).Ok();
        Assert.Empty(StructureCalls.FlowPairs(deleted.GetProperty("map")));

        var undone = (await UndoAsync(facilitator, project.RootMapId)).Ok();
        Assert.Equal((deleted.GetProperty("operation_id").GetGuid(), "supprimer"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        var after = await project.MapAsync();
        Assert.Equal(before, Cells(after));
        Assert.Equal("suite", Assert.Single(after.GetProperty("flows").EnumerateArray()).GetProperty("label").GetString());
        var history = undone.GetProperty("history");
        Assert.DoesNotContain(deleted.GetProperty("operation_id").GetGuid(), UndoOperations(history));
        Assert.Equal([deleted.GetProperty("operation_id").GetGuid()], RedoOperations(history));
    }

    /// <summary>T-SCAN-16 (RG-C04).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_put_meanwhile_on_the_original_cell_is_shifted_by_the_undo(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var x = await project.AddStepAsync(1, "X");
        var y = await project.AddStepAsync(2, "Y");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/deletion", new { steps = new[] { x.Id, y.Id } })).Ok();
        (await admin.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column = 1, label = "Intruse" })).Ok();

        (await UndoAsync(facilitator, project.RootMapId)).Ok();
        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal) { ["X"] = 1, ["Y"] = 2, ["Intruse"] = 3 }, ColumnsByLabel(await project.MapAsync()));
    }

    /// <summary>T-SCAN-17: the end of the flow is deleted meanwhile by a second user.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_whose_end_disappeared_meanwhile_stays_deleted(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var x = await project.AddStepAsync(0, "X");
        var y = await project.AddStepAsync(1, "Y");
        var flow = await project.AddFlowAsync(x.Id, y.Id);
        (await facilitator.DeleteAsync($"/api/flows/{flow}", await project.FlowVersionAsync(flow))).Ok();
        (await admin.DeleteAsync($"/api/steps/{y.Id}", await project.StepVersionAsync(y.Id))).Ok();

        (await UndoAsync(facilitator, project.RootMapId)).Ok();
        var map = await project.MapAsync();
        Assert.Empty(StructureCalls.FlowPairs(map));
        Assert.Equal(["X"], Labels(map));
    }

    /// <summary>T-SCAN-18: the flow is drawn again meanwhile by a second user.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_drawn_again_meanwhile_is_not_doubled_by_the_undo(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var x = await project.AddStepAsync(0, "X");
        var y = await project.AddStepAsync(1, "Y");
        var flow = await project.AddFlowAsync(x.Id, y.Id);
        (await facilitator.DeleteAsync($"/api/flows/{flow}", await project.FlowVersionAsync(flow))).Ok();
        var drawn = (await admin.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = x.Id, target_step_id = y.Id })).Ok().GetProperty("id").GetGuid();

        (await UndoAsync(facilitator, project.RootMapId)).Ok();
        Assert.Equal(drawn, Assert.Single((await project.MapAsync()).GetProperty("flows").EnumerateArray()).GetProperty("id").GetGuid());
    }

    /// <summary>T-SCAN-19: « Delete the detail » of a filled sub-map, undone on the map of its parent step where it is logged.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_deleted_filled_sub_map_comes_back_with_its_content(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var macro = await project.AddStepAsync(0, "Macro");
        var (subMap, subLane) = await project.DetailAsync(macro.Id);
        await project.AddStepAsync(0, "Dedans", subLane, subMap);

        var deleted = (await facilitator.DeleteAsync($"/api/maps/{subMap}", await MapVersionAsync(project, subMap))).Ok();
        Assert.Equal(JsonValueKind.Null, StructureCalls.StepOf(await project.MapAsync(), macro.Id).GetProperty("sub_map_id").ValueKind);
        (await facilitator.GetAsync($"/api/maps/{subMap}")).Refused(404, "INTROUVABLE");

        var undone = (await UndoAsync(facilitator, project.RootMapId)).Ok();
        Assert.Equal((deleted.GetProperty("operation_id").GetGuid(), "supprimer_detail"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        Assert.Equal(subMap, StructureCalls.StepOf(await project.MapAsync(), macro.Id).GetProperty("sub_map_id").GetGuid());
        Assert.Equal(["Dedans"], Labels(await project.MapAsync(subMap)));
    }

    /// <summary>T-SCAN-20.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_macro_step_deleted_with_its_descent_comes_back_with_its_sub_map_and_its_steps(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var macro = await project.AddStepAsync(0, "Macro");
        var (subMap, subLane) = await project.DetailAsync(macro.Id);
        await project.AddStepAsync(0, "Dedans", subLane, subMap);
        (await facilitator.DeleteAsync($"/api/steps/{macro.Id}?sub_map=delete", await project.StepVersionAsync(macro.Id))).Ok();

        (await UndoAsync(facilitator, project.RootMapId)).Ok();
        Assert.Equal(subMap, Assert.Single((await project.MapAsync()).GetProperty("steps").EnumerateArray()).GetProperty("sub_map_id").GetGuid());
        Assert.Equal(["Dedans"], Labels(await project.MapAsync(subMap)));
    }

    /// <summary>T-SCAN-21.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_deleted_with_its_steps_comes_back_whole_at_its_rank_and_in_its_columns(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = await project.AddLaneAsync("B");
        await project.AddStepAsync(0, "B0", lane);
        await project.AddStepAsync(3, "B3", lane);
        (await facilitator.DeleteAsync($"/api/lanes/{lane}?steps=delete", await project.LaneVersionAsync(lane))).Ok();

        (await UndoAsync(facilitator, project.RootMapId)).Ok();
        var map = await project.MapAsync();
        Assert.Equal(["Acteur 1", "B"], DissolutionPortedPlan.LaneNames(map));
        Assert.Equal(lane, map.GetProperty("lanes")[1].GetProperty("id").GetGuid());
        Assert.Equal([0, 3], map.GetProperty("steps").EnumerateArray().Where(step => step.GetProperty("lane_id").GetGuid() == lane)
            .Select(step => step.GetProperty("column").GetInt32()).Order());
    }

    /// <summary>
    /// T-SCAN-22: the sub-map of the step is deleted since by a second user; the undo is refused with a code of the undo
    /// (AR-43: LOT_NON_RESTAURABLE is no longer answered) and nothing comes back.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_whose_map_was_deleted_since_is_not_brought_back(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var macro = await project.AddStepAsync(0, "Macro");
        var (subMap, subLane) = await project.DetailAsync(macro.Id);
        var inside = await project.AddStepAsync(0, "Dedans", subLane, subMap);
        (await facilitator.DeleteAsync($"/api/steps/{inside.Id}", await project.StepVersionAsync(inside.Id, subMap))).Ok();
        (await admin.DeleteAsync($"/api/maps/{subMap}", await MapVersionAsync(project, subMap))).Ok();

        (await UndoAsync(facilitator, subMap)).Refused(409, "CARTE_MODIFIEE");
        Assert.Equal(JsonValueKind.Null, StructureCalls.StepOf(await project.MapAsync(), macro.Id).GetProperty("sub_map_id").ValueKind);
        (await facilitator.GetAsync($"/api/maps/{subMap}")).Refused(404, "INTROUVABLE");
    }

    /// <summary>
    /// T-SCAN-23: the undo needs the right to change the structure of the project and takes back only the operations of
    /// its author (AR-43: LOT_INTROUVABLE is no longer answered; an empty stack is INTROUVABLE).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_undo_is_for_a_facilitator_of_the_project_and_takes_back_only_his_own_operations(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        using var admin = project.Admin;
        var step = await project.AddStepAsync(0, "X");
        (await facilitator.DeleteAsync($"/api/steps/{step.Id}", await project.StepVersionAsync(step.Id))).Ok();
        await ProjectLedByTheStrangerAsync(project.Host);

        (await UndoAsync(contributor, project.RootMapId)).Refused(403, "ACCES_REFUSE");
        (await UndoAsync(stranger, project.RootMapId)).Refused(404, "INTROUVABLE");
        (await UndoAsync(admin, project.RootMapId)).Refused(404, "INTROUVABLE");
        Assert.Equal(0, (await project.MapAsync()).GetProperty("steps").GetArrayLength());
        (await UndoAsync(facilitator, project.RootMapId)).Ok();
        Assert.Equal(["X"], Labels(await project.MapAsync()));
    }

    /// <summary>T-SCAN-24.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_group_deletion_of_steps_and_flows_is_one_operation_undone_at_once(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var x = await project.AddStepAsync(0, "X");
        var y = await project.AddStepAsync(1, "Y");
        var z = await project.AddStepAsync(2, "Z");
        var xy = await project.AddFlowAsync(x.Id, y.Id);
        await project.AddFlowAsync(y.Id, z.Id);
        var deleted = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/deletion", new { steps = new[] { z.Id }, flows = new[] { xy } })).Ok();
        Assert.Equal(["X", "Y"], Labels(deleted.GetProperty("map")).Order(StringComparer.Ordinal));
        Assert.Empty(StructureCalls.FlowPairs(deleted.GetProperty("map")));

        var undone = (await UndoAsync(facilitator, project.RootMapId)).Ok();
        Assert.Equal(deleted.GetProperty("operation_id").GetGuid(), undone.GetProperty("id").GetGuid());
        var map = undone.GetProperty("map");
        Assert.Equal(3, map.GetProperty("steps").GetArrayLength());
        Assert.Equal([(x.Id, y.Id), (y.Id, z.Id)], StructureCalls.FlowPairs(map).OrderBy(pair => pair.Source == x.Id ? 0 : 1));
    }
}
