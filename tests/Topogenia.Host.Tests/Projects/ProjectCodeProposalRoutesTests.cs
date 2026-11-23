// ProjectCodeProposalRoutesTests.cs
// The code proposed from the name of a new project through the whole host on both databases (ADR 111 point 7): free among the live projects and the trash, the name kept in the body and out of the log, the administrator alone, the bounds of the name and of the body.

using System.Net.Http.Headers;
using System.Text;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Search;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Projects;

public sealed class ProjectCodeProposalRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Route = "/api/projects/code-proposal";

    /// <summary>The limit of the body, written here as the decision gives it (4 KB), not read from the code under test.</summary>
    private const int FourKilobytes = 4 * 1024;

    private static async Task<string?> ProposeAsync(ApiClient client, string? name) =>
        (await client.PostAsync(Route, new { name })).Ok().GetProperty("code").GetString();

    private static async Task<Guid> CreateAsync(ApiClient admin, string code) =>
        (await admin.PostAsync("/api/projects", new { code, name = $"Projet {code}" })).Ok(201).GetProperty("id").GetGuid();

    /// <summary>
    /// The name of the test (one word with an accent: its six first letters once folded) is used by no other test of the
    /// class: the codes of its start are those this test creates, on a base of its own per engine.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_administrator_gets_a_code_proposed_free_among_live_projects_and_the_trash(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        Assert.Equal("TDFF", await ProposeAsync(admin, "Traitement des factures fournisseurs"));
        Assert.Equal("LARH", await ProposeAsync(admin, "La RH"));
        Assert.Null(await ProposeAsync(admin, "RH"));
        Assert.Null(await ProposeAsync(admin, "2026"));
        Assert.Null(await ProposeAsync(admin, null));
        Assert.Equal("ZEPHYR", await ProposeAsync(admin, "Zéphyrine"));

        // A live project holds the code: the next code of the rule of the trash.
        await CreateAsync(admin, "ZEPHYR");
        Assert.Equal("ZEPHYA", await ProposeAsync(admin, "Zéphyrine"));

        // A project in the trash still holds its code.
        var trashed = await CreateAsync(admin, "ZEPHYA");
        var version = (await admin.GetAsync($"/api/projects/{trashed}")).Ok().GetProperty("version").GetInt32();
        (await admin.DeleteAsync($"/api/projects/{trashed}?confirmation=ZEPHYA", version)).Ok(204);
        Assert.Equal("ZEPHYB", await ProposeAsync(admin, "Zéphyrine"));

        // The code proposed is one the creation takes; a proposal wrote nothing (the codes are those the test created).
        await CreateAsync(admin, "ZEPHYB");
        Assert.Equal(["ZEPHYB", "ZEPHYR"], (await admin.GetAsync("/api/projects")).Ok().EnumerateArray().Select(item => item.GetProperty("code").GetString())
            .Where(code => code!.StartsWith("ZEPHY", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The name is a business content: it travels in the body of a POST, never in the address that the logs of IIS write;
    /// the host reads no query string for it (a name in the address alone proposes nothing), and its log writes one line of
    /// the route, without the name, even at the most detailed level of its own categories (ADR 110, ADR 111 point 7).
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_name_never_leaves_the_body_the_log_holds_the_route_alone(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        const string secret = "Zorglubienne";
        using var capture = new ProjectSearchRoutesTests.LogCapture();
        await host.WithVariantAsync(capture.Register, async clientOf =>
        {
            using var admin = clientOf(Logins.Admin);
            Assert.Equal("ZORGLU", await ProposeAsync(admin, secret));
            Assert.Null((await admin.PostAsync($"{Route}?name=Achats", new { })).Ok().GetProperty("code").GetString());
            // No route answers a GET of the proposal: a name in an address is never read.
            Assert.Equal(404, (await admin.GetAsync($"{Route}?name=Achats")).Status);
        });

        var lines = capture.Lines;
        Assert.Equal(2, lines.Count(line => line.StartsWith($"POST {Route} 200 in ", StringComparison.Ordinal)));
        Assert.DoesNotContain(lines, line => line.Contains(secret, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, line => line.Contains("ZORGLU", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The administrator alone (ADR 111 point 7, table 3.7 of the lot): a facilitator, a contributor, a reader and a stranger
    /// are refused 403, an anonymous user 401 before his body is read.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_stranger_to_administration_is_refused_and_an_anonymous_user_is_unauthenticated(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var body = new { name = "Achats" };
        foreach (var client in new[] { project.Facilitator, project.Contributor, project.Reader, project.Stranger })
        {
            using var caller = client;
            (await caller.PostAsync(Route, body)).Refused(403, "ACCES_REFUSE");
        }
        using var anonymous = project.Host.As(null);
        Assert.Equal(401, (await anonymous.PostAsync(Route, body)).Status);
        // The anonymous is refused before his body is read: a body that is not JSON is not even looked at (400 otherwise).
        Assert.Equal(401, (await anonymous.PostAsync(Route, "{ pas du JSON")).Status);
        using var admin = project.Admin;
        Assert.Equal("ACHATS", await ProposeAsync(admin, "Achats"));
    }

    /// <summary>A name of 200 characters is read, one more is refused on its field; a body of 4 KB is read, one byte more refused 413 before it is read.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_name_too_long_or_a_body_too_large_is_refused(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using var admin = host.As(Logins.Admin);
        Assert.Equal("NAMEAA", await ProposeAsync(admin, "Nameaa" + new string('a', 194)));
        Assert.Equal("name", (await admin.PostAsync(Route, new { name = new string('a', 201) })).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());

        using var unbuffered = host.Unbuffered(Logins.Admin);
        Assert.Equal("ACHATS", (await SendAsync(unbuffered, Padded(FourKilobytes))).Ok().GetProperty("code").GetString());
        var over = await SendAsync(unbuffered, Padded(FourKilobytes + 1));
        var details = over.Refused(413, "REQUETE_TROP_GROSSE");
        Assert.Equal((4, "KB"), (details.GetProperty("max").GetInt32(), details.GetProperty("unit").GetString()));
        Assert.DoesNotContain("0 M", over.Json.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    /// <summary>`{"name":"Achats"}` padded with spaces before its closing brace to exactly <paramref name="size"/> bytes.</summary>
    private static byte[] Padded(int size)
    {
        const string head = "{\"name\":\"Achats\"";
        return Encoding.ASCII.GetBytes(head + new string(' ', size - head.Length - 1) + "}");
    }

    private static async Task<ApiResponse> SendAsync(ApiClient client, byte[] body)
    {
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await client.SendContentAsync(HttpMethod.Post, Route, content, null);
    }
}
