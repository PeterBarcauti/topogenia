// ExportRoutesTests.cs
// `POST /api/projects/{id}/export` through the whole host on both databases: the package file, its people, its right, its audit.

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Interchange.Packages;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Exchange;

public sealed class ExportRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static Dictionary<string, byte[]> Entries(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var stream = entry.Open();
            using var content = new MemoryStream();
            stream.CopyTo(content);
            return content.ToArray();
        });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_facilitator_downloads_the_package_of_the_project_named_after_its_code_its_name_and_the_day(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;

        var reply = await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = false });

        Assert.True(reply.Status == 200, reply.Body);
        Assert.Equal("application/zip", reply.Headers["Content-Type"]);
        Assert.Matches($"^attachment; filename={project.Code}_Projet-{project.Code}_[0-9]{{4}}-[0-9]{{2}}-[0-9]{{2}}\\.topogenia;", reply.Headers["Content-Disposition"]);
        var entries = Entries(reply.Bytes);
        Assert.Equal(["manifest.json", "project.json", "members.json", "maps.json", "sheets.json", "documents.json", "painpoints.json", "translations.json"],
            entries.Keys);
        foreach (var (path, content) in entries)
            Assert.Empty(PackageDefinition.Current.Validate(path, JsonDocument.Parse(content).RootElement));
        var manifest = JsonDocument.Parse(entries["manifest.json"]).RootElement;
        Assert.Equal((project.Code, Logins.Facilitator), (manifest.GetProperty("projectCode").GetString(), manifest.GetProperty("exportedBy").GetString()));
        var logins = JsonDocument.Parse(entries["members.json"]).RootElement.GetProperty("members").EnumerateArray()
            .Select(member => member.GetProperty("login").GetString()).ToList();
        Assert.Contains(Logins.Facilitator, logins, StringComparer.OrdinalIgnoreCase);
        var creator = JsonDocument.Parse(entries["project.json"]).RootElement.GetProperty("project").GetProperty("createdBy").GetString();
        Assert.Equal(Logins.Admin, creator, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Without_the_people_the_package_names_nobody(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;

        var reply = await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true });

        Assert.True(reply.Status == 200, reply.Body);
        var entries = Entries(reply.Bytes);
        Assert.DoesNotContain("members.json", entries.Keys);
        Assert.All(entries.Values, content => Assert.DoesNotContain("TEST\\\\", Encoding.UTF8.GetString(content), StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_option_without_people_is_required_never_a_silent_default(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;

        var details = (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { })).Refused(400, "CHAMP_OBLIGATOIRE");

        Assert.Equal("without_people", details.GetProperty("field").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Only_the_facilitator_exports_and_every_export_is_in_the_audit_trail(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;

        (await contributor.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Refused(403, "ACCES_REFUSE");
        (await reader.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Refused(403, "ACCES_REFUSE");
        (await stranger.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Refused(404, "INTROUVABLE");
        Assert.Equal(200, (await facilitator.PostAsync($"/api/projects/{project.Id}/export", new { without_people = true })).Status);

        var events = (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}&kind={AuditKinds.ProjectExported}")).Ok().EnumerateArray().ToList();
        var exported = Assert.Single(events);
        Assert.Equal("""{"without_people":true}""", exported.GetProperty("details").GetString());
    }
}
