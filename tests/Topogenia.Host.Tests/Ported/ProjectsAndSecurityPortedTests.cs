// ProjectsAndSecurityPortedTests.cs
// The 1.x integration tests of the profile, the projects, the members and the security ported to the whole host on both databases (T-PSEC).

using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

public sealed class ProjectsAndSecurityPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    /// <summary>A login never seen by the host, in the form `DOMAIN\account`.</summary>
    private static string NewLogin(string prefix) => $@"TEST\{prefix}{Guid.NewGuid():N}"[..(5 + prefix.Length + 12)];

    private static string AccountOf(string login) => login.Split('\\')[1];

    private static List<string?> CodesOf(JsonElement list) => [.. list.EnumerateArray().Select(item => item.GetProperty("code").GetString())];

    private static List<string?> LoginsOf(JsonElement members) => [.. members.EnumerateArray().Select(member => member.GetProperty("login").GetString())];

    private static string Message(ApiResponse response) => response.Json.GetProperty("message").GetString()!;

    /// <summary>The members of the project without its creator, so that the administrator acts as administrator only.</summary>
    private static async Task WithoutAdministratorAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        var members = (await facilitator.PutAsync($"/api/projects/{project.Id}/members", new object[]
        {
            new { login = Logins.Facilitator, role = "facilitator", is_sponsor = true },
            new { login = Logins.Contributor, role = "contributor", is_sponsor = false },
            new { login = Logins.Reader, role = "reader", is_sponsor = false },
        }, await project.ProjectVersionAsync())).Ok().GetProperty("members");
        Assert.DoesNotContain(Logins.Admin, LoginsOf(members));
    }

    /// <summary>
    /// A client of the host without the anti-CSRF header that <see cref="ApiClient"/> adds to every write: the server in
    /// memory of the host, called at the address of its mode.
    /// </summary>
    private static HttpClient RawClient(ApiHost host, string login)
    {
        var server = (Microsoft.AspNetCore.TestHost.TestServer)host.Services.GetRequiredService<IServer>();
        var client = server.CreateClient();
        client.BaseAddress = OriginOf(host);
        client.DefaultRequestHeaders.Add(TestAuthentication.Header, login);
        return client;
    }

    private static Uri OriginOf(ApiHost host) =>
        host.Kind == EngineKind.Sqlite ? new Uri($"http://127.0.0.1:{ApiHost.DesktopPort}") : new Uri($"https://{ApiHost.ServerHost}");

    // T-PSEC-01
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_first_request_of_a_login_creates_his_profile_from_the_directory_and_the_next_one_only_reads_it(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var login = NewLogin("new");
        using var newcomer = host.As(login);
        var first = (await newcomer.GetAsync("/api/me")).Ok();
        Assert.Equal((login, AccountOf(login), JsonValueKind.Null, false, false, 0),
            (first.GetProperty("login").GetString(), first.GetProperty("display_name").GetString(), first.GetProperty("language").ValueKind,
                first.GetProperty("is_admin").GetBoolean(), first.GetProperty("first_visit_done").GetBoolean(), first.GetProperty("roles").GetArrayLength()));

        var again = (await newcomer.GetAsync("/api/me")).Ok();
        var id = first.GetProperty("id").GetGuid();
        Assert.Equal((id, first.GetProperty("version").GetInt32()), (again.GetProperty("id").GetGuid(), again.GetProperty("version").GetInt32()));
        using var admin = host.As(Logins.Admin);
        var created = (await admin.GetAsync($"/api/admin/audit?user_id={id}&kind={AuditKinds.UserCreated}")).Ok();
        Assert.Equal(id, Assert.Single(created.EnumerateArray()).GetProperty("user_id").GetGuid());
    }

    // T-PSEC-02
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_language_and_the_first_visit_are_remembered_and_a_partial_patch_keeps_the_other_fields(EngineKind kind)
    {
        using var user = (await hosts.OfAsync(kind)).As(NewLogin("lang"));
        var read = (await user.GetAsync("/api/me")).Ok();
        var changed = (await user.PatchAsync("/api/me", new { language = "en", first_visit_done = true }, read.GetProperty("version").GetInt32())).Ok();
        Assert.Equal(("en", true), (changed.GetProperty("language").GetString(), changed.GetProperty("first_visit_done").GetBoolean()));

        var renamed = (await user.PatchAsync("/api/me", new { display_name = "Isabelle" }, changed.GetProperty("version").GetInt32())).Ok();
        Assert.Equal(("en", "Isabelle", true),
            (renamed.GetProperty("language").GetString(), renamed.GetProperty("display_name").GetString(), renamed.GetProperty("first_visit_done").GetBoolean()));
        var reread = (await user.GetAsync("/api/me")).Ok();
        Assert.Equal(("en", "Isabelle", true),
            (reread.GetProperty("language").GetString(), reread.GetProperty("display_name").GetString(), reread.GetProperty("first_visit_done").GetBoolean()));
    }

    // T-PSEC-05
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_new_project_has_its_root_map_its_first_lane_coded_and_coloured_and_its_creator_as_facilitator(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var detail = (await admin.GetAsync($"/api/projects/{project.Id}")).Ok();
        Assert.Equal((project.Code, "facilitator", 4), (detail.GetProperty("code").GetString(), detail.GetProperty("role").GetString(), detail.GetProperty("members").GetArrayLength()));
        var creator = detail.GetProperty("members").EnumerateArray().Single(member => member.GetProperty("login").GetString() == Logins.Admin);
        Assert.Equal("facilitator", creator.GetProperty("role").GetString());
        var sponsor = detail.GetProperty("members").EnumerateArray().Single(member => member.GetProperty("is_sponsor").GetBoolean());
        Assert.Equal(Logins.Facilitator, sponsor.GetProperty("login").GetString());

        var map = (await admin.GetAsync($"/api/maps/{project.RootMapId}")).Ok().GetProperty("map");
        Assert.Equal(($"Projet {project.Code}", JsonValueKind.Null, 1, 0),
            (map.GetProperty("name").GetString(), map.GetProperty("parent_step_id").ValueKind, map.GetProperty("depth").GetInt32(), map.GetProperty("steps").GetArrayLength()));
        var lane = Assert.Single(map.GetProperty("lanes").EnumerateArray());
        Assert.Equal(("Acteur 1", $"{project.Code}-C01", "#F9DCE6"),
            (lane.GetProperty("name").GetString(), lane.GetProperty("code").GetString(), lane.GetProperty("color").GetString()));
    }

    // T-PSEC-06
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_first_lane_of_an_english_project_is_named_in_english(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        var created = (await admin.PostAsync("/api/projects", new { code = ApiProject.NewCode(), name = "Sales", source_language = "en" })).Ok(201);
        var map = (await admin.GetAsync($"/api/maps/{created.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map");
        Assert.Equal("Actor 1", Assert.Single(map.GetProperty("lanes").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("en", map.GetProperty("source_language").GetString());
    }

    // T-PSEC-07
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_translation_of_the_name_is_kept_apart_from_the_language_of_origin_and_copied_to_the_root_map_and_the_breadcrumb(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        var response = await admin.PostAsync("/api/projects", new
        {
            code = ApiProject.NewCode(),
            name = "Processus ventes",
            source_language = "fr",
            translations = new Dictionary<string, string> { ["en"] = "Sales process", ["fr"] = "ignoré" },
        });
        var created = response.Ok(201);
        var detail = (await admin.GetAsync(response.Location!.OriginalString)).Ok();
        Assert.Equal("Processus ventes", detail.GetProperty("name").GetString());
        Assert.Equal([("en", "Sales process")],
            detail.GetProperty("name_translations").EnumerateObject().Select(translation => (translation.Name, translation.Value.GetString())));

        var map = (await admin.GetAsync($"/api/maps/{created.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map");
        Assert.Equal("Sales process", map.GetProperty("translations").GetProperty("en").GetString());
        Assert.Equal("Sales process", map.GetProperty("breadcrumb")[0].GetProperty("translations").GetProperty("en").GetString());
    }

    // T-PSEC-08
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_code_out_of_its_format_is_refused_and_nothing_is_created(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        foreach (var code in new[] { "ab", "abc", "ABCDEFG", "AB1", "" })
            (await admin.PostAsync("/api/projects", new { code, name = "Nom" })).Refused(400, "PROJET_CODE_INVALIDE");
        Assert.DoesNotContain("ABCDEFG", CodesOf((await admin.GetAsync("/api/projects")).Ok()));
    }

    // T-PSEC-09
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_code_already_taken_is_a_conflict_whose_message_names_the_code(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var refused = await admin.PostAsync("/api/projects", new { code = project.Code, name = "Autre" });
        refused.Refused(409, "PROJET_CODE_EXISTANT");
        Assert.Contains(project.Code, Message(refused), StringComparison.Ordinal);
        Assert.Equal(1, CodesOf((await admin.GetAsync("/api/projects")).Ok()).Count(code => code == project.Code));
    }

    // T-PSEC-11
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_name_of_a_project_is_required_and_without_html(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        var (blank, html) = (ApiProject.NewCode(), ApiProject.NewCode());
        Assert.Equal("name", (await admin.PostAsync("/api/projects", new { code = blank, name = " " })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        (await admin.PostAsync("/api/projects", new { code = html, name = "<b>x</b>" })).Refused(400, "TEXTE_HTML_INTERDIT");
        var codes = CodesOf((await admin.GetAsync("/api/projects")).Ok());
        Assert.DoesNotContain(blank, codes);
        Assert.DoesNotContain(html, codes);
    }

    // T-PSEC-12
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_list_holds_the_projects_of_the_member_only_and_every_project_for_an_administrator_who_is_no_member(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        using var contributor = project.Contributor;
        var alone = (await admin.PostAsync("/api/projects", new { code = ApiProject.NewCode(), name = "Seul" })).Ok(201).GetProperty("code").GetString();
        await WithoutAdministratorAsync(project);

        var listed = (await contributor.GetAsync("/api/projects")).Ok();
        Assert.Equal("contributor", listed.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == project.Id).GetProperty("role").GetString());
        Assert.DoesNotContain(alone, CodesOf(listed));
        var all = CodesOf((await admin.GetAsync("/api/projects")).Ok());
        Assert.Contains(project.Code, all);
        Assert.Contains(alone, all);
    }

    // T-PSEC-14
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_facilitator_replaces_the_members_a_removed_member_loses_the_project_and_an_invited_user_reaches_it_at_his_first_visit(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        var invited = NewLogin("inv");
        var url = $"/api/projects/{project.Id}/members";
        var version = await project.ProjectVersionAsync();
        (await facilitator.PutAsync(url, new object[] { new { login = invited, role = "contributor", is_sponsor = false } }, version))
            .Refused(400, "PROJET_SANS_ANIMATEUR");

        var replaced = (await facilitator.PutAsync(url, new object[]
        {
            new { login = Logins.Facilitator, role = "facilitator", is_sponsor = false },
            new { login = invited, role = "contributor", is_sponsor = false, is_key_contributor = true },
        }, version)).Ok().GetProperty("members");
        Assert.Equal([Logins.Facilitator, invited], LoginsOf(replaced));
        var member = replaced.EnumerateArray().Single(item => item.GetProperty("login").GetString() == invited);
        Assert.Equal((true, AccountOf(invited)), (member.GetProperty("is_key_contributor").GetBoolean(), member.GetProperty("display_name").GetString()));

        (await contributor.GetAsync($"/api/projects/{project.Id}")).Refused(404, "INTROUVABLE");
        using var newcomer = project.Host.As(invited);
        Assert.Equal("contributor", (await newcomer.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("role").GetString());
    }

    // T-PSEC-15
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_unknown_role_a_login_without_domain_or_a_duplicate_refuses_the_list_without_changing_anything(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = $"/api/projects/{project.Id}/members";
        var newcomer = NewLogin("dup");
        var keep = new { login = Logins.Facilitator, role = "facilitator", is_sponsor = true };
        var version = await project.ProjectVersionAsync();
        (await facilitator.PutAsync(url, new object[] { keep, new { login = newcomer, role = "chef", is_sponsor = false } }, version)).Refused(400, "MEMBRE_ROLE_INVALIDE");
        (await facilitator.PutAsync(url, new object[] { keep, new { login = "sansdomaine", role = "reader", is_sponsor = false } }, version)).Refused(400, "MEMBRE_LOGIN_INVALIDE");
        (await facilitator.PutAsync(url, new object[]
        {
            keep, new { login = newcomer, role = "reader", is_sponsor = false }, new { login = $@"test\{AccountOf(newcomer).ToUpperInvariant()}", role = "contributor", is_sponsor = false },
        }, version)).Refused(400, "MEMBRE_DOUBLON");

        Assert.Equal(4, (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("members").GetArrayLength());
        var accepted = (await facilitator.PutAsync(url, new object[] { keep, new { login = newcomer, role = "reader", is_sponsor = false } }, version)).Ok();
        var created = accepted.GetProperty("members").EnumerateArray().Single(member => member.GetProperty("login").GetString() == newcomer).GetProperty("user_id").GetGuid();
        Assert.Equal([created], accepted.GetProperty("invited_user_ids").EnumerateArray().Select(id => id.GetGuid()));
    }

    // T-PSEC-16
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Neither_a_contributor_nor_a_reader_manages_the_members(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        foreach (var client in new[] { contributor, reader })
            (await client.PutAsync($"/api/projects/{project.Id}/members", Array.Empty<object>())).Refused(403, "ACCES_REFUSE");
        using var facilitator = project.Facilitator;
        Assert.Equal(4, (await facilitator.GetAsync($"/api/projects/{project.Id}/participants")).Ok().GetArrayLength());
    }

    // T-PSEC-19
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_write_without_the_anti_csrf_header_or_from_another_origin_writes_nothing_and_a_read_needs_no_header(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var url = $"/api/maps/{project.RootMapId}/lanes";
        using (var raw = RawClient(project.Host, Logins.Facilitator))
        {
            using var body = new StringContent("""{"name":"Pirate"}""", System.Text.Encoding.UTF8, "application/json");
            using var write = await raw.PostAsync(url, body, TestContext.Current.CancellationToken);
            var refused = JsonDocument.Parse(await write.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
            Assert.Equal((400, "EN_TETE_MANQUANT"), ((int)write.StatusCode, refused.GetProperty("code").GetString()));
            using var read = await raw.GetAsync($"/api/maps/{project.RootMapId}", TestContext.Current.CancellationToken);
            Assert.Equal(200, (int)read.StatusCode);
        }
        using var facilitator = project.Facilitator;
        (await facilitator.SendAsync(HttpMethod.Post, url, new { name = "Pirate" }, null, ("Origin", "http://attacker.example"))).Refused(403, "ACCES_REFUSE");
        (await facilitator.SendAsync(HttpMethod.Post, url, new { name = "Pirate" }, null, ("Sec-Fetch-Site", "cross-site"))).Refused(403, "ACCES_REFUSE");
        Assert.Single((await project.MapAsync()).GetProperty("lanes").EnumerateArray());

        var origin = OriginOf(project.Host).GetLeftPart(UriPartial.Authority);
        (await facilitator.SendAsync(HttpMethod.Post, url, new { name = "Achats" }, null, ("Origin", origin), ("Sec-Fetch-Site", "same-origin"))).Ok();
        Assert.Equal(2, (await project.MapAsync()).GetProperty("lanes").GetArrayLength());
    }

    // T-PSEC-20
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Contributor_and_reader_change_no_structure_a_stranger_is_told_not_found_and_an_administrator_has_every_right(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        using var admin = project.Admin;
        var steps = $"/api/maps/{project.RootMapId}/steps";
        var step = new { lane_id = project.FirstLaneId, column = 0, label = "x" };
        foreach (var (client, role) in new[] { (contributor, "contributor"), (reader, "reader") })
        {
            (await client.PostAsync(steps, step)).Refused(403, "ACCES_REFUSE");
            (await client.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "x" })).Refused(403, "ACCES_REFUSE");
            var rights = (await client.GetAsync($"/api/maps/{project.RootMapId}")).Ok().GetProperty("map").GetProperty("rights");
            Assert.Equal((false, role), (rights.GetProperty("structure").GetBoolean(), rights.GetProperty("role").GetString()));
        }
        (await stranger.PostAsync(steps, step)).Refused(404, "INTROUVABLE");
        Assert.True((await project.MapAsync()).GetProperty("rights").GetProperty("structure").GetBoolean());
        Assert.Empty((await project.MapAsync()).GetProperty("steps").EnumerateArray());

        await WithoutAdministratorAsync(project);
        var created = (await admin.PostAsync(steps, new { lane_id = project.FirstLaneId, column = 0, label = "Par l'administrateur" })).Ok();
        Assert.True(created.GetProperty("map").GetProperty("rights").GetProperty("structure").GetBoolean());
        Assert.Equal(["Par l'administrateur"], (await project.MapAsync()).GetProperty("steps").EnumerateArray().Select(item => item.GetProperty("label").GetString()));
    }

    // T-PSEC-23
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_unknown_identifier_is_not_found_on_a_read_and_on_a_write(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        (await facilitator.GetAsync($"/api/maps/{Guid.NewGuid()}")).Refused(404, "INTROUVABLE");
        (await facilitator.PatchAsync($"/api/steps/{Guid.NewGuid()}", new { label = "x" }, 1)).Refused(404, "INTROUVABLE");
        (await facilitator.GetAsync($"/api/projects/{Guid.NewGuid()}")).Refused(404, "INTROUVABLE");
        (await facilitator.DeleteAsync($"/api/flows/{Guid.NewGuid()}")).Refused(404, "INTROUVABLE");
    }
}
