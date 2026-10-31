// PmapRoutesTests.cs
// The .pmap routes through the whole host on both databases (ADR 054, A-1.2-15 to 22): opening, faults in two languages, limit, copy, duplication.

using System.Text.Json;
using Topogenia.Host.Api.Exchange;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Exchange;

public sealed class PmapRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static async Task<JsonElement> OpenAsync(ApiClient admin, string content, string? code = null, string? name = null)
    {
        var reply = await admin.PostAsync("/api/projects/import-pmap", new { content, code = code ?? ApiProject.NewCode(), name });
        var opened = reply.Ok(201);
        Assert.Equal($"/api/projects/{opened.GetProperty("id").GetGuid()}", reply.Location!.OriginalString);
        return opened;
    }

    private static async Task<JsonElement> RootMapAsync(ApiClient client, JsonElement opened) =>
        (await client.GetAsync($"/api/maps/{opened.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map");

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_administrator_opens_a_file_with_its_sheets_as_drafts_and_gets_its_counts_and_uncertainties(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;

        var opened = await OpenAsync(admin, TestPmaps.WithSheet);

        Assert.Equal((1, 2, 2, 1, 2), (opened.GetProperty("maps").GetInt32(), opened.GetProperty("lanes").GetInt32(), opened.GetProperty("steps").GetInt32(),
            opened.GetProperty("flows").GetInt32(), opened.GetProperty("sheets").GetInt32()));
        Assert.Equal(["Le délai de 8 heures est à confirmer."], opened.GetProperty("uncertainties").EnumerateArray().Select(text => text.GetString()));
        var map = await RootMapAsync(admin, opened);
        var steps = map.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(["Saisir la demande", "Passer la commande"], steps.Select(step => step.GetProperty("label").GetString()));
        var sheet = (await admin.GetAsync($"/api/steps/{steps[0].GetProperty("id").GetGuid()}/sheet")).Ok();
        Assert.Equal("draft", sheet.GetProperty("status").GetString());
        var detail = (await admin.GetAsync($"/api/projects/{opened.GetProperty("id").GetGuid()}")).Ok();
        var member = Assert.Single(detail.GetProperty("members").EnumerateArray());
        Assert.Equal((Logins.Admin, "facilitator"), (member.GetProperty("login").GetString(), member.GetProperty("role").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_file_with_faults_is_refused_with_each_fault_at_its_place_in_the_language_of_the_user_and_nothing_is_written(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var before = (await admin.GetAsync("/api/projects")).Ok().GetArrayLength();
        var faulty = TestPmaps.Minimal.Replace("\"lane\": \"c2\", \"label\": \"Passer la commande\"", "\"lane\": \"c9\", \"label\": \"Passer la commande\"",
            StringComparison.Ordinal);

        var french = await admin.SendAsync(HttpMethod.Post, "/api/projects/import-pmap", new { content = faulty, code = ApiProject.NewCode() }, null,
            ("Accept-Language", "fr-FR"));
        var english = await admin.SendAsync(HttpMethod.Post, "/api/projects/import-pmap", new { content = faulty, code = ApiProject.NewCode() }, null,
            ("Accept-Language", "en-GB"));

        var details = french.Refused(400, "FICHIER_INVALIDE");
        Assert.Equal(1, details.GetProperty("count").GetInt32());
        var fault = Assert.Single(details.GetProperty("errors").EnumerateArray());
        Assert.Equal(("map.steps[1].lane", "PMAP_COULOIR_INCONNU"), (fault.GetProperty("path").GetString(), fault.GetProperty("code").GetString()));
        Assert.Equal("Le couloir « c9 » n'existe pas dans cette carte.", fault.GetProperty("message").GetString());
        Assert.Equal("Lane \"c9\" does not exist in this map.", english.Refused(400, "FICHIER_INVALIDE").GetProperty("errors")[0].GetProperty("message").GetString());
        Assert.Equal(before, (await admin.GetAsync("/api/projects")).Ok().GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_code_and_the_name_entered_win_and_a_code_in_use_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var code = ApiProject.NewCode();

        var opened = await OpenAsync(admin, TestPmaps.WithSheet, code, "Commandes");

        var detail = (await admin.GetAsync($"/api/projects/{opened.GetProperty("id").GetGuid()}")).Ok();
        Assert.Equal((code, "Commandes"), (detail.GetProperty("code").GetString(), detail.GetProperty("name").GetString()));
        Assert.Empty(detail.GetProperty("name_translations").EnumerateObject());
        (await admin.PostAsync("/api/projects/import-pmap", new { content = TestPmaps.Minimal, code })).Refused(409, "PROJET_CODE_EXISTANT");
        (await admin.PostAsync("/api/projects/import-pmap", new { content = TestPmaps.Minimal })).Refused(400, "PROJET_CODE_INVALIDE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_opening_longer_than_ten_megabytes_is_refused_413_before_it_is_read_and_a_content_of_8_million_characters_400(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;

        var refused = await admin.SendAsync(HttpMethod.Post, "/api/projects/import-pmap", new string(' ', (int)PmapApi.OpeningLimit + 1), null);

        Assert.Equal(10, refused.Refused(413, "REQUETE_TROP_GROSSE").GetProperty("max").GetInt32());
        // Under the limit of the body, a content beyond 8 million characters is refused as the 1.x did (FICHIER_TROP_GROS).
        var content = new string(' ', 8 * 1024 * 1024 + 1);
        Assert.Equal(8, (await admin.PostAsync("/api/projects/import-pmap", new { content })).Refused(400, "FICHIER_TROP_GROS").GetProperty("max").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_reader_downloads_a_copy_named_by_the_rule_of_the_export_that_opens_again_as_the_same_project(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var opened = await OpenAsync(admin, TestPmaps.WithSheet);
        var id = opened.GetProperty("id").GetGuid();
        var members = new object[]
        {
            new { login = Logins.Admin, role = "facilitator", is_sponsor = false },
            new { login = Logins.Reader, role = "reader", is_sponsor = false },
        };
        (await admin.PutAsync($"/api/projects/{id}/members", members, (await admin.GetAsync($"/api/projects/{id}")).Ok().GetProperty("version").GetInt32())).Ok();
        using var reader = project.Reader;

        var copy = await reader.GetAsync($"/api/projects/{id}/pmap");
        var withSheets = await reader.GetAsync($"/api/projects/{id}/pmap?with_sheets=true");

        Assert.Equal(200, copy.Status);
        Assert.StartsWith("text/plain", copy.Headers["Content-Type"], StringComparison.Ordinal);
        Assert.Matches($"filename={opened.GetProperty("code").GetString()}_Achats_\\d{{4}}-\\d{{2}}-\\d{{2}}\\.pmap", copy.Headers["Content-Disposition"]);
        Assert.DoesNotContain("\"sheet\"", copy.Body, StringComparison.Ordinal);
        // The reader sees no draft: the sheet of the first step was never validated; its output and the use stay (visible to every member).
        Assert.DoesNotContain("Exprimer le besoin", withSheets.Body, StringComparison.Ordinal);
        Assert.Contains("\"pain_point\": \"La demande arrive incomplète.\"", withSheets.Body, StringComparison.Ordinal);
        var again = await OpenAsync(admin, copy.Body);
        var first = await RootMapAsync(admin, opened);
        var second = await RootMapAsync(admin, again);
        Assert.Equal(first.GetProperty("steps").EnumerateArray().Select(step => (step.GetProperty("label").GetString(), step.GetProperty("column").GetInt32())),
            second.GetProperty("steps").EnumerateArray().Select(step => (step.GetProperty("label").GetString(), step.GetProperty("column").GetInt32())));
        (await reader.GetAsync($"/api/projects/{id}/pmap?with_sheets=oui")).Refused(400, "VALEUR_INVALIDE");
    }

    /// <summary>
    /// A project of 13 levels, each map holding a step: its copy writes 12 and says the cut by the header of the route
    /// (A-2.17-47, A-6-32), a project of fewer levels has no header; its duplication writes no file and keeps the 13 (AR-118).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_copy_of_a_project_of_thirteen_levels_says_its_cut_and_its_duplication_keeps_every_level(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        using var reader = project.Reader;
        var (mapId, laneId) = (project.RootMapId, project.FirstLaneId);
        for (var level = 1; level <= 12; level++)
        {
            var step = await project.AddStepAsync(0, $"Niveau {level}", laneId, mapId);
            (mapId, laneId) = await project.DetailAsync(step.Id, mapId);
        }
        var shallow = await reader.GetAsync($"/api/projects/{project.Id}/pmap");
        await project.AddStepAsync(0, "Niveau 13", laneId, mapId);

        var copy = await reader.GetAsync($"/api/projects/{project.Id}/pmap");
        var duplicated = await admin.PostAsync($"/api/projects/{project.Id}/duplicate", new { code = ApiProject.NewCode() });

        Assert.Equal(200, copy.Status);
        Assert.Equal("PMAP_PROFONDEUR_COUPEE", copy.Headers[PmapApi.WarningHeader]);
        Assert.Contains("Niveau 12", copy.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Niveau 13", copy.Body, StringComparison.Ordinal);
        Assert.False(shallow.Headers.ContainsKey(PmapApi.WarningHeader));
        Assert.Equal(13, duplicated.Ok(201).GetProperty("maps").GetInt32());
        Assert.False(duplicated.Headers.ContainsKey(PmapApi.WarningHeader));
    }

    /// <summary>
    /// The bound of a duplication is the one of the paths of the domain, 51 maps (AR-118, AR-67): a project of 51 levels
    /// is duplicated whole and without header; one level more, which the domain admits, is cut and says so by the header.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_duplication_keeps_fifty_one_levels_and_says_its_cut_beyond(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var (mapId, laneId) = (project.RootMapId, project.FirstLaneId);
        Guid deepest = default;
        for (var level = 1; level <= 51; level++)
        {
            deepest = (await project.AddStepAsync(0, $"Niveau {level}", laneId, mapId)).Id;
            if (level < 51)
                (mapId, laneId) = await project.DetailAsync(deepest, mapId);
        }

        var whole = await admin.PostAsync($"/api/projects/{project.Id}/duplicate", new { code = ApiProject.NewCode() });
        (mapId, laneId) = await project.DetailAsync(deepest, mapId);
        await project.AddStepAsync(0, "Niveau 52", laneId, mapId);
        var cut = await admin.PostAsync($"/api/projects/{project.Id}/duplicate", new { code = ApiProject.NewCode() });

        Assert.Equal(51, whole.Ok(201).GetProperty("maps").GetInt32());
        Assert.False(whole.Headers.ContainsKey(PmapApi.WarningHeader));
        Assert.Equal(51, cut.Ok(201).GetProperty("maps").GetInt32());
        Assert.Equal("PMAP_PROFONDEUR_COUPEE", cut.Headers[PmapApi.WarningHeader]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_duplication_copies_the_maps_and_the_translations_of_the_steps_without_the_sheets(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var opened = await OpenAsync(admin, TestPmaps.WithSheet);
        var code = ApiProject.NewCode();

        var reply = await admin.PostAsync($"/api/projects/{opened.GetProperty("id").GetGuid()}/duplicate", new { code });

        var copy = reply.Ok(201);
        Assert.Equal((code, 0), (copy.GetProperty("code").GetString(), copy.GetProperty("sheets").GetInt32()));
        var text = (await admin.GetAsync($"/api/projects/{copy.GetProperty("id").GetGuid()}/pmap?with_sheets=true")).Body;
        Assert.Contains("\"en\": \"Enter the request\"", text, StringComparison.Ordinal);
        Assert.Contains("\"en\": \"valid\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"sheet\"", text, StringComparison.Ordinal);
        (await admin.PostAsync($"/api/projects/{opened.GetProperty("id").GetGuid()}/duplicate", new { code })).Refused(409, "PROJET_CODE_EXISTANT");
    }
}
