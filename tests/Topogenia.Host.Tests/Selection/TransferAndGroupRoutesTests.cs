// TransferAndGroupRoutesTests.cs
// Cut and paste of a block and « Detail the selection » through the whole host on both databases (annex A 1.3-20 to 26, A 2.8).

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Selection;

public sealed class TransferAndGroupRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_block_cut_to_a_sub_map_keeps_its_code_leaves_its_flow_stitched_and_the_undo_of_the_target_map_brings_it_back(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander", "Valider", "Livrer");
        using var facilitator = project.Facilitator;
        var grouped = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = new[] { row[2] }, label = "Achats" })).Ok();
        var (macro, subMap) = (grouped.GetProperty("id").GetGuid(), Ids(grouped)[0]);
        var code = Step(await project.MapAsync(), row[1]).GetProperty("code").GetString();
        var lane = FirstLane(await project.MapAsync(subMap));

        var moved = (await facilitator.PostAsync($"/api/maps/{subMap}/transfer",
            new { steps = new[] { new { id = row[1], lane_id = lane, column = 3, row = 1 } }, cut = true })).Ok();
        Assert.Equal([row[1]], Ids(moved));
        var step = Step(moved.GetProperty("map"), row[1]);
        Assert.Equal((code, 3, 1), (step.GetProperty("code").GetString(), step.GetProperty("column").GetInt32(), step.GetProperty("row").GetInt32()));
        var root = await project.MapAsync();
        Assert.DoesNotContain(row[1], StepIds(root));
        Assert.Equal(new HashSet<(Guid, Guid)> { (row[0], macro) }, Flows(root));

        var undone = (await facilitator.PostAsync($"/api/maps/{subMap}/undo")).Ok();
        Assert.Equal("deplacer_bloc", undone.GetProperty("code").GetString());
        Assert.Equal(new HashSet<(Guid, Guid)> { (row[0], row[1]), (row[1], macro) }, Flows(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_transfer_needs_the_identifier_of_each_step_and_refuses_an_unknown_step_or_its_own_detail(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander");
        using var facilitator = project.Facilitator;
        var grouped = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = row, label = "Achats" })).Ok();
        var (macro, subMap) = (grouped.GetProperty("id").GetGuid(), Ids(grouped)[0]);
        var lane = FirstLane(await project.MapAsync(subMap));
        var url = $"/api/maps/{subMap}/transfer";

        Assert.Equal("id", (await facilitator.PostAsync(url, new { steps = new[] { new { lane_id = lane, column = 3 } } })).Refused(400, "CHAMP_OBLIGATOIRE")
            .GetProperty("field").GetString());
        (await facilitator.PostAsync(url, new { steps = new[] { new { id = Guid.NewGuid(), lane_id = lane, column = 3 } } })).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync(url, new { steps = new[] { new { id = macro, lane_id = lane, column = 3 } }, cut = true })).Refused(400, "TRANSFERT_CYCLE");
        Assert.Equal([macro], StepIds(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Detailing_a_selection_answers_the_new_step_and_its_sub_map_and_is_undone_by_the_map(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander", "Valider", "Livrer");
        using var facilitator = project.Facilitator;
        var grouped = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = new[] { row[1] }, label = "Achats" })).Ok();
        var (macro, subMap) = (grouped.GetProperty("id").GetGuid(), Ids(grouped)[0]);
        var step = Step(grouped.GetProperty("map"), macro);
        Assert.Equal((grouped.GetProperty("code").GetString(), "Achats", subMap),
            (step.GetProperty("code").GetString(), step.GetProperty("label").GetString(), step.GetProperty("sub_map_id").GetGuid()));
        Assert.Equal(new HashSet<(Guid, Guid)> { (row[0], macro), (macro, row[2]) }, Flows(grouped.GetProperty("map")));
        Assert.Empty(Warnings(grouped));
        Assert.Equal([row[1]], StepIds(await project.MapAsync(subMap)));

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal("regrouper", undone.GetProperty("code").GetString());
        Assert.Equal(row.Order(), StepIds(undone.GetProperty("map")).Order());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Detailing_a_selection_answers_the_fields_of_a_structure_reply_only(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander", "Valider", "Livrer");
        using var facilitator = project.Facilitator;
        // The grouping cuts the two flows of the middle step: the 1.x answered them in `liens_coupes`, the 2.0 keeps them
        // in the operation of the log only (AR-50), so the reply has the fields of every structure command and nothing else.
        var grouped = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = new[] { row[1] }, label = "Achats" })).Ok();
        Assert.Equal(["code", "history", "id", "ids", "map", "operation_id", "structure_version", "warnings"],
            grouped.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(new HashSet<(Guid, Guid)> { (row[0], grouped.GetProperty("id").GetGuid()), (grouped.GetProperty("id").GetGuid(), row[2]) },
            Flows(grouped.GetProperty("map")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_grouping_needs_a_label_and_steps_of_its_map_and_warns_beyond_the_recommended_depth(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var row = await RowAsync(project, "Commander", "Livrer");
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/group";
        Assert.Equal("label", (await facilitator.PostAsync(url, new { steps = new[] { row[0] } })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        var map = project.RootMapId;
        var replies = new List<IReadOnlyList<string?>>();
        for (var level = 1; level <= 4; level++)
        {
            var grouped = (await facilitator.PostAsync($"/api/maps/{map}/group", new { steps = new[] { row[0] }, label = $"Niveau {level}" })).Ok();
            replies.Add(Warnings(grouped));
            map = Ids(grouped)[0];
        }
        Assert.Equal([[], [], [], ["PROFONDEUR_DEPASSEE"]], replies);
        Assert.Equal(Step(await project.MapAsync(map), row[0]).GetProperty("code").GetString(),
            (await facilitator.PostAsync(url, new { steps = new[] { row[0] }, label = "Achats" })).Refused(400, "ETAPE_HORS_CARTE").GetProperty("code").GetString());
        Assert.Contains(row[1], StepIds(await project.MapAsync()));
    }
}
