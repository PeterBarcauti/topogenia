// AssistantTranslationTests.cs
// A translation by the assistant through the whole host on both databases (ADR 083): only the translations into the language asked are previewed, with the texts they write, and written, the version of origin locked, one undo; the coverage of the languages in the map.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantTranslationTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>Two steps linked by a flow labelled « ok » in the first lane: the project, the code and name of the lane, the codes of the steps.</summary>
    private static async Task<(ApiProject Project, string Lane, string LaneName, string[] Codes)> ProjectAsync(ApiHost host)
    {
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/steps";
        var first = (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column = 0, label = "Saisir" })).Ok().GetProperty("id").GetGuid();
        var second = (await facilitator.PostAsync(url, new { lane_id = project.FirstLaneId, column = 1, label = "Envoyer" })).Ok().GetProperty("id").GetGuid();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = first, target_step_id = second, label = "ok" })).Ok();
        var map = await project.MapAsync();
        var lane = map.GetProperty("lanes")[0];
        string Code(Guid id) => map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("id").GetGuid() == id).GetProperty("code").GetString()!;
        return (project, lane.GetProperty("code").GetString()!, lane.GetProperty("name").GetString()!, [Code(first), Code(second)]);
    }

    /// <summary>
    /// What a model may answer to « translate into Portuguese »: the translations, and also the second step renamed and a
    /// step added, which the lock leaves out.
    /// </summary>
    private static string Answer(string projectCode, string lane, string laneName, string[] codes) => JsonSerializer.Serialize(new
    {
        format = "topogenia",
        version = "1.1",
        project = new { name = "Projet", code = projectCode, translations = new { pt = "Projeto" } },
        map = new
        {
            lanes = new object[] { new { id = lane, name = laneName, translations = new { pt = "Ator 1" } } },
            steps = new object[]
            {
                new { id = codes[0], lane, label = "Saisir", column = 0, translations = new { pt = "Introduzir" } },
                new { id = codes[1], lane, label = "Expédier", column = 1, translations = new { pt = "Enviar" } },
                new { id = "new", lane, label = "Payer", column = 2, translations = new { pt = "Pagar" } },
            },
            flows = new object[] { new { source = codes[0], target = codes[1], label = "ok", translations = new { pt = "certo" } } },
        },
    });

    private static string? Portuguese(JsonElement item) =>
        item.GetProperty("translations").TryGetProperty("pt", out var text) ? text.GetString() : null;

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_writes_only_the_translations_into_its_language_the_original_locked_and_one_undo_takes_them_back(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, lane, laneName, codes) = await ProjectAsync(host);
        using var facilitator = project.Facilitator;
        var pmap = Answer(project.Code, lane, laneName, codes);
        var before = await project.MapAsync();
        Assert.Equal(("fr", 0), (before.GetProperty("languages").GetProperty("origin").GetString(),
            before.GetProperty("languages").GetProperty("written").TryGetProperty("pt", out var none) ? none.GetInt32() : 0));

        var preview = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap, language = "pt" })).Ok();
        var changes = preview.GetProperty("changes").EnumerateArray().ToList();
        Assert.All(changes, change => Assert.True(change.GetProperty("action").GetString() == "kept"
            || change.GetProperty("fields").EnumerateArray().Select(field => field.GetString()).SequenceEqual(["translations"]), change.ToString()));
        Assert.Contains(changes, change => change.GetProperty("kind").GetString() == "project");
        // The texts the translation writes, which the map shows in place before anything is written: the step renamed
        // and the step added are not among them.
        var texts = preview.GetProperty("translations").EnumerateArray().ToList();
        Assert.Equal(["project", "lane", "step", "step", "flow"], texts.Select(text => text.GetProperty("kind").GetString()));
        Assert.Equal(["Projeto", "Ator 1", "Introduzir", "Enviar", "certo"], texts.Select(text => text.GetProperty("text").GetString()));
        Assert.Equal([laneName, "Saisir", "Envoyer", "ok"], texts.Skip(1).Select(text => text.GetProperty("original").GetString()));
        Assert.Equal([project.Code, lane, codes[0], codes[1], $"{codes[0]} > {codes[1]}"], texts.Select(text => text.GetProperty("code").GetString()));
        Assert.All(texts, text => Assert.False(text.GetProperty("kept").GetBoolean()));
        Assert.Empty((await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap })).Ok().GetProperty("translations").EnumerateArray());

        var applied = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/apply", new { pmap, language = "pt" },
            preview.GetProperty("structure_version").GetInt32())).Ok();
        var map = applied.GetProperty("map");
        var steps = map.GetProperty("steps").EnumerateArray().OrderBy(step => step.GetProperty("column").GetInt32()).ToList();
        Assert.Equal(["Saisir", "Envoyer"], steps.Select(step => step.GetProperty("label").GetString()));
        Assert.Equal(["Introduzir", "Enviar"], steps.Select(Portuguese));
        Assert.Equal("certo", Portuguese(map.GetProperty("flows")[0]));
        Assert.Equal("Ator 1", Portuguese(map.GetProperty("lanes")[0]));
        Assert.Equal("Projeto", Portuguese(map));
        var languages = map.GetProperty("languages");
        Assert.Equal(languages.GetProperty("total").GetInt32(), languages.GetProperty("written").GetProperty("pt").GetInt32());

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        var undone = await project.MapAsync();
        Assert.All(undone.GetProperty("steps").EnumerateArray(), step => Assert.Null(Portuguese(step)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_asks_the_model_for_the_process_into_its_language_only_from_a_facilitator(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, _, _, codes) = await ProjectAsync(host);
        using (var admin = host.As(Logins.Admin))
        {
            var version = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
            (await admin.PutAsync("/api/admin/assistant", new { connector = "copy_paste", allowed_classifications = new[] { "internal" } }, version)).Ok();
        }
        var url = $"/api/maps/{project.RootMapId}/assistant/prompt";
        object Ask(string? language) => new { intent = "translate", request = "Traduire le processus", selection = Array.Empty<string>(), history = Array.Empty<object>(), language };
        using var facilitator = project.Facilitator;
        // The second step detailed by a map of one step: the map given to the model holds every level (ADR 083).
        var second = (await project.MapAsync()).GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("code").GetString() == codes[1]).GetProperty("id").GetGuid();
        var detail = (await facilitator.PostAsync($"/api/steps/{second}/detail", null, await project.StepVersionAsync(second))).Ok().GetProperty("id").GetGuid();
        var subLane = (await project.MapAsync(detail)).GetProperty("lanes")[0].GetProperty("id").GetGuid();
        var inner = (await facilitator.PostAsync($"/api/maps/{detail}/steps", new { lane_id = subLane, column = 0, label = "Contrôler" })).Ok().GetProperty("code").GetString()!;
        var prompt = (await facilitator.PostAsync(url, Ask("pt"))).Ok().GetProperty("prompt").GetString()!;
        foreach (var part in new[] { "target=\"pt\"", "practice=\"translate\"", "allowed_forms=\"proposal, answer\"", "The person wants the **whole process translated**", codes[1] })
            Assert.Contains(part, prompt, StringComparison.Ordinal);
        var open = prompt[prompt.IndexOf("<map name=", StringComparison.Ordinal)..prompt.IndexOf("</map>", StringComparison.Ordinal)];
        Assert.Contains(inner, open, StringComparison.Ordinal);
        (await facilitator.PostAsync(url, Ask("fr"))).Refused(400, "VALEUR_INVALIDE");
        (await facilitator.PostAsync(url, Ask(null))).Refused(400, "LANGUE_INVALIDE");
        using var contributor = project.Contributor;
        (await contributor.PostAsync(url, Ask("pt"))).Refused(403, "ACCES_REFUSE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_into_a_language_out_of_the_configuration_is_refused(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var (project, lane, laneName, codes) = await ProjectAsync(host);
        using var facilitator = project.Facilitator;
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/preview", new { pmap = Answer(project.Code, lane, laneName, codes), language = "eo" }))
            .Refused(400, "LANGUE_INVALIDE");
    }
}
