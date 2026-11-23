// CollectionPreviewRoutesTests.cs
// The preview of a template collection through the whole host on both databases (ADR 114 points 4 to 6): the list of its processes, faults in the language of the user, bounds of the body and of the content, the right before the body, nothing written, each line opened by the route of the opening.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Application.Audit;
using Topogenia.Domain.Projects;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Exchange;

public sealed class CollectionPreviewRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Route = "/api/projects/import-collection/preview";

    /// <summary>The limit of the body, written here as the decision gives it (10 MB), not read from the code under test.</summary>
    private const int TenMegabytes = 10 * 1024 * 1024;

    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static async Task<JsonElement> PreviewAsync(ApiClient admin, string content, string language = "fr-FR") =>
        (await admin.SendAsync(HttpMethod.Post, Route, new { content }, null, ("Accept-Language", language))).Ok();

    private static List<JsonElement> Processes(JsonElement preview) => [.. preview.GetProperty("processes").EnumerateArray()];

    private static string? Text(JsonElement element, string name) => element.GetProperty(name).GetString();

    private static async Task<int> ProjectCountAsync(ApiClient admin) => (await admin.GetAsync("/api/projects")).Ok().GetArrayLength();

    /// <summary>The audit events of the base, all of them (fewer than the limit of a read: the test checks it).</summary>
    private static async Task<int> AuditCountAsync(ApiClient admin)
    {
        var count = (await admin.GetAsync("/api/admin/audit?limit=1000")).Ok().GetArrayLength();
        Assert.True(count < 1000, "The base holds more events than one read gives.");
        return count;
    }

    private static async Task<string?> ProposedFromNameAsync(ApiClient admin, string name) =>
        (await admin.PostAsync("/api/projects/code-proposal", new { name })).Ok().GetProperty("code").GetString();

    private static async Task<ApiResponse> OpenAsync(ApiClient admin, JsonElement line, string? code = null) =>
        await admin.PostAsync("/api/projects/import-pmap", new { content = Text(line, "content"), code = code ?? Text(line, "proposed_code") });

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_administrator_previews_a_collection_with_counts_codes_uncertainties_and_contents(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var code = ApiProject.NewCode();
        var withSheet = TestPmaps.WithSheet.Replace("\"code\": \"ACHA\"", $"\"code\": \"{code}\"", StringComparison.Ordinal);
        var english = TestPmaps.MinimalCoded(code).Replace("\"name\": \"Demande d'achat\"", "\"name\": \"Purchase request\", \"language\": \"en\", \"description\": \"From the need to the order.\"",
            StringComparison.Ordinal);

        var preview = await PreviewAsync(admin, TestPmaps.Collection(withSheet, TestPmaps.Minimal, english));

        Assert.Equal(("Achats", "Les processus d'achat."), (Text(preview, "name"), Text(preview, "description")));
        var lines = Processes(preview);
        Assert.Equal([0, 1, 2], lines.Select(line => line.GetProperty("index").GetInt32()));
        var first = lines[0];
        Assert.Equal(("Achats", "fr", code, code), (Text(first, "name"), Text(first, "language"), Text(first, "file_code"), Text(first, "proposed_code")));
        Assert.Equal(JsonValueKind.Null, first.GetProperty("description").ValueKind);
        var counts = first.GetProperty("counts");
        Assert.Equal((1, 2, 2, 1, 2), (counts.GetProperty("maps").GetInt32(), counts.GetProperty("lanes").GetInt32(), counts.GetProperty("steps").GetInt32(),
            counts.GetProperty("flows").GetInt32(), counts.GetProperty("sheets").GetInt32()));
        Assert.Equal(["Le délai de 8 heures est à confirmer."], first.GetProperty("uncertainties").EnumerateArray().Select(text => text.GetString()));
        Assert.Empty(first.GetProperty("faults").EnumerateArray());
        // The text of each process is the process of the file, as the host read it.
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(withSheet), JsonNode.Parse(Text(first, "content")!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(TestPmaps.Minimal), JsonNode.Parse(Text(lines[1], "content")!)));
        // Without a code of its own, the code its name proposes; the same code twice, the next free one.
        Assert.Equal(JsonValueKind.Null, lines[1].GetProperty("file_code").ValueKind);
        Assert.Equal(await ProposedFromNameAsync(admin, "Demande d'achat"), Text(lines[1], "proposed_code"));
        Assert.Equal(("Purchase request", "en", "From the need to the order.", code), (Text(lines[2], "name"), Text(lines[2], "language"),
            Text(lines[2], "description"), Text(lines[2], "file_code")));
        Assert.Equal(ProjectCodeRules.TrashCode(code, [code]), Text(lines[2], "proposed_code"));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_invalid_process_is_listed_with_its_faults_in_the_language_of_the_user(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var faulty = TestPmaps.Minimal.Replace("\"lane\": \"c2\", \"label\": \"Passer la commande\"", "\"lane\": \"c9\", \"label\": \"Passer la commande\"",
            StringComparison.Ordinal);
        // The host configures the eighteen languages of the interface: Korean is none of them.
        var korean = TestPmaps.MinimalCoded(ApiProject.NewCode()).Replace("\"name\": \"Demande d'achat\"", "\"name\": \"Ordering\", \"language\": \"ko\"",
            StringComparison.Ordinal);
        var collection = TestPmaps.Collection(TestPmaps.MinimalCoded(ApiProject.NewCode()), faulty, korean);

        var french = Processes(await PreviewAsync(admin, collection, "fr-FR"));
        var english = Processes(await PreviewAsync(admin, collection, "en-GB"));

        Assert.NotNull(Text(french[0], "proposed_code"));
        foreach (var line in new[] { french[1], french[2] })
        {
            Assert.Equal(JsonValueKind.Null, line.GetProperty("proposed_code").ValueKind);
            Assert.Equal(JsonValueKind.Null, line.GetProperty("counts").ValueKind);
            Assert.False(string.IsNullOrEmpty(Text(line, "content")));
        }
        var fault = Assert.Single(french[1].GetProperty("faults").EnumerateArray());
        Assert.Equal(("map.steps[1].lane", "PMAP_COULOIR_INCONNU", "Le couloir « c9 » n'existe pas dans cette carte."),
            (Text(fault, "path"), Text(fault, "code"), Text(fault, "message")));
        Assert.Equal("Lane \"c9\" does not exist in this map.", Text(english[1].GetProperty("faults")[0], "message"));
        // A language the installation does not configure refuses that process alone.
        var language = Assert.Single(french[2].GetProperty("faults").EnumerateArray());
        Assert.Equal(("project.language", "PMAP_LANGUE"), (Text(language, "path"), Text(language, "code")));
        Assert.StartsWith("Langue « ko » non prise en charge", Text(language, "message"), StringComparison.Ordinal);
        Assert.StartsWith("Language \"ko\" is not supported", Text(english[2].GetProperty("faults")[0], "message"), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_invalid_process_gives_its_name_as_written_only_when_it_is_a_readable_text(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        string Faulty(string project) => TestPmaps.Minimal.Replace("\"lane\": \"c2\", \"label\": \"Passer la commande\"", "\"lane\": \"c9\", \"label\": \"Passer la commande\"",
            StringComparison.Ordinal).Replace("\"project\": { \"name\": \"Demande d'achat\" }", project, StringComparison.Ordinal);
        var collection = TestPmaps.Collection(
            Faulty("\"project\": { \"name\": \"<img src=x> Demande & co\" }"),
            Faulty("\"other\": 1"),
            Faulty("\"project\": { \"name\": 12 }"),
            Faulty("\"project\": { \"name\": \"\" }"),
            Faulty($"\"project\": {{ \"name\": \"{new string('x', 201)}\" }}"),
            Faulty("\"project\": { \"name\": \"a\\tb\" }"));

        var lines = Processes(await PreviewAsync(admin, collection));

        Assert.All(lines, line => Assert.NotEmpty(line.GetProperty("faults").EnumerateArray()));
        Assert.Equal("<img src=x> Demande & co", Text(lines[0], "name"));
        Assert.All(lines.Skip(1), line => Assert.Equal(JsonValueKind.Null, line.GetProperty("name").ValueKind));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_faulty_envelope_is_refused_as_a_file_and_nothing_is_written(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var projects = await ProjectCountAsync(admin);
        var events = await AuditCountAsync(admin);
        var many = TestPmaps.Collection([.. Enumerable.Repeat(TestPmaps.Minimal, 51)]);

        var tooMany = await admin.SendAsync(HttpMethod.Post, Route, new { content = many }, null, ("Accept-Language", "fr-FR"));
        var unknown = await admin.SendAsync(HttpMethod.Post, Route, new { content = TestPmaps.Collection(TestPmaps.Minimal).Replace("\"version\": \"1.0\"",
            "\"version\": \"1.0\", \"code\": \"ACHAT\"", StringComparison.Ordinal) }, null, ("Accept-Language", "en-GB"));
        var single = await admin.PostAsync(Route, new { content = TestPmaps.Minimal });

        var details = tooMany.Refused(400, "FICHIER_INVALIDE");
        Assert.Equal(1, details.GetProperty("count").GetInt32());
        var fault = Assert.Single(details.GetProperty("errors").EnumerateArray());
        Assert.Equal(("processes", "COLLECTION_TROP_DE_PROCESSUS", "Le fichier compte 51 processus : 50 au maximum."),
            (Text(fault, "path"), Text(fault, "code"), Text(fault, "message")));
        var property = Assert.Single(unknown.Refused(400, "FICHIER_INVALIDE").GetProperty("errors").EnumerateArray());
        Assert.Equal(("code", "PMAP_PROPRIETE_INCONNUE"), (Text(property, "path"), Text(property, "code")));
        Assert.StartsWith("Unknown property \"code\".", Text(property, "message"), StringComparison.Ordinal);
        // A .pmap alone is no collection: its format is refused, and its properties that an envelope does not have.
        Assert.Contains(("format", "PMAP_FORMAT"), single.Refused(400, "FICHIER_INVALIDE").GetProperty("errors").EnumerateArray()
            .Select(fault => (Text(fault, "path"), Text(fault, "code"))));
        Assert.Equal(400, (await admin.PostAsync(Route, "{ pas du JSON")).Status);
        Assert.Equal((projects, events), (await ProjectCountAsync(admin), await AuditCountAsync(admin)));
    }

    /// <summary>A body of 10 MB is read (its content then refused beyond 8 million characters), one byte more refused 413 before it is read.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_preview_longer_than_ten_megabytes_is_refused_413_before_it_is_read(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using var admin = host.Unbuffered(Logins.Admin);

        var refused = await SendAsync(admin, Padded(TenMegabytes + 1));
        var read = await SendAsync(admin, Padded(TenMegabytes));

        var details = refused.Refused(413, "REQUETE_TROP_GROSSE");
        Assert.Equal((10, "MB"), (details.GetProperty("max").GetInt32(), details.GetProperty("unit").GetString()));
        Assert.Equal(8, read.Refused(400, "FICHIER_TROP_GROS").GetProperty("max").GetInt32());
        // Under the limit of the body, a content of 8 million characters is read, one more refused as the opening refuses it.
        using var buffered = host.As(Logins.Admin);
        var longest = new string(' ', 8 * 1024 * 1024);
        Assert.Equal("PMAP_VIDE", (await buffered.PostAsync(Route, new { content = longest })).Refused(400, "FICHIER_INVALIDE")
            .GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(8, (await buffered.PostAsync(Route, new { content = longest + " " })).Refused(400, "FICHIER_TROP_GROS").GetProperty("max").GetInt32());
    }

    /// <summary>
    /// A facilitator, a contributor, a reader and a stranger are refused 403 before a byte of the body is read; an anonymous
    /// user 401, a body that is not JSON not even looked at; the administrator's body is read whole.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_refused_user_never_makes_the_host_read_the_body(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        // A collection that is no collection: read first, it would answer FICHIER_INVALIDE whoever sends it.
        var body = JsonSerializer.SerializeToUtf8Bytes(new { content = "{ pas une collection" });
        foreach (var login in new[] { Logins.Facilitator, Logins.Contributor, Logins.Reader, Logins.Stranger })
        {
            var (reply, read) = await ImportRoutesTests.SendGatedAsync(project.Host, login, Route, body, "application/json");
            reply.Refused(403, "ACCES_REFUSE");
            Assert.Equal((login, 0), (login, read));
        }
        using var anonymous = project.Host.As(null);
        Assert.Equal(401, (await anonymous.PostAsync(Route, new { content = TestPmaps.Collection(TestPmaps.Minimal) })).Status);
        Assert.Equal(401, (await anonymous.PostAsync(Route, "{ pas du JSON")).Status);
        var (granted, whole) = await ImportRoutesTests.SendGatedAsync(project.Host, Logins.Admin, Route, body, "application/json", held: false);
        granted.Refused(400, "FICHIER_INVALIDE");
        Assert.Equal(body.Length, whole);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_content_of_a_line_opens_as_a_project_by_the_opening_route_all_or_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var code = ApiProject.NewCode();
        var faulty = TestPmaps.MinimalCoded(ApiProject.NewCode()).Replace("\"target\": \"e3\"", "\"target\": \"e9\"", StringComparison.Ordinal);
        var lines = Processes(await PreviewAsync(admin, TestPmaps.Collection(TestPmaps.WithSheet.Replace("\"code\": \"ACHA\"", $"\"code\": \"{code}\"",
            StringComparison.Ordinal), faulty)));
        var projects = await ProjectCountAsync(admin);

        // The line in fault, sent all the same, is refused as a file: nothing is written.
        var refused = await OpenAsync(admin, lines[1], ApiProject.NewCode());
        Assert.NotEmpty(refused.Refused(400, "FICHIER_INVALIDE").GetProperty("errors").EnumerateArray());
        Assert.Equal(projects, await ProjectCountAsync(admin));
        var opened = (await OpenAsync(admin, lines[0])).Ok(201);

        Assert.Equal(code, Text(opened, "code"));
        var counts = lines[0].GetProperty("counts");
        Assert.Equal(["maps", "lanes", "steps", "flows", "sheets"], counts.EnumerateObject().Select(count => count.Name));
        Assert.All(counts.EnumerateObject(), count => Assert.Equal(count.Value.GetInt32(), opened.GetProperty(count.Name).GetInt32()));
        Assert.Equal(projects + 1, await ProjectCountAsync(admin));
        var id = opened.GetProperty("id").GetGuid();
        var member = Assert.Single((await admin.GetAsync($"/api/projects/{id}")).Ok().GetProperty("members").EnumerateArray());
        Assert.Equal((Logins.Admin, "facilitator"), (Text(member, "login"), Text(member, "role")));
        var imported = (await admin.GetAsync($"/api/admin/audit?project_id={id}&kind={AuditKinds.ProjectImported}")).Ok();
        Assert.Single(imported.EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_code_taken_after_the_preview_refuses_that_line_alone(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        var (first, second) = (ApiProject.NewCode(), ApiProject.NewCode());
        var collection = TestPmaps.Collection(TestPmaps.MinimalCoded(first), TestPmaps.MinimalCoded(second));
        var lines = Processes(await PreviewAsync(admin, collection));
        Assert.Equal([first, second], lines.Select(line => Text(line, "proposed_code")));

        // Another administrator takes the first code between the preview and the creation.
        using var owner = project.Host.As(Logins.Owner);
        (await owner.PostAsync("/api/projects", new { code = first, name = "Pris entre-temps" })).Ok(201);

        Assert.Equal(first, (await OpenAsync(admin, lines[0])).Refused(409, "PROJET_CODE_EXISTANT").GetProperty("code").GetString());
        Assert.Equal(second, Text((await OpenAsync(admin, lines[1])).Ok(201), "code"));
        // Previewed again, the line refused gets the next free code, under which it opens.
        var again = Processes(await PreviewAsync(admin, collection));
        Assert.Equal([ProjectCodeRules.TrashCode(first, [first]), ProjectCodeRules.TrashCode(second, [second])], again.Select(line => Text(line, "proposed_code")));
        Assert.Equal(Text(again[0], "proposed_code"), Text((await OpenAsync(admin, again[0])).Ok(201), "code"));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_preview_leaves_no_audit_event_nor_operation(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        using var facilitator = project.Facilitator;
        var code = ApiProject.NewCode();
        var collection = TestPmaps.Collection(TestPmaps.MinimalCoded(code), TestPmaps.MinimalCoded(code));
        var projects = await ProjectCountAsync(admin);
        var events = await AuditCountAsync(admin);
        var history = (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Body;

        var lines = Processes(await PreviewAsync(admin, collection));
        var again = Processes(await PreviewAsync(admin, collection));

        Assert.Equal([code, ProjectCodeRules.TrashCode(code, [code])], lines.Select(line => Text(line, "proposed_code")));
        Assert.Equal(lines.Select(line => Text(line, "proposed_code")), again.Select(line => Text(line, "proposed_code")));
        Assert.Equal((projects, events), (await ProjectCountAsync(admin), await AuditCountAsync(admin)));
        Assert.Equal(history, (await facilitator.GetAsync($"/api/maps/{project.RootMapId}/history")).Body);
    }

    /// <summary>`{"content":"aaa…"}` of exactly <paramref name="size"/> bytes.</summary>
    private static byte[] Padded(int size)
    {
        const string head = "{\"content\":\"";
        const string tail = "\"}";
        return Encoding.ASCII.GetBytes(head + new string('a', size - head.Length - tail.Length) + tail);
    }

    private static async Task<ApiResponse> SendAsync(ApiClient client, byte[] body)
    {
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await client.SendContentAsync(HttpMethod.Post, Route, content, null);
    }
}
