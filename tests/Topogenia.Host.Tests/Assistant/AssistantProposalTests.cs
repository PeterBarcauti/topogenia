// AssistantProposalTests.cs
// A proposal of the assistant on the map open, through the whole host on both databases: its preview, its application in one operation undone by one undo, its translations (ADR 081), a stale preview refused, the rights and a file refused (spec 3.0 sections 3.1-5 to 7, ADR 079).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantProposalTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>A project with two steps in its first lane, linked: its code, the codes of its first lane and of its steps.</summary>
    private static async Task<(ApiProject Project, string Lane, string First, string Second)> ProjectAsync(ApiHost host)
    {
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/steps";
        var first = (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column = 0, label = "Saisir" })).Ok().GetProperty("id").GetGuid();
        var second = (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column = 1, label = "Envoyer" })).Ok().GetProperty("id").GetGuid();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = first, target_step_id = second })).Ok();
        var map = await project.MapAsync();
        string CodeOf(Guid id) => map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("id").GetGuid() == id).GetProperty("code").GetString()!;
        return (project, map.GetProperty("lanes")[0].GetProperty("code").GetString()!, CodeOf(first), CodeOf(second));
    }

    /// <summary>The first step renamed, the second removed, a new lane and a new step after the first.</summary>
    private static string Proposal(string projectCode, string lane, string first) => JsonSerializer.Serialize(new
    {
        format = "topogenia", version = "1.1", project = new { name = "Projet", code = projectCode },
        map = new
        {
            lanes = new object[] { new { id = lane, name = "Demandeur" }, new { id = "val", name = "Direction" } },
            steps = new object[]
            {
                new { id = first, lane, label = "Saisir la demande", column = 0 }, new { id = "new", lane = "val", label = "Valider la demande", column = 1 },
            },
            flows = new object[] { new { source = first, target = "new" } },
        },
    });

    private static string[] Labels(JsonElement map) => [.. map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("label").GetString()!).Order()];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_proposal_is_previewed_applied_in_one_operation_and_undone_by_one_undo(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, lane, first, second) = await ProjectAsync(host);
        using var facilitator = project.Facilitator;
        var pmap = Proposal(project.Code, lane, first);
        var preview = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap })).Ok();
        var changes = preview.GetProperty("changes").EnumerateArray().Select(change =>
            (change.GetProperty("kind").GetString(), change.GetProperty("code").GetString(), change.GetProperty("action").GetString())).ToList();
        Assert.Contains(("step", first, "changed"), changes);
        Assert.Contains(("step", second, "removed"), changes);
        Assert.Contains(("step", (string?)null, "added"), changes);
        Assert.Contains(("lane", (string?)null, "added"), changes);
        Assert.False(preview.GetProperty("empty").GetBoolean());
        // The preview wrote nothing.
        Assert.Equal(["Envoyer", "Saisir"], Labels(await project.MapAsync()));

        var applied = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap }, preview.GetProperty("structure_version").GetInt32())).Ok();
        Assert.Equal(["Saisir la demande", "Valider la demande"], Labels(applied.GetProperty("map")));
        Assert.Equal(2, applied.GetProperty("map").GetProperty("lanes").GetArrayLength());
        var undo = applied.GetProperty("history").GetProperty("undo");
        Assert.Equal("proposition_ia", undo[0].GetProperty("kind").GetString());

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        var back = await project.MapAsync();
        Assert.Equal(["Envoyer", "Saisir"], Labels(back));
        Assert.Single(back.GetProperty("lanes").EnumerateArray());
        Assert.Single(back.GetProperty("flows").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_map_given_back_by_the_application_shows_the_sheets_of_the_steps_as_the_map_read_again(EngineKind kind)
    {
        // « Appliquer » reads with the map it gives back the sheets, the documents and the translations of the steps: the
        // map shows them as the map read again (ADR 081).
        var host = await hosts.OfAsync(kind);
        var (project, lane, first, second) = await ProjectAsync(host);
        using var facilitator = project.Facilitator;
        var map = await project.MapAsync();
        var kept = StepOf(map, second).GetProperty("id").GetGuid();
        (await facilitator.PutAsync($"/api/steps/{kept}/sheet", new { fields = new { purpose = new { value = "Prévenir les achats" } } }, 0)).Ok();
        var laneName = map.GetProperty("lanes")[0].GetProperty("name").GetString()!;
        var pmap = JsonSerializer.Serialize(new
        {
            format = "topogenia", version = "1.1", project = new { name = "Projet", code = project.Code },
            map = new
            {
                lanes = new object[] { new { id = lane, name = laneName } },
                steps = new object[] { new { id = first, lane, label = "Saisir la demande", column = 0 }, new { id = second, lane, label = "Envoyer", column = 1 } },
                flows = new object[] { new { source = first, target = second } },
            },
        });
        var preview = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap })).Ok();
        var applied = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap }, preview.GetProperty("structure_version").GetInt32())).Ok();
        var given = StepOf(applied.GetProperty("map"), second).GetProperty("sheet");
        Assert.Equal("draft", given.GetProperty("status").GetString());
        Assert.Equal(StepOf(await project.MapAsync(), second).GetProperty("sheet").GetRawText(), given.GetRawText());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_preview_made_stale_by_a_change_of_the_map_is_refused_and_writes_nothing(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, lane, first, _) = await ProjectAsync(host);
        using var facilitator = project.Facilitator;
        var pmap = Proposal(project.Code, lane, first);
        var preview = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap })).Ok();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Logistique" })).Ok();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap }, preview.GetProperty("structure_version").GetInt32()))
            .Refused(409, "CONFLIT_VERSION");
        Assert.Equal(["Envoyer", "Saisir"], Labels(await project.MapAsync()));
    }

    /// <summary>The map as it is, its lane, its steps and their flow translated in a language (ADR 081).</summary>
    private static string Translation(string projectCode, string lane, string laneName, string first, string second, string language) => JsonSerializer.Serialize(new
    {
        format = "topogenia", version = "1.1", project = new { name = "Projet", code = projectCode },
        map = new
        {
            lanes = new object[] { new { id = lane, name = laneName, translations = new Dictionary<string, string> { [language] = "Requester" } } },
            steps = new object[]
            {
                new { id = first, lane, label = "Saisir", column = 0, translations = new Dictionary<string, string> { [language] = "Enter" } },
                new { id = second, lane, label = "Envoyer", column = 1, translations = new Dictionary<string, string> { [language] = "Send" } },
            },
            flows = new object[] { new { source = first, target = second } },
        },
    });

    private static JsonElement StepOf(JsonElement map, string code) =>
        map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("code").GetString() == code);

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_is_previewed_written_as_automatic_ones_kept_over_a_manual_one_and_undone_by_one_undo(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, lane, first, second) = await ProjectAsync(host);
        using var facilitator = project.Facilitator;
        var map = await project.MapAsync();
        var laneName = map.GetProperty("lanes")[0].GetProperty("name").GetString()!;
        // A translation entered by a person on the second step: the proposal never replaces it.
        var secondStep = StepOf(map, second);
        (await facilitator.PutAsync($"/api/steps/{secondStep.GetProperty("id").GetGuid()}/translations", new { translations = new { en = "Dispatch" } },
            secondStep.GetProperty("version").GetInt32())).Ok();

        var pmap = Translation(project.Code, lane, laneName, first, second, "en");
        var preview = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap })).Ok();
        var changes = preview.GetProperty("changes").EnumerateArray().Select(change =>
            (change.GetProperty("code").GetString(), change.GetProperty("action").GetString(), string.Join(",", change.GetProperty("fields").EnumerateArray().Select(field => field.GetString())))).ToList();
        Assert.Contains((first, "changed", "translations"), changes);
        Assert.Contains((lane, "changed", "translations"), changes);
        Assert.DoesNotContain(changes, change => change.Item1 == second);
        Assert.Equal([$"translation_kept:{second}"], preview.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()));

        var applied = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap }, preview.GetProperty("structure_version").GetInt32())).Ok();
        var after = applied.GetProperty("map");
        Assert.Equal("Enter", StepOf(after, first).GetProperty("translations").GetProperty("en").GetString());
        Assert.Equal("Saisir", StepOf(after, first).GetProperty("label").GetString());
        Assert.Equal("Dispatch", StepOf(after, second).GetProperty("translations").GetProperty("en").GetString());
        Assert.Equal("Requester", after.GetProperty("lanes")[0].GetProperty("translations").GetProperty("en").GetString());
        Assert.Equal("Enter", StepOf(await project.MapAsync(), first).GetProperty("translations").GetProperty("en").GetString());

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        var back = await project.MapAsync();
        Assert.False(StepOf(back, first).GetProperty("translations").TryGetProperty("en", out _));
        Assert.Equal("Dispatch", StepOf(back, second).GetProperty("translations").GetProperty("en").GetString());
        Assert.False(back.GetProperty("lanes")[0].GetProperty("translations").TryGetProperty("en", out _));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_in_a_language_that_is_not_configured_is_refused_by_the_format_and_writes_nothing(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, lane, first, second) = await ProjectAsync(host);
        using var facilitator = project.Facilitator;
        var laneName = (await project.MapAsync()).GetProperty("lanes")[0].GetProperty("name").GetString()!;
        var read = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap = Translation(project.Code, lane, laneName, first, second, "en") })).Ok();
        var pmap = Translation(project.Code, lane, laneName, first, second, "eo");
        // The reader of the file refuses it first, each translation named by its path (the core refuses it too, ADR 081).
        var errors = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap })).Refused(400, "FICHIER_INVALIDE")
            .GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("path").GetString()).ToList();
        Assert.Contains("map.steps[0].translations.eo", errors);
        Assert.Contains("map.lanes[0].translations.eo", errors);
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap }, read.GetProperty("structure_version").GetInt32()))
            .Refused(400, "FICHIER_INVALIDE");
        Assert.False(StepOf(await project.MapAsync(), first).GetProperty("translations").TryGetProperty("de", out _));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_member_who_is_not_facilitator_and_a_file_refused_by_the_format_change_nothing(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, lane, first, _) = await ProjectAsync(host);
        using var reader = project.Reader;
        using var facilitator = project.Facilitator;
        var pmap = Proposal(project.Code, lane, first);
        (await reader.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap })).Refused(403, "ACCES_REFUSE");
        (await reader.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap }, 1)).Refused(403, "ACCES_REFUSE");
        var refused = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap = """{"format":"topogenia","version":"1.1"}""" }))
            .Refused(400, "FICHIER_INVALIDE");
        Assert.True(refused.GetProperty("errors").GetArrayLength() > 0);
        Assert.Equal(["Envoyer", "Saisir"], Labels(await project.MapAsync()));
    }
}
