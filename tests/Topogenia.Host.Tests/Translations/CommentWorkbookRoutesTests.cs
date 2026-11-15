// CommentWorkbookRoutesTests.cs
// The comments of presentation in the workbook of the translations through the whole host on both databases (ADR 099 revised): listed with the texts of the project, imported with them in one request, the whole file refused on one invalid translation.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Translations;

public sealed class CommentWorkbookRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<(ApiProject Project, Guid First, Guid Second)> ProjectAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var first = (await project.AddStepAsync(0, "Demander")).Id;
        var second = (await project.AddStepAsync(1, "Valider")).Id;
        (await facilitator.PutAsync($"/api/steps/{first}/presentation-comment", new { text = "Le salarié saisit sa demande.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/steps/{second}/presentation-comment", new { text = "Le responsable valide.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/steps/{second}/presentation-comment", new { text = "The manager approves.", language = "en" }, 0)).Ok();
        return (project, first, second);
    }

    private static async Task<List<JsonElement>> LinesAsync(ApiClient client, ApiProject project, Guid step) =>
        [.. (await client.GetAsync($"/api/maps/{project.RootMapId}/presentation-comments")).Ok().EnumerateArray()
            .Where(line => line.GetProperty("step_id").GetGuid() == step).OrderBy(line => line.GetProperty("language").GetString(), StringComparer.Ordinal)];

    private static object Row(int line, Guid step, string original, object translations) =>
        new { line, type = "presentation_comment", id = step, original, translations };

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_workbook_lists_the_comments_after_the_texts_by_scope_with_their_translations(EngineKind kind)
    {
        var (project, first, second) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = $"/api/projects/{project.Id}/translations";

        var all = (await facilitator.GetAsync(url + "?scope=all&languages=en")).Ok();
        Assert.Equal(["fr", "en"], all.GetProperty("languages").EnumerateArray().Select(language => language.GetString()));
        Assert.DoesNotContain(all.GetProperty("texts").EnumerateArray(), text => text.GetProperty("type").GetString() == "presentation_comment");
        var comments = all.GetProperty("comments").EnumerateArray().ToList();
        Assert.Equal([first, second], comments.Select(comment => comment.GetProperty("step_id").GetGuid()));
        Assert.Equal(("Le responsable valide.", "fr", "The manager approves.", "manual"),
            (comments[1].GetProperty("original").GetString(), comments[1].GetProperty("language").GetString(),
                comments[1].GetProperty("translations").GetProperty("en").GetString(), comments[1].GetProperty("sources").GetProperty("en").GetString()));
        Assert.EndsWith("E001", comments[0].GetProperty("code").GetString(), StringComparison.Ordinal);
        Assert.Contains(" / ", comments[0].GetProperty("context").GetString(), StringComparison.Ordinal);

        var missing = (await facilitator.GetAsync(url + "?scope=missing&languages=en")).Ok();
        Assert.Equal([first], missing.GetProperty("comments").EnumerateArray().Select(comment => comment.GetProperty("step_id").GetGuid()));
        using var contributor = project.Contributor;
        (await contributor.GetAsync(url)).Refused(403, "ACCES_REFUSE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_import_writes_the_comments_with_the_texts_and_reports_the_rows_it_ignores(EngineKind kind)
    {
        var (project, first, second) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = (await project.MapAsync()).GetProperty("steps").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == first);

        var outcome = (await facilitator.PutAsync($"/api/projects/{project.Id}/translations", new
        {
            scope = "auto",
            rows = new object[]
            {
                new { line = 2, type = "step", id = first, original = step.GetProperty("label").GetString(), translations = new { en = "Request" } },
                Row(3, first, "Le salarié saisit sa demande.", new { en = "The employee enters the request.", de = "Der Mitarbeiter erfasst seinen Antrag." }),
                Row(4, second, "Le responsable valide.", new { en = "From the workbook." }),
                Row(5, second, "Un ancien original.", new { de = "Alt." }),
                Row(6, Guid.CreateVersion7(), "Inconnu.", new { en = "Unknown." }),
            },
        })).Ok();

        Assert.Equal(3, outcome.GetProperty("written").GetInt32());
        Assert.Equal([(4, "TRADUCTION_MANUELLE"), (5, "TEXTE_MODIFIE"), (6, "OBJET_INTROUVABLE")],
            outcome.GetProperty("ignored").EnumerateArray().Select(row => (row.GetProperty("line").GetInt32(), row.GetProperty("reason").GetString()!)));
        Assert.Equal([("de", "Der Mitarbeiter erfasst seinen Antrag.", "workbook", 1), ("en", "The employee enters the request.", "workbook", 1),
            ("fr", "Le salarié saisit sa demande.", "manual", 1)],
            (await LinesAsync(facilitator, project, first)).Select(line => (line.GetProperty("language").GetString()!, line.GetProperty("text").GetString()!,
                line.GetProperty("source").GetString()!, line.GetProperty("version").GetInt32())));
        Assert.Equal("The manager approves.", (await LinesAsync(facilitator, project, second))[0].GetProperty("text").GetString());
        Assert.Equal("Request", (await project.MapAsync()).GetProperty("steps").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == first)
            .GetProperty("translations").GetProperty("en").GetString());

        // The scope « all » replaces the translation typed by hand.
        (await facilitator.PutAsync($"/api/projects/{project.Id}/translations", new { scope = "all", rows = new[] { Row(2, second, "Le responsable valide.", new { en = "From the workbook." }) } })).Ok();
        var replaced = (await LinesAsync(facilitator, project, second))[0];
        Assert.Equal(("From the workbook.", "workbook", 2), (replaced.GetProperty("text").GetString(), replaced.GetProperty("source").GetString(), replaced.GetProperty("version").GetInt32()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task One_invalid_translation_of_a_comment_refuses_the_whole_file_and_writes_none_of_its_texts(EngineKind kind)
    {
        var (project, first, _) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var step = (await project.MapAsync()).GetProperty("steps").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == first);

        var refusal = (await facilitator.PutAsync($"/api/projects/{project.Id}/translations", new
        {
            scope = "missing",
            rows = new object[]
            {
                new { line = 2, type = "step", id = first, original = step.GetProperty("label").GetString(), translations = new { en = "Request" } },
                Row(3, first, "Le salarié saisit sa demande.", new { en = "<b>Bold</b>" }),
            },
        })).Refused(400, "TRADUCTIONS_INVALIDES");

        var error = Assert.Single(refusal.GetProperty("errors").EnumerateArray());
        Assert.Equal((3, "en", "TEXTE_HTML_INTERDIT"), (error.GetProperty("line").GetInt32(), error.GetProperty("language").GetString(), error.GetProperty("code").GetString()));
        Assert.Single(await LinesAsync(facilitator, project, first));
        Assert.False((await project.MapAsync()).GetProperty("steps").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == first)
            .GetProperty("translations").TryGetProperty("en", out _));
    }
}
