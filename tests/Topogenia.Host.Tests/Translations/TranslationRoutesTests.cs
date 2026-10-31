// TranslationRoutesTests.cs
// The routes of the translations of a project through the whole host on both databases: workbook texts, import in one pass (annex A 1, A 2.14).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Translations;

public sealed class TranslationRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static string Url(ApiProject project) => $"/api/projects/{project.Id}/translations";

    private static List<(string?, Guid)> Texts(JsonElement texts) =>
        [.. texts.EnumerateArray().Select(text => (text.GetProperty("type").GetString(), text.GetProperty("id").GetGuid()))];

    private static JsonElement TextOf(JsonElement texts, Guid id) =>
        texts.EnumerateArray().Single(text => text.GetProperty("id").GetGuid() == id);

    private static object ProjectRow(ApiProject project, int line, string english) =>
        new { line, type = "project", id = project.Id, original = $"Projet {project.Code}", translations = new Dictionary<string, string> { ["en"] = english } };

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_texts_to_translate_are_listed_in_reading_order_then_imported_in_one_pass_as_automatic(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var list = (await facilitator.GetAsync($"{Url(project)}?scope=missing&languages=en")).Ok();
        Assert.Equal((project.Id, project.Code, "missing"), (list.GetProperty("project_id").GetGuid(), list.GetProperty("project_code").GetString(), list.GetProperty("scope").GetString()));
        Assert.Equal(["fr", "en"], list.GetProperty("languages").EnumerateArray().Select(language => language.GetString()));
        var missing = list.GetProperty("texts");
        Assert.Equal([("project", project.Id), ("lane", project.FirstLaneId)], Texts(missing));
        var first = missing[0];
        Assert.Equal((project.Code, "fr", $"Projet {project.Code}"), (first.GetProperty("code").GetString(), first.GetProperty("language").GetString(), first.GetProperty("original").GetString()));
        Assert.Equal(Texts(missing), Texts((await facilitator.GetAsync(Url(project))).Ok().GetProperty("texts")));

        var imported = (await facilitator.PutAsync(Url(project), new
        {
            scope = "missing",
            rows = new[]
            {
                ProjectRow(project, 2, "Project of tests"),
                new { line = 3, type = "lane", id = Guid.NewGuid(), original = "Acteur 1", translations = new Dictionary<string, string> { ["en"] = "Actor" } },
            },
        })).Ok();
        Assert.Equal(1, imported.GetProperty("written").GetInt32());
        var ignored = imported.GetProperty("ignored").EnumerateArray().Single();
        Assert.Equal((3, "OBJET_INTROUVABLE"), (ignored.GetProperty("line").GetInt32(), ignored.GetProperty("reason").GetString()));

        Assert.Equal([("lane", project.FirstLaneId)], Texts((await facilitator.GetAsync($"{Url(project)}?scope=missing&languages=en")).Ok().GetProperty("texts")));
        var all = (await facilitator.GetAsync($"{Url(project)}?scope=all")).Ok().GetProperty("texts")[0];
        Assert.Equal(("Project of tests", "auto"), (all.GetProperty("translations").GetProperty("en").GetString(), all.GetProperty("sources").GetProperty("en").GetString()));
        var detail = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok();
        Assert.Equal("Project of tests", detail.GetProperty("name_translations").GetProperty("en").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_translations_of_a_project_are_reserved_to_its_facilitators(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        var body = new { scope = "missing", rows = new[] { ProjectRow(project, 2, "Project of tests") } };
        (await contributor.GetAsync(Url(project))).Refused(403, "ACCES_REFUSE");
        (await stranger.GetAsync($"{Url(project)}?scope=everything")).Refused(404, "INTROUVABLE");
        (await contributor.PutAsync(Url(project), body)).Refused(403, "ACCES_REFUSE");
        (await stranger.PutAsync(Url(project), body)).Refused(404, "INTROUVABLE");
        using var facilitator = project.Facilitator;
        Assert.Equal(2, (await facilitator.GetAsync(Url(project))).Ok().GetProperty("texts").GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_unknown_scope_or_a_missing_field_of_the_workbook_is_refused_by_its_name(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = Url(project);
        Assert.Equal("scope", (await facilitator.GetAsync($"{url}?scope=everything")).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        Assert.Equal("scope", (await facilitator.PutAsync(url, new { scope = "everything", rows = Array.Empty<object>() })).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        Assert.Equal("rows", (await facilitator.PutAsync(url, new { scope = "missing" })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        Assert.Equal("id", (await facilitator.PutAsync(url, new { rows = new[] { new { line = 2, type = "project" } } })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        Assert.Equal("line", (await facilitator.PutAsync(url, new { rows = new[] { new { type = "project", id = project.Id } } })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        Assert.Equal(0, (await facilitator.PutAsync(url, new { rows = Array.Empty<object>() })).Ok().GetProperty("written").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task One_invalid_translation_refuses_the_whole_file_with_its_line_and_writes_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var refused = (await facilitator.PutAsync(Url(project), new
        {
            scope = "all",
            rows = new[]
            {
                ProjectRow(project, 2, "Project of tests"),
                new { line = 3, type = "lane", id = project.FirstLaneId, original = "Acteur 1", translations = new Dictionary<string, string> { ["en"] = "<b>Actor</b>" } },
            },
        })).Refused(400, "TRADUCTIONS_INVALIDES");
        var error = refused.GetProperty("errors").EnumerateArray().Single();
        Assert.Equal((3, "en", "TEXTE_HTML_INTERDIT"), (error.GetProperty("line").GetInt32(), error.GetProperty("language").GetString(), error.GetProperty("code").GetString()));
        Assert.Equal([("project", project.Id), ("lane", project.FirstLaneId)], Texts((await facilitator.GetAsync(Url(project))).Ok().GetProperty("texts")));
    }

    /// <summary>
    /// A-2.14-08: the project, then the detailed maps renamed (here a map reattached to a step of another label), the
    /// lanes map by map, the steps, and the labelled flows last.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_texts_come_in_reading_order_with_the_renamed_detailed_map_before_the_lanes_and_the_labelled_flows_after_the_steps(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (enter, approve) = (await project.AddStepAsync(0, "Saisir"), await project.AddStepAsync(1, "Valider"));
        var yes = await project.AddFlowAsync(enter.Id, approve.Id, "oui");
        var (detail, detailLane) = await project.DetailAsync(enter.Id);
        var version = (await project.MapAsync(detail)).GetProperty("version").GetInt32();
        (await facilitator.PostAsync($"/api/maps/{detail}/reattach", new { step_id = approve.Id }, version)).Ok();

        var texts = (await facilitator.GetAsync($"{Url(project)}?scope=all")).Ok().GetProperty("texts");
        Assert.Equal(
        [
            ("project", project.Id), ("map", detail), ("lane", project.FirstLaneId), ("lane", detailLane),
            ("step", enter.Id), ("step", approve.Id), ("flow", yes),
        ], Texts(texts));
        var map = TextOf(texts, detail);
        Assert.Equal((approve.Code, "Saisir"), (map.GetProperty("code").GetString(), map.GetProperty("original").GetString()));
    }

    /// <summary>A-2.14-11: a flow without label is not a text; a labelled flow is coded `A > B` by the codes of its ends.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Only_the_labelled_flows_are_listed_with_the_codes_of_their_ends(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (enter, approve, file) = (await project.AddStepAsync(0, "Saisir"), await project.AddStepAsync(1, "Valider"), await project.AddStepAsync(2, "Classer"));
        await project.AddFlowAsync(enter.Id, approve.Id);
        var yes = await project.AddFlowAsync(approve.Id, file.Id, "oui");

        var texts = (await facilitator.GetAsync($"{Url(project)}?scope=all")).Ok().GetProperty("texts");
        var flow = Assert.Single(texts.EnumerateArray(), text => text.GetProperty("type").GetString() == "flow");
        Assert.Equal((yes, $"{approve.Code} > {file.Code}", "Valider > Classer", "oui"),
            (flow.GetProperty("id").GetGuid(), flow.GetProperty("code").GetString(), flow.GetProperty("context").GetString(), flow.GetProperty("original").GetString()));
    }

    /// <summary>
    /// A-2.14-19: a row without `original` is compared with its cell in the language of origin (the column `fr` of the
    /// workbook): the current text is written, another text is ignored as TEXTE_MODIFIE.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_row_without_original_is_compared_with_its_column_of_the_language_of_origin(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var imported = (await facilitator.PutAsync(Url(project), new
        {
            scope = "all",
            rows = new object[]
            {
                new { line = 2, type = "project", id = project.Id, translations = new Dictionary<string, string> { ["fr"] = $"Projet {project.Code}", ["en"] = "Project of tests" } },
                new { line = 3, type = "lane", id = project.FirstLaneId, translations = new Dictionary<string, string> { ["fr"] = "Acteur un", ["en"] = "Actor" } },
            },
        })).Ok();
        Assert.Equal(1, imported.GetProperty("written").GetInt32());
        var ignored = imported.GetProperty("ignored").EnumerateArray().Single();
        Assert.Equal((3, "TEXTE_MODIFIE"), (ignored.GetProperty("line").GetInt32(), ignored.GetProperty("reason").GetString()));

        var texts = (await facilitator.GetAsync($"{Url(project)}?scope=all")).Ok().GetProperty("texts");
        Assert.Equal("Project of tests", TextOf(texts, project.Id).GetProperty("translations").GetProperty("en").GetString());
        Assert.False(TextOf(texts, project.FirstLaneId).GetProperty("translations").TryGetProperty("en", out _));
    }

    /// <summary>
    /// A-6-18: each rejected row carries its message « L{n} : » written by the host in the language of the user, its
    /// field being the language and its maximum that of the type (1.x `ServiceTraductions.cs` 124-128).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Each_refused_row_of_a_workbook_has_its_line_message_built_by_the_host(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var body = new
        {
            scope = "all",
            rows = new[]
            {
                ProjectRow(project, 2, new string('P', 201)),
                new { line = 3, type = "lane", id = project.FirstLaneId, original = "Acteur 1", translations = new Dictionary<string, string> { ["en"] = "<b>Actor</b>" } },
            },
        };
        var french = (await facilitator.PutAsync(Url(project), body)).Refused(400, "TRADUCTIONS_INVALIDES");
        Assert.Equal(["L2 : Le champ EN dépasse 200 caractères.", "L3 : Le champ EN ne peut pas contenir de balises HTML."], Messages(french));
        var english = (await facilitator.SendAsync(HttpMethod.Put, Url(project), body, null, ("Accept-Language", "en"))).Refused(400, "TRADUCTIONS_INVALIDES");
        Assert.Equal(["L2: The field EN exceeds 200 characters.", "L3: The field EN cannot contain HTML tags."], Messages(english));
        Assert.Equal((2, "en", "TEXTE_TROP_LONG", 200), Row(english.GetProperty("errors")[0]));
    }

    private static List<string?> Messages(JsonElement details) =>
        [.. details.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("message").GetString())];

    private static (int, string?, string?, int) Row(JsonElement error) =>
        (error.GetProperty("line").GetInt32(), error.GetProperty("language").GetString(), error.GetProperty("code").GetString(), error.GetProperty("max").GetInt32());
}
