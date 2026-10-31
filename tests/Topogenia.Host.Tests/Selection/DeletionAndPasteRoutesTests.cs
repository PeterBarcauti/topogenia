// DeletionAndPasteRoutesTests.cs
// The deletion of a selection and the paste of a copy through the whole host on both databases (annex A 1.3-14 to 19, A 2.8).

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Selection;

public sealed class DeletionAndPasteRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_deleted_selection_is_stitched_answered_with_its_steps_and_undone_by_the_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander", "Valider", "Livrer");
        using var facilitator = project.Facilitator;
        var deleted = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/deletion", new { steps = new[] { row[1], row[1] }, stitch = true })).Ok();
        Assert.Equal([row[1]], Ids(deleted));
        Assert.Equal([row[0], row[2]], StepIds(deleted.GetProperty("map")).Order());
        Assert.Equal(new HashSet<(Guid, Guid)> { (row[0], row[2]) }, Flows(deleted.GetProperty("map")));
        var operation = deleted.GetProperty("operation_id").GetGuid();

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal((operation, "supprimer"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        Assert.Equal(new HashSet<(Guid, Guid)> { (row[0], row[1]), (row[1], row[2]) }, Flows(undone.GetProperty("map")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_empty_deletion_is_accepted_and_more_than_2000_steps_and_flows_after_removing_duplicates_are_refused_on_steps(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander", "Valider", "Livrer");
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/deletion";
        var before = Flows(await project.MapAsync());

        // An empty selection is no refusal (1.x ServiceStructure.cs:529-533) and deletes nothing.
        var empty = (await facilitator.PostAsync(url, new { steps = Array.Empty<Guid>(), flows = Array.Empty<Guid>() })).Ok();
        Assert.Empty(Ids(empty));
        Assert.Equal(row.Order(), StepIds(empty.GetProperty("map")).Order());
        Assert.Equal(before, Flows(empty.GetProperty("map")));

        // 1 500 distinct steps and 501 distinct flows make 2 001 items: the limit counts steps and flows together.
        var steps = Enumerable.Range(0, 1500).Select(_ => Guid.NewGuid()).ToArray();
        var flows = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToArray();
        Assert.Equal("steps", (await facilitator.PostAsync(url, new { steps, flows })).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        Assert.Equal(row.Order(), StepIds(await project.MapAsync()).Order());

        // 2 500 times the same step count once after removing the duplicates: one step deleted, no refusal.
        var repeated = (await facilitator.PostAsync(url, new { steps = Enumerable.Repeat(row[1], 2500).ToArray() })).Ok();
        Assert.Equal([row[1]], Ids(repeated));
        Assert.Equal(new[] { row[0], row[2] }.Order(), StepIds(repeated.GetProperty("map")).Order());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_deleted_by_an_earlier_operation_is_not_found_and_one_deleted_with_its_step_in_the_same_deletion_is_accepted(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander", "Valider", "Livrer");
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/deletion";
        var flowIds = (await project.MapAsync()).GetProperty("flows").EnumerateArray()
            .ToDictionary(flow => flow.GetProperty("source_step_id").GetGuid(), flow => flow.GetProperty("id").GetGuid());
        var (first, second) = (flowIds[row[0]], flowIds[row[1]]);
        (await facilitator.PostAsync(url, new { flows = new[] { first } })).Ok();

        // The flow deleted by the earlier operation is not live and not in this batch: 404, and the step named with it stays.
        (await facilitator.PostAsync(url, new { steps = new[] { row[2] }, flows = new[] { first } })).Refused(404, "INTROUVABLE");
        var kept = await project.MapAsync();
        Assert.Equal(row.Order(), StepIds(kept).Order());
        Assert.Equal(new HashSet<(Guid, Guid)> { (row[1], row[2]) }, Flows(kept));

        // The flow of a step deleted in the same batch is already deleted by its step: it is accepted.
        var deleted = (await facilitator.PostAsync(url, new { steps = new[] { row[1] }, flows = new[] { second } })).Ok();
        Assert.Equal([row[1]], Ids(deleted));
        Assert.Equal(new[] { row[0], row[2] }.Order(), StepIds(deleted.GetProperty("map")).Order());
        Assert.Empty(Flows(deleted.GetProperty("map")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_filled_sub_map_asks_for_the_delete_choice_whose_code_is_checked_and_which_takes_the_sub_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander");
        using var facilitator = project.Facilitator;
        var grouped = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = new[] { row[0] }, label = "Achats" })).Ok();
        var (macro, subMap) = (grouped.GetProperty("id").GetGuid(), Ids(grouped)[0]);
        var url = $"/api/maps/{project.RootMapId}/deletion";

        (await facilitator.PostAsync(url, new { steps = new[] { macro } })).Refused(409, "MACRO_CHOIX_REQUIS");
        Assert.Equal("sub_maps", (await facilitator.PostAsync(url, new { steps = new[] { macro }, sub_maps = "supprimer" })).Refused(400, "VALEUR_INVALIDE")
            .GetProperty("field").GetString());
        Assert.Equal([macro], StepIds(await project.MapAsync()));

        var deleted = (await facilitator.PostAsync(url, new { steps = new[] { macro }, sub_maps = "delete" })).Ok();
        Assert.Empty(StepIds(deleted.GetProperty("map")));
        (await facilitator.GetAsync($"/api/maps/{subMap}")).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_deletion_names_steps_and_flows_of_its_own_map_only(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander", "Livrer");
        using var facilitator = project.Facilitator;
        var subMap = Ids((await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = row, label = "Achats" })).Ok())[0];
        var inside = await project.MapAsync(subMap);
        var inner = inside.GetProperty("flows")[0].GetProperty("id").GetGuid();
        var url = $"/api/maps/{project.RootMapId}/deletion";

        (await facilitator.PostAsync(url, new { flows = new[] { inner } })).Refused(404, "INTROUVABLE");
        Assert.Equal(Step(inside, row[0]).GetProperty("code").GetString(),
            (await facilitator.PostAsync(url, new { steps = new[] { row[0] } })).Refused(400, "ETAPE_HORS_CARTE").GetProperty("code").GetString());
        Assert.Single((await project.MapAsync(subMap)).GetProperty("flows").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_pasted_copy_answers_its_new_steps_in_order_with_their_flow_and_is_undone_by_the_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var pasted = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/paste", new
        {
            steps = new object[]
            {
                new { key = "a", label = "Commander", lane_id = project.FirstLaneId, column = 0 },
                new { key = "b", label = "Livrer", type = "decision", lane_id = project.FirstLaneId, column = 1, row = 1 },
            },
            flows = new[] { new { source = "a", target = "b", label = "accord" } },
        })).Ok();
        var ids = Ids(pasted);
        var map = pasted.GetProperty("map");
        Assert.Equal(("Commander", "Livrer"), (Step(map, ids[0]).GetProperty("label").GetString(), Step(map, ids[1]).GetProperty("label").GetString()));
        Assert.Equal(("decision", 1), (Step(map, ids[1]).GetProperty("type").GetString(), Step(map, ids[1]).GetProperty("row").GetInt32()));
        Assert.Equal("accord", map.GetProperty("flows").EnumerateArray().Single().GetProperty("label").GetString());
        Assert.Equal(new HashSet<(Guid, Guid)> { (ids[0], ids[1]) }, Flows(map));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal("coller", undone.GetProperty("code").GetString());
        Assert.Empty(StepIds(undone.GetProperty("map")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_paste_after_an_anchor_takes_the_place_of_its_outgoing_flow(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander", "Livrer");
        using var facilitator = project.Facilitator;
        var pasted = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/paste", new
        {
            steps = new[] { new { key = "x", label = "Valider", lane_id = project.FirstLaneId, column = 5 } },
            anchor = new { step_id = row[0], position = "after" },
        })).Ok();
        var copy = Ids(pasted)[0];
        Assert.Equal(new HashSet<(Guid, Guid)> { (row[0], copy), (copy, row[1]) }, Flows(pasted.GetProperty("map")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_paste_refuses_a_missing_lane_or_column_an_unknown_lane_and_an_unknown_anchor_position_writing_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander");
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/paste";
        string? Field(ApiResponse response, int status, string code) => response.Refused(status, code).GetProperty("field").GetString();

        Assert.Equal("lane_id", Field(await facilitator.PostAsync(url, new { steps = new[] { new { key = "x", label = "Valider", column = 1 } } }), 400, "CHAMP_OBLIGATOIRE"));
        Assert.Equal("column", Field(await facilitator.PostAsync(url, new { steps = new[] { new { key = "x", label = "Valider", lane_id = project.FirstLaneId } } }),
            400, "CHAMP_OBLIGATOIRE"));
        Assert.Equal("lane_id", Field(await facilitator.PostAsync(url, new { steps = new[] { new { key = "x", label = "Valider", lane_id = Guid.NewGuid(), column = 1 } } }),
            400, "VALEUR_INVALIDE"));
        var copy = new[] { new { key = "x", label = "Valider", lane_id = project.FirstLaneId, column = 1 } };
        Assert.Equal("position", Field(await facilitator.PostAsync(url, new { steps = copy, anchor = new { step_id = row[0], position = "inside" } }), 400, "VALEUR_INVALIDE"));
        Assert.Equal("step_id", Field(await facilitator.PostAsync(url, new { steps = copy, anchor = new { position = "after" } }), 400, "CHAMP_OBLIGATOIRE"));
        Assert.Equal([row[0]], StepIds(await project.MapAsync()));
    }
}
