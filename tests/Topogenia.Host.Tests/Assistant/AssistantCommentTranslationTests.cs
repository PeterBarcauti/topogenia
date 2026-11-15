// AssistantCommentTranslationTests.cs
// The comments of presentation translated by the assistant through the whole host on both databases (ADR 099 revised): asked with the translation of the process, read from the reply, shown by the preview with the other texts, written by « Appliquer » in the same request, never over a translation typed by hand.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantCommentTranslationTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private sealed record Commented(ApiProject Project, string Lane, string LaneName, Guid[] Steps, string[] Codes);

    /// <summary>
    /// Three steps in the first lane. The first has a comment in French; the second a comment in French and its
    /// Portuguese typed by hand; the third none.
    /// </summary>
    private static async Task<Commented> ProjectAsync(ApiHost host)
    {
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var steps = new Guid[3];
        foreach (var (label, column) in new[] { ("Saisir", 0), ("Valider", 1), ("Envoyer", 2) })
            steps[column] = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column, label })).Ok().GetProperty("id").GetGuid();
        await SayAsync(facilitator, steps[0], "Le salarié saisit sa demande.\nIl joint le justificatif.", "fr");
        await SayAsync(facilitator, steps[1], "Le responsable valide.", "fr");
        await SayAsync(facilitator, steps[1], "Feito à mão.", "pt");
        var map = await project.MapAsync();
        var lane = map.GetProperty("lanes")[0];
        var codes = steps.Select(id => map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("id").GetGuid() == id).GetProperty("code").GetString()!).ToArray();
        return new Commented(project, lane.GetProperty("code").GetString()!, lane.GetProperty("name").GetString()!, steps, codes);
    }

    private static async Task SayAsync(ApiClient client, Guid step, string text, string language, int version = 0) =>
        (await client.PutAsync($"/api/steps/{step}/presentation-comment", new { text, language }, version)).Ok();

    /// <summary>The `.pmap` a model answers to « translate into Portuguese »: the map as it is, its texts translated.</summary>
    private static string Pmap(Commented commented) => JsonSerializer.Serialize(new
    {
        format = "topogenia",
        version = "1.1",
        project = new { name = "Projet", code = commented.Project.Code, translations = new { pt = "Projeto" } },
        map = new
        {
            lanes = new object[] { new { id = commented.Lane, name = commented.LaneName, translations = new { pt = "Ator 1" } } },
            steps = new object[]
            {
                new { id = commented.Codes[0], lane = commented.Lane, label = "Saisir", column = 0, translations = new { pt = "Introduzir" } },
                new { id = commented.Codes[1], lane = commented.Lane, label = "Valider", column = 1, translations = new { pt = "Validar" } },
                new { id = commented.Codes[2], lane = commented.Lane, label = "Envoyer", column = 2, translations = new { pt = "Enviar" } },
            },
            flows = Array.Empty<object>(),
        },
    });

    /// <summary>The lines of the comments of the root map: the code of the step, the language, the text, the source and the version.</summary>
    private static async Task<List<(string Code, string Language, string Text, string Source, int Version)>> LinesAsync(Commented commented, ApiClient client)
    {
        var codes = commented.Steps.Zip(commented.Codes).ToDictionary(pair => pair.First, pair => pair.Second);
        return [.. (await client.GetAsync($"/api/maps/{commented.Project.RootMapId}/presentation-comments")).Ok().EnumerateArray()
            .Select(line => (codes[line.GetProperty("step_id").GetGuid()], line.GetProperty("language").GetString()!, line.GetProperty("text").GetString()!,
                line.GetProperty("source").GetString()!, line.GetProperty("version").GetInt32()))
            .Order()];
    }

    private static async Task CopyPasteAsync(ApiHost host)
    {
        using var admin = host.As(Logins.Admin);
        var version = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
        (await admin.PutAsync("/api/admin/assistant", new { connector = "copy_paste", allowed_classifications = new[] { "internal" } }, version)).Ok();
    }

    private static object Ask(string language) =>
        new { intent = "translate", request = "Traduire le processus", selection = Array.Empty<string>(), history = Array.Empty<object>(), language };

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_asks_the_model_for_the_comments_that_have_no_line_in_its_language_and_reads_them_from_its_reply(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var commented = await ProjectAsync(host);
        await CopyPasteAsync(host);
        using var facilitator = commented.Project.Facilitator;
        var root = commented.Project.RootMapId;

        var prompt = (await facilitator.PostAsync($"/api/maps/{root}/assistant/prompt", Ask("pt"))).Ok().GetProperty("prompt").GetString()!;
        var asked = prompt[prompt.IndexOf("<comments>\n", StringComparison.Ordinal)..prompt.IndexOf("\n</comments>", StringComparison.Ordinal)]["<comments>\n".Length..];
        var list = JsonDocument.Parse(asked).RootElement.EnumerateArray().ToList();
        // The first step only: the second has its Portuguese typed by hand, the third has no comment.
        var only = Assert.Single(list);
        Assert.Equal((commented.Codes[0], "Le salarié saisit sa demande.\nIl joint le justificatif."), (only.GetProperty("step").GetString(), only.GetProperty("text").GetString()));
        Assert.Contains("comments of presentation", prompt, StringComparison.Ordinal);
        Assert.Contains("\"comments\"", prompt[prompt.IndexOf("# The schema of your reply", StringComparison.Ordinal)..], StringComparison.Ordinal);
        // Into English, the two originals are asked; nothing is asked of a practice that does not translate.
        var english = (await facilitator.PostAsync($"/api/maps/{root}/assistant/prompt", Ask("en"))).Ok().GetProperty("prompt").GetString()!;
        Assert.Contains(commented.Codes[1] + "\",\"text\":\"Le responsable valide.", english, StringComparison.Ordinal);
        var question = (await facilitator.PostAsync($"/api/maps/{root}/assistant/prompt",
            new { intent = "ask", request = "Que fait cette carte ?", selection = Array.Empty<string>(), history = Array.Empty<object>() })).Ok().GetProperty("prompt").GetString()!;
        // The instructions name the list; the list itself is given to a translation only.
        Assert.DoesNotContain("<comments>\n", question, StringComparison.Ordinal);

        var reply = JsonSerializer.Serialize(new
        {
            form = "proposal", message = "Traduit.", cited = Array.Empty<string>(), questions = Array.Empty<object>(), findings = Array.Empty<object>(),
            pmap = Pmap(commented), report = (object?)null, summaries = (object?)null,
            comments = new object[]
            {
                new { step = commented.Codes[0], text = "O colaborador introduz o pedido.\nJunta o comprovativo." },
                new { step = commented.Codes[1], text = "Não pedido: já escrito à mão." },
                new { step = "XYZ-E999", text = "Etapa desconhecida." },
            },
        });
        var read = (await facilitator.PostAsync($"/api/maps/{root}/assistant/replies",
            new { intent = "translate", request = "Traduire le processus", language = "pt", reply })).Ok().GetProperty("reply");
        var comment = Assert.Single(read.GetProperty("comments").EnumerateArray());
        Assert.Equal((commented.Steps[0], commented.Codes[0], "Le salarié saisit sa demande.\nIl joint le justificatif.", "O colaborador introduz o pedido.\nJunta o comprovativo."),
            (comment.GetProperty("step_id").GetGuid(), comment.GetProperty("code").GetString(), comment.GetProperty("original").GetString(),
                comment.GetProperty("text").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_preview_shows_the_comments_with_the_other_texts_and_the_application_writes_them_with_the_map_never_over_one_typed(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var commented = await ProjectAsync(host);
        using var facilitator = commented.Project.Facilitator;
        var root = commented.Project.RootMapId;
        var body = new
        {
            pmap = Pmap(commented), language = "pt",
            comments = new object[]
            {
                new { step_id = commented.Steps[0], original = "Le salarié saisit sa demande.\nIl joint le justificatif.", text = "O colaborador introduz o pedido.\nJunta o comprovativo." },
                new { step_id = commented.Steps[1], original = "Le responsable valide.", text = "O responsável valida." },
                new { step_id = commented.Steps[2], original = "Sans commentaire.", text = "Sem comentário." },
            },
        };

        var preview = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", body)).Ok();
        var texts = preview.GetProperty("translations").EnumerateArray().ToList();
        Assert.Equal(["project", "lane", "step", "step", "step", "presentation_comment", "presentation_comment"], texts.Select(text => text.GetProperty("kind").GetString()));
        var comments = texts.Where(text => text.GetProperty("kind").GetString() == "presentation_comment").ToList();
        Assert.Equal([(commented.Steps[0], commented.Codes[0], "O colaborador introduz o pedido.\nJunta o comprovativo.", false),
            (commented.Steps[1], commented.Codes[1], "Feito à mão.", true)],
            comments.Select(text => (text.GetProperty("id").GetGuid(), text.GetProperty("code").GetString()!, text.GetProperty("text").GetString()!,
                text.GetProperty("kept").GetBoolean())));
        Assert.Equal("Le responsable valide.", comments[1].GetProperty("original").GetString());
        // Nothing is written by a preview.
        Assert.DoesNotContain(await LinesAsync(commented, facilitator), line => line.Source == "assistant");

        (await facilitator.PostAsync($"/api/maps/{root}/assistant/apply", body, preview.GetProperty("structure_version").GetInt32())).Ok();

        Assert.Equal([(commented.Codes[0], "fr", "Le salarié saisit sa demande.\nIl joint le justificatif.", "manual", 1),
            (commented.Codes[0], "pt", "O colaborador introduz o pedido.\nJunta o comprovativo.", "assistant", 1),
            (commented.Codes[1], "fr", "Le responsable valide.", "manual", 1), (commented.Codes[1], "pt", "Feito à mão.", "manual", 1)],
            await LinesAsync(commented, facilitator));
        Assert.All((await commented.Project.MapAsync()).GetProperty("steps").EnumerateArray(), step => Assert.True(step.GetProperty("translations").TryGetProperty("pt", out _)));

        // Ctrl+Z takes the translations of the map back; the comments are outside the history of the map, as the comment itself.
        (await facilitator.PostAsync($"/api/maps/{root}/undo")).Ok();
        Assert.All((await commented.Project.MapAsync()).GetProperty("steps").EnumerateArray(), step => Assert.False(step.GetProperty("translations").TryGetProperty("pt", out _)));
        Assert.Contains(await LinesAsync(commented, facilitator), line => line is { Language: "pt", Source: "assistant" });

        // The original changed: the translation of the assistant falls, the one typed by hand stays.
        await SayAsync(facilitator, commented.Steps[0], "Le salarié saisit sa demande dans l'outil.", "fr", 1);
        await SayAsync(facilitator, commented.Steps[1], "Le responsable valide ou refuse.", "fr", 1);
        Assert.Equal([(commented.Codes[0], "fr"), (commented.Codes[1], "fr"), (commented.Codes[1], "pt")],
            (await LinesAsync(commented, facilitator)).Select(line => (line.Code, line.Language)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_whose_original_changed_since_is_left_aside_and_one_that_only_brings_comments_is_not_empty(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var commented = await ProjectAsync(host);
        using var facilitator = commented.Project.Facilitator;
        var root = commented.Project.RootMapId;
        object Body(string original) => new
        {
            pmap = Pmap(commented), language = "pt",
            comments = new object[] { new { step_id = commented.Steps[0], original, text = "O colaborador introduz o pedido." } },
        };
        // The translations of the map are written first: what is left to write is the comment alone.
        var first = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", new { pmap = Pmap(commented), language = "pt" })).Ok();
        (await facilitator.PostAsync($"/api/maps/{root}/assistant/apply", new { pmap = Pmap(commented), language = "pt" }, first.GetProperty("structure_version").GetInt32())).Ok();
        Assert.True((await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", new { pmap = Pmap(commented), language = "pt" })).Ok().GetProperty("empty").GetBoolean());

        var stale = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", Body("Un autre original."))).Ok();
        Assert.DoesNotContain(stale.GetProperty("translations").EnumerateArray(), text => text.GetProperty("kind").GetString() == "presentation_comment");
        Assert.True(stale.GetProperty("empty").GetBoolean());

        var preview = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", Body("Le salarié saisit sa demande.\nIl joint le justificatif."))).Ok();
        Assert.False(preview.GetProperty("empty").GetBoolean());
        // A comment written since the preview moved the project on: the stale preview is refused, and nothing is written.
        await SayAsync(facilitator, commented.Steps[2], "Ajouté entre-temps.", "fr");
        (await facilitator.PostAsync($"/api/maps/{root}/assistant/apply", Body("Le salarié saisit sa demande.\nIl joint le justificatif."),
            preview.GetProperty("structure_version").GetInt32())).Refused(409, "CONFLIT_VERSION");
        Assert.DoesNotContain(await LinesAsync(commented, facilitator), line => line.Source == "assistant");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_comments_of_a_proposal_are_refused_without_the_language_of_a_translation_twice_on_a_step_or_invalid(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var commented = await ProjectAsync(host);
        using var facilitator = commented.Project.Facilitator;
        var root = commented.Project.RootMapId;
        var pmap = Pmap(commented);
        object Entry(string text = "O colaborador introduz o pedido.") =>
            new { step_id = commented.Steps[0], original = "Le salarié saisit sa demande.\nIl joint le justificatif.", text };
        // The application is asked with the version of a preview, as the panel does: a refusal writes nothing and leaves it as it is.
        var version = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", new { pmap, language = "pt" })).Ok().GetProperty("structure_version").GetInt32();
        foreach (var route in new[] { "preview", "apply" })
        {
            var url = $"/api/maps/{root}/assistant/{route}";
            (await facilitator.PostAsync(url, new { pmap, comments = new[] { Entry() } }, version)).Refused(400, "VALEUR_INVALIDE");
            (await facilitator.PostAsync(url, new { pmap, language = "pt", comments = new[] { Entry(), Entry() } }, version)).Refused(400, "VALEUR_INVALIDE");
            (await facilitator.PostAsync(url, new { pmap, language = "pt", comments = new[] { Entry("<b>Negrito</b>") } }, version)).Refused(400, "TEXTE_HTML_INTERDIT");
            (await facilitator.PostAsync(url, new { pmap, language = "pt", comments = new[] { new { original = "x", text = "y" } } }, version)).Refused(400, "CHAMP_OBLIGATOIRE");
        }
        using var contributor = commented.Project.Contributor;
        (await contributor.PostAsync($"/api/maps/{root}/assistant/apply", new { pmap, language = "pt", comments = new[] { Entry() } }, version)).Refused(403, "ACCES_REFUSE");
        // Nothing of the refused applications was written: the same version still applies the translation.
        (await facilitator.PostAsync($"/api/maps/{root}/assistant/apply", new { pmap, language = "pt", comments = new[] { Entry() } }, version)).Ok();
        Assert.Single(await LinesAsync(commented, facilitator), line => line.Source == "assistant");
    }
}
