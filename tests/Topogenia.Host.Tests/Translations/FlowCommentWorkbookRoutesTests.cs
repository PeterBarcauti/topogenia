// FlowCommentWorkbookRoutesTests.cs
// The comments of presentation of the flows in the workbook of the translations through the whole host on both databases (ADR 099, lot J10): listed with the texts of the project and the comments of the steps, each flow named as a labelled flow, imported with them in one request, the whole file refused on one invalid translation.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Translations;

public sealed class FlowCommentWorkbookRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private sealed record Commented(ApiProject Project, CreatedStep[] Steps, Guid First, Guid Second);

    /// <summary>
    /// Three steps in a row and the two flows between them, the second one labelled. The first flow has a comment in
    /// French; the second a comment in French and its English typed by hand; the first step a comment in French.
    /// </summary>
    private async Task<Commented> ProjectAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        CreatedStep[] steps = [await project.AddStepAsync(0, "Demander"), await project.AddStepAsync(1, "Valider"), await project.AddStepAsync(2, "Payer")];
        var first = await project.AddFlowAsync(steps[0].Id, steps[1].Id);
        var second = await project.AddFlowAsync(steps[1].Id, steps[2].Id, "oui");
        (await facilitator.PutAsync($"/api/flows/{first}/presentation-comment", new { text = "La demande part chez le responsable.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/flows/{second}/presentation-comment", new { text = "Le dossier validé part au paiement.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/flows/{second}/presentation-comment", new { text = "The approved file goes to payment.", language = "en" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/steps/{steps[0].Id}/presentation-comment", new { text = "Le salarié saisit sa demande.", language = "fr" }, 0)).Ok();
        return new Commented(project, steps, first, second);
    }

    private static async Task<List<JsonElement>> LinesAsync(ApiClient client, ApiProject project, Guid flow) =>
        [.. (await client.GetAsync($"/api/maps/{project.RootMapId}/flow-presentation-comments")).Ok().EnumerateArray()
            .Where(line => line.GetProperty("flow_id").GetGuid() == flow).OrderBy(line => line.GetProperty("language").GetString(), StringComparer.Ordinal)];

    private static object Row(int line, Guid flow, string original, object translations) =>
        new { line, type = "flow_presentation_comment", id = flow, original, translations };

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_workbook_lists_the_comments_of_the_flows_beside_those_of_the_steps_by_scope_named_as_a_labelled_flow(EngineKind kind)
    {
        var (project, steps, first, second) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = $"/api/projects/{project.Id}/translations";

        var all = (await facilitator.GetAsync(url + "?scope=all&languages=en")).Ok();
        Assert.Equal(["fr", "en"], all.GetProperty("languages").EnumerateArray().Select(language => language.GetString()));
        Assert.DoesNotContain(all.GetProperty("texts").EnumerateArray(), text => text.GetProperty("type").GetString() == "flow_presentation_comment");
        Assert.Equal([steps[0].Id], all.GetProperty("comments").EnumerateArray().Select(comment => comment.GetProperty("step_id").GetGuid()));
        var comments = all.GetProperty("flow_comments").EnumerateArray().ToList();
        Assert.Equal([first, second], comments.Select(comment => comment.GetProperty("flow_id").GetGuid()));
        Assert.Equal(("Le dossier validé part au paiement.", "fr", "The approved file goes to payment.", "manual"),
            (comments[1].GetProperty("original").GetString(), comments[1].GetProperty("language").GetString(),
                comments[1].GetProperty("translations").GetProperty("en").GetString(), comments[1].GetProperty("sources").GetProperty("en").GetString()));
        // The comment of a flow is named as the label of that flow is: the codes of its two steps, and their labels for its context.
        var label = all.GetProperty("texts").EnumerateArray().Single(text => text.GetProperty("type").GetString() == "flow");
        Assert.Equal(($"{steps[1].Code} > {steps[2].Code}", "Valider > Payer"), (label.GetProperty("code").GetString(), label.GetProperty("context").GetString()));
        Assert.Equal((label.GetProperty("code").GetString(), label.GetProperty("context").GetString()),
            (comments[1].GetProperty("code").GetString(), comments[1].GetProperty("context").GetString()));
        Assert.Equal($"{steps[0].Code} > {steps[1].Code}", comments[0].GetProperty("code").GetString());

        var missing = (await facilitator.GetAsync(url + "?scope=missing&languages=en")).Ok();
        Assert.Equal([first], missing.GetProperty("flow_comments").EnumerateArray().Select(comment => comment.GetProperty("flow_id").GetGuid()));
        using var contributor = project.Contributor;
        (await contributor.GetAsync(url)).Refused(403, "ACCES_REFUSE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_import_writes_the_comments_of_the_flows_with_the_texts_and_reports_the_rows_it_ignores(EngineKind kind)
    {
        var (project, steps, first, second) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;

        var outcome = (await facilitator.PutAsync($"/api/projects/{project.Id}/translations", new
        {
            scope = "auto",
            rows = new object[]
            {
                new { line = 2, type = "step", id = steps[0].Id, original = "Demander", translations = new { en = "Request" } },
                new { line = 3, type = "presentation_comment", id = steps[0].Id, original = "Le salarié saisit sa demande.", translations = new { en = "The employee enters the request." } },
                Row(4, first, "La demande part chez le responsable.", new { en = "The request goes to the manager.", de = "Der Antrag geht an die Leitung." }),
                Row(5, second, "Le dossier validé part au paiement.", new { en = "From the workbook." }),
                Row(6, second, "Un ancien original.", new { de = "Alt." }),
                Row(7, steps[0].Id, "Le salarié saisit sa demande.", new { en = "A step is no flow." }),
                Row(8, Guid.CreateVersion7(), "Inconnu.", new { en = "Unknown." }),
            },
        })).Ok();

        Assert.Equal(4, outcome.GetProperty("written").GetInt32());
        Assert.Equal([(5, "TRADUCTION_MANUELLE"), (6, "TEXTE_MODIFIE"), (7, "OBJET_INTROUVABLE"), (8, "OBJET_INTROUVABLE")],
            outcome.GetProperty("ignored").EnumerateArray().Select(row => (row.GetProperty("line").GetInt32(), row.GetProperty("reason").GetString()!)));
        Assert.Equal([("de", "Der Antrag geht an die Leitung.", "workbook", 1), ("en", "The request goes to the manager.", "workbook", 1),
            ("fr", "La demande part chez le responsable.", "manual", 1)],
            (await LinesAsync(facilitator, project, first)).Select(line => (line.GetProperty("language").GetString()!, line.GetProperty("text").GetString()!,
                line.GetProperty("source").GetString()!, line.GetProperty("version").GetInt32())));
        Assert.Equal("The approved file goes to payment.", (await LinesAsync(facilitator, project, second))[0].GetProperty("text").GetString());
        // The comment of the step and the label of the step were written by the same import.
        Assert.Contains((await facilitator.GetAsync($"/api/maps/{project.RootMapId}/presentation-comments")).Ok().EnumerateArray(),
            line => line.GetProperty("language").GetString() == "en" && line.GetProperty("source").GetString() == "workbook");
        Assert.Equal("Request", StructureCalls.StepOf(await project.MapAsync(), steps[0].Id).GetProperty("translations").GetProperty("en").GetString());

        // The scope « all » replaces the translation typed by hand.
        (await facilitator.PutAsync($"/api/projects/{project.Id}/translations",
            new { scope = "all", rows = new[] { Row(2, second, "Le dossier validé part au paiement.", new { en = "From the workbook." }) } })).Ok();
        var replaced = (await LinesAsync(facilitator, project, second))[0];
        Assert.Equal(("From the workbook.", "workbook", 2), (replaced.GetProperty("text").GetString(), replaced.GetProperty("source").GetString(), replaced.GetProperty("version").GetInt32()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task One_invalid_translation_of_the_comment_of_a_flow_refuses_the_whole_file_and_writes_none_of_its_texts(EngineKind kind)
    {
        var (project, steps, first, _) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;

        var refusal = (await facilitator.PutAsync($"/api/projects/{project.Id}/translations", new
        {
            scope = "missing",
            rows = new object[]
            {
                new { line = 2, type = "step", id = steps[0].Id, original = "Demander", translations = new { en = "Request" } },
                new { line = 3, type = "presentation_comment", id = steps[0].Id, original = "Le salarié saisit sa demande.", translations = new { en = "The employee enters the request." } },
                Row(4, first, "La demande part chez le responsable.", new { en = "<b>Bold</b>" }),
            },
        })).Refused(400, "TRADUCTIONS_INVALIDES");

        var error = Assert.Single(refusal.GetProperty("errors").EnumerateArray());
        Assert.Equal((4, "en", "TEXTE_HTML_INTERDIT"), (error.GetProperty("line").GetInt32(), error.GetProperty("language").GetString(), error.GetProperty("code").GetString()));
        Assert.Single(await LinesAsync(facilitator, project, first));
        Assert.Single((await facilitator.GetAsync($"/api/maps/{project.RootMapId}/presentation-comments")).Ok().EnumerateArray());
        Assert.False(StructureCalls.StepOf(await project.MapAsync(), steps[0].Id).GetProperty("translations").TryGetProperty("en", out _));
    }
}
