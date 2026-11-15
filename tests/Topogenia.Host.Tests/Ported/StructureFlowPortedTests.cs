// StructureFlowPortedTests.cs
// The 1.x flow and layout tests (StructureTests.LiensTests and DispositionTests, T-STRU-25 to 31) ported to the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.StructurePortedCalls;

namespace Topogenia.Host.Tests.Ported;

public sealed class StructureFlowPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static IReadOnlyList<(Guid StepId, string? Code)> Warnings(JsonElement map) =>
        [.. map.GetProperty("warnings").EnumerateArray().Select(warning => (warning.GetProperty("step_id").GetGuid(), warning.GetProperty("code").GetString()))];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_is_created_with_its_label_relabelled_with_its_version_emptied_to_null_and_once_deleted_is_not_found(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        var decision = await project.AddStepAsync(0, "Conforme ?", type: "decision");
        var yes = await project.AddStepAsync(1, "Valider", b);
        var flow = await project.AddFlowAsync(decision.Id, yes.Id, "oui");
        var read = Assert.Single((await project.MapAsync()).GetProperty("flows").EnumerateArray());
        Assert.Equal(("oui", decision.Id, yes.Id), (read.GetProperty("label").GetString(), read.GetProperty("source_step_id").GetGuid(), read.GetProperty("target_step_id").GetGuid()));

        var url = $"/api/flows/{flow}";
        var relabelled = StructureCalls.FlowOf((await facilitator.PatchAsync(url, new { label = "non" }, read.GetProperty("version").GetInt32())).Ok().GetProperty("map"), flow);
        Assert.Equal("non", relabelled.GetProperty("label").GetString());
        (await facilitator.PatchAsync(url, new { label = "" }, relabelled.GetProperty("version").GetInt32())).Ok();
        Assert.Equal(JsonValueKind.Null, StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("label").ValueKind);
        (await facilitator.PatchAsync(url, new { label = "x" }, read.GetProperty("version").GetInt32())).Refused(409, "CONFLIT_VERSION");
        Assert.Equal(JsonValueKind.Null, StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("label").ValueKind);

        var version = StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("version").GetInt32();
        (await facilitator.DeleteAsync(url, version)).Ok();
        Assert.Empty((await project.MapAsync()).GetProperty("flows").EnumerateArray());
        // Without If-Match (desktop mode) as in the 1.x; with the version read before the deletion, the conflict of ADR 045.
        if (kind == EngineKind.SqlServer)
            (await facilitator.DeleteAsync(url, version)).Refused(409, "CONFLIT_VERSION");
        else
            (await facilitator.DeleteAsync(url)).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_relabelled_with_the_same_text_keeps_its_automatic_translation_and_a_new_text_drops_it(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (source, target) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Payer"));
        var flow = await project.AddFlowAsync(source.Id, target.Id, "oui");
        var imported = (await facilitator.PutAsync($"/api/projects/{project.Id}/translations", new
        {
            scope = "all",
            rows = new[] { new { line = 2, type = "flow", id = flow, original = "oui", translations = new Dictionary<string, string> { ["en"] = "yes" } } },
        })).Ok();
        Assert.Equal(1, imported.GetProperty("written").GetInt32());
        var url = $"/api/flows/{flow}";

        var same = StructureCalls.FlowOf(await project.MapAsync(), flow);
        (await facilitator.PatchAsync(url, new { label = "oui" }, same.GetProperty("version").GetInt32())).Ok();
        var kept = StructureCalls.FlowOf(await project.MapAsync(), flow);
        Assert.Equal("yes", kept.GetProperty("translations").GetProperty("en").GetString());

        (await facilitator.PatchAsync(url, new { label = "d'accord" }, kept.GetProperty("version").GetInt32())).Ok();
        Assert.Empty(StructureCalls.FlowOf(await project.MapAsync(), flow).GetProperty("translations").EnumerateObject());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_duplicate_flow_is_refused_the_reverse_flow_is_written_and_a_flow_to_itself_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (x, y) = (await project.AddStepAsync(0, "X"), await project.AddStepAsync(1, "Y"));
        var url = FlowsUrl(project);
        await project.AddFlowAsync(x.Id, y.Id);
        (await facilitator.PostAsync(url, new { source_step_id = x.Id, target_step_id = y.Id })).Refused(409, "LIEN_DOUBLON");
        await project.AddFlowAsync(y.Id, x.Id, "retour");
        Assert.Equal(["X>Y", "Y>X:retour"], FlowTexts(await project.MapAsync()));
        (await facilitator.PostAsync(url, new { source_step_id = x.Id, target_step_id = x.Id })).Refused(400, "LIEN_REFLEXIF");
        Assert.Equal(2, (await project.MapAsync()).GetProperty("flows").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_layout_warnings_name_an_isolated_end_a_start_with_an_incoming_flow_and_an_end_with_an_outgoing_flow_rg_c08(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var start = await project.AddStepAsync(0, "Début", type: "start");
        var middle = await project.AddStepAsync(1, "Milieu");
        var end = await project.AddStepAsync(2, "Fin", type: "end");
        await project.AddFlowAsync(start.Id, middle.Id);
        // The end linked to nothing is named, even when a flow passes next to it.
        Assert.Equal([(end.Id, "ETAPE_ISOLEE")], Warnings(await project.MapAsync()));
        await project.AddFlowAsync(middle.Id, end.Id);
        Assert.Empty(Warnings(await project.MapAsync()));
        await project.AddFlowAsync(end.Id, start.Id);
        var warnings = Warnings(await project.MapAsync());
        Assert.Contains((start.Id, "DEBUT_LIEN_ENTRANT"), warnings);
        Assert.Contains((end.Id, "FIN_LIEN_SORTANT"), warnings);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_deleted_step_takes_its_flows_and_leaves_the_others(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (x, y, z) = (await project.AddStepAsync(0, "X"), await project.AddStepAsync(1, "Y"), await project.AddStepAsync(2, "Z"));
        await project.AddFlowAsync(x.Id, y.Id);
        await project.AddFlowAsync(y.Id, z.Id);
        await project.AddFlowAsync(x.Id, z.Id);
        (await facilitator.DeleteAsync($"/api/steps/{y.Id}", await project.StepVersionAsync(y.Id))).Ok();
        Assert.Equal([(x.Id, z.Id)], StructureCalls.FlowPairs(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_layout_that_exchanges_two_steps_and_reorders_the_lanes_is_written_in_one_call_and_undone_in_one_step(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var a = project.FirstLaneId;
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        var (x, y) = (await project.AddStepAsync(0, "X"), await project.AddStepAsync(1, "Y"));
        var laidOut = (await facilitator.PutAsync($"/api/maps/{project.RootMapId}/layout", new
        {
            lane_order = new[] { b, a },
            steps = new object[] { new { id = x.Id, lane_id = a, column = 1 }, new { id = y.Id, lane_id = a, column = 0 } },
        })).Ok();
        var map = await project.MapAsync();
        Assert.Equal(["B", "Acteur 1"], LaneNames(map));
        Assert.Equal(((a, 1, 0), (a, 0, 0)), (CellOf(map, x.Id), CellOf(map, y.Id)));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(laidOut.GetProperty("operation_id").GetGuid(), undone.GetProperty("id").GetGuid());
        Assert.Equal(["Acteur 1", "B"], LaneNames(undone.GetProperty("map")));
        Assert.Equal(((a, 0, 0), (a, 1, 0)), (CellOf(undone.GetProperty("map"), x.Id), CellOf(undone.GetProperty("map"), y.Id)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_layout_whose_final_state_shares_a_cell_or_names_a_step_twice_is_refused_and_writes_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var a = project.FirstLaneId;
        var (x, y) = (await project.AddStepAsync(0, "X"), await project.AddStepAsync(1, "Y"));
        var url = $"/api/maps/{project.RootMapId}/layout";
        var history = UndoKinds((await facilitator.GetAsync($"/api/maps/{project.RootMapId}")).Ok()).Count;
        (await facilitator.PutAsync(url, new { steps = new[] { new { id = x.Id, lane_id = a, column = 1 } } })).Refused(400, "DISPOSITION_CASE_OCCUPEE");
        (await facilitator.PutAsync(url, new { steps = new[] { new { id = x.Id, lane_id = a, column = 3 }, new { id = x.Id, lane_id = a, column = 4 } } }))
            .Refused(400, "VALEUR_INVALIDE");
        var read = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}")).Ok();
        Assert.Equal(((a, 0, 0), (a, 1, 0)), (CellOf(read.GetProperty("map"), x.Id), CellOf(read.GetProperty("map"), y.Id)));
        Assert.Equal(history, UndoKinds(read).Count);
    }
}
