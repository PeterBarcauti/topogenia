// ServerRunTests.cs
// The server mode on a real server: Windows authentication, anonymous health, HTTPS, HSTS, allowed hosts, anti-CSRF, static files, administrators read again only on a change, log (spec 5.3).

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Server;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Server;

public sealed class ServerRunTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Account { get; } = new ProcessAccount().Login;

    private static async Task<(int Status, string Body, HttpResponseMessage Response)> SendAsync(HttpClient client, HttpMethod method, string url,
        string host, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(method, new Uri(url, UriKind.Relative));
        request.Headers.Host = host;
        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);
        if (method != HttpMethod.Get)
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request, Token);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(Token), response);
    }

    private static string Local(ServerRun run) => $"localhost:{run.Https.Port}";

    private static string Named(ServerRun run) => $"{ServerRun.ServerName}:{run.Https.Port}";

    [Fact]
    public async Task An_anonymous_request_is_challenged_by_negotiate_without_json_and_the_windows_account_is_signed_in()
    {
        await using var run = await ServerRun.StartAsync();
        using var anonymous = run.Anonymous();
        foreach (var url in new[] { "/api/me", "/api/projects", "/index.html", "/openapi/v1.json" })
        {
            var (status, body, response) = await SendAsync(anonymous, HttpMethod.Get, url, Local(run));
            Assert.Equal((401, ""), (status, body));
            Assert.Contains("Negotiate", response.Headers.WwwAuthenticate.Select(challenge => challenge.Scheme));
        }
        using var client = run.Client();
        var me = JsonDocument.Parse((await SendAsync(client, HttpMethod.Get, "/api/me", Local(run))).Body).RootElement;
        Assert.Equal(Account, me.GetProperty("login").GetString(), ignoreCase: true);
        var entry = await new WindowsDirectory(NullLogger<WindowsDirectory>.Instance).FindAsync(Account, Token);
        Assert.Equal(entry?.DisplayName ?? Account.Split('\\')[1], me.GetProperty("display_name").GetString());
    }

    [Fact]
    public async Task The_health_is_anonymous_and_gives_its_state_only_and_the_detail_to_an_authenticated_user()
    {
        await using var run = await ServerRun.StartAsync();
        using var anonymous = run.Anonymous();
        var minimal = await SendAsync(anonymous, HttpMethod.Get, "/api/health", Local(run));
        Assert.Equal((200, """{"status":"ok"}"""), (minimal.Status, minimal.Body));
        using var client = run.Client();
        // The health challenges nobody: the client authenticates its connection on a route that does (NTLM keeps it).
        Assert.Equal(200, (await SendAsync(client, HttpMethod.Get, "/api/me", Local(run))).Status);
        var detailed = JsonDocument.Parse((await SendAsync(client, HttpMethod.Get, "/api/health", Local(run))).Body).RootElement;
        Assert.Equal(["status", "database", "version", "timestamp"], detailed.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task Https_carries_hsts_for_a_year_and_http_is_redirected_to_https_keeping_the_method()
    {
        await using var run = await ServerRun.StartAsync();
        using var anonymous = run.Anonymous();
        var secure = await SendAsync(anonymous, HttpMethod.Get, "/api/health", Named(run));
        Assert.Equal("max-age=31536000", Assert.Single(secure.Response.Headers.GetValues("Strict-Transport-Security")));
        using var plain = run.Plain();
        var read = await SendAsync(plain, HttpMethod.Get, "/api/health", $"{ServerRun.ServerName}:{run.Http.Port}");
        Assert.Equal(308, read.Status);
        Assert.Equal(new Uri($"https://{ServerRun.ServerName}:{run.Https.Port}/api/health"), read.Response.Headers.Location);
        Assert.False(read.Response.Headers.Contains("Strict-Transport-Security"));
        var write = await SendAsync(plain, HttpMethod.Patch, "/api/me", $"{ServerRun.ServerName}:{run.Http.Port}", ("X-Requested-With", "XMLHttpRequest"));
        Assert.Equal(308, write.Status);
    }

    /// <summary>
    /// F-05: a refusal over HTTPS carries HSTS as a success does: the refusal of the guard, written again by the error
    /// middleware (a write without the anti-CSRF header), and the challenge of the authentication.
    /// </summary>
    [Fact]
    public async Task A_refusal_over_https_carries_hsts_too()
    {
        await using var run = await ServerRun.StartAsync();
        using var anonymous = run.Anonymous();
        var guarded = await SendAsync(anonymous, HttpMethod.Patch, "/api/me", Named(run));
        Assert.Equal((400, "EN_TETE_MANQUANT"), (guarded.Status, JsonDocument.Parse(guarded.Body).RootElement.GetProperty("code").GetString()));
        var challenged = await SendAsync(anonymous, HttpMethod.Get, $"/api/projects/{Guid.NewGuid()}", Named(run));
        Assert.Equal(401, challenged.Status);
        foreach (var refused in new[] { guarded, challenged })
            Assert.Equal("max-age=31536000", Assert.Single(refused.Response.Headers.GetValues("Strict-Transport-Security")));
    }

    [Fact]
    public async Task A_host_outside_the_allowed_list_is_refused_before_the_authentication_and_the_redirection()
    {
        await using var run = await ServerRun.StartAsync();
        using var client = run.Client();
        foreach (var host in new[] { $"attacker.example:{run.Https.Port}", $"127.0.0.1:{run.Https.Port}", $"topogenia.test.attacker.example:{run.Https.Port}" })
        {
            var refused = await SendAsync(client, HttpMethod.Get, "/api/me", host);
            Assert.Equal((400, "REQUETE_INVALIDE"), (refused.Status, JsonDocument.Parse(refused.Body).RootElement.GetProperty("code").GetString()));
        }
        using var plain = run.Plain();
        Assert.Equal(400, (await SendAsync(plain, HttpMethod.Get, "/api/health", $"attacker.example:{run.Http.Port}")).Status);
        Assert.Equal(200, (await SendAsync(client, HttpMethod.Get, "/api/me", $"LOCALHOST:{run.Https.Port}")).Status);
    }

    [Fact]
    public async Task A_write_needs_the_anti_csrf_header_and_is_refused_from_another_origin_or_site()
    {
        await using var run = await ServerRun.StartAsync();
        using var client = run.Client();
        var origin = $"https://{Local(run)}";
        var me = JsonDocument.Parse((await SendAsync(client, HttpMethod.Get, "/api/me", Local(run))).Body).RootElement;
        var version = ("If-Match", me.GetProperty("version").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture));
        var missing = await SendAsync(client, HttpMethod.Patch, "/api/me", Local(run), version, ("Origin", origin));
        Assert.Equal((400, "EN_TETE_MANQUANT"), (missing.Status, JsonDocument.Parse(missing.Body).RootElement.GetProperty("code").GetString()));
        var csrf = ("X-Requested-With", "XMLHttpRequest");
        var foreign = await SendAsync(client, HttpMethod.Patch, "/api/me", Local(run), version, csrf, ("Origin", "https://attacker.example"));
        var crossSite = await SendAsync(client, HttpMethod.Patch, "/api/me", Local(run), version, csrf, ("Sec-Fetch-Site", "cross-site"));
        var otherScheme = await SendAsync(client, HttpMethod.Patch, "/api/me", Local(run), version, csrf, ("Origin", $"http://{Local(run)}"));
        Assert.Equal([403, 403, 403], new[] { foreign.Status, crossSite.Status, otherScheme.Status });
        var accepted = await SendAsync(client, HttpMethod.Patch, "/api/me", Local(run), version, csrf, ("Origin", origin), ("Sec-Fetch-Site", "same-origin"));
        Assert.Equal(200, accepted.Status);
    }

    [Fact]
    public async Task No_cors_is_configured_so_the_preflight_of_another_site_gets_no_permission()
    {
        await using var run = await ServerRun.StartAsync();
        using var client = run.Client();
        var preflight = await SendAsync(client, HttpMethod.Options, "/api/me", Local(run), ("Origin", "https://attacker.example"),
            ("Access-Control-Request-Method", "PATCH"), ("Access-Control-Request-Headers", "x-requested-with,if-match"));
        Assert.DoesNotContain(preflight.Response.Headers, header => header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(204, preflight.Status);
    }

    [Fact]
    public async Task The_static_files_are_served_to_an_authenticated_user_only()
    {
        await using var run = await ServerRun.StartAsync();
        using var anonymous = run.Anonymous();
        Assert.Equal(401, (await SendAsync(anonymous, HttpMethod.Get, "/index.html", Local(run))).Status);
        using var client = run.Client();
        var served = await SendAsync(client, HttpMethod.Get, "/index.html", Local(run));
        Assert.Equal((200, "<!doctype html><title>Topogenia</title>"), (served.Status, served.Body));
    }

    [Fact]
    public async Task A_server_without_an_https_address_refuses_every_request_in_clear_the_health_included_before_the_authentication()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.SqlServer);
        await using var run = await ServerRun.StartAsync(database, null, ownsDatabase: false, withHttps: false);
        using var plain = run.Plain();
        var host = $"{ServerRun.ServerName}:{run.Http.Port}";
        var health = await SendAsync(plain, HttpMethod.Get, "/api/health", host);
        Assert.Equal(403, health.Status);
        Assert.Contains("HTTPS_EXIGE", health.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(health.Response.Headers, header => header.Key == "WWW-Authenticate");
        using var authenticated = new HttpClient(new SocketsHttpHandler { Credentials = System.Net.CredentialCache.DefaultCredentials, UseCookies = false })
        {
            BaseAddress = run.Http,
        };
        var me = await SendAsync(authenticated, HttpMethod.Get, "/api/me", $"localhost:{run.Http.Port}");
        Assert.Equal(403, me.Status);
        Assert.DoesNotContain(Account, me.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Any_other_address_gets_the_page_of_the_application_but_an_unknown_api_route_or_a_missing_file_does_not()
    {
        await using var run = await ServerRun.StartAsync();
        using var anonymous = run.Anonymous();
        Assert.Equal(401, (await SendAsync(anonymous, HttpMethod.Get, "/projects/PRJ/maps/42", Local(run))).Status);
        using var client = run.Client();
        var deep = await SendAsync(client, HttpMethod.Get, "/projects/PRJ/maps/42", Local(run));
        Assert.Equal((200, "<!doctype html><title>Topogenia</title>"), (deep.Status, deep.Body));
        Assert.Equal("text/html", deep.Response.Content.Headers.ContentType?.MediaType);
        var api = await SendAsync(client, HttpMethod.Get, "/api/unknown/route", Local(run));
        Assert.Equal(404, api.Status);
        Assert.Contains("INTROUVABLE", api.Body, StringComparison.Ordinal);
        Assert.Equal(404, (await SendAsync(client, HttpMethod.Get, "/missing.js", Local(run))).Status);
    }

    [Fact]
    public async Task The_administrators_follow_each_change_of_the_configuration_file_and_the_quit_command_does_not_exist()
    {
        await using var run = await ServerRun.StartAsync();
        using var client = run.Client();
        async Task<bool> IsAdminAsync() =>
            JsonDocument.Parse((await SendAsync(client, HttpMethod.Get, "/api/me", Local(run))).Body).RootElement.GetProperty("is_admin").GetBoolean();
        async Task<bool> BecomesAsync(bool expected)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (await IsAdminAsync() == expected)
                    return true;
                await Task.Delay(100, Token);
            }
            return false;
        }
        Assert.False(await IsAdminAsync());
        await run.SetAdministratorsAsync(Account.ToUpperInvariant());
        Assert.True(await BecomesAsync(true));
        await run.SetAdministratorsAsync();
        Assert.True(await BecomesAsync(false));
        var quit = await SendAsync(client, HttpMethod.Post, "/api/desktop/quit", Local(run), ("X-Requested-With", "XMLHttpRequest"));
        Assert.Equal((404, "INTROUVABLE"), (quit.Status, JsonDocument.Parse(quit.Body).RootElement.GetProperty("code").GetString()));
    }

    /// <summary>
    /// The list of the administrators is bound again when its file changes, never at each request (spec 5.3, R465): a
    /// source of the configuration counts the readings of its key. Ten requests of an administrator read it no more; the
    /// change of the file reads it again, then ten requests read it no more.
    /// </summary>
    [Fact]
    public async Task The_list_of_the_administrators_is_read_again_only_when_the_file_changes()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.SqlServer);
        var readings = new ReadingsOf($"{ProductOptions.Section}:Administrators");
        await using var run = await ServerVariantRun.StartAsync(database, [Account], configure: configuration => configuration.Add(readings));
        using var client = new ApiClient(run.Client());
        async Task<bool> IsAdminAsync() => (await client.GetAsync("/api/me")).Ok().GetProperty("is_admin").GetBoolean();
        Assert.True(await IsAdminAsync());
        var before = readings.Count;
        for (var request = 0; request < 5; request++)
        {
            Assert.True(await IsAdminAsync());
            (await client.GetAsync("/api/trash")).Ok();
        }
        Assert.Equal(before, readings.Count);

        await run.SetAdministratorsAsync();
        var revoked = false;
        for (var attempt = 0; attempt < 100 && !revoked; attempt++)
        {
            revoked = !await IsAdminAsync();
            if (!revoked)
                await Task.Delay(100, Token);
        }
        Assert.True(revoked);
        var after = await readings.SettledAsync();
        Assert.True(after > before, $"The change of the file did not read the list again ({before} readings before, {after} after).");
        for (var request = 0; request < 10; request++)
            Assert.False(await IsAdminAsync());
        Assert.Equal(after, readings.Count);
    }

    [Fact]
    public async Task The_log_is_written_in_the_configured_folder_without_the_connection_string_nor_the_login()
    {
        string log;
        await using (var run = await ServerRun.StartAsync())
        {
            using var client = run.Client();
            Assert.Equal(200, (await SendAsync(client, HttpMethod.Get, "/api/me", Local(run))).Status);
            // A write is logged by the request log (a read is not, and the framework writes its errors only).
            await SendAsync(client, HttpMethod.Patch, "/api/me", Local(run), ("X-Requested-With", "XMLHttpRequest"));
            var file = Assert.Single(Directory.GetFiles(run.LogsFolder, "topogenia-*.log"));
            using var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            log = await reader.ReadToEndAsync(Token);
            Assert.NotEmpty(log);
            Assert.DoesNotContain(run.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Composition.ProductOptions>>().Value.Database.ConnectionString, log, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain(Account, log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_logs_folder_defaults_to_logs_in_the_folder_of_the_application_and_a_relative_folder_is_resolved_there()
    {
        var root = Path.Combine(Path.GetTempPath(), "application");
        Assert.Equal(Path.Combine(root, "logs"), ServerServices.LogsFolder(new Composition.ServerOptions(), root));
        Assert.Equal(Path.Combine(root, "journal"), ServerServices.LogsFolder(new Composition.ServerOptions { LogsFolder = "journal" }, root));
        var rooted = Path.Combine(Path.GetTempPath(), "elsewhere");
        Assert.Equal(rooted, ServerServices.LogsFolder(new Composition.ServerOptions { LogsFolder = rooted }, root));
    }

    [Fact]
    public async Task The_directory_knows_the_account_of_the_tests_and_nothing_of_an_unknown_login()
    {
        var directory = new WindowsDirectory(NullLogger<WindowsDirectory>.Instance);
        Assert.NotNull(await directory.FindAsync(Account, Token));
        Assert.Null(await directory.FindAsync($@"{Environment.MachineName}\nobody-{Guid.NewGuid():N}"[..40], Token));
        Assert.IsType<IUserDirectory>(directory, exactMatch: false);
    }

    /// <summary>A source of the configuration without values that counts how many times its key is enumerated (a binding reads it so).</summary>
    private sealed class ReadingsOf(string key) : IConfigurationSource
    {
        private int count;

        private string Key { get; } = key;

        public int Count => Volatile.Read(ref count);

        public IConfigurationProvider Build(IConfigurationBuilder builder) => new Counting(this);

        /// <summary>The count once it has not moved for a second (a change of a file may be signalled more than once).</summary>
        public async Task<int> SettledAsync()
        {
            var last = Count;
            for (var quiet = 0; quiet < 10;)
            {
                await Task.Delay(100, Token);
                quiet = Count == last ? quiet + 1 : 0;
                last = Count;
            }
            return last;
        }

        private sealed class Counting(ReadingsOf source) : ConfigurationProvider
        {
            public override IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
            {
                if (string.Equals(parentPath, source.Key, StringComparison.OrdinalIgnoreCase))
                    Interlocked.Increment(ref source.count);
                return base.GetChildKeys(earlierKeys, parentPath);
            }
        }
    }
}
