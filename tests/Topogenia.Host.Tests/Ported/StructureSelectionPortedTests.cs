// StructureSelectionPortedTests.cs
// The 1.x selection tests (SelectionTests, T-SELE-01 to 07) ported to the whole host on both databases, their client undo replaced by the undo of the server.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.StructurePortedCalls;

namespace Topogenia.Host.Tests.Ported;

public sealed class StructureSelectionPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static string Label(JsonElement map, Guid source, Guid target) => FlowBetween(map, source, target).GetProperty("label").GetString() ?? "";

    private static IReadOnlyList<(Guid Id, Guid Lane, int Column, int Row)> Cells(JsonElement map) =>
        [.. map.GetProperty("steps").EnumerateArray().Select(step => (step.GetProperty("id").GetGuid(), step.GetProperty("lane_id").GetGuid(),
            step.GetProperty("column").GetInt32(), step.GetProperty("row").GetInt32())).OrderBy(cell => cell.Item1)];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stitched_deletion_links_the_predecessor_to_the_successor_with_the_incoming_label_and_is_undone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var chain = await ChainAsync(project, true, "A", "B", "C");
        (await RelabelFlowAsync(project, facilitator, chain[0], chain[1], "ok")).Ok();
        var url = $"/api/maps/{project.RootMapId}/deletion";
        var deleted = (await facilitator.PostAsync(url, new { steps = new[] { chain[1] }, stitch = true })).Ok();
        Assert.Equal(["A>C:ok"], FlowTexts(deleted.GetProperty("map")));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(deleted.GetProperty("operation_id").GetGuid(), undone.GetProperty("id").GetGuid());
        Assert.Equal(["A>B:ok", "B>C"], FlowTexts(await project.MapAsync()));

        // Without stitching, nothing is created.
        (await facilitator.PostAsync(url, new { steps = new[] { chain[1] } })).Ok();
        var map = await project.MapAsync();
        Assert.Equal(["A", "C"], Labels(map));
        Assert.Empty(map.GetProperty("flows").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stitched_switch_hands_its_labels_on_and_an_ambiguous_link_is_not_made(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var chain = await ChainAsync(project, true, "A", "X");
        var yes = await project.AddStepAsync(2, "Oui");
        var no = await project.AddStepAsync(3, "Non");
        await project.AddFlowAsync(chain[1], yes.Id, "oui");
        await project.AddFlowAsync(chain[1], no.Id, "non");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/deletion", new { steps = new[] { chain[1] }, stitch = true })).Ok();
        var map = await project.MapAsync();
        Assert.Equal(("oui", "non"), (Label(map, chain[0], yes.Id), Label(map, chain[0], no.Id)));

        // Two predecessors and two successors: nobody knows whom to link to whom.
        var other = await ApiProject.CreateAsync(host);
        using var otherFacilitator = other.Facilitator;
        var steps = await ChainAsync(other, false, "P1", "P2", "Y", "S1", "S2");
        await other.AddFlowAsync(steps[0], steps[2]);
        await other.AddFlowAsync(steps[1], steps[2]);
        await other.AddFlowAsync(steps[2], steps[3]);
        await other.AddFlowAsync(steps[2], steps[4]);
        var deleted = (await otherFacilitator.PostAsync($"/api/maps/{other.RootMapId}/deletion", new { steps = new[] { steps[2] }, stitch = true })).Ok();
        Assert.Empty(deleted.GetProperty("map").GetProperty("flows").EnumerateArray());
        Assert.Equal(["P1", "P2", "S1", "S2"], Labels(await other.MapAsync()).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_pasted_copy_creates_new_coded_steps_inserted_in_the_flow_and_is_undone_and_redone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = project.FirstLaneId;
        var chain = await ChainAsync(project, true, "A", "B");
        var (a, b) = (chain[0], chain[1]);
        (await RelabelFlowAsync(project, facilitator, a, b, "suite")).Ok();
        var url = $"/api/maps/{project.RootMapId}/paste";
        var pasted = (await facilitator.PostAsync(url, new
        {
            steps = new object[]
            {
                new { key = "x", label = "X", type = "task", lane_id = lane, column = 1 },
                new { key = "y", label = "Y", type = "decision", lane_id = lane, column = 2 },
            },
            flows = new[] { new { source = "x", target = "y", label = "vers Y" } },
            anchor = new { step_id = a, position = "after" },
        })).Ok();
        var (x, y) = (pasted.Ids()[0], pasted.Ids()[1]);
        var map = await project.MapAsync();
        // RG-C04: B is pushed two columns; A > B becomes A > X > Y > B.
        Assert.Equal(3, CellOf(map, b).Column);
        Assert.Equal(["A>X:suite", "X>Y:vers Y", "Y>B"], FlowTexts(map));
        Assert.Equal("decision", StructureCalls.StepOf(map, y).GetProperty("type").GetString());
        var codes = map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("code").GetString()!).ToList();
        Assert.Equal(4, codes.Distinct().Count());
        Assert.All(codes, code => Assert.StartsWith($"{project.Code}-E", code, StringComparison.Ordinal));
        Assert.Empty(pasted.Ids().Intersect(chain));
        Assert.Equal(("X", "Y"), (StructureCalls.StepOf(map, x).GetProperty("label").GetString(), StructureCalls.StepOf(map, y).GetProperty("label").GetString()));

        // Before B, with no flow to cut on that side (two flows reach B): only Z > B.
        await project.AddFlowAsync(a, b);
        var z = (await facilitator.PostAsync(url, new
        {
            steps = new[] { new { key = "z", label = "Z", lane_id = lane, column = 3 } },
            anchor = new { step_id = b, position = "before" },
        })).Ok();
        Assert.Equal(["A>B", "A>X:suite", "X>Y:vers Y", "Y>B", "Z>B"], FlowTexts(await project.MapAsync()));

        // Duplicate cells, an unknown key in the flows, a weaker role: refused, nothing written.
        (await facilitator.PostAsync(url, new { steps = new[] { new { key = "a", label = "A", lane_id = lane, column = 9 }, new { key = "b", label = "B", lane_id = lane, column = 9 } } }))
            .Refused(400, "DISPOSITION_CASE_OCCUPEE");
        (await facilitator.PostAsync(url, new { steps = new[] { new { key = "a", label = "A", lane_id = lane, column = 9 } }, flows = new[] { new { source = "a", target = "zz" } } }))
            .Refused(400, "VALEUR_INVALIDE");
        using var contributor = project.Contributor;
        (await contributor.PostAsync(url, new { steps = new[] { new { key = "a", label = "A", lane_id = lane, column = 9 } } })).Refused(403, "ACCES_REFUSE");
        Assert.Equal(5, (await project.MapAsync()).GetProperty("steps").GetArrayLength());

        // The paste is an operation of the log (4.6): undone, then redone.
        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal((z.GetProperty("operation_id").GetGuid(), "coller"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        Assert.DoesNotContain("Z", Labels(undone.GetProperty("map")));
        var redone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Ok();
        Assert.Contains("Z>B", FlowTexts(redone.GetProperty("map")));
        Assert.Equal("Z", StructureCalls.StepOf(redone.GetProperty("map"), z.Ids()[0]).GetProperty("label").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_block_cut_and_pasted_in_its_map_leaves_the_flow_stitched_comes_back_with_its_code_and_sheet_and_is_undone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = project.FirstLaneId;
        var chain = await ChainAsync(project, true, "A", "B", "C", "D", "E");
        var (b, c, d, e) = (chain[1], chain[2], chain[3], chain[4]);
        var before = await project.MapAsync();
        var code = StructureCalls.StepOf(before, b).GetProperty("code").GetString();
        (await facilitator.PutAsync($"/api/steps/{b}/sheet", StepSheets.Purpose("Préparer"), await StepSheets.ReadVersionAsync(facilitator, b))).Ok();

        // B > C cut then pasted after D: A > D (stitched), D > B > C > E.
        var moved = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/transfer", new
        {
            steps = new[] { new { id = b, lane_id = lane, column = 4 }, new { id = c, lane_id = lane, column = 5 } },
            cut = true,
            anchor = new { step_id = d, position = "after" },
        })).Ok();
        var map = await project.MapAsync();
        Assert.Equal(["A>D", "B>C", "C>E", "D>B"], FlowTexts(map));
        Assert.Equal(6, CellOf(map, e).Column);
        Assert.Equal(code, StructureCalls.StepOf(map, b).GetProperty("code").GetString());
        Assert.Equal("Préparer", StepSheets.PurposeOf((await facilitator.GetAsync($"/api/steps/{b}/sheet")).Ok()));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(moved.GetProperty("operation_id").GetGuid(), undone.GetProperty("id").GetGuid());
        var after = await project.MapAsync();
        Assert.Equal(FlowTexts(before), FlowTexts(after));
        Assert.Equal(Cells(before), Cells(after));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_block_cut_to_another_map_keeps_its_sheet_documents_and_inner_flows_refuses_a_flow_between_maps_and_is_undone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var chain = await ChainAsync(project, true, "A", "B", "C", "D");
        var (a, b, c, d) = (chain[0], chain[1], chain[2], chain[3]);
        var (subMap, subLane) = await project.DetailAsync(d);
        (await facilitator.PostAsync($"/api/steps/{b}/documents", new { direction = "output", name = "Tableau" })).Ok();
        (await facilitator.PutAsync($"/api/steps/{b}/sheet", StepSheets.Purpose("Préparer"), await StepSheets.ReadVersionAsync(facilitator, b))).Ok();

        (await facilitator.PostAsync($"/api/maps/{subMap}/transfer", new
        {
            steps = new[] { new { id = b, lane_id = subLane, column = 0 }, new { id = c, lane_id = subLane, column = 1 } },
            cut = true,
        })).Ok();
        var root = await project.MapAsync();
        var inside = await project.MapAsync(subMap);
        Assert.Equal(["A>D"], FlowTexts(root));
        Assert.Equal(["B>C"], FlowTexts(inside));
        Assert.Equal(["B", "C"], Labels(inside));
        var sheet = (await facilitator.GetAsync($"/api/steps/{b}/sheet")).Ok();
        Assert.Equal("Tableau", sheet.GetProperty("outputs").EnumerateArray().Single().GetProperty("document").GetProperty("name").GetString());
        Assert.Equal("Préparer", StepSheets.PurposeOf(sheet));
        Assert.Equal(["Tableau"], StructureCalls.StepOf(inside, b).GetProperty("markers").GetProperty("documents").EnumerateArray()
            .Select(marker => marker.GetProperty("name").GetString()));
        // D now has a filled detailed map.
        Assert.False(StructureCalls.StepOf(root, d).GetProperty("sub_map_empty").GetBoolean());

        // A flow links only two steps of one map.
        (await facilitator.PostAsync(FlowsUrl(project), new { source_step_id = a, target_step_id = b })).Refused(400, "LIEN_HORS_CARTE");
        Assert.Equal(["A>D"], FlowTexts(await project.MapAsync()));

        // The undo of the target map brings everything back as before.
        var undone = (await facilitator.PostAsync($"/api/maps/{subMap}/undo")).Ok();
        Assert.Equal("deplacer_bloc", undone.GetProperty("code").GetString());
        Assert.Equal(["A>B", "B>C", "C>D"], FlowTexts(await project.MapAsync()));
        Assert.Empty((await project.MapAsync(subMap)).GetProperty("steps").EnumerateArray());
        Assert.Equal("Tableau", (await facilitator.GetAsync($"/api/steps/{b}/sheet")).Ok().GetProperty("outputs").EnumerateArray().Single()
            .GetProperty("document").GetProperty("name").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_cut_and_paste_putting_two_steps_in_one_cell_is_refused_and_moves_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var lane = project.FirstLaneId;
        var chain = await ChainAsync(project, true, "A", "B");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/transfer", new
        {
            steps = new[] { new { id = chain[0], lane_id = lane, column = 4 }, new { id = chain[1], lane_id = lane, column = 4 } },
            cut = true,
        })).Refused(400, "DISPOSITION_CASE_OCCUPEE");
        var map = await project.MapAsync();
        Assert.Equal(((lane, 0, 0), (lane, 1, 0)), (CellOf(map, chain[0]), CellOf(map, chain[1])));
        Assert.Equal(["A>B"], FlowTexts(map));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Detailing_a_selection_on_two_lanes_moves_the_block_to_the_sub_map_of_a_new_step_renamed_with_it_and_undone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var chain = await ChainAsync(project, true, "A", "B", "C", "D");
        var (b, c) = (chain[1], chain[2]);
        var accounting = (await AddLaneAsync(project, "Comptabilité")).GetProperty("id").GetGuid();
        // C goes to the lane Comptabilite, row 1: the block spans two lanes.
        (await PatchStepAsync(project, facilitator, c, new { lane_id = accounting, row = 1 })).Ok();
        var before = await project.MapAsync();

        var grouped = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = new[] { b, c }, label = "Valider" })).Ok();
        var (macro, subMap) = (grouped.GetProperty("id").GetGuid(), Assert.Single(grouped.Ids()));
        var map = await project.MapAsync();
        Assert.Equal(["A>Valider", "Valider>D"], FlowTexts(map));
        Assert.Equal((project.FirstLaneId, 1, 0), CellOf(map, macro));
        Assert.Equal(subMap, StructureCalls.StepOf(map, macro).GetProperty("sub_map_id").GetGuid());

        var inside = await project.MapAsync(subMap);
        Assert.Equal(["Acteur 1", "Comptabilité"], LaneNames(inside));
        Assert.Equal(["B>C"], FlowTexts(inside));
        var lanes = LaneIds(inside);
        Assert.Equal((lanes[0], 0, 0), CellOf(inside, b));
        Assert.Equal((lanes[1], 1, 1), CellOf(inside, c));
        Assert.Equal(macro, inside.GetProperty("parent_step_id").GetGuid());

        // Renaming the step renames its detailed map (breadcrumb).
        (await PatchStepAsync(project, facilitator, macro, new { label = "Valider le budget" })).Ok();
        Assert.Equal("Valider le budget", (await project.MapAsync(subMap)).GetProperty("name").GetString());

        // The undo of the rename, then of the grouping, gives the initial state back.
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal("regrouper", undone.GetProperty("code").GetString());
        var after = await project.MapAsync();
        Assert.Equal(FlowTexts(before), FlowTexts(after));
        Assert.Equal(Cells(before), Cells(after));
    }
}
