// AssistantCommentDraftsTests.cs
// The comments of presentation written by the assistant through the whole host on both databases (3.3, lot 3, F-130): the practice `comments` lists the steps and the flows of the map open and of its detailed maps with their comment of today, reads the comments of its reply, and the comments kept after review are written as the assistant's, never over one typed by hand unless the person accepts it.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantCommentDraftsTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private sealed record Drafted(ApiProject Project, Guid[] Steps, string[] Codes, Guid[] Flows);

    /// <summary>Three steps in the first lane linked one after the other; the first has a comment typed in French.</summary>
    private static async Task<Drafted> ProjectAsync(ApiHost host)
    {
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var steps = new Guid[3];
        foreach (var (label, column) in new[] { ("Saisir", 0), ("Valider", 1), ("Envoyer", 2) })
            steps[column] = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column, label })).Ok().GetProperty("id").GetGuid();
        var flows = new Guid[2];
        for (var index = 0; index < 2; index++)
        {
            (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = steps[index], target_step_id = steps[index + 1] })).Ok();
        }
        (await facilitator.PutAsync($"/api/steps/{steps[0]}/presentation-comment", new { text = "Tapé à la main.", language = "fr" }, 0)).Ok();
        var map = await project.MapAsync();
        var codes = steps.Select(id => map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("id").GetGuid() == id).GetProperty("code").GetString()!).ToArray();
        foreach (var flow in map.GetProperty("flows").EnumerateArray())
            flows[Array.IndexOf(steps, flow.GetProperty("source_step_id").GetGuid())] = flow.GetProperty("id").GetGuid();
        return new Drafted(project, steps, codes, flows);
    }

    private static async Task CopyPasteAsync(ApiHost host)
    {
        using var admin = host.As(Logins.Admin);
        var version = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
        (await admin.PutAsync("/api/admin/assistant", new { connector = "copy_paste", allowed_classifications = new[] { "internal" } }, version)).Ok();
    }

    /// <summary>The entries of `&lt;comments_to_write&gt;` in a prompt.</summary>
    private static List<JsonElement> Listed(string prompt)
    {
        var listed = prompt[prompt.IndexOf("<comments_to_write>\n", StringComparison.Ordinal)..prompt.IndexOf("\n</comments_to_write>", StringComparison.Ordinal)]["<comments_to_write>\n".Length..];
        return JsonDocument.Parse(listed).RootElement.EnumerateArray().ToList();
    }

    private static object Ask() => new { intent = "comments", request = "Écris les commentaires des étapes et des relais", selection = Array.Empty<string>(), history = Array.Empty<object>() };

    /// <summary>The lines of the comments of the steps of the root map: the code of the step, the language, the text and the source.</summary>
    private static async Task<List<(string Code, string Language, string Text, string Source)>> LinesAsync(Drafted drafted, ApiClient client)
    {
        var codes = drafted.Steps.Zip(drafted.Codes).ToDictionary(pair => pair.First, pair => pair.Second);
        return [.. (await client.GetAsync($"/api/maps/{drafted.Project.RootMapId}/presentation-comments")).Ok().EnumerateArray()
            .Select(line => (codes[line.GetProperty("step_id").GetGuid()], line.GetProperty("language").GetString()!, line.GetProperty("text").GetString()!,
                line.GetProperty("source").GetString()!))
            .Order()];
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_practice_lists_the_steps_and_the_flows_of_the_map_with_their_comment_and_reads_the_comments_of_its_reply(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var drafted = await ProjectAsync(host);
        await CopyPasteAsync(host);
        using var facilitator = drafted.Project.Facilitator;
        var root = drafted.Project.RootMapId;

        var prompt = (await facilitator.PostAsync($"/api/maps/{root}/assistant/prompt", Ask())).Ok().GetProperty("prompt").GetString()!;
        var list = Listed(prompt);
        Assert.Equal([drafted.Codes[0], drafted.Codes[1], drafted.Codes[2], $"{drafted.Codes[0]} > {drafted.Codes[1]}", $"{drafted.Codes[1]} > {drafted.Codes[2]}"],
            list.Select(entry => entry.GetProperty("step").GetString()));
        Assert.Equal(("step", "Saisir", "fr", "Tapé à la main.", "manual"), (list[0].GetProperty("kind").GetString(), list[0].GetProperty("context").GetString(),
            list[0].GetProperty("language").GetString(), list[0].GetProperty("today").GetString(), list[0].GetProperty("written_by").GetString()));
        Assert.Equal(("flow", "Saisir > Valider", JsonValueKind.Null), (list[3].GetProperty("kind").GetString(), list[3].GetProperty("context").GetString(),
            list[3].GetProperty("today").ValueKind));
        var schema = prompt[prompt.IndexOf("# The schema of your reply", StringComparison.Ordinal)..];
        Assert.Contains("\"comments\"", schema, StringComparison.Ordinal);
        Assert.DoesNotContain("\"pmap\"", schema, StringComparison.Ordinal);

        var reply = JsonSerializer.Serialize(new
        {
            form = "comments", message = "Deux commentaires.", cited = Array.Empty<string>(),
            comments = new object[]
            {
                new { step = drafted.Codes[1], text = "Le responsable valide la demande.\nIl peut la refuser." },
                new { step = $"{drafted.Codes[1]} -> {drafted.Codes[2]}", text = "La demande validée part à l'envoi." },
                new { step = "XYZ-E999", text = "Étape inconnue." },
            },
        });
        var read = (await facilitator.PostAsync($"/api/maps/{root}/assistant/replies", new { intent = "comments", request = "Écris", reply })).Ok().GetProperty("reply");
        var drafts = read.GetProperty("comment_drafts").EnumerateArray().ToList();
        Assert.Equal(2, drafts.Count);
        Assert.Equal((drafted.Steps[1], JsonValueKind.Null, "Le responsable valide la demande.\nIl peut la refuser.", JsonValueKind.Null, 0),
            (drafts[0].GetProperty("step_id").GetGuid(), drafts[0].GetProperty("flow_id").ValueKind, drafts[0].GetProperty("text").GetString(),
                drafts[0].GetProperty("current").ValueKind, drafts[0].GetProperty("version").GetInt32()));
        Assert.Equal((drafted.Flows[1], $"{drafted.Codes[1]} > {drafted.Codes[2]}"), (drafts[1].GetProperty("flow_id").GetGuid(), drafts[1].GetProperty("reference").GetString()));

        // A reply that names nothing of the map is no reply of the practice; a reader may not ask it.
        var nothing = JsonSerializer.Serialize(new { form = "comments", message = "Rien.", cited = Array.Empty<string>(), comments = new[] { new { step = "XYZ-E999", text = "Rien." } } });
        var refused = (await facilitator.PostAsync($"/api/maps/{root}/assistant/replies", new { intent = "comments", request = "Écris", reply = nothing })).Ok();
        Assert.Equal("invalid_reply", refused.GetProperty("failure").GetString());
        using var reader = drafted.Project.Reader;
        (await reader.PostAsync($"/api/maps/{root}/assistant/prompt", Ask())).Refused(403, "ACCES_REFUSE");
    }

    /// <summary>
    /// Essay of the 18/11/2026: asked on the main map for every step and detailed step, the practice wrote for the map
    /// open only. It lists the detailed maps after the map open, map by map, each entry with the name of its map; the
    /// review reads them in that order whatever the order of the reply; and the comments kept are written on every level,
    /// even when the review is saved from the detailed map the person went to meanwhile.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_practice_asked_on_the_main_map_writes_for_its_detailed_maps_too_and_their_comments_are_kept_from_any_level(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var drafted = await ProjectAsync(host);
        await CopyPasteAsync(host);
        using var facilitator = drafted.Project.Facilitator;
        var root = drafted.Project.RootMapId;
        var detail = (await facilitator.PostAsync($"/api/steps/{drafted.Steps[1]}/detail", null, await drafted.Project.StepVersionAsync(drafted.Steps[1]))).Ok()
            .GetProperty("id").GetGuid();
        var subLane = (await drafted.Project.MapAsync(detail)).GetProperty("lanes")[0].GetProperty("id").GetGuid();
        var inner = new (Guid Id, string Code)[2];
        foreach (var (label, column) in new[] { ("Contrôler", 0), ("Signer", 1) })
        {
            var created = (await facilitator.PostAsync($"/api/maps/{detail}/steps", new { lane_id = subLane, column, label })).Ok();
            inner[column] = (created.GetProperty("id").GetGuid(), created.GetProperty("code").GetString()!);
        }
        (await facilitator.PostAsync($"/api/maps/{detail}/flows", new { source_step_id = inner[0].Id, target_step_id = inner[1].Id })).Ok();
        var detailMap = await drafted.Project.MapAsync(detail);
        var innerFlow = detailMap.GetProperty("flows")[0].GetProperty("id").GetGuid();
        var (rootName, detailName) = ((await drafted.Project.MapAsync()).GetProperty("name").GetString()!, detailMap.GetProperty("name").GetString()!);

        var list = Listed((await facilitator.PostAsync($"/api/maps/{root}/assistant/prompt", Ask())).Ok().GetProperty("prompt").GetString()!);
        Assert.Equal([drafted.Codes[0], drafted.Codes[1], drafted.Codes[2], $"{drafted.Codes[0]} > {drafted.Codes[1]}", $"{drafted.Codes[1]} > {drafted.Codes[2]}",
            inner[0].Code, inner[1].Code, $"{inner[0].Code} > {inner[1].Code}"], list.Select(entry => entry.GetProperty("step").GetString()));
        Assert.Equal(Enumerable.Repeat(rootName, 5).Concat(Enumerable.Repeat(detailName, 3)), list.Select(entry => entry.GetProperty("map").GetString()!));
        Assert.Equal("Contrôler", list[5].GetProperty("context").GetString());
        // Asked on the detailed map, the practice lists that map only: the levels above are not below it.
        var below = Listed((await facilitator.PostAsync($"/api/maps/{detail}/assistant/prompt", Ask())).Ok().GetProperty("prompt").GetString()!);
        Assert.Equal([inner[0].Code, inner[1].Code, $"{inner[0].Code} > {inner[1].Code}"], below.Select(entry => entry.GetProperty("step").GetString()));

        var reply = JsonSerializer.Serialize(new
        {
            form = "comments", message = "Trois commentaires.", cited = Array.Empty<string>(),
            comments = new object[]
            {
                new { step = $"{inner[0].Code} > {inner[1].Code}", text = "Contrôlée, la demande va à la signature." },
                new { step = drafted.Codes[2], text = "La demande part au client." },
                new { step = inner[0].Code, text = "Le responsable contrôle la demande." },
            },
        });
        var drafts = (await facilitator.PostAsync($"/api/maps/{root}/assistant/replies", new { intent = "comments", request = "Écris", reply })).Ok()
            .GetProperty("reply").GetProperty("comment_drafts").EnumerateArray().ToList();
        Assert.Equal([(drafted.Steps[2], rootName), (inner[0].Id, detailName), (innerFlow, detailName)],
            drafts.Select(draft => ((draft.GetProperty("step_id").ValueKind == JsonValueKind.Null ? draft.GetProperty("flow_id") : draft.GetProperty("step_id")).GetGuid(),
                draft.GetProperty("map").GetString()!)));

        var result = (await facilitator.PostAsync($"/api/maps/{detail}/presentation-comments", new
        {
            comments = new object[]
            {
                new { step_id = drafted.Steps[2], text = "La demande part au client.", version = 0 },
                new { step_id = inner[0].Id, text = "Le responsable contrôle la demande.", version = 0 },
                new { flow_id = innerFlow, text = "Contrôlée, la demande va à la signature.", version = 0 },
            },
        })).Ok();
        Assert.Equal((3, 0), (result.GetProperty("written").GetInt32(), result.GetProperty("kept").GetInt32()));
        Assert.Equal([(drafted.Codes[0], "fr", "Tapé à la main.", "manual"), (drafted.Codes[2], "fr", "La demande part au client.", "assistant")],
            await LinesAsync(drafted, facilitator));
        var innerLine = Assert.Single((await facilitator.GetAsync($"/api/maps/{detail}/presentation-comments")).Ok().EnumerateArray());
        Assert.Equal((inner[0].Id, "assistant", "Le responsable contrôle la demande."), (innerLine.GetProperty("step_id").GetGuid(), innerLine.GetProperty("source").GetString(),
            innerLine.GetProperty("text").GetString()));
        var innerFlowLine = Assert.Single((await facilitator.GetAsync($"/api/maps/{detail}/flow-presentation-comments")).Ok().EnumerateArray());
        Assert.Equal((innerFlow, "assistant"), (innerFlowLine.GetProperty("flow_id").GetGuid(), innerFlowLine.GetProperty("source").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_comments_kept_are_written_as_the_assistant_s_and_one_typed_by_hand_is_replaced_only_when_the_person_accepts_it(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var drafted = await ProjectAsync(host);
        using var facilitator = drafted.Project.Facilitator;
        var root = drafted.Project.RootMapId;

        var result = (await facilitator.PostAsync($"/api/maps/{root}/presentation-comments", new
        {
            comments = new object[]
            {
                new { step_id = drafted.Steps[0], text = "Proposé par l'IA.", version = 1 },
                new { step_id = drafted.Steps[1], text = "Le responsable valide.", version = 0 },
                new { flow_id = drafted.Flows[0], text = "La demande passe au responsable.", version = 0 },
            },
        })).Ok();
        Assert.Equal((2, 1), (result.GetProperty("written").GetInt32(), result.GetProperty("kept").GetInt32()));
        Assert.Equal([(drafted.Codes[0], "fr", "Tapé à la main.", "manual"), (drafted.Codes[1], "fr", "Le responsable valide.", "assistant")], await LinesAsync(drafted, facilitator));
        var flowLine = Assert.Single((await facilitator.GetAsync($"/api/maps/{root}/flow-presentation-comments")).Ok().EnumerateArray());
        Assert.Equal((drafted.Flows[0], "assistant", "La demande passe au responsable."), (flowLine.GetProperty("flow_id").GetGuid(), flowLine.GetProperty("source").GetString(),
            flowLine.GetProperty("text").GetString()));

        // Accepted, the typed one is replaced; a version read before a change is a conflict, and nothing is written.
        (await facilitator.PostAsync($"/api/maps/{root}/presentation-comments", new { comments = new[] { new { step_id = drafted.Steps[1], text = "Encore.", version = 0, replace = true } } }))
            .Refused(409, "CONFLIT_VERSION");
        (await facilitator.PostAsync($"/api/maps/{root}/presentation-comments", new { comments = new[] { new { step_id = drafted.Steps[0], text = "Accepté.", version = 1, replace = true } } })).Ok();
        Assert.Equal([(drafted.Codes[0], "fr", "Accepté.", "assistant"), (drafted.Codes[1], "fr", "Le responsable valide.", "assistant")], await LinesAsync(drafted, facilitator));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_reader_writes_no_comment_and_a_list_empty_doubled_or_off_the_project_is_refused_whole(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var drafted = await ProjectAsync(host);
        using var facilitator = drafted.Project.Facilitator;
        using var reader = drafted.Project.Reader;
        var root = drafted.Project.RootMapId;
        var path = $"/api/maps/{root}/presentation-comments";

        (await reader.PostAsync(path, new { comments = new[] { new { step_id = drafted.Steps[1], text = "Non." } } })).Refused(403, "ACCES_REFUSE");
        Assert.Equal("comments", (await facilitator.PostAsync(path, new { comments = Array.Empty<object>() })).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        Assert.Equal("comments", (await facilitator.PostAsync(path, new { comments = new[] { new { step_id = drafted.Steps[1], text = "Un." }, new { step_id = drafted.Steps[1], text = "Deux." } } }))
            .Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        Assert.Equal("text", (await facilitator.PostAsync(path, new { comments = new[] { new { step_id = drafted.Steps[1], text = "  " } } })).Refused(400, "VALEUR_INVALIDE")
            .GetProperty("field").GetString());
        (await facilitator.PostAsync(path, new { comments = new object[] { new { step_id = drafted.Steps[2], text = "Bon." }, new { step_id = Guid.NewGuid(), text = "Ailleurs." } } }))
            .Refused(404, "INTROUVABLE");
        // A step of another project, though live, is not one of this project.
        var other = await ProjectAsync(host);
        (await facilitator.PostAsync(path, new { comments = new object[] { new { step_id = drafted.Steps[2], text = "Bon." }, new { step_id = other.Steps[1], text = "Ailleurs." } } }))
            .Refused(404, "INTROUVABLE");
        // Refused whole: the good entry was not written either.
        Assert.Equal([(drafted.Codes[0], "fr", "Tapé à la main.", "manual")], await LinesAsync(drafted, facilitator));
    }
}
