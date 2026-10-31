// ProjectRoutesTests.cs
// The routes of the profile and of the projects through the whole host on both databases (annex A 1 moi et projets, A 2.1, spec 4.1, 5.2).

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Projects;

public sealed class ProjectRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static IEnumerable<string?> Codes(JsonElement list) => list.EnumerateArray().Select(item => item.GetProperty("code").GetString());

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_administrator_creates_a_project_answered_201_with_its_location_and_its_root_map(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        var code = ApiProject.NewCode();
        var created = await admin.PostAsync("/api/projects", new { code, name = "Achats fournisseurs", description = "De la demande au paiement" });
        var body = created.Ok(201);
        Assert.Equal($"/api/projects/{body.GetProperty("id").GetGuid()}", created.Location!.OriginalString);
        Assert.Equal(code, body.GetProperty("code").GetString());
        var detail = (await admin.GetAsync(created.Location.OriginalString)).Ok();
        Assert.Equal((body.GetProperty("root_map_id").GetGuid(), "Achats fournisseurs", "internal"),
            (detail.GetProperty("root_map_id").GetGuid(), detail.GetProperty("name").GetString(), detail.GetProperty("classification").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Only_an_administrator_creates_a_project_and_its_code_is_checked(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        (await facilitator.PostAsync("/api/projects", new { code = ApiProject.NewCode(), name = "Ventes" })).Refused(403, "ACCES_REFUSE");
        (await admin.PostAsync("/api/projects", new { code = "ab1", name = "Ventes" })).Refused(400, "PROJET_CODE_INVALIDE");
        (await admin.PostAsync("/api/projects", new { code = project.Code, name = "Ventes" })).Refused(409, "PROJET_CODE_EXISTANT");
        Assert.Equal("name", (await admin.PostAsync("/api/projects", new { code = ApiProject.NewCode() })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_list_shows_the_projects_of_the_member_with_his_role_and_every_project_to_the_administrator(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        var other = await ApiProject.CreateAsync(project.Host);
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        using var admin = project.Admin;
        var listed = (await reader.GetAsync("/api/projects")).Ok().EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == project.Id);
        Assert.Equal(("reader", 0, 1), (listed.GetProperty("role").GetString(), listed.GetProperty("step_count").GetInt32(), listed.GetProperty("map_count").GetInt32()));
        Assert.DoesNotContain(project.Code, Codes((await stranger.GetAsync("/api/projects")).Ok()));
        var all = Codes((await admin.GetAsync("/api/projects")).Ok()).ToList();
        Assert.Contains(project.Code, all);
        Assert.Contains(other.Code, all);
        Assert.Equal(all.Order(StringComparer.Ordinal), all);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_detail_of_a_project_gives_its_members_and_version_and_hides_the_project_from_a_stranger(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        var detail = (await reader.GetAsync($"/api/projects/{project.Id}")).Ok();
        Assert.Equal((project.RootMapId, "reader"), (detail.GetProperty("root_map_id").GetGuid(), detail.GetProperty("role").GetString()));
        var members = detail.GetProperty("members").EnumerateArray().ToList();
        Assert.Equal(["admin", "anne", "lea", "paul"], members.Select(member => member.GetProperty("display_name").GetString()).Order(StringComparer.Ordinal));
        var anne = members.Single(member => member.GetProperty("display_name").GetString() == "anne");
        Assert.Equal(("facilitator", true), (anne.GetProperty("role").GetString(), anne.GetProperty("is_sponsor").GetBoolean()));
        Assert.Equal(JsonValueKind.Number, detail.GetProperty("version").ValueKind);
        (await stranger.GetAsync($"/api/projects/{project.Id}")).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_label_of_the_sharing_group_is_changed_by_the_facilitator_with_the_version_of_the_project(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        var url = $"/api/projects/{project.Id}/sharing-group-label";
        var version = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("version").GetInt32();
        (await contributor.PutAsync(url, new { label = "Equipe achats" })).Refused(403, "ACCES_REFUSE");
        var changed = (await facilitator.PutAsync(url, new { label = "Equipe achats" }, version)).Ok();
        Assert.Equal("Equipe achats", changed.GetProperty("sharing_group_label").GetString());
        (await facilitator.PutAsync(url, new { label = "Equipe ventes" }, version)).Refused(409, "CONFLIT_VERSION");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_classification_is_changed_by_the_facilitator_with_the_version_of_the_project_and_read_by_every_member_in_the_detail_and_the_list(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var url = $"/api/projects/{project.Id}/classification";
        var detail = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok();
        Assert.Equal("internal", detail.GetProperty("classification").GetString());
        var version = detail.GetProperty("version").GetInt32();
        (await contributor.PutAsync(url, new { classification = "confidential" }, version)).Refused(403, "ACCES_REFUSE");
        (await reader.PutAsync(url, new { classification = "confidential" }, version)).Refused(403, "ACCES_REFUSE");
        Assert.Equal("classification", (await facilitator.PutAsync(url, new { classification = "secret" }, version)).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        Assert.Equal("classification", (await facilitator.PutAsync(url, new { }, version)).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        var changed = (await facilitator.PutAsync(url, new { classification = "strictly_confidential" }, version)).Ok();
        Assert.Equal(("strictly_confidential", version + 1), (changed.GetProperty("classification").GetString(), changed.GetProperty("version").GetInt32()));
        Assert.Equal("strictly_confidential", (await reader.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("classification").GetString());
        var listed = (await reader.GetAsync("/api/projects")).Ok().EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == project.Id);
        Assert.Equal("strictly_confidential", listed.GetProperty("classification").GetString());
        (await facilitator.PutAsync(url, new { classification = "internal" }, version)).Refused(409, "CONFLIT_VERSION");
        using var stranger = project.Stranger;
        (await stranger.PutAsync(url, new { classification = "internal" }, version + 1)).Refused(404, "INTROUVABLE");
        Assert.Equal("strictly_confidential", (await reader.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("classification").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_change_of_the_classification_is_audited_with_both_levels_and_the_same_level_again_is_not(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var url = $"/api/projects/{project.Id}/classification";
        var version = await project.ProjectVersionAsync();
        (await facilitator.PutAsync(url, new { classification = "confidential" }, version)).Ok();
        (await facilitator.PutAsync(url, new { classification = "confidential" }, version + 1)).Ok();
        var events = (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}&kind={AuditKinds.ClassificationChanged}")).Ok();
        var audit = Assert.Single(events.EnumerateArray());
        var facilitatorId = (await facilitator.GetAsync("/api/me")).Ok().GetProperty("id").GetGuid();
        Assert.Equal((facilitatorId, """{"from":"internal","to":"confidential"}"""),
            (audit.GetProperty("user_id").GetGuid(), audit.GetProperty("details").GetString()));
        Assert.Equal(version + 1, await project.ProjectVersionAsync());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_created_with_a_classification_keeps_it_in_its_detail_and_in_the_list(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        var code = ApiProject.NewCode();
        var created = (await admin.PostAsync("/api/projects", new { code, name = "Paie", classification = "strictly_confidential" })).Ok(201);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("strictly_confidential", (await admin.GetAsync($"/api/projects/{id}")).Ok().GetProperty("classification").GetString());
        var listed = (await admin.GetAsync("/api/projects")).Ok().EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);
        Assert.Equal("strictly_confidential", listed.GetProperty("classification").GetString());
        Assert.Equal("classification", (await admin.PostAsync("/api/projects", new { code = ApiProject.NewCode(), name = "Paie", classification = "Interne" }))
            .Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_profile_says_whether_the_host_runs_in_desktop_mode_on_each_of_its_replies(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        var desktop = kind == EngineKind.Sqlite;
        var me = (await contributor.GetAsync("/api/me")).Ok();
        Assert.Equal(desktop, me.GetProperty("desktop").GetBoolean());
        // A change that changes nothing still answers the profile; the profile of the other tests is left as it is.
        var unchanged = (await contributor.PatchAsync("/api/me", new { }, me.GetProperty("version").GetInt32())).Ok();
        Assert.Equal((desktop, me.GetProperty("version").GetInt32()), (unchanged.GetProperty("desktop").GetBoolean(), unchanged.GetProperty("version").GetInt32()));
        Assert.Equal(desktop, (await contributor.PutAsync("/api/me/last-project", new { project_id = project.Id })).Ok().GetProperty("desktop").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_glossary_of_the_translations_is_changed_by_the_facilitator_on_several_lines_and_read_by_every_member(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var url = $"/api/projects/{project.Id}/translation-glossary";
        const string Glossary = "clôture mensuelle = monthly closing\n\trapprochement = reconciliation";
        var detail = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok();
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("translation_glossary").ValueKind);
        var version = detail.GetProperty("version").GetInt32();
        (await contributor.PutAsync(url, new { glossary = Glossary }, version)).Refused(403, "ACCES_REFUSE");
        (await facilitator.PutAsync(url, new { glossary = "<b>closing</b>" }, version)).Refused(400, "TEXTE_HTML_INTERDIT");
        var changed = (await facilitator.PutAsync(url, new { glossary = Glossary + "\n" }, version)).Ok();
        Assert.Equal((Glossary, version + 1), (changed.GetProperty("translation_glossary").GetString(), changed.GetProperty("version").GetInt32()));
        Assert.Equal(Glossary, (await reader.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("translation_glossary").GetString());
        (await facilitator.PutAsync(url, new { glossary = "achat = purchase" }, version)).Refused(409, "CONFLIT_VERSION");
        var cleared = (await facilitator.PutAsync(url, new { glossary = (string?)null }, version + 1)).Ok();
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("translation_glossary").ValueKind);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_profile_gives_the_roles_in_the_live_projects_the_effective_language_and_the_configured_languages(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        var me = (await contributor.SendAsync(HttpMethod.Get, "/api/me", null, null, ("Accept-Language", "en"))).Ok();
        Assert.Equal((Logins.Contributor, "paul", "en", JsonValueKind.Null), (me.GetProperty("login").GetString(), me.GetProperty("display_name").GetString(),
            me.GetProperty("effective_language").GetString(), me.GetProperty("language").ValueKind));
        var role = me.GetProperty("roles").EnumerateArray().Single(item => item.GetProperty("project_id").GetGuid() == project.Id);
        Assert.Equal("contributor", role.GetProperty("role").GetString());
        Assert.False(me.GetProperty("is_admin").GetBoolean());
        Assert.Equal(["cs", "de", "en", "es", "fr", "it", "hu", "nl", "pl", "pt", "ro", "sv", "tr", "el", "ru", "uk", "zh", "ja"], me.GetProperty("languages").EnumerateArray().Select(language => language.GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_profile_changes_only_the_fields_sent_and_refuses_a_null_name_an_unknown_language_and_a_stale_version(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        var before = (await reader.GetAsync("/api/me")).Ok();
        var changed = (await reader.PatchAsync("/api/me", new { first_visit_done = true }, before.GetProperty("version").GetInt32())).Ok();
        Assert.Equal((true, before.GetProperty("display_name").GetString()), (changed.GetProperty("first_visit_done").GetBoolean(), changed.GetProperty("display_name").GetString()));
        var version = changed.GetProperty("version").GetInt32();
        Assert.Equal("display_name", (await reader.PatchAsync("/api/me", new { display_name = (string?)null }, version)).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        (await reader.PatchAsync("/api/me", new { language = "xx" }, version)).Refused(400, "LANGUE_INVALIDE");
        (await reader.PatchAsync("/api/me", new { display_name = "Léa" }, before.GetProperty("version").GetInt32())).Refused(409, "CONFLIT_VERSION");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_last_project_opened_is_remembered_for_a_member_only(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        var me = (await reader.PutAsync("/api/me/last-project", new { project_id = project.Id })).Ok();
        Assert.Equal(project.Id, me.GetProperty("last_project_id").GetGuid());
        (await stranger.PutAsync("/api/me/last-project", new { project_id = project.Id })).Refused(404, "INTROUVABLE");
        Assert.Equal("project_id", (await reader.PutAsync("/api/me/last-project", new { })).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
    }
}
