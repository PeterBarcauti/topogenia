// PresentationVoiceRoutesTests.cs
// The routes of the voices of a project through the whole host on both databases (ADR 103): kept language by language with If-Match, read with the project, out of the history and of the audit, kept by a project in the trash and back with it, gone with its purge, left by a copy.

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Projects;

public sealed class PresentationVoiceRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Hortense = "Hortense - voix du poste";

    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static string VoicesUrl(Guid project) => $"/api/projects/{project}/presentation-voices";

    private static string VoiceUrl(Guid project, string language) => $"{VoicesUrl(project)}/{language}";

    /// <summary>The voices of a project as a presentation reads them: language, name, where it is read, version.</summary>
    private static async Task<List<(string Language, string Name, bool Local, int Version)>> KeptAsync(ApiClient client, Guid project) =>
        [.. (await client.GetAsync(VoicesUrl(project))).Ok().EnumerateArray().Select(Line)];

    private static (string Language, string Name, bool Local, int Version) Line(JsonElement voice)
    {
        Assert.Equal(["language", "local", "name", "version"], voice.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        return (voice.GetProperty("language").GetString()!, voice.GetProperty("name").GetString()!, voice.GetProperty("local").GetBoolean(),
            voice.GetProperty("version").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_facilitator_keeps_the_voice_of_each_language_and_every_reader_of_the_project_reads_them(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        Assert.Empty(await KeptAsync(reader, project.Id));

        var written = (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = $"  {Hortense}  ", local = true }, 0)).Ok();
        Assert.Equal(["voices"], written.EnumerateObject().Select(property => property.Name));
        Assert.Equal([("fr", Hortense, true, 1)], written.GetProperty("voices").EnumerateArray().Select(Line));
        // The reply gives every voice of the project after the change, by language.
        var english = (await facilitator.PutAsync(VoiceUrl(project.Id, "en"), new { name = "Julie", local = false }, 0)).Ok();
        Assert.Equal([("en", "Julie", false, 1), ("fr", Hortense, true, 1)], english.GetProperty("voices").EnumerateArray().Select(Line));
        (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = "Julie", local = true }, 1)).Ok();
        Assert.Equal([("en", "Julie", false, 1), ("fr", "Julie", true, 2)], await KeptAsync(reader, project.Id));

        // The very voice already kept writes nothing: its version stays.
        var same = (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = "Julie", local = true }, 2)).Ok();
        Assert.Equal(2, same.GetProperty("voices").EnumerateArray().Select(Line).Single(voice => voice.Language == "fr").Version);
        // An empty name removes the voice of its language only; removing it again changes nothing.
        var removed = (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = "" }, 2)).Ok();
        Assert.Equal([("en", "Julie", false, 1)], removed.GetProperty("voices").EnumerateArray().Select(Line));
        (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = (string?)null, local = (bool?)null }, 0)).Ok();
        Assert.Equal([("en", "Julie", false, 1)], await KeptAsync(reader, project.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_invalid_voice_an_unknown_project_and_a_stale_version_are_refused_and_nothing_is_written(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        var url = VoiceUrl(project.Id, "fr");
        Assert.Equal("language", (await facilitator.PutAsync(VoiceUrl(project.Id, "xx"), new { name = "Julie", local = true }, 0)).Refused(400, "LANGUE_INVALIDE")
            .GetProperty("field").GetString());
        (await facilitator.PutAsync(VoiceUrl(project.Id, "FR"), new { name = "Julie", local = true }, 0)).Refused(400, "LANGUE_INVALIDE");
        Assert.Equal("name", (await facilitator.PutAsync(url, new { name = "<b>Julie</b>", local = true }, 0)).Refused(400, "TEXTE_HTML_INTERDIT").GetProperty("field").GetString());
        Assert.Equal(200, (await facilitator.PutAsync(url, new { name = new string('x', 201), local = true }, 0)).Refused(400, "TEXTE_TROP_LONG").GetProperty("max").GetInt32());
        Assert.Equal("name", (await facilitator.PutAsync(url, new { name = "Julie\nHortense", local = true }, 0)).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        // Where the voice is read is never guessed: a voice named without it is refused.
        Assert.Equal("local", (await facilitator.PutAsync(url, new { name = "Julie" }, 0)).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        (await facilitator.SendAsync(HttpMethod.Put, url, new { name = "Julie", local = true }, null, ("If-Match", "abc"))).Refused(400, "VERSION_INVALIDE");
        // An unknown project, a project the person is no member of, a reader who writes.
        (await facilitator.PutAsync(VoiceUrl(Guid.NewGuid(), "fr"), new { name = "Julie", local = true }, 0)).Refused(404, "INTROUVABLE");
        (await facilitator.GetAsync(VoicesUrl(Guid.NewGuid()))).Refused(404, "INTROUVABLE");
        (await stranger.GetAsync(VoicesUrl(project.Id))).Refused(404, "INTROUVABLE");
        (await stranger.PutAsync(url, new { name = "Julie", local = true }, 0)).Refused(404, "INTROUVABLE");
        (await reader.PutAsync(url, new { name = "Julie", local = true }, 0)).Refused(403, "ACCES_REFUSE");
        Assert.Empty(await KeptAsync(reader, project.Id));

        (await facilitator.PutAsync(url, new { name = Hortense, local = true }, 0)).Ok();
        var conflict = (await facilitator.PutAsync(url, new { name = "Julie", local = true }, 0)).Refused(409, "CONFLIT_VERSION");
        Assert.Equal("anne", conflict.GetProperty("by").GetString());
        // A removal with a version that is no longer the one kept is refused too.
        (await facilitator.PutAsync(url, new { name = "" }, 2)).Refused(409, "CONFLIT_VERSION");
        Assert.Equal([("fr", Hortense, true, 1)], await KeptAsync(reader, project.Id));
    }

    /// <summary>A voice is no operation of a map nor an event of the audit: Ctrl+Z does not undo it, and it leaves the project as it was.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_voice_of_a_project_is_out_of_the_history_of_its_maps_and_of_the_audit_and_leaves_the_project_as_it_was(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var step = (await project.AddStepAsync(0, "Commander")).Id;
        var history = $"/api/maps/{project.RootMapId}/history";
        var before = (await facilitator.GetAsync(history)).Ok().GetProperty("undo").GetArrayLength();
        var events = (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}")).Ok().GetArrayLength();
        var version = await project.ProjectVersionAsync();

        (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = Hortense, local = true }, 0)).Ok();

        Assert.Equal(before, (await facilitator.GetAsync(history)).Ok().GetProperty("undo").GetArrayLength());
        Assert.Equal(events, (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}")).Ok().GetArrayLength());
        Assert.Equal(version, await project.ProjectVersionAsync());
        // The undo takes back the step created before, never the voice kept since.
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.DoesNotContain((await project.MapAsync()).GetProperty("steps").EnumerateArray(), item => item.GetProperty("id").GetGuid() == step);
        Assert.Equal([("fr", Hortense, true, 1)], await KeptAsync(facilitator, project.Id));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_in_the_trash_keeps_its_voices_out_of_reach_and_finds_them_again_when_it_is_restored(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = Hortense, local = true }, 0)).Ok();
        (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = "Julie", local = false }, 1)).Ok();
        Assert.Equal("", (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Body);

        // In the trash: its voices are neither read nor written.
        (await facilitator.GetAsync(VoicesUrl(project.Id))).Refused(404, "INTROUVABLE");
        (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = Hortense, local = true }, 2)).Refused(404, "INTROUVABLE");
        (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = "" }, 2)).Refused(404, "INTROUVABLE");

        (await facilitator.PostAsync($"/api/trash/{project.Id}/restore")).Ok(204);
        Assert.Equal([("fr", "Julie", false, 2)], await KeptAsync(facilitator, project.Id));
        Assert.Equal(3, (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = Hortense, local = true }, 2)).Ok().GetProperty("voices")[0]
            .GetProperty("version").GetInt32());
    }

    /// <summary>A project in the trash is purged with its voices: they go before it, or the purge would be refused by the database.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_is_purged_with_its_voices_and_another_project_keeps_its_own(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var other = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = Hortense, local = true }, 0)).Ok();
        (await facilitator.PutAsync(VoiceUrl(project.Id, "en"), new { name = "Julie", local = false }, 0)).Ok();
        (await facilitator.PutAsync(VoiceUrl(other.Id, "fr"), new { name = "Julie", local = true }, 0)).Ok();
        Assert.Equal("", (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Body);

        var purged = await admin.DeleteAsync($"/api/trash/{project.Id}");

        Assert.Equal((204, ""), (purged.Status, purged.Body));
        (await admin.GetAsync(VoicesUrl(project.Id))).Refused(404, "INTROUVABLE");
        (await admin.GetAsync($"/api/projects/{project.Id}")).Refused(404, "INTROUVABLE");
        Assert.Equal([("fr", "Julie", true, 1)], await KeptAsync(facilitator, other.Id));
    }

    /// <summary>« Dupliquer » goes through a `.pmap` copy, which carries no voice, as it carries no comment of presentation (ADR 099, point 7): the copy keeps none.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_duplicated_project_keeps_no_voice(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        (await facilitator.PutAsync(VoiceUrl(project.Id, "fr"), new { name = Hortense, local = true }, 0)).Ok();
        var copy = (await facilitator.GetAsync($"/api/projects/{project.Id}/pmap")).Body;
        Assert.DoesNotContain("Hortense", copy, StringComparison.Ordinal);

        var duplicated = (await admin.PostAsync($"/api/projects/{project.Id}/duplicate", new { code = ApiProject.NewCode() })).Ok(201);

        Assert.Empty(await KeptAsync(admin, duplicated.GetProperty("id").GetGuid()));
        Assert.Single(await KeptAsync(facilitator, project.Id));
    }
}
