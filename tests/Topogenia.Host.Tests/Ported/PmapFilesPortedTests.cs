// PmapFilesPortedTests.cs
// The 1.x FichiersTests and BranchesTests.Fichier_pmap ported to the .pmap 1.1 routes on both databases (T-FICR-01 to 09, T-BRAN-05).

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Domain.Values;
using Topogenia.Host.Composition;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

public sealed partial class PmapFilesPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    [GeneratedRegex("```json\\n(?<json>.*?)\\n```", RegexOptions.Singleline)]
    private static partial Regex JsonBlock();

    private static string Repository(params string[] path) => RepositoryPaths.Text(path);

    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static async Task<JsonElement> OpenAsync(ApiClient admin, string content, string? code = null) =>
        (await admin.PostAsync("/api/projects/import-pmap", new { content, code = code ?? ApiProject.NewCode() })).Ok(201);

    private static async Task<JsonElement> MapAsync(ApiClient client, Guid mapId) => (await client.GetAsync($"/api/maps/{mapId}")).Ok().GetProperty("map");

    private static Guid RootOf(JsonElement opened) => opened.GetProperty("root_map_id").GetGuid();

    private static (int Maps, int Lanes, int Steps, int Flows) Counts(JsonElement opened) =>
        (opened.GetProperty("maps").GetInt32(), opened.GetProperty("lanes").GetInt32(), opened.GetProperty("steps").GetInt32(), opened.GetProperty("flows").GetInt32());

    /// <summary>T-FICR-01: the example of the guide for an AI opens as a project with its counts, its RACI and its sheets.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_example_of_the_guide_for_an_ai_opens_as_a_project_with_its_counts_and_its_raci(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var guide = JsonBlock().Matches(Repository("docs", "pmap", "PMAP_GUIDE_IA.md"))[0].Groups["json"].Value;

        var opened = await OpenAsync(admin, guide);

        Assert.Equal((2, 6, 17, 18), Counts(opened));
        Assert.Equal((2, 3), (opened.GetProperty("sheets").GetInt32(), opened.GetProperty("uncertainties").GetArrayLength()));
        var map = await MapAsync(admin, RootOf(opened));
        var pay = map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("label").GetString() == "Programmer le paiement").GetProperty("id").GetGuid();
        Assert.Equal(["A", "I"], map.GetProperty("raci").EnumerateArray().Where(role => role.GetProperty("step_id").GetGuid() == pay)
            .Select(role => role.GetProperty("role").GetString()).Order());
    }

    /// <summary>
    /// T-FICR-02: the complete example creates the project with its name, translation, description, uncertainties, lanes,
    /// types, codes in reading order, RACI and sub-map; the one who opens it is its only facilitator.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_complete_example_creates_every_content_and_the_opener_is_the_only_facilitator(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var code = ApiProject.NewCode();

        var opened = await OpenAsync(admin, Repository("samples", "cloture-mensuelle.pmap"), code);

        Assert.Equal((2, 5, 11, 10), Counts(opened));
        var detail = (await admin.GetAsync($"/api/projects/{opened.GetProperty("id").GetGuid()}")).Ok();
        Assert.Equal(("Clôture mensuelle des ventes", "Monthly sales closing"), (detail.GetProperty("name").GetString(),
            detail.GetProperty("name_translations").GetProperty("en").GetString()));
        Assert.StartsWith("Du chargement des ventes", detail.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Equal(["facilitator"], detail.GetProperty("members").EnumerateArray().Select(member => member.GetProperty("role").GetString()));
        var map = await MapAsync(admin, RootOf(opened));
        Assert.Equal(Enumerable.Range(1, 4).Select(n => $"{code}-C{n:D2}"), map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("code").GetString()));
        Assert.Equal(["department", "system", "department", "person"], map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("type").GetString()));
        var lanes = map.GetProperty("lanes").EnumerateArray().ToList();
        Assert.Equal("Controlling North", lanes[0].GetProperty("translations").GetProperty("en").GetString());
        // The file gives no colour: the lanes alternate as in the 1.x (`FichiersTests.cs:73`), the tints of the charter (AR-45).
        Assert.Equal(Enumerable.Range(0, 4).Select(LaneColors.ForIndex), lanes.Select(lane => lane.GetProperty("color").GetString()));
        var steps = map.GetProperty("steps").EnumerateArray().OrderBy(step => step.GetProperty("column").GetInt32()).ToList();
        Assert.Equal(Enumerable.Range(1, 8).Select(n => $"{code}-E{n:D3}"), steps.Select(step => step.GetProperty("code").GetString()));
        Assert.Equal(["start", "task", "task", "task", "decision", "task", "task", "end"], steps.Select(step => step.GetProperty("type").GetString()));
        Assert.Equal("automated", steps[1].GetProperty("automation_status").GetString());
        var gap = steps[4].GetProperty("id").GetGuid();
        Assert.Equal(["non", "oui"], map.GetProperty("flows").EnumerateArray().Where(flow => flow.GetProperty("source_step_id").GetGuid() == gap)
            .Select(flow => flow.GetProperty("label").GetString()).Order(StringComparer.Ordinal));
        var names = lanes.ToDictionary(lane => lane.GetProperty("id").GetGuid(), lane => lane.GetProperty("name").GetString());
        var table = steps[2].GetProperty("id").GetGuid();
        Assert.Equal(["A:Direction", "I:Contrôle de gestion Sud"], map.GetProperty("raci").EnumerateArray()
            .Where(role => role.GetProperty("step_id").GetGuid() == table)
            .Select(role => $"{role.GetProperty("role").GetString()}:{names[role.GetProperty("lane_id").GetGuid()]}").Order(StringComparer.Ordinal));
        Assert.False(steps[2].GetProperty("sub_map_empty").GetBoolean());
        var detailMap = await MapAsync(admin, steps[2].GetProperty("sub_map_id").GetGuid());
        Assert.Equal(("Construire le tableau croisé", table), (detailMap.GetProperty("name").GetString(), detailMap.GetProperty("parent_step_id").GetGuid()));
        Assert.Equal(["Importer l'extraction dans le tableur", "Mettre en forme le tableau", "Comparer au mois précédent"],
            detailMap.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("label").GetString()));
        Assert.Equal(["3.1", "3.2", "3.3"], detailMap.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("path").GetString()));
        Assert.Equal(Enumerable.Range(9, 3).Select(n => $"{code}-E{n:D3}"), detailMap.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("code").GetString()));
        Assert.Equal(2, detailMap.GetProperty("flows").GetArrayLength());
    }

    /// <summary>
    /// T-FICR-04, from a project drawn by the API as in the 1.x (`FichiersTests.cs:160`): its copy opened again gives the
    /// same lanes (name, type, colour, translation) and the same steps; the copy writes the steps in reading order, not
    /// in the order of their codes (A-2.17-44), so that the project opened again numbers them in that order.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_drawn_by_the_api_comes_back_from_its_copy_with_its_lanes_and_its_steps_in_reading_order(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var admin = project.Admin;
        var lanes = $"/api/maps/{project.RootMapId}/lanes";
        (await facilitator.PostAsync(lanes, new { name = "Comptabilité", type = "department", color = "#ffe0b2", translations = new { en = "Accounting" } })).Ok();
        var system = (await facilitator.PostAsync(lanes, new { name = "SAP", type = "system", color = "#C8E6C9" })).Ok().GetProperty("id").GetGuid();
        await project.AddStepAsync(1, "Créée en premier");
        await project.AddStepAsync(0, "Créée en second", system);

        using var reader = project.Reader;
        var copy = (await reader.GetAsync($"/api/projects/{project.Id}/pmap")).Body;
        var again = await OpenAsync(admin, copy, ApiProject.NewCode());

        Assert.True(copy.IndexOf("Créée en second", StringComparison.Ordinal) < copy.IndexOf("Créée en premier", StringComparison.Ordinal));
        static IEnumerable<(string?, string?, string?, string?)> Lanes(JsonElement map) => map.GetProperty("lanes").EnumerateArray().Select(lane =>
            (lane.GetProperty("name").GetString(), lane.GetProperty("type").GetString(), lane.GetProperty("color").GetString(),
                lane.GetProperty("translations").TryGetProperty("en", out var en) ? en.GetString() : null));
        static IEnumerable<(string?, string?, int)> Steps(JsonElement map) => map.GetProperty("steps").EnumerateArray()
            .Select(step => (step.GetProperty("label").GetString(), step.GetProperty("type").GetString(), step.GetProperty("column").GetInt32())).OrderBy(step => step.Item3);
        var before = await project.MapAsync();
        var after = await MapAsync(admin, RootOf(again));
        Assert.Contains(("Comptabilité", "department", "#FFE0B2", "Accounting"), Lanes(before));
        Assert.Equal(Lanes(before), Lanes(after));
        Assert.Equal(Steps(before), Steps(after));
        var code = again.GetProperty("code").GetString();
        Assert.Equal([($"{code}-E001", "Créée en second"), ($"{code}-E002", "Créée en premier")], after.GetProperty("steps").EnumerateArray()
            .Select(step => (step.GetProperty("code").GetString(), step.GetProperty("label").GetString())).Order());
    }

    /// <summary>T-FICR-03: a file without columns is placed by the flows.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_file_without_columns_is_placed_by_its_flows(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;

        var opened = await OpenAsync(admin, Repository("samples", "minimal.pmap"));

        var steps = (await MapAsync(admin, RootOf(opened))).GetProperty("steps").EnumerateArray();
        Assert.Equal([("Besoin identifié", 0), ("Saisir la demande", 1), ("Passer la commande", 2), ("Commande envoyée", 3)],
            steps.Select(step => (step.GetProperty("label").GetString()!, step.GetProperty("column").GetInt32())).OrderBy(pair => pair.Item2));
    }

    /// <summary>
    /// T-FICR-04, T-BRAN-05: a copy (any member, named by the rule of the export) opened again and copied gives the same
    /// file, its rows, gateways, translations of steps and flows kept; deleted objects are not in it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_copy_opened_again_and_copied_gives_the_same_file_with_its_rows_gateways_and_translations(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var guide = JsonBlock().Matches(Repository("docs", "pmap", "PMAP_GUIDE_IA.md"))[0].Groups["json"].Value;
        var code = ApiProject.NewCode();
        var opened = await OpenAsync(admin, guide, code);
        var root = RootOf(opened);
        var drawn = (await admin.PostAsync($"/api/maps/{root}/steps", new { lane_id = (await MapAsync(admin, root)).GetProperty("lanes")[0].GetProperty("id").GetGuid(),
            column = 11, label = "Étape supprimée" })).Ok();
        (await admin.PostAsync($"/api/maps/{root}/deletion", new { steps = new[] { drawn.GetProperty("id").GetGuid() }, stitch = false })).Ok();

        var first = await admin.GetAsync($"/api/projects/{opened.GetProperty("id").GetGuid()}/pmap");
        var again = await OpenAsync(admin, first.Body, code: ApiProject.NewCode());
        var second = (await admin.GetAsync($"/api/projects/{again.GetProperty("id").GetGuid()}/pmap")).Body;

        Assert.Matches($"filename={code}_Traitement-dune-facture-[a-z]+_\\d{{4}}-\\d{{2}}-\\d{{2}}\\.pmap", first.Headers["Content-Disposition"]);
        Assert.DoesNotContain("Étape supprimée", first.Body, StringComparison.Ordinal);
        Assert.Equal(Normalised(first.Body), Normalised(second));
        Assert.Contains("\"row\": 1", first.Body, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(first.Body, "\"type\": \"parallel\"").Count);
        Assert.Contains("\"en\": \"Enter the invoice\"", second, StringComparison.Ordinal);
        Assert.Contains("\"en\": \"yes\"", second, StringComparison.Ordinal);
    }

    /// <summary>The file without the codes of the project and the time of the copy, which differ between two projects.</summary>
    private static string Normalised(string text) =>
        Regex.Replace(Regex.Replace(text, "\"[A-Z]{3,6}-([CEI]\\d+(-S\\d+)?)\"", "\"$1\""), "\"(code|generated_at)\": \"[^\"]*\"", "\"$1\"");

    /// <summary>
    /// T-FICR-05: an invalid file lists its problems, localised, an unreadable JSON included, and creates nothing; a body
    /// beyond 10 MB is refused before it is read (5.4, 5.6 point 7).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_invalid_file_lists_its_problems_and_creates_nothing_and_a_body_too_long_is_refused_before_it_is_read(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var before = (await admin.GetAsync("/api/projects")).Ok().GetArrayLength();

        var unreadable = await admin.SendAsync(HttpMethod.Post, "/api/projects/import-pmap", new { content = "{ \"format\": ", code = ApiProject.NewCode() }, null,
            ("Accept-Language", "en"));
        var faulty = await admin.PostAsync("/api/projects/import-pmap", new { content = """{ "format": "topogenia", "version": "1.1", "project": {}, "map": { "lanes": [] } }""" });

        Assert.Equal("PMAP_JSON", unreadable.Refused(400, "FICHIER_INVALIDE").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.StartsWith("The content is not valid JSON", unreadable.Json.GetProperty("details").GetProperty("errors")[0].GetProperty("message").GetString(),
            StringComparison.Ordinal);
        var errors = faulty.Refused(400, "FICHIER_INVALIDE").GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("path").GetString()).ToList();
        Assert.Equal(["project.name", "map.lanes"], errors);
        Assert.Equal(413, (await admin.SendAsync(HttpMethod.Post, "/api/projects/import-pmap", new string('x', 10 * 1024 * 1024 + 1), null)).Status);
        Assert.Equal(before, (await admin.GetAsync("/api/projects")).Ok().GetArrayLength());
    }

    /// <summary>
    /// T-FICR-06: the code is required, the one of the file is taken when it is free, a code in use is refused without
    /// creating anything, a name entered wins and the translation of the name is lost.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_code_and_the_name_of_the_project_at_the_opening(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var code = ApiProject.NewCode();
        var file = Repository("samples", "cloture-mensuelle.pmap").Replace("\"code\": \"VTE\"", $"\"code\": \"{code}\"", StringComparison.Ordinal);

        (await admin.PostAsync("/api/projects/import-pmap", new { content = Repository("samples", "minimal.pmap") })).Refused(400, "PROJET_CODE_INVALIDE");
        var fromFile = (await admin.PostAsync("/api/projects/import-pmap", new { content = file })).Ok(201);
        (await admin.PostAsync("/api/projects/import-pmap", new { content = file })).Refused(409, "PROJET_CODE_EXISTANT");
        var renamed = (await admin.PostAsync("/api/projects/import-pmap", new { content = file, code = ApiProject.NewCode(), name = "Clôture" })).Ok(201);

        // The name of the file entered between spaces is trimmed before it is compared (`FichiersTests.cs:210`): the translations stay.
        var spaced = (await admin.PostAsync("/api/projects/import-pmap", new { content = file, code = ApiProject.NewCode(), name = "  Clôture mensuelle des ventes  " }))
            .Ok(201);

        Assert.Equal(code, fromFile.GetProperty("code").GetString());
        var detail = (await admin.GetAsync($"/api/projects/{renamed.GetProperty("id").GetGuid()}")).Ok();
        Assert.Equal("Clôture", detail.GetProperty("name").GetString());
        Assert.Empty(detail.GetProperty("name_translations").EnumerateObject());
        var same = (await admin.GetAsync($"/api/projects/{spaced.GetProperty("id").GetGuid()}")).Ok();
        Assert.Equal(("Clôture mensuelle des ventes", "Monthly sales closing"), (same.GetProperty("name").GetString(),
            same.GetProperty("name_translations").GetProperty("en").GetString()));
        Assert.Equal("Clôture mensuelle des ventes", (await MapAsync(admin, RootOf(spaced))).GetProperty("name").GetString());
    }

    /// <summary>T-FICR-07: a duplication creates a distinct project of the same content and leaves the source as it is.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_duplication_creates_a_distinct_project_of_the_same_content_and_leaves_the_source_unchanged(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var opened = await OpenAsync(admin, Repository("samples", "cloture-mensuelle.pmap"));
        var source = opened.GetProperty("id").GetGuid();
        var before = (await admin.GetAsync($"/api/projects/{source}/pmap")).Body;
        var version = (await admin.GetAsync($"/api/projects/{source}")).Ok().GetProperty("version").GetInt32();

        var copy = (await admin.PostAsync($"/api/projects/{source}/duplicate", new { code = ApiProject.NewCode(), name = "Copie" })).Ok(201);

        Assert.NotEqual(source, copy.GetProperty("id").GetGuid());
        Assert.Equal(Counts(opened), Counts(copy));
        Assert.Equal(Normalised(before), Normalised((await admin.GetAsync($"/api/projects/{source}/pmap")).Body));
        Assert.Equal(version, (await admin.GetAsync($"/api/projects/{source}")).Ok().GetProperty("version").GetInt32());
        // Same maps, lanes, steps, flows, RACI and translations: the file from its map on, the codes of the project aside.
        var copied = (await admin.GetAsync($"/api/projects/{copy.GetProperty("id").GetGuid()}/pmap")).Body;
        static string Maps(string text) => Normalised(text)[Normalised(text).IndexOf("\"map\":", StringComparison.Ordinal)..];
        Assert.Equal(Maps(before).Replace("Clôture mensuelle des ventes", "Copie", StringComparison.Ordinal), Maps(copied));
    }

    /// <summary>
    /// T-FICR-08: opening and duplicating are for the administrator, the copy for any member, a stranger gets 404 for the
    /// copy, and a write without the anti-CSRF header is refused before anything else (EN_TETE_MANQUANT, 5.2, 5.3).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Opening_and_duplicating_are_for_the_administrator_the_copy_for_every_member(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        using var stranger = project.Stranger;

        (await facilitator.PostAsync("/api/projects/import-pmap", new { content = Repository("samples", "minimal.pmap"), code = ApiProject.NewCode() }))
            .Refused(403, "ACCES_REFUSE");
        (await facilitator.PostAsync($"/api/projects/{project.Id}/duplicate", new { code = ApiProject.NewCode() })).Refused(403, "ACCES_REFUSE");
        Assert.Equal(200, (await reader.GetAsync($"/api/projects/{project.Id}/pmap")).Status);
        (await stranger.GetAsync($"/api/projects/{project.Id}/pmap")).Refused(404, "INTROUVABLE");

        using var raw = RawClient(project.Host, Logins.Admin);
        using var content = new StringContent(JsonSerializer.Serialize(new { content = Repository("samples", "minimal.pmap"), code = ApiProject.NewCode() }),
            Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        var withoutHeader = await raw.PostAsync(new Uri("/api/projects/import-pmap", UriKind.Relative), content, TestContext.Current.CancellationToken);
        var refused = JsonDocument.Parse(await withoutHeader.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal((400, "EN_TETE_MANQUANT"), ((int)withoutHeader.StatusCode, refused.GetProperty("code").GetString()));
    }

    private static HttpClient RawClient(ApiHost host, string login)
    {
        var server = (Microsoft.AspNetCore.TestHost.TestServer)host.Services.GetRequiredService<IServer>();
        var client = server.CreateClient();
        client.BaseAddress = host.Kind == EngineKind.Sqlite ? new Uri($"http://127.0.0.1:{ApiHost.DesktopPort}") : new Uri($"https://{ApiHost.ServerHost}");
        client.DefaultRequestHeaders.Add(TestAuthentication.Header, login);
        return client;
    }

    /// <summary>T-FICR-09: the RACI letters of an opening follow the setting `RaciLetters` (S refused in ACI, accepted in ASCI).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_raci_letters_of_an_opening_follow_the_setting(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var file = Repository("samples", "cloture-mensuelle.pmap").Replace("\"raci\": { \"cdg\": \"C\" }", "\"raci\": { \"cdg\": \"S\" }", StringComparison.Ordinal);

        var refused = (await admin.PostAsync("/api/projects/import-pmap", new { content = file, code = ApiProject.NewCode() })).Refused(400, "FICHIER_INVALIDE");

        Assert.Equal("PMAP_RACI_ROLE", refused.GetProperty("errors")[0].GetProperty("code").GetString());
        await project.Host.WithServicesAsync(services => services.PostConfigure<ProductOptions>(options => options.RaciLetters = "ASCI"), Logins.Admin,
            async configured => (await configured.PostAsync("/api/projects/import-pmap", new { content = file, code = ApiProject.NewCode() })).Ok(201));
    }
}
