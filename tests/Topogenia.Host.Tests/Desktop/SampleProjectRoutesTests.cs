// SampleProjectRoutesTests.cs
// The sample project of the first launch: imported from a package of the samples for the user of the desktop mode, by the chain of every import, written in the language of the interface; nothing in server mode (spec 6.1, 7.3).

using Topogenia.Application.Audit;
using Topogenia.Host.Api.Exchange;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

/// <summary>SQLite runs the desktop mode and SQL Server the server mode (`ApiHost`); the tests of the class share one base per engine.</summary>
public sealed class SampleProjectRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Route = "/api/desktop/sample-project";

    [Fact]
    public void The_packages_embedded_in_the_host_are_the_files_of_the_samples_themselves()
    {
        Assert.Equal(File.ReadAllBytes(RepositoryPaths.Of("samples", "conges.topogenia")), SampleProjectApi.Package(SampleProjectApi.ResourceName));
        Assert.Equal(File.ReadAllBytes(RepositoryPaths.Of("samples", "leave.topogenia")), SampleProjectApi.Package(SampleProjectApi.EnglishResourceName));
    }

    [Fact]
    public async Task An_interface_in_another_language_than_french_gets_the_sample_written_in_english()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        using var admin = host.As(Logins.Admin);
        var me = (await admin.GetAsync("/api/me")).Ok();
        // The profile may hold no language of its own (the default of the application, French here): it is given French back.
        var french = me.GetProperty("language").GetString() is { Length: > 0 } own ? own : "fr";
        (await admin.PatchAsync("/api/me", new { language = "en" }, me.GetProperty("version").GetInt32())).Ok();
        try
        {
            var created = (await admin.PostAsync(Route)).Ok(201);
            Assert.Equal("LEAVE", created.GetProperty("code").GetString());
            var detail = (await admin.GetAsync($"/api/projects/{created.GetProperty("id").GetGuid()}")).Ok();
            Assert.Equal(("Leave request", "en", "HR team"),
                (detail.GetProperty("name").GetString(), detail.GetProperty("source_language").GetString(), detail.GetProperty("sharing_group_label").GetString()));
            var map = (await admin.GetAsync($"/api/maps/{detail.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map");
            Assert.Contains(map.GetProperty("steps").EnumerateArray(), step => step.GetProperty("label").GetString() == "Approve the request");
        }
        finally
        {
            var back = (await admin.GetAsync("/api/me")).Ok();
            (await admin.PatchAsync("/api/me", new { language = french }, back.GetProperty("version").GetInt32())).Ok();
        }
    }

    [Fact]
    public async Task The_administrator_of_the_desktop_mode_creates_the_sample_project_once_as_its_only_facilitator_and_the_import_is_audited()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        using var admin = host.As(Logins.Admin);
        var reply = await admin.PostAsync(Route);
        var created = reply.Ok(201);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(($"/api/projects/{id}", "CONGES"), (reply.Location!.OriginalString, created.GetProperty("code").GetString()));
        Assert.Empty(created.GetProperty("created_user_ids").EnumerateArray());
        var detail = (await admin.GetAsync($"/api/projects/{id}")).Ok();
        Assert.Equal(("Demande de congés", "confidential", "Équipe RH", created.GetProperty("root_map_id").GetGuid()),
            (detail.GetProperty("name").GetString(), detail.GetProperty("classification").GetString(), detail.GetProperty("sharing_group_label").GetString(),
                detail.GetProperty("root_map_id").GetGuid()));
        var member = Assert.Single(detail.GetProperty("members").EnumerateArray());
        Assert.Equal((Logins.Admin, "facilitator"), (member.GetProperty("login").GetString(), member.GetProperty("role").GetString()));
        Assert.True((await admin.GetAsync($"/api/maps/{detail.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map").GetProperty("steps").GetArrayLength() > 0);
        var audit = Assert.Single((await admin.GetAsync($"/api/admin/audit?project_id={id}")).Ok().EnumerateArray());
        Assert.Equal(AuditKinds.ProjectImported, audit.GetProperty("kind").GetString());

        Assert.Equal("CONGES", (await admin.PostAsync(Route)).Refused(409, "PROJET_CODE_EXISTANT").GetProperty("code").GetString());
        (await admin.DeleteAsync($"/api/projects/{id}?confirmation=CONGES", detail.GetProperty("version").GetInt32())).Ok(204);
        (await admin.PostAsync(Route)).Refused(409, "PROJET_CODE_CORBEILLE");
        Assert.DoesNotContain((await admin.GetAsync("/api/projects")).Ok().EnumerateArray(), item => item.GetProperty("code").GetString() == "CONGES");
    }

    [Fact]
    public async Task In_desktop_mode_the_sample_project_needs_the_session_and_the_right_of_the_administrator_and_a_refusal_creates_nothing()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        using var anonymous = host.As(null);
        using var facilitator = host.As(Logins.Facilitator);
        Assert.Equal(401, (await anonymous.PostAsync(Route)).Status);
        (await facilitator.PostAsync(Route)).Refused(403, "ACCES_REFUSE");
        Assert.Empty((await facilitator.GetAsync("/api/projects")).Ok().EnumerateArray());
    }

    [Fact]
    public async Task The_server_mode_offers_no_sample_project_even_to_an_administrator()
    {
        var host = await hosts.OfAsync(EngineKind.SqlServer);
        using var admin = host.As(Logins.Admin);
        (await admin.PostAsync(Route)).Refused(404, "INTROUVABLE");
        Assert.DoesNotContain((await admin.GetAsync("/api/projects")).Ok().EnumerateArray(), item => item.GetProperty("code").GetString() == "CONGES");
        Assert.False((await admin.GetAsync("/api/me")).Ok().GetProperty("desktop").GetBoolean());
    }
}
