// StructureStepPortedTests.cs
// The 1.x step tests (StructureTests.EtapesTests, T-STRU-13 to 24) ported to the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Host.Tests.Ported.StructurePortedCalls;

namespace Topogenia.Host.Tests.Ported;

public sealed class StructureStepPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Step_codes_are_sequential_kept_on_a_change_and_never_given_again_after_a_deletion(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var start = await project.AddStepAsync(0, "Début", type: "start");
        var second = await project.AddStepAsync(1, "Extraction");
        Assert.Equal(($"{project.Code}-E001", $"{project.Code}-E002"), (start.Code, second.Code));

        (await PatchStepAsync(project, facilitator, second.Id, new { label = "Extraction des ventes", column = 5 })).Ok();
        var changed = StructureCalls.StepOf(await project.MapAsync(), second.Id);
        Assert.Equal(($"{project.Code}-E002", "Extraction des ventes", 5),
            (changed.GetProperty("code").GetString(), changed.GetProperty("label").GetString(), changed.GetProperty("column").GetInt32()));

        (await facilitator.DeleteAsync($"/api/steps/{second.Id}", await project.StepVersionAsync(second.Id))).Ok();
        Assert.Equal($"{project.Code}-E003", (await project.AddStepAsync(1, "Nouvelle")).Code);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_is_created_with_its_type_the_language_of_its_author_and_the_manual_state_and_an_unknown_type_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        string[] types = ["task", "decision", "wait", "start", "end"];
        for (var column = 0; column < types.Length; column++)
            await project.AddStepAsync(column, $"Type {types[column]}", type: types[column]);
        var steps = (await project.MapAsync()).GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(types, steps.Select(step => step.GetProperty("type").GetString()));
        Assert.All(steps, step => Assert.Equal(("fr", "manual"), (step.GetProperty("source_language").GetString(), step.GetProperty("automation_status").GetString())));

        Assert.Equal("type", (await facilitator.PostAsync(StepsUrl(project), new { lane_id = project.FirstLaneId, column = 9, label = "X", type = "tache" }))
            .Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal(types.Length, (await project.MapAsync()).GetProperty("steps").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_column_out_of_the_grid_an_empty_long_or_html_label_and_an_unknown_lane_create_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = StepsUrl(project);
        var lane = project.FirstLaneId;
        Assert.Equal("column", (await facilitator.PostAsync(url, new { lane_id = lane, column = -1, label = "X" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("column", (await facilitator.PostAsync(url, new { lane_id = lane, column = 10_001, label = "X" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("label", (await facilitator.PostAsync(url, new { lane_id = lane, column = 0, label = "" })).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        (await facilitator.PostAsync(url, new { lane_id = lane, column = 0, label = new string('a', 151) })).Refused(400, "TEXTE_TROP_LONG");
        (await facilitator.PostAsync(url, new { lane_id = lane, column = 0, label = "<img src=x onerror=alert(1)>" })).Refused(400, "TEXTE_HTML_INTERDIT");
        Assert.Equal("lane_id", (await facilitator.PostAsync(url, new { lane_id = Guid.NewGuid(), column = 0, label = "X" })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Empty((await project.MapAsync()).GetProperty("steps").EnumerateArray());

        // The last column of the grid is a column.
        var last = await project.AddStepAsync(10_000, "Dernière colonne");
        Assert.Equal((lane, 10_000, 0), CellOf(await project.MapAsync(), last.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_is_neither_created_nor_moved_in_a_lane_of_another_map_rg_c02(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        var other = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        Assert.Equal("lane_id", (await facilitator.PostAsync(StepsUrl(project), new { lane_id = other.FirstLaneId, column = 0, label = "X" }))
            .Refused(400, "VALEUR_INVALIDE").Field());
        var step = await project.AddStepAsync(0, "X");
        var (_, subLane) = await project.DetailAsync((await project.AddStepAsync(1, "Macro")).Id);
        Assert.Equal("lane_id", (await PatchStepAsync(project, facilitator, step.Id, new { lane_id = other.FirstLaneId })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("lane_id", (await PatchStepAsync(project, facilitator, step.Id, new { lane_id = subLane })).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal((project.FirstLaneId, 0, 0), CellOf(await project.MapAsync(), step.Id));
        Assert.Empty((await other.MapAsync()).GetProperty("steps").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_dropped_on_a_taken_cell_shifts_only_the_contiguous_block_rg_c04(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var a = await project.AddStepAsync(0, "A");
        var b = await project.AddStepAsync(1, "B");
        var c = await project.AddStepAsync(2, "C");
        var d = await project.AddStepAsync(4, "D");
        var dropped = await project.AddStepAsync(1, "Nouvelle");
        var map = await project.MapAsync();
        Assert.Equal([0, 1, 2, 3, 4], new[] { a, dropped, b, c, d }.Select(step => CellOf(map, step.Id).Column));

        // A new drop on column 1: the block 1 to 4 is now contiguous and slides whole.
        await project.AddStepAsync(1, "Encore");
        Assert.Equal(5, CellOf(await project.MapAsync(), d.Id).Column);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_shift_of_a_drop_leaves_the_other_lanes_where_they_are(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        var inB = await project.AddStepAsync(1, "Dans B", b);
        var first = await project.AddStepAsync(1, "A1");
        await project.AddStepAsync(1, "A1 bis");
        var map = await project.MapAsync();
        Assert.Equal((b, 1, 0), CellOf(map, inB.Id));
        Assert.Equal((project.FirstLaneId, 2, 0), CellOf(map, first.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_moved_left_then_right_in_its_lane_shifts_its_neighbours_without_blocking_itself(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (a, b, c) = (await project.AddStepAsync(0, "A"), await project.AddStepAsync(1, "B"), await project.AddStepAsync(2, "C"));

        // C to column 0: A and B slide; C does not block itself.
        (await PatchStepAsync(project, facilitator, c.Id, new { column = 0 })).Ok();
        var map = await project.MapAsync();
        Assert.Equal(["C", "A", "B"], LabelsByColumn(map));
        Assert.Equal([0, 1, 2], Columns(map));

        // C to column 2, taken by B: B goes to 3 and column 0 is free again.
        (await PatchStepAsync(project, facilitator, c.Id, new { column = 2 })).Ok();
        map = await project.MapAsync();
        Assert.Equal((1, 2, 3), (CellOf(map, a.Id).Column, CellOf(map, c.Id).Column, CellOf(map, b.Id).Column));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_moved_to_another_lane_shifts_the_step_that_held_its_cell(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        var x = await project.AddStepAsync(0, "X");
        var y = await project.AddStepAsync(0, "Y", b);
        (await PatchStepAsync(project, facilitator, x.Id, new { lane_id = b, column = 0 })).Ok();
        var map = await project.MapAsync();
        Assert.Equal((b, 0, 0), CellOf(map, x.Id));
        Assert.Equal((b, 1, 0), CellOf(map, y.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_automation_state_and_the_assignee_change_with_the_version_and_unknown_values_or_wrong_types_are_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var step = await project.AddStepAsync(0, "X");
        var contributor = await MemberIdAsync(project, Logins.Contributor);
        var url = $"/api/steps/{step.Id}";
        (await PatchStepAsync(project, facilitator, step.Id, new { automation_status = "automated" })).Ok();
        var read = StructureCalls.StepOf(await project.MapAsync(), step.Id);

        // The assignment alone is a change of the step: new version and author of the change (AR-32).
        (await admin.PatchAsync(url, new { assignee_user_id = contributor }, read.GetProperty("version").GetInt32())).Ok();
        var assigned = StructureCalls.StepOf(await project.MapAsync(), step.Id);
        Assert.Equal(("automated", contributor), (assigned.GetProperty("automation_status").GetString(), assigned.GetProperty("assignee_user_id").GetGuid()));
        Assert.True(assigned.GetProperty("version").GetInt32() > read.GetProperty("version").GetInt32());
        Assert.Equal("admin", (await facilitator.PatchAsync(url, new { label = "Y" }, read.GetProperty("version").GetInt32())).Refused(409, "CONFLIT_VERSION")
            .GetProperty("by").GetString());

        var version = assigned.GetProperty("version").GetInt32();
        Assert.Equal("automation_status", (await facilitator.PatchAsync(url, new { automation_status = "magique" }, version)).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("assignee_user_id", (await facilitator.PatchAsync(url, new { assignee_user_id = Guid.NewGuid() }, version)).Refused(400, "VALEUR_INVALIDE").Field());
        (await facilitator.PatchAsync(url, new { column = "deux" }, version)).Refused(400, "REQUETE_INVALIDE");
        var after = StructureCalls.StepOf(await project.MapAsync(), step.Id);
        Assert.Equal(("automated", contributor, 0, "X"), (after.GetProperty("automation_status").GetString(), after.GetProperty("assignee_user_id").GetGuid(),
            after.GetProperty("column").GetInt32(), after.GetProperty("label").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_reading_paths_follow_the_columns_then_the_lanes(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var b = (await AddLaneAsync(project, "B")).GetProperty("id").GetGuid();
        await project.AddStepAsync(0, "B0", b);
        await project.AddStepAsync(1, "A1");
        await project.AddStepAsync(0, "A0");
        var map = await project.MapAsync();
        Assert.Equal(["A0", "B0", "A1"], Labels(map));
        Assert.Equal(["1", "2", "3"], map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("path").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Ten_creations_in_one_cell_at_the_same_time_all_succeed_with_distinct_columns_and_codes(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var clients = Enumerable.Range(0, 10).Select(_ => project.Facilitator).ToList();
        try
        {
            var replies = await Task.WhenAll(clients.Select((client, rank) =>
                client.PostAsync(StepsUrl(project), new { lane_id = project.FirstLaneId, column = 0, label = $"Parallèle {rank}" })));
            Assert.All(replies, reply => reply.Ok());
        }
        finally
        {
            clients.ForEach(client => client.Dispose());
        }
        var steps = (await project.MapAsync()).GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(10, steps.Count);
        Assert.Equal(Enumerable.Range(0, 10), steps.Select(step => step.GetProperty("column").GetInt32()).Order());
        Assert.Equal(10, steps.Select(step => step.GetProperty("code").GetString()).Distinct().Count());
        Assert.All(steps, step => Assert.Equal(JsonValueKind.String, step.GetProperty("code").ValueKind));
    }
}
