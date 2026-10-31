// AssistantLevelsTests.cs
// The levels of a proposal through the whole host on both databases (ADR 082): a map split into levels in place and undone by one undo, a proposal that brings the steps back to one level refused in place and opened as a new project with the sheets of its steps of origin.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantLevelsTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>A project of four steps in a chain in its first lane: the project, the code and name of the lane, the codes of the steps.</summary>
    private static async Task<(ApiProject Project, string Lane, string LaneName, string[] Codes)> ProjectAsync(ApiHost host)
    {
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/steps";
        var ids = new List<Guid>();
        foreach (var (label, column) in new[] { ("Saisir", 0), ("Compléter", 1), ("Envoyer", 2), ("Classer", 3) })
            ids.Add((await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column, label })).Ok().GetProperty("id").GetGuid());
        for (var i = 0; i + 1 < ids.Count; i++)
            (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = ids[i], target_step_id = ids[i + 1] })).Ok();
        var map = await project.MapAsync();
        var lane = map.GetProperty("lanes")[0];
        return (project, lane.GetProperty("code").GetString()!, lane.GetProperty("name").GetString()!,
            [.. ids.Select(id => map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("id").GetGuid() == id).GetProperty("code").GetString()!)]);
    }

    private static string Pmap(string projectCode, object map) =>
        JsonSerializer.Serialize(new { format = "topogenia", version = "1.1", project = new { name = "Projet", code = projectCode }, map });

    /// <summary>The second and third steps under a new step `prep`: the map in two levels.</summary>
    private static string Split(string projectCode, string lane, string laneName, string[] codes) => Pmap(projectCode, new
    {
        lanes = new object[] { new { id = lane, name = laneName } },
        steps = new object[]
        {
            new { id = codes[0], lane, label = "Saisir", column = 0 },
            new
            {
                id = "prep", lane, label = "Préparer", column = 1,
                sub_map = new
                {
                    lanes = new object[] { new { id = "sub", name = laneName } },
                    steps = new object[] { new { id = codes[1], lane = "sub", label = "Compléter", column = 0 }, new { id = codes[2], lane = "sub", label = "Envoyer", column = 1 } },
                    flows = new object[] { new { source = codes[1], target = codes[2] } },
                },
            },
            new { id = codes[3], lane, label = "Classer", column = 2 },
        },
        flows = new object[] { new { source = codes[0], target = "prep" }, new { source = "prep", target = codes[3] } },
    });

    private static string[] Labels(JsonElement map) => [.. map.GetProperty("steps").EnumerateArray().OrderBy(step => step.GetProperty("column").GetInt32())
        .Select(step => step.GetProperty("label").GetString()!)];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_map_split_into_levels_is_previewed_applied_in_one_operation_and_undone_by_one_undo(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, lane, laneName, codes) = await ProjectAsync(host);
        using var facilitator = project.Facilitator;
        var pmap = Split(project.Code, lane, laneName, codes);
        var preview = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap })).Ok();
        Assert.Equal("in_place", preview.GetProperty("mode").GetString());
        var changes = preview.GetProperty("changes").EnumerateArray().Select(change => (change.GetProperty("kind").GetString(), change.GetProperty("code").GetString(),
            change.GetProperty("action").GetString(), string.Join(",", change.GetProperty("fields").EnumerateArray().Select(field => field.GetString())))).ToList();
        Assert.Contains(("map", (string?)null, "added", ""), changes);
        Assert.Contains(("step", codes[1], "changed", "level"), changes);
        Assert.DoesNotContain(changes, change => change.Item3 == "removed" && change.Item1 == "step");

        var applied = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap }, preview.GetProperty("structure_version").GetInt32())).Ok();
        var root = applied.GetProperty("map");
        Assert.Equal(["Saisir", "Préparer", "Classer"], Labels(root));
        var prepare = root.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("label").GetString() == "Préparer");
        var detail = await project.MapAsync(prepare.GetProperty("sub_map_id").GetGuid());
        Assert.Equal(["Compléter", "Envoyer"], Labels(detail));
        Assert.Equal([codes[1], codes[2]], detail.GetProperty("steps").EnumerateArray().OrderBy(step => step.GetProperty("column").GetInt32())
            .Select(step => step.GetProperty("code").GetString()));
        Assert.Equal("proposition_ia", applied.GetProperty("history").GetProperty("undo")[0].GetProperty("kind").GetString());

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal(["Saisir", "Compléter", "Envoyer", "Classer"], Labels(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_proposal_that_brings_the_steps_back_to_one_level_is_refused_in_place_and_opens_as_a_new_project_with_their_sheets(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, lane, laneName, codes) = await ProjectAsync(host);
        using var facilitator = project.Facilitator;
        var split = Split(project.Code, lane, laneName, codes);
        var read = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap = split })).Ok();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap = split }, read.GetProperty("structure_version").GetInt32())).Ok();
        var detail = (await project.MapAsync()).GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("label").GetString() == "Préparer");
        var sent = (await project.MapAsync(detail.GetProperty("sub_map_id").GetGuid())).GetProperty("steps").EnumerateArray()
            .Single(step => step.GetProperty("code").GetString() == codes[2]).GetProperty("id").GetGuid();
        (await facilitator.PutAsync($"/api/steps/{sent}/sheet", new { fields = new { purpose = new { value = "Prévenir les achats" } } }, 0)).Ok();
        (await facilitator.PutAsync($"/api/steps/{detail.GetProperty("id").GetGuid()}/sheet", new { fields = new { purpose = new { value = "Préparer la demande" } } }, 0)).Ok();

        var flat = Pmap(project.Code, new
        {
            lanes = new object[] { new { id = lane, name = laneName } },
            steps = codes.Select((code, column) => (object)new { id = code, lane, label = new[] { "Saisir", "Compléter", "Envoyer", "Classer" }[column], column }).ToArray(),
            flows = new object[] { new { source = codes[0], target = codes[1] }, new { source = codes[1], target = codes[2] }, new { source = codes[2], target = codes[3] } },
        });
        var preview = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap = flat })).Ok();
        Assert.Equal("new_project", preview.GetProperty("mode").GetString());
        Assert.Equal([$"raised:{codes[1]}", $"raised:{codes[2]}"], preview.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
        var missing = Assert.Single(preview.GetProperty("missing").EnumerateArray());
        // The detailed step vanishes, and its sheet with it: the preview says so.
        Assert.Equal(("Préparer", true, true), (missing.GetProperty("label").GetString(), missing.GetProperty("detailed").GetBoolean(), missing.GetProperty("has_sheet").GetBoolean()));
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap = flat }, preview.GetProperty("structure_version").GetInt32()))
            .Refused(409, "PROPOSITION_NOUVEAU_PROJET");

        using var admin = project.Admin;
        var opened = (await admin.PostAsync("/api/projects/import-pmap", new { content = flat, code = ApiProject.NewCode(), name = "À plat", origin_map_id = project.RootMapId })).Ok(201);
        var map = (await admin.GetAsync($"/api/maps/{opened.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map");
        Assert.Equal(["Saisir", "Compléter", "Envoyer", "Classer"], Labels(map));
        var carried = map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("label").GetString() == "Envoyer");
        Assert.Equal("draft", carried.GetProperty("sheet").GetProperty("status").GetString());
        // The original stays in two levels.
        Assert.Equal(["Saisir", "Préparer", "Classer"], Labels(await project.MapAsync()));
    }
}
