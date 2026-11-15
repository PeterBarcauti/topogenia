// AssistantFlowCommentTranslationTests.cs
// The comments of presentation of the flows translated by the assistant through the whole host on both databases (ADR 099, lot J10): asked with those of the steps, each flow named by the codes of its two steps, read from the reply, shown by the preview with the other texts, written by « Appliquer » in the same request, never over a translation typed by hand.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantFlowCommentTranslationTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string StepOriginal = "Le salarié saisit sa demande.";
    private const string FirstOriginal = "La demande part chez le responsable.\nIl la reçoit le lendemain.";
    private const string SecondOriginal = "Le dossier validé part au service paie.";

    private sealed record Commented(ApiProject Project, string Lane, string LaneName, Guid[] Steps, string[] Codes, Guid[] Flows)
    {
        /// <summary>The readable code of a flow of the chain: the codes of its two steps.</summary>
        public string FlowCode(int index) => $"{Codes[index]} > {Codes[index + 1]}";
    }

    /// <summary>
    /// Three steps in a row and the two flows between them. The first step has a comment in French; the first flow a
    /// comment in French; the second flow a comment in French and its Portuguese typed by hand.
    /// </summary>
    private static async Task<Commented> ProjectAsync(ApiHost host)
    {
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var steps = new Guid[3];
        foreach (var (label, column) in new[] { ("Saisir", 0), ("Valider", 1), ("Envoyer", 2) })
            steps[column] = (await project.AddStepAsync(column, label)).Id;
        Guid[] flows = [await project.AddFlowAsync(steps[0], steps[1]), await project.AddFlowAsync(steps[1], steps[2])];
        (await facilitator.PutAsync($"/api/steps/{steps[0]}/presentation-comment", new { text = StepOriginal, language = "fr" }, 0)).Ok();
        await SayAsync(facilitator, flows[0], FirstOriginal, "fr");
        await SayAsync(facilitator, flows[1], SecondOriginal, "fr");
        await SayAsync(facilitator, flows[1], "Feito à mão.", "pt");
        var map = await project.MapAsync();
        var lane = map.GetProperty("lanes")[0];
        var codes = steps.Select(id => StructureCalls.StepOf(map, id).GetProperty("code").GetString()!).ToArray();
        return new Commented(project, lane.GetProperty("code").GetString()!, lane.GetProperty("name").GetString()!, steps, codes, flows);
    }

    private static async Task SayAsync(ApiClient client, Guid flow, string text, string language, int version = 0) =>
        (await client.PutAsync($"/api/flows/{flow}/presentation-comment", new { text, language }, version)).Ok();

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
            flows = new object[] { new { source = commented.Codes[0], target = commented.Codes[1] }, new { source = commented.Codes[1], target = commented.Codes[2] } },
        },
    });

    /// <summary>The lines of the comments of the flows of the root map: the index of the flow in the chain, the language, the text, the source and the version.</summary>
    private static async Task<List<(int Flow, string Language, string Text, string Source, int Version)>> LinesAsync(Commented commented, ApiClient client) =>
        [.. (await client.GetAsync($"/api/maps/{commented.Project.RootMapId}/flow-presentation-comments")).Ok().EnumerateArray()
            .Select(line => (Array.IndexOf(commented.Flows, line.GetProperty("flow_id").GetGuid()), line.GetProperty("language").GetString()!, line.GetProperty("text").GetString()!,
                line.GetProperty("source").GetString()!, line.GetProperty("version").GetInt32()))
            .Order()];

    private static async Task CopyPasteAsync(ApiHost host)
    {
        using var admin = host.As(Logins.Admin);
        var version = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
        (await admin.PutAsync("/api/admin/assistant", new { connector = "copy_paste", allowed_classifications = new[] { "internal" } }, version)).Ok();
    }

    private static object Ask(string language) =>
        new { intent = "translate", request = "Traduire le processus", selection = Array.Empty<string>(), history = Array.Empty<object>(), language };

    /// <summary>The comments a prompt asks, as its list gives them: the reference and the text of each.</summary>
    private static List<(string? Reference, string? Text)> Asked(string prompt)
    {
        var asked = prompt[prompt.IndexOf("<comments>\n", StringComparison.Ordinal)..prompt.IndexOf("\n</comments>", StringComparison.Ordinal)]["<comments>\n".Length..];
        return [.. JsonDocument.Parse(asked).RootElement.EnumerateArray().Select(entry => (entry.GetProperty("step").GetString(), entry.GetProperty("text").GetString()))];
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_asks_the_model_for_the_comments_of_the_flows_after_those_of_the_steps_and_reads_them_from_its_reply(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var commented = await ProjectAsync(host);
        await CopyPasteAsync(host);
        using var facilitator = commented.Project.Facilitator;
        var root = commented.Project.RootMapId;

        var prompt = (await facilitator.PostAsync($"/api/maps/{root}/assistant/prompt", Ask("pt"))).Ok().GetProperty("prompt").GetString()!;
        // The step first, then the first flow, named by the codes of its two steps; the second flow has its Portuguese typed by hand.
        Assert.Equal([(commented.Codes[0], StepOriginal), (commented.FlowCode(0), FirstOriginal)], Asked(prompt));
        Assert.Contains("comments of presentation", prompt, StringComparison.Ordinal);
        Assert.Contains("SOURCE > TARGET", prompt, StringComparison.Ordinal);
        // Into English, the two flows are asked, in the order of the map.
        var english = (await facilitator.PostAsync($"/api/maps/{root}/assistant/prompt", Ask("en"))).Ok().GetProperty("prompt").GetString()!;
        Assert.Equal([(commented.Codes[0], StepOriginal), (commented.FlowCode(0), FirstOriginal), (commented.FlowCode(1), SecondOriginal)], Asked(english));

        var reply = JsonSerializer.Serialize(new
        {
            form = "proposal", message = "Traduit.", cited = Array.Empty<string>(), questions = Array.Empty<object>(), findings = Array.Empty<object>(),
            pmap = Pmap(commented), report = (object?)null, summaries = (object?)null,
            comments = new object[]
            {
                new { step = commented.FlowCode(0), text = "O pedido segue para o responsável.\nEle recebe-o no dia seguinte." },
                new { step = commented.Codes[0], text = "O colaborador introduz o pedido." },
                new { step = commented.FlowCode(1), text = "Não pedido: já escrito à mão." },
                new { step = $"{commented.Codes[1]} > {commented.Codes[0]}", text = "No outro sentido: seta desconhecida." },
            },
        });
        var read = (await facilitator.PostAsync($"/api/maps/{root}/assistant/replies",
            new { intent = "translate", request = "Traduire le processus", language = "pt", reply })).Ok().GetProperty("reply");
        var step = Assert.Single(read.GetProperty("comments").EnumerateArray());
        Assert.Equal((commented.Steps[0], commented.Codes[0], "O colaborador introduz o pedido."),
            (step.GetProperty("step_id").GetGuid(), step.GetProperty("code").GetString(), step.GetProperty("text").GetString()));
        var flow = Assert.Single(read.GetProperty("flow_comments").EnumerateArray());
        Assert.Equal((commented.Flows[0], commented.Codes[0], commented.Codes[1], FirstOriginal, "O pedido segue para o responsável.\nEle recebe-o no dia seguinte."),
            (flow.GetProperty("flow_id").GetGuid(), flow.GetProperty("source_code").GetString(), flow.GetProperty("target_code").GetString(),
                flow.GetProperty("original").GetString(), flow.GetProperty("text").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_comments_of_the_flows_alone_are_still_asked_with_the_field_of_the_reply_that_carries_them(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var commented = await ProjectAsync(host);
        await CopyPasteAsync(host);
        using var facilitator = commented.Project.Facilitator;
        var root = commented.Project.RootMapId;
        (await facilitator.PutAsync($"/api/steps/{commented.Steps[0]}/presentation-comment", new { text = "", language = "fr" }, 1)).Ok();

        var prompt = (await facilitator.PostAsync($"/api/maps/{root}/assistant/prompt", Ask("pt"))).Ok().GetProperty("prompt").GetString()!;

        Assert.Equal([(commented.FlowCode(0), FirstOriginal)], Asked(prompt));
        Assert.Contains("\"comments\"", prompt[prompt.IndexOf("# The schema of your reply", StringComparison.Ordinal)..], StringComparison.Ordinal);
        var reply = JsonSerializer.Serialize(new
        {
            form = "proposal", message = "Traduit.", cited = Array.Empty<string>(), pmap = Pmap(commented),
            comments = new object[] { new { step = commented.FlowCode(0), text = "O pedido segue para o responsável." } },
        });
        var read = (await facilitator.PostAsync($"/api/maps/{root}/assistant/replies",
            new { intent = "translate", request = "Traduire le processus", language = "pt", reply })).Ok().GetProperty("reply");
        Assert.Equal(commented.Flows[0], Assert.Single(read.GetProperty("flow_comments").EnumerateArray()).GetProperty("flow_id").GetGuid());
        Assert.True(!read.TryGetProperty("comments", out var steps) || steps.ValueKind == JsonValueKind.Null);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_preview_shows_the_comments_of_the_flows_after_those_of_the_steps_and_the_application_writes_them_with_the_map_never_over_one_typed(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var commented = await ProjectAsync(host);
        using var facilitator = commented.Project.Facilitator;
        var root = commented.Project.RootMapId;
        var body = new
        {
            pmap = Pmap(commented), language = "pt",
            comments = new object[] { new { step_id = commented.Steps[0], original = StepOriginal, text = "O colaborador introduz o pedido." } },
            flow_comments = new object[]
            {
                new { flow_id = commented.Flows[0], original = FirstOriginal, text = "O pedido segue para o responsável.\nEle recebe-o no dia seguinte." },
                new { flow_id = commented.Flows[1], original = SecondOriginal, text = "O processo validado segue para os salários." },
                new { flow_id = commented.Steps[2], original = "Une étape.", text = "Uma etapa, não uma seta." },
            },
        };

        var preview = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", body)).Ok();
        var texts = preview.GetProperty("translations").EnumerateArray().ToList();
        Assert.Equal(["project", "lane", "step", "step", "step", "presentation_comment", "flow_presentation_comment", "flow_presentation_comment"],
            texts.Select(text => text.GetProperty("kind").GetString()));
        Assert.Equal([(commented.Flows[0], commented.FlowCode(0), FirstOriginal, "O pedido segue para o responsável.\nEle recebe-o no dia seguinte.", false),
            (commented.Flows[1], commented.FlowCode(1), SecondOriginal, "Feito à mão.", true)],
            texts.Skip(6).Select(text => (text.GetProperty("id").GetGuid(), text.GetProperty("code").GetString()!, text.GetProperty("original").GetString()!,
                text.GetProperty("text").GetString()!, text.GetProperty("kept").GetBoolean())));
        // Nothing is written by a preview.
        Assert.DoesNotContain(await LinesAsync(commented, facilitator), line => line.Source == "assistant");

        (await facilitator.PostAsync($"/api/maps/{root}/assistant/apply", body, preview.GetProperty("structure_version").GetInt32())).Ok();

        Assert.Equal([(0, "fr", FirstOriginal, "manual", 1), (0, "pt", "O pedido segue para o responsável.\nEle recebe-o no dia seguinte.", "assistant", 1),
            (1, "fr", SecondOriginal, "manual", 1), (1, "pt", "Feito à mão.", "manual", 1)], await LinesAsync(commented, facilitator));
        // The comment of the step was written by the same request, in its own list.
        Assert.Contains((await facilitator.GetAsync($"/api/maps/{root}/presentation-comments")).Ok().EnumerateArray(),
            line => line.GetProperty("language").GetString() == "pt" && line.GetProperty("source").GetString() == "assistant");

        // Ctrl+Z takes the translations of the map back; the comments are outside the history of the map, as the comment itself.
        (await facilitator.PostAsync($"/api/maps/{root}/undo")).Ok();
        Assert.All((await commented.Project.MapAsync()).GetProperty("steps").EnumerateArray(), step => Assert.False(step.GetProperty("translations").TryGetProperty("pt", out _)));
        Assert.Contains(await LinesAsync(commented, facilitator), line => line is { Language: "pt", Source: "assistant" });

        // The original changed: the translation of the assistant falls, the one typed by hand stays.
        await SayAsync(facilitator, commented.Flows[0], "La demande part chez le responsable, par l'outil.", "fr", 1);
        await SayAsync(facilitator, commented.Flows[1], "Le dossier validé ou refusé part au service paie.", "fr", 1);
        Assert.Equal([(0, "fr"), (1, "fr"), (1, "pt")], (await LinesAsync(commented, facilitator)).Select(line => (line.Flow, line.Language)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_of_the_comment_of_a_flow_whose_original_changed_is_left_aside_and_one_that_only_brings_them_is_not_empty(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var commented = await ProjectAsync(host);
        using var facilitator = commented.Project.Facilitator;
        var root = commented.Project.RootMapId;
        object Body(string original) => new
        {
            pmap = Pmap(commented), language = "pt",
            flow_comments = new object[] { new { flow_id = commented.Flows[0], original, text = "O pedido segue para o responsável." } },
        };
        // The translations of the map are written first: what is left to write is the comment of the flow alone.
        var first = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", new { pmap = Pmap(commented), language = "pt" })).Ok();
        (await facilitator.PostAsync($"/api/maps/{root}/assistant/apply", new { pmap = Pmap(commented), language = "pt" }, first.GetProperty("structure_version").GetInt32())).Ok();
        Assert.True((await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", new { pmap = Pmap(commented), language = "pt" })).Ok().GetProperty("empty").GetBoolean());

        var stale = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", Body("Un autre original."))).Ok();
        Assert.DoesNotContain(stale.GetProperty("translations").EnumerateArray(), text => text.GetProperty("kind").GetString() == "flow_presentation_comment");
        Assert.True(stale.GetProperty("empty").GetBoolean());

        var preview = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", Body(FirstOriginal))).Ok();
        Assert.False(preview.GetProperty("empty").GetBoolean());
        (await facilitator.PostAsync($"/api/maps/{root}/assistant/apply", Body(FirstOriginal), preview.GetProperty("structure_version").GetInt32())).Ok();
        Assert.Single(await LinesAsync(commented, facilitator), line => line is { Flow: 0, Language: "pt", Source: "assistant" });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_comments_of_the_flows_of_a_proposal_are_refused_without_the_language_of_a_translation_twice_on_a_flow_or_invalid(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var commented = await ProjectAsync(host);
        using var facilitator = commented.Project.Facilitator;
        var root = commented.Project.RootMapId;
        var pmap = Pmap(commented);
        object Entry(string text = "O pedido segue para o responsável.") => new { flow_id = commented.Flows[0], original = FirstOriginal, text };
        // The application is asked with the version of a preview, as the panel does: a refusal writes nothing and leaves it as it is.
        var version = (await facilitator.PostAsync($"/api/maps/{root}/assistant/preview", new { pmap, language = "pt" })).Ok().GetProperty("structure_version").GetInt32();
        foreach (var route in new[] { "preview", "apply" })
        {
            var url = $"/api/maps/{root}/assistant/{route}";
            Assert.Equal("flow_comments", (await facilitator.PostAsync(url, new { pmap, flow_comments = new[] { Entry() } }, version)).Refused(400, "VALEUR_INVALIDE").Field());
            Assert.Equal("flow_comments",
                (await facilitator.PostAsync(url, new { pmap, language = "pt", flow_comments = new[] { Entry(), Entry() } }, version)).Refused(400, "VALEUR_INVALIDE").Field());
            (await facilitator.PostAsync(url, new { pmap, language = "pt", flow_comments = new[] { Entry("<b>Negrito</b>") } }, version)).Refused(400, "TEXTE_HTML_INTERDIT");
            Assert.Equal("flow_id",
                (await facilitator.PostAsync(url, new { pmap, language = "pt", flow_comments = new[] { new { original = "x", text = "y" } } }, version)).Refused(400, "CHAMP_OBLIGATOIRE").Field());
        }
        using var contributor = commented.Project.Contributor;
        (await contributor.PostAsync($"/api/maps/{root}/assistant/apply", new { pmap, language = "pt", flow_comments = new[] { Entry() } }, version)).Refused(403, "ACCES_REFUSE");
        // Nothing of the refused applications was written: the same version still applies the translation.
        (await facilitator.PostAsync($"/api/maps/{root}/assistant/apply", new { pmap, language = "pt", flow_comments = new[] { Entry() } }, version)).Ok();
        Assert.Single(await LinesAsync(commented, facilitator), line => line.Source == "assistant");
    }
}
