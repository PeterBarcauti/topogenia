// StructureInsertionPortedTests.cs
// The 1.x insertion and branch tests (InsertionTests, BranchesTests, T-INSE-01 to 09, T-BRAN-01 to 04) ported to the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.StructurePortedCalls;

namespace Topogenia.Host.Tests.Ported;

public sealed class StructureInsertionPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static async Task<JsonElement> InsertAsync(ApiProject project, Guid stepId, string position, string label, string type = "task")
    {
        using var facilitator = project.Facilitator;
        return (await facilitator.PostAsync($"/api/steps/{stepId}/insert", new { position, label, type })).Ok();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Inserting_before_b_in_a_b_c_cuts_its_single_incoming_flow_into_a_x_b_and_shifts_the_flow(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var chain = await ChainAsync(project, true, "A", "B", "C");
        var inserted = await InsertAsync(project, chain[1], "before", "X");
        Assert.Equal($"{project.Code}-E004", inserted.GetProperty("code").GetString());
        Assert.Equal("inserer", UndoKinds(inserted)[0]);
        Assert.Equal(inserted.GetProperty("operation_id").GetGuid(), inserted.GetProperty("history").GetProperty("undo")[0].GetProperty("operation_id").GetGuid());
        var map = await project.MapAsync();
        Assert.Equal(["A>X", "B>C", "X>B"], FlowTexts(map));
        Assert.Equal(["A", "X", "B", "C"], LabelsByColumn(map));
        Assert.Equal([0, 1, 2, 3], Columns(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_label_of_a_decision_branch_stays_on_the_flow_that_leaves_the_decision_after_and_before(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var d = await project.AddStepAsync(0, "D", type: "decision");
        var e = await project.AddStepAsync(1, "E");
        await project.AddFlowAsync(d.Id, e.Id, "oui");
        await InsertAsync(project, d.Id, "after", "X");
        Assert.Equal(["D>X:oui", "X>E"], FlowTexts(await project.MapAsync()));
        await InsertAsync(project, e.Id, "before", "Y");
        Assert.Equal(["D>X:oui", "X>Y", "Y>E"], FlowTexts(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task With_several_incoming_flows_none_is_cut_and_the_new_step_is_only_linked(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var logistics = (await AddLaneAsync(project, "Logistique")).GetProperty("id").GetGuid();
        var a1 = await project.AddStepAsync(0, "A1");
        var l1 = await project.AddStepAsync(0, "L1", logistics);
        var b = await project.AddStepAsync(1, "B");
        await project.AddFlowAsync(a1.Id, b.Id);
        await project.AddFlowAsync(l1.Id, b.Id);
        await InsertAsync(project, b.Id, "before", "X");
        Assert.Equal(["A1>B", "L1>B", "X>B"], FlowTexts(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_isolated_step_is_simply_linked_to_the_step_inserted_before_or_after_it(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var b = await project.AddStepAsync(0, "B");
        await InsertAsync(project, b.Id, "before", "X");
        await InsertAsync(project, b.Id, "after", "Y");
        var map = await project.MapAsync();
        Assert.Equal(["B>Y", "X>B"], FlowTexts(map));
        Assert.Equal(["X", "B", "Y"], LabelsByColumn(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_insertion_is_undone_with_its_cut_flow_restored_and_redone_identically(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var chain = await ChainAsync(project, true, "A", "B", "C");
        var inserted = await InsertAsync(project, chain[1], "before", "X");
        var x = inserted.GetProperty("id").GetGuid();

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal((inserted.GetProperty("operation_id").GetGuid(), "inserer"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        var map = await project.MapAsync();
        Assert.Equal(["A>B", "B>C"], FlowTexts(map));
        Assert.Equal(["A", "B", "C"], LabelsByColumn(map));
        Assert.Equal([0, 1, 2], Columns(map));

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Ok();
        map = await project.MapAsync();
        Assert.Equal(["A>X", "B>C", "X>B"], FlowTexts(map));
        Assert.Equal((inserted.GetProperty("code").GetString(), 1), (StructureCalls.StepOf(map, x).GetProperty("code").GetString(), CellOf(map, x).Column));
        Assert.Equal(["A", "X", "B", "C"], LabelsByColumn(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Inserting_below_or_above_puts_a_relay_in_the_neighbour_lane_shifts_its_occupant_and_adds_a_flow_without_cutting(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var top = project.FirstLaneId;
        var bottom = (await AddLaneAsync(project, "Logistique")).GetProperty("id").GetGuid();
        var b = await project.AddStepAsync(1, "B");
        var c = await project.AddStepAsync(2, "C");
        await project.AddFlowAsync(b.Id, c.Id);
        var occupant = await project.AddStepAsync(2, "Occupante", bottom);

        var x = (await InsertAsync(project, b.Id, "below", "X")).GetProperty("id").GetGuid();
        var map = await project.MapAsync();
        Assert.Equal((bottom, 2, 0), CellOf(map, x));
        Assert.Equal((bottom, 3, 0), CellOf(map, occupant.Id));
        // The flow B > C is not cut: the relay is added.
        Assert.Equal(["B>C", "B>X"], FlowTexts(map));

        var y = (await InsertAsync(project, x, "above", "Y")).GetProperty("id").GetGuid();
        map = await project.MapAsync();
        Assert.Equal((top, 3, 0), CellOf(map, y));
        Assert.Equal(["B>C", "B>X", "X>Y"], FlowTexts(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Inserting_below_or_above_without_a_neighbour_lane_is_refused_and_creates_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var b = await project.AddStepAsync(0, "B");
        Assert.Equal("position", (await facilitator.PostAsync($"/api/steps/{b.Id}/insert", new { position = "below", label = "X" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("position", (await facilitator.PostAsync($"/api/steps/{b.Id}/insert", new { position = "above", label = "X" })).Refused(400, "VALEUR_INVALIDE").Field());
        var map = await project.MapAsync();
        Assert.Equal(["B"], Labels(map));
        Assert.Empty(map.GetProperty("flows").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_insertion_with_a_wrong_position_an_empty_label_a_weaker_role_or_an_unknown_step_changes_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var chain = await ChainAsync(project, true, "A", "B", "C");
        var url = $"/api/steps/{chain[1]}/insert";
        Assert.Equal("position", (await facilitator.PostAsync(url, new { position = "above", label = "X" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("label", (await facilitator.PostAsync(url, new { position = "before", label = " " })).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        (await contributor.PostAsync(url, new { position = "before", label = "X" })).Refused(403, "ACCES_REFUSE");
        (await reader.PostAsync(url, new { position = "before", label = "X" })).Refused(403, "ACCES_REFUSE");
        (await facilitator.PostAsync($"/api/steps/{Guid.NewGuid()}/insert", new { position = "before", label = "X" })).Refused(404, "INTROUVABLE");
        var map = await project.MapAsync();
        Assert.Equal(["A", "B", "C"], Labels(map));
        Assert.Equal(["A>B", "B>C"], FlowTexts(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_parallel_insertion_takes_the_same_lane_and_column_the_next_row_and_copies_the_flows_read_down_the_column(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var lane = project.FirstLaneId;
        var chain = await ChainAsync(project, true, "A", "B", "C");
        var (a, b, c) = (chain[0], chain[1], chain[2]);
        var second = (await InsertAsync(project, b, "parallel", "B bis")).GetProperty("id").GetGuid();
        var third = (await InsertAsync(project, b, "parallel", "B ter")).GetProperty("id").GetGuid();
        var map = await project.MapAsync();
        Assert.Equal((lane, 1, 0), CellOf(map, b));
        Assert.Equal((lane, 1, 1), CellOf(map, second));
        Assert.Equal((lane, 1, 2), CellOf(map, third));
        // Each branch takes the flows of B: A > B bis > C.
        var flows = StructureCalls.FlowPairs(map);
        Assert.Contains((a, second), flows);
        Assert.Contains((second, c), flows);
        Assert.Equal(6, flows.Count);
        // Reading from the top down in the column: B, B bis, B ter.
        Assert.Equal(["A", "B", "B bis", "B ter", "C"], Labels(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_rg_c04_shift_moves_only_its_row_the_layout_keeps_cells_distinct_and_the_row_is_bounded(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = project.FirstLaneId;
        var chain = await ChainAsync(project, true, "A", "B", "C");
        var (b, c) = (chain[1], chain[2]);
        var second = (await InsertAsync(project, b, "parallel", "B bis")).GetProperty("id").GetGuid();

        // A step dropped on (column 1, row 0) pushes B and C, not B bis.
        await project.AddStepAsync(1, "X");
        var map = await project.MapAsync();
        Assert.Equal(((lane, 2, 0), (lane, 3, 0), (lane, 1, 1)), (CellOf(map, b), CellOf(map, c), CellOf(map, second)));

        // B bis moved to row 0 of column 3 pushes C.
        (await PatchStepAsync(project, facilitator, second, new { column = 3, row = 0 })).Ok();
        Assert.Equal((lane, 4, 0), CellOf(await project.MapAsync(), c));

        // Grouped layout: two steps in one cell refused; different rows accepted.
        var url = $"/api/maps/{project.RootMapId}/layout";
        (await facilitator.PutAsync(url, new { steps = new[] { new { id = b, lane_id = lane, column = 7, row = 2 }, new { id = c, lane_id = lane, column = 7, row = 2 } } }))
            .Refused(400, "DISPOSITION_CASE_OCCUPEE");
        (await facilitator.PutAsync(url, new { steps = new[] { new { id = b, lane_id = lane, column = 7, row = 2 }, new { id = c, lane_id = lane, column = 7, row = 3 } } })).Ok();
        map = await project.MapAsync();
        Assert.Equal([2, 3], map.GetProperty("steps").EnumerateArray().Where(step => step.GetProperty("column").GetInt32() == 7)
            .Select(step => step.GetProperty("row").GetInt32()).Order());
        Assert.Equal("row", (await PatchStepAsync(project, facilitator, b, new { row = 51 })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal((lane, 7, 2), CellOf(await project.MapAsync(), b));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_change_of_lane_without_a_row_brings_the_step_back_to_the_first_row(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var chain = await ChainAsync(project, true, "A", "B", "C");
        var second = (await InsertAsync(project, chain[1], "parallel", "B bis")).GetProperty("id").GetGuid();
        Assert.Equal(1, CellOf(await project.MapAsync(), second).Row);
        var logistics = (await AddLaneAsync(project, "Logistique")).GetProperty("id").GetGuid();
        (await PatchStepAsync(project, facilitator, second, new { lane_id = logistics })).Ok();
        Assert.Equal((logistics, 1, 0), CellOf(await project.MapAsync(), second));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_parallel_and_inclusive_gateways_are_step_types_that_cannot_be_detailed_and_an_unknown_type_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var and = await project.AddStepAsync(0, "Parallèle", type: "parallel");
        var or = await project.AddStepAsync(1, "Inclusif", type: "inclusive");
        Assert.Equal(["parallel", "inclusive"], (await project.MapAsync()).GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("type").GetString()));
        (await facilitator.PostAsync($"/api/steps/{and.Id}/detail", null, await project.StepVersionAsync(and.Id))).Refused(400, "ETAPE_NON_DETAILLABLE");
        (await facilitator.PostAsync($"/api/steps/{or.Id}/detail", null, await project.StepVersionAsync(or.Id))).Refused(400, "ETAPE_NON_DETAILLABLE");
        Assert.All((await project.MapAsync()).GetProperty("steps").EnumerateArray(), step => Assert.Equal(JsonValueKind.Null, step.GetProperty("sub_map_id").ValueKind));
        Assert.Equal("type", (await facilitator.PostAsync(StepsUrl(project), new { lane_id = project.FirstLaneId, column = 2, label = "X", type = "porte" }))
            .Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal(2, (await project.MapAsync()).GetProperty("steps").GetArrayLength());
    }
}
