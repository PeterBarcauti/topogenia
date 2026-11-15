// VoiceRoundTripTests.cs
// The voices of a project through the whole host on both databases (ADR 103): carried by its package with or without the people, written again by its import, by a target state and by a replacement, left out for a language that is not configured, refused when invalid.

using System.IO.Compression;
using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Exchange;

public sealed class VoiceRoundTripTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Hortense = "Hortense - voix du poste";

    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    /// <summary>The voices of a project as a presentation reads them: language, name, where it is read, version.</summary>
    private static async Task<List<(string Language, string Name, bool Local, int Version)>> KeptAsync(ApiClient client, Guid project) =>
        [.. (await client.GetAsync($"/api/projects/{project}/presentation-voices")).Ok().EnumerateArray()
            .Select(voice => (voice.GetProperty("language").GetString()!, voice.GetProperty("name").GetString()!, voice.GetProperty("local").GetBoolean(),
                voice.GetProperty("version").GetInt32()))];

    /// <summary>A project that keeps a French voice of the computer, chosen twice, and an English voice read online.</summary>
    private static async Task<ApiProject> WithVoicesAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        var url = $"/api/projects/{project.Id}/presentation-voices";
        (await facilitator.PutAsync($"{url}/fr", new { name = "Julie", local = true }, 0)).Ok();
        (await facilitator.PutAsync($"{url}/fr", new { name = Hortense, local = true }, 1)).Ok();
        (await facilitator.PutAsync($"{url}/en", new { name = "Julie", local = false }, 0)).Ok();
        return project;
    }

    /// <summary>The object `project` of project.json and the format the manifest of a package declares.</summary>
    private static (JsonElement Project, string Format) Read(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        JsonElement Entry(string name)
        {
            using var stream = archive.GetEntry(name)!.Open();
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.Clone();
        }
        return (Entry("project.json").GetProperty("project"), Entry("manifest.json").GetProperty("formatVersion").GetString()!);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_package_carries_the_voices_with_or_without_the_people_and_its_import_writes_them_on_the_new_project(EngineKind kind)
    {
        var project = await WithVoicesAsync(await ProjectAsync(kind));
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var kept = await KeptAsync(facilitator, project.Id);
        Assert.Equal([("en", "Julie", false, 1), ("fr", Hortense, true, 2)], kept);

        foreach (var withoutPeople in new[] { true, false })
        {
            var package = (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = withoutPeople })).Bytes;
            var (written, format) = Read(package);
            Assert.Equal("2.4", format);
            // Each voice by its language, its name and where it is read: neither version, nor author, nor date, with the people as without.
            Assert.Equal(["""{"language":"en","local":false,"name":"Julie"}""", """{"language":"fr","local":true,"name":"Hortense - voix du poste"}"""],
                written.GetProperty("presentationVoices").EnumerateArray().Select(voice => JsonSerializer.Serialize(voice)));

            var imported = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?code={ApiProject.NewCode()}&with_members=false", package, null)).Ok(201);

            // The same voices on the new project, each in version 1: the package carries no version.
            Assert.Equal(kept.Select(voice => voice with { Version = 1 }), await KeptAsync(admin, imported.GetProperty("id").GetGuid()));
        }
        // The source keeps its own: the imports wrote new voices, they moved none.
        Assert.Equal(kept, await KeptAsync(facilitator, project.Id));
    }

    /// <summary>A project that keeps no voice gives the package of before: its format is the one of its content, and its project names no voice.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_without_voice_gives_a_package_of_the_format_of_its_content(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (written, format) = Read((await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Bytes);
        Assert.Equal("2.1", format);
        Assert.False(written.TryGetProperty("presentationVoices", out _));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_target_state_takes_the_voices_of_its_source(EngineKind kind)
    {
        var project = await WithVoicesAsync(await ProjectAsync(kind));
        using var admin = project.Admin;

        var target = (await admin.PostAsync($"/api/projects/{project.Id}/target", new { code = ApiProject.NewCode(), name = "État cible", label = "État actuel" })).Ok(201);

        Assert.Equal((await KeptAsync(admin, project.Id)).Select(voice => voice with { Version = 1 }), await KeptAsync(admin, target.GetProperty("id").GetGuid()));
        Assert.Equal(2, (await KeptAsync(admin, project.Id)).Single(voice => voice.Language == "fr").Version);
    }

    /// <summary>A replacement by a package writes the voices of the new project; the replaced one goes to the trash with its own.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_replaced_by_its_own_package_keeps_its_voices_again(EngineKind kind)
    {
        var project = await WithVoicesAsync(await ProjectAsync(kind));
        using var facilitator = project.Facilitator;
        var kept = await KeptAsync(facilitator, project.Id);
        var package = (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Bytes;

        var replaced = (await facilitator.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=false", package, await project.ProjectVersionAsync())).Ok(201);

        var id = replaced.GetProperty("id").GetGuid();
        Assert.NotEqual(project.Id, id);
        Assert.Equal(kept.Select(voice => voice with { Version = 1 }), await KeptAsync(facilitator, id));
        // The replaced project is in the trash: its voices wait there with it, out of reach.
        (await facilitator.GetAsync($"/api/projects/{project.Id}/presentation-voices")).Refused(404, "INTROUVABLE");
    }

    /// <summary>A package written elsewhere: the voice of a language this installation does not configure is left out, as a translation (AR-119).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_import_leaves_out_the_voice_of_a_language_that_is_not_configured_here(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        // Danish is none of the eighteen languages of the installation.
        var package = TestPackages.WithVoices("PKG", ("da", "Voix danoise", true), ("fr", Hortense, true), ("en", "Julie", false));

        (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import/preview?code={ApiProject.NewCode()}&with_members=false", package, null)).Ok();
        var imported = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?code={ApiProject.NewCode()}&with_members=false", package, null)).Ok(201);

        Assert.Equal([("en", "Julie", false, 1), ("fr", Hortense, true, 1)], await KeptAsync(admin, imported.GetProperty("id").GetGuid()));
    }

    /// <summary>A package forged with a name its rule refuses is refused whole by its schema, at the preview as at the import.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_package_whose_voice_breaks_its_rule_is_refused_and_no_project_is_made(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        var code = ApiProject.NewCode();
        var package = TestPackages.WithVoices("PKG", ("fr", "Hortense\nJulie", true));

        var preview = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import/preview?code={code}&with_members=false", package, null)).Refused(400, "PAQUET_INVALIDE");
        Assert.Equal(("schema", "project.json"), (preview.GetProperty("reason").GetString(), preview.GetProperty("entry").GetString()));
        (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?code={code}&with_members=false", package, null)).Refused(400, "PAQUET_INVALIDE");

        Assert.DoesNotContain((await admin.GetAsync("/api/projects")).Ok().EnumerateArray(), project => project.GetProperty("code").GetString() == code);
    }
}
