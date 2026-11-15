// OriginLanguageRoutesTests.cs
// The change of the language of origin of a project through the whole host on both databases (ADR 100): a language wholly translated becomes the version of origin, each text exchanged with its translation; refused while a text lacks; the way back; the package exported afterwards.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Translations;

public sealed class OriginLanguageRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private sealed record Written(ApiProject Project, Guid First, Guid Second);

    /// <summary>Two steps linked by an arrow labelled « oui », the first with a comment of presentation in French and in English.</summary>
    private async Task<Written> ProjectAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var facilitator = project.Facilitator;
        var first = (await project.AddStepAsync(0, "Demander")).Id;
        var second = (await project.AddStepAsync(1, "Valider")).Id;
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/flows", new { source_step_id = first, target_step_id = second, label = "oui" })).Ok();
        (await facilitator.PutAsync($"/api/steps/{first}/presentation-comment", new { text = "Le salarié saisit sa demande.", language = "fr" }, 0)).Ok();
        (await facilitator.PutAsync($"/api/steps/{first}/presentation-comment", new { text = "The employee enters the request.", language = "en" }, 0)).Ok();
        return new Written(project, first, second);
    }

    private static async Task<int> VersionAsync(ApiClient client, ApiProject project) =>
        (await client.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("version").GetInt32();

    private static async Task<ApiResponse> ChangeAsync(ApiClient client, ApiProject project, string? language) =>
        await client.PutAsync($"/api/projects/{project.Id}/origin-language", new { language }, await VersionAsync(client, project));

    /// <summary>The labels of the steps of the root map in the order of their columns, with their language of origin.</summary>
    private static async Task<List<(string Label, string Language)>> StepsAsync(ApiProject project) =>
        [.. (await project.MapAsync()).GetProperty("steps").EnumerateArray().OrderBy(step => step.GetProperty("column").GetInt32())
            .Select(step => (step.GetProperty("label").GetString()!, step.GetProperty("source_language").GetString()!))];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_language_wholly_translated_becomes_the_language_of_origin_and_the_same_change_back_gives_the_texts_back(EngineKind kind)
    {
        var (project, first, _) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var name = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("name").GetString()!;
        await ProjectTranslations.TranslateAllAsync(facilitator, project.Id, "en");

        var changed = (await ChangeAsync(facilitator, project, "en")).Ok();

        Assert.Equal(("en", ProjectTranslations.Of("en", name)), (changed.GetProperty("source_language").GetString(), changed.GetProperty("name").GetString()));
        Assert.Equal([(ProjectTranslations.Of("en", "Demander"), "en"), (ProjectTranslations.Of("en", "Valider"), "en")], await StepsAsync(project));
        var map = await project.MapAsync();
        Assert.Equal(("en", ProjectTranslations.Of("en", "oui")), (map.GetProperty("languages").GetProperty("origin").GetString(), map.GetProperty("flows")[0].GetProperty("label").GetString()));
        var step = map.GetProperty("steps").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == first);
        Assert.Equal("Demander", step.GetProperty("translations").GetProperty("fr").GetString());
        Assert.False(step.GetProperty("translations").TryGetProperty("en", out _));
        var languages = map.GetProperty("languages");
        Assert.Equal(languages.GetProperty("total").GetInt32(), languages.GetProperty("written").GetProperty("fr").GetInt32());
        // The comments of presentation have nothing to exchange: both lines stay, the English one the original now (another test empties it).
        var lines = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/presentation-comments")).Ok().EnumerateArray().ToList();
        Assert.Equal(["en", "fr"], lines.Select(line => line.GetProperty("language").GetString()).Order());
        // The audit keeps the change with both languages.
        using (var admin = project.Admin)
        {
            var audited = (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}&kind={AuditKinds.OriginLanguageChanged}")).Ok().EnumerateArray().Single();
            var raw = audited.GetProperty("details");
            var details = raw.ValueKind == JsonValueKind.String ? JsonDocument.Parse(raw.GetString()!).RootElement : raw;
            Assert.Equal(("fr", "en"), (details.GetProperty("from").GetString(), details.GetProperty("to").GetString()));
        }

        var back = (await ChangeAsync(facilitator, project, "fr")).Ok();
        Assert.Equal(("fr", name), (back.GetProperty("source_language").GetString(), back.GetProperty("name").GetString()));
        Assert.Equal([("Demander", "fr"), ("Valider", "fr")], await StepsAsync(project));
        Assert.Equal("oui", (await project.MapAsync()).GetProperty("flows")[0].GetProperty("label").GetString());
        (await facilitator.PutAsync($"/api/steps/{first}/presentation-comment", new { text = "", language = "fr" }, 1)).Ok();
        Assert.Empty((await facilitator.GetAsync($"/api/maps/{project.RootMapId}/presentation-comments")).Ok().EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_language_that_lacks_a_translation_is_refused_with_the_texts_that_lack_and_nothing_changes(EngineKind kind)
    {
        var (project, first, second) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var before = await StepsAsync(project);
        (await facilitator.PutAsync($"/api/steps/{first}/translations", new { translations = new { en = "Request" } }, await project.StepVersionAsync(first))).Ok();

        var refusal = (await ChangeAsync(facilitator, project, "en")).Refused(409, "TRADUCTION_INCOMPLETE");

        Assert.Equal("en", refusal.GetProperty("language").GetString());
        var missing = refusal.GetProperty("missing").EnumerateArray().ToList();
        Assert.Equal(refusal.GetProperty("count").GetInt32(), missing.Count);
        Assert.Contains(missing, text => text.GetProperty("type").GetString() == "step" && text.GetProperty("id").GetGuid() == second);
        Assert.DoesNotContain(missing, text => text.GetProperty("id").GetGuid() == first);
        Assert.Equal(before, await StepsAsync(project));
        Assert.Equal("fr", (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("source_language").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_comment_of_presentation_without_its_line_in_the_language_is_a_text_that_lacks_and_translated_the_change_passes(EngineKind kind)
    {
        var (project, first, second) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        await ProjectTranslations.TranslateAllAsync(facilitator, project.Id, "en");
        (await facilitator.PutAsync($"/api/steps/{second}/presentation-comment", new { text = "Le responsable valide.", language = "fr" }, 0)).Ok();
        var before = await StepsAsync(project);

        var refusal = (await ChangeAsync(facilitator, project, "en")).Refused(409, "TRADUCTION_INCOMPLETE");

        // Every text of the closed core is translated: the comment of the second step alone lacks, named by its step.
        Assert.Equal(("en", 1), (refusal.GetProperty("language").GetString(), refusal.GetProperty("count").GetInt32()));
        var missing = Assert.Single(refusal.GetProperty("missing").EnumerateArray());
        Assert.Equal(("presentation_comment", second), (missing.GetProperty("type").GetString(), missing.GetProperty("id").GetGuid()));
        Assert.False(string.IsNullOrEmpty(missing.GetProperty("code").GetString()));
        Assert.DoesNotContain(first.ToString(), refusal.GetProperty("missing").ToString(), StringComparison.Ordinal);
        Assert.Equal(before, await StepsAsync(project));

        (await facilitator.PutAsync($"/api/steps/{second}/presentation-comment", new { text = "The manager approves.", language = "en" }, 0)).Ok();
        Assert.Equal("en", (await ChangeAsync(facilitator, project, "en")).Ok().GetProperty("source_language").GetString());

        // While the project is in English, the English line is the original: emptied, it takes the French one with it.
        (await facilitator.PutAsync($"/api/steps/{second}/presentation-comment", new { text = "", language = "en" }, 1)).Ok();
        var left = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/presentation-comments")).Ok().EnumerateArray().ToList();
        Assert.Equal([first, first], left.Select(line => line.GetProperty("step_id").GetGuid()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_text_and_a_comment_that_lack_are_refused_together_the_texts_first(EngineKind kind)
    {
        var (project, first, second) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        (await facilitator.PutAsync($"/api/steps/{first}/translations", new { translations = new { en = "Request" } }, await project.StepVersionAsync(first))).Ok();
        (await facilitator.PutAsync($"/api/steps/{second}/presentation-comment", new { text = "Le responsable valide.", language = "fr" }, 0)).Ok();

        var refusal = (await ChangeAsync(facilitator, project, "en")).Refused(409, "TRADUCTION_INCOMPLETE");

        var missing = refusal.GetProperty("missing").EnumerateArray().ToList();
        Assert.Equal(refusal.GetProperty("count").GetInt32(), missing.Count);
        Assert.Contains(missing, text => text.GetProperty("type").GetString() == "step" && text.GetProperty("id").GetGuid() == second);
        Assert.Equal(("presentation_comment", second), (missing[^1].GetProperty("type").GetString(), missing[^1].GetProperty("id").GetGuid()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_change_is_for_the_facilitator_of_a_live_project_into_a_configured_language_that_is_not_the_origin(EngineKind kind)
    {
        var (project, _, _) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        await ProjectTranslations.TranslateAllAsync(facilitator, project.Id, "en");
        (await ChangeAsync(facilitator, project, "fr")).Refused(400, "VALEUR_INVALIDE");
        (await ChangeAsync(facilitator, project, "eo")).Refused(400, "LANGUE_INVALIDE");
        (await ChangeAsync(facilitator, project, null)).Refused(400, "LANGUE_INVALIDE");
        using (var contributor = project.Contributor)
            (await contributor.PutAsync($"/api/projects/{project.Id}/origin-language", new { language = "en" }, await VersionAsync(facilitator, project))).Refused(403, "ACCES_REFUSE");
        using (var stranger = project.Stranger)
            (await stranger.PutAsync($"/api/projects/{project.Id}/origin-language", new { language = "en" }, 1)).Refused(404, "INTROUVABLE");
        (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await VersionAsync(facilitator, project))).Ok(204);
        (await facilitator.PutAsync($"/api/projects/{project.Id}/origin-language", new { language = "en" }, 1)).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_change_empties_the_undo_and_redo_stacks_so_that_a_labelled_flow_removed_before_it_does_not_come_back_in_the_former_language(EngineKind kind)
    {
        var (project, first, _) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        await ProjectTranslations.TranslateAllAsync(facilitator, project.Id, "en");
        // An operation undone then redone leaves a chain in the log; then the labelled flow is removed: the exchange does
        // not touch a flow in the trash, so the undo of its removal would pass and give back a label of the former language.
        (await facilitator.PatchAsync($"/api/steps/{first}", new { label = "Demander un congé" }, await project.StepVersionAsync(first))).Ok();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Ok();
        (await facilitator.PutAsync($"/api/steps/{first}/translations", new { translations = new { en = "Request a leave" } }, await project.StepVersionAsync(first))).Ok();
        var flow = (await project.MapAsync()).GetProperty("flows")[0];
        (await facilitator.DeleteAsync($"/api/flows/{flow.GetProperty("id").GetGuid()}", flow.GetProperty("version").GetInt32())).Ok();

        (await ChangeAsync(facilitator, project, "en")).Ok();

        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Refused(404, "INTROUVABLE");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/redo")).Refused(404, "INTROUVABLE");
        Assert.Empty((await project.MapAsync()).GetProperty("flows").EnumerateArray());
        // A refused change empties nothing: the stack of before stays.
        (await facilitator.PatchAsync($"/api/steps/{first}", new { label = "Request leave" }, await project.StepVersionAsync(first))).Ok();
        (await ChangeAsync(facilitator, project, "en")).Refused(400, "VALEUR_INVALIDE");
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_operation_of_before_the_change_is_no_longer_undone_and_a_package_exported_after_it_is_imported_in_the_new_language(EngineKind kind)
    {
        var (project, _, _) = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        await ProjectTranslations.TranslateAllAsync(facilitator, project.Id, "en");
        (await ChangeAsync(facilitator, project, "en")).Ok();

        // The steps were created by operations of the facilitator: the change emptied the stacks, there is nothing left to undo.
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Refused(404, "INTROUVABLE");

        var package = (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Bytes;
        var imported = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?code={ApiProject.NewCode()}&with_members=false", package, null)).Ok(201);
        var copy = (await admin.GetAsync($"/api/projects/{imported.GetProperty("id").GetGuid()}")).Ok();
        Assert.Equal("en", copy.GetProperty("source_language").GetString());
        var map = (await admin.GetAsync($"/api/maps/{imported.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map");
        Assert.Equal([ProjectTranslations.Of("en", "Demander"), ProjectTranslations.Of("en", "Valider")],
            map.GetProperty("steps").EnumerateArray().OrderBy(step => step.GetProperty("column").GetInt32()).Select(step => step.GetProperty("label").GetString()));
        Assert.All(map.GetProperty("steps").EnumerateArray(), step => Assert.Equal("en", step.GetProperty("source_language").GetString()));
        Assert.Equal(["Demander", "Valider"],
            map.GetProperty("steps").EnumerateArray().OrderBy(step => step.GetProperty("column").GetInt32()).Select(step => step.GetProperty("translations").GetProperty("fr").GetString()));
    }
}
