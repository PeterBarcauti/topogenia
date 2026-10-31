// ImportRoutesTests.cs
// The import routes through the whole host on both databases (spec 7.3, 7.4, 5.4, ADR 052): round trip, new project, replacement, refusals.

using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Exchange;

public sealed class ImportRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    /// <summary>The content entries of a package, the manifest left out (spec 7.4: its metadata are outside the comparison).</summary>
    private static Dictionary<string, string> Content(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return archive.Entries.Where(entry => entry.FullName != "manifest.json").ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }

    private static async Task<byte[]> ExportAsync(ApiClient client, Guid projectId, bool withoutPeople)
    {
        var reply = await client.PostAsync($"/api/projects/{projectId}/export", new { without_people = withoutPeople });
        Assert.True(reply.Status == 200, reply.Body);
        return reply.Bytes;
    }

    private static async Task<int> ProjectCountAsync(ApiClient admin) => (await admin.GetAsync("/api/projects")).Ok().GetArrayLength();

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Export_of_the_replacement_by_its_own_export_gives_the_same_package_content(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = project.FirstLaneId, column = 0, label = "Besoin" })).Ok();
        var package = await ExportAsync(facilitator, project.Id, withoutPeople: false);

        var imported = (await facilitator.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=true", package,
            await project.ProjectVersionAsync())).Ok(201);

        var id = imported.GetProperty("id").GetGuid();
        Assert.NotEqual(project.Id, id);
        Assert.Equal(project.Code, imported.GetProperty("code").GetString());
        Assert.Equal(Content(package), Content(await ExportAsync(facilitator, id, withoutPeople: false)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_replaced_project_waits_in_the_trash_under_its_derived_code_with_its_audit_events(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var package = await ExportAsync(facilitator, project.Id, withoutPeople: true);

        var imported = (await facilitator.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=false", package,
            await project.ProjectVersionAsync())).Ok(201);

        var trashCode = imported.GetProperty("trash_code").GetString();
        Assert.Equal(project.Code + "A", trashCode);
        var trashed = (await facilitator.GetAsync("/api/trash")).Ok().GetProperty("projects").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == project.Id);
        Assert.Equal(trashCode, trashed.GetProperty("code").GetString());
        (await facilitator.GetAsync($"/api/projects/{project.Id}")).Refused(404, "INTROUVABLE");
        var kinds = (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}")).Ok().EnumerateArray().Select(audit => audit.GetProperty("kind").GetString());
        Assert.Contains(AuditKinds.ProjectTrashed, kinds);
        var id = imported.GetProperty("id").GetGuid();
        var importedEvent = Assert.Single((await admin.GetAsync($"/api/admin/audit?project_id={id}&kind={AuditKinds.ProjectImported}")).Ok().EnumerateArray());
        Assert.Equal("""{"mode":"replace","with_members":false}""", importedEvent.GetProperty("details").GetString());
        var map = (await facilitator.GetAsync($"/api/maps/{imported.GetProperty("root_map_id").GetGuid()}")).Ok();
        Assert.Equal(project.Code + "-C01", map.GetProperty("map").GetProperty("lanes")[0].GetProperty("code").GetString());
        (await facilitator.PostAsync($"/api/trash/{project.Id}/restore")).Ok(204);
        Assert.Equal(trashCode, (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("code").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_replacement_by_a_package_without_the_people_keeps_the_team_and_its_roles(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        static IEnumerable<(string?, string?)> Team(JsonElement detail) => detail.GetProperty("members").EnumerateArray()
            .Select(member => (member.GetProperty("login").GetString(), member.GetProperty("role").GetString()));
        var team = Team((await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok()).ToList();
        var package = await ExportAsync(facilitator, project.Id, withoutPeople: true);

        var imported = (await facilitator.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=true", package,
            await project.ProjectVersionAsync())).Ok(201);

        var id = imported.GetProperty("id").GetGuid();
        Assert.Contains(((string?)Logins.Reader, (string?)"reader"), team);
        Assert.Contains(((string?)Logins.Contributor, (string?)"contributor"), team);
        Assert.Equal(team, Team((await facilitator.GetAsync($"/api/projects/{id}")).Ok()));
        (await reader.GetAsync($"/api/maps/{imported.GetProperty("root_map_id").GetGuid()}")).Ok();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_administrator_imports_a_new_project_under_a_free_code_that_rewrites_every_readable_code(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var package = TestPackages.Minimal(project.Code);
        var code = ApiProject.NewCode();

        (await admin.SendAsync(HttpMethod.Post, "/api/projects/import?with_members=false", package, null)).Refused(409, "PROJET_CODE_EXISTANT");
        var reply = await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?with_members=false&code={code}", package, null);

        var imported = reply.Ok(201);
        Assert.Equal($"/api/projects/{imported.GetProperty("id").GetGuid()}", reply.Location!.OriginalString);
        Assert.Equal(code, (await admin.GetAsync(reply.Location.OriginalString)).Ok().GetProperty("code").GetString());
        var map = (await admin.GetAsync($"/api/maps/{imported.GetProperty("root_map_id").GetGuid()}")).Ok();
        Assert.Equal(code + "-C01", map.GetProperty("map").GetProperty("lanes")[0].GetProperty("code").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_preview_reports_the_package_and_its_code_and_writes_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var before = await ProjectCountAsync(admin);

        var preview = (await admin.SendAsync(HttpMethod.Post, "/api/projects/import/preview?with_members=false", TestPackages.Minimal(project.Code), null)).Ok();

        Assert.Equal(before, await ProjectCountAsync(admin));
        Assert.Equal((project.Code, project.Code, "taken", false), (preview.GetProperty("package_code").GetString(), preview.GetProperty("project_code").GetString(),
            preview.GetProperty("code_state").GetString(), preview.GetProperty("with_people").GetBoolean()));
        Assert.Equal(1, preview.GetProperty("counts").GetProperty("maps").GetInt32());
        Assert.Equal(1, preview.GetProperty("counts").GetProperty("lanes").GetInt32());
        Assert.True(preview.GetProperty("settlement").GetProperty("importer_added").GetBoolean());
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("trash_code").ValueKind);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_invalid_package_is_refused_and_nothing_is_written(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        using var facilitator = project.Facilitator;
        var before = await ProjectCountAsync(admin);
        var broken = TestPackages.Minimal(ApiProject.NewCode());
        broken[^30] ^= 0xFF;

        var details = (await admin.SendAsync(HttpMethod.Post, "/api/projects/import?with_members=false", broken, null)).Refused(400, "PAQUET_INVALIDE");
        (await facilitator.SendAsync(HttpMethod.Post, $"/api/projects/{project.Id}/import?with_members=false", "{}"u8.ToArray(),
            await project.ProjectVersionAsync())).Refused(400, "PAQUET_INVALIDE");

        Assert.False(string.IsNullOrEmpty(details.GetProperty("reason").GetString()));
        Assert.Equal(before, await ProjectCountAsync(admin));
        Assert.Equal(project.Code, (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("code").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_option_with_members_is_required_never_a_silent_default(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var package = TestPackages.Minimal(ApiProject.NewCode());

        var absent = (await admin.SendAsync(HttpMethod.Post, "/api/projects/import", package, null)).Refused(400, "CHAMP_OBLIGATOIRE");
        var wrong = (await admin.SendAsync(HttpMethod.Post, "/api/projects/import/preview?with_members=oui", package, null)).Refused(400, "VALEUR_INVALIDE");

        Assert.Equal(("with_members", "with_members"), (absent.GetProperty("field").GetString(), wrong.GetProperty("field").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_package_longer_than_100_megabytes_is_refused_413_before_it_is_read_and_a_longer_one_than_1_megabyte_is_read(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var before = await ProjectCountAsync(admin);
        // The limit as the specification writes it (spec 5.4: 100 MB), not read from the code under test.
        using var content = new ByteArrayContent(new byte[(100 * 1024 * 1024) + 1]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var refused = await admin.SendContentAsync(HttpMethod.Post, "/api/projects/import?with_members=false", content, null);
        var read = await admin.SendAsync(HttpMethod.Post, "/api/projects/import?with_members=false", new byte[2 * 1024 * 1024], null);

        Assert.Equal(100, refused.Refused(413, "REQUETE_TROP_GROSSE").GetProperty("max").GetInt32());
        Assert.Equal("archive", read.Refused(400, "PAQUET_INVALIDE").GetProperty("reason").GetString());
        Assert.Equal(before, await ProjectCountAsync(admin));
    }
}
