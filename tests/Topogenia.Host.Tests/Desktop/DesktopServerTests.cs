// DesktopServerTests.cs
// The desktop mode on a real server: loopback only, no Server header, launch token, session cookie, checks of Host, anti-CSRF header, Origin and Sec-Fetch-Site, files, quit (spec 5.2).

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Topogenia.Host.Desktop;

namespace Topogenia.Host.Tests.Desktop;

public sealed class DesktopServerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A request of the application: a write carries the anti-CSRF header of the API (A-4-10).</summary>
    private static Task<(int Status, string Body, HttpResponseMessage Response)> SendAsync(HttpClient client, HttpMethod method, string url,
        string? cookie, params (string Name, string Value)[] headers) =>
        SendAsync(client, method, url, cookie, method != HttpMethod.Get, headers);

    private static async Task<(int Status, string Body, HttpResponseMessage Response)> SendAsync(HttpClient client, HttpMethod method, string url,
        string? cookie, bool antiCsrf, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(method, new Uri(url, UriKind.Relative));
        if (cookie is not null)
            request.Headers.Add("Cookie", cookie);
        if (antiCsrf)
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);
        if (method != HttpMethod.Get)
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request, Token);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(Token), response);
    }

    [Fact]
    public async Task The_host_listens_on_the_loopback_only_on_a_free_port_chosen_at_the_start()
    {
        await using var run = await DesktopRun.StartAsync();
        var address = new Uri(Assert.Single(run.Addresses));
        Assert.Equal(("http", IPAddress.Loopback.ToString()), (address.Scheme, address.Host));
        Assert.NotEqual(0, address.Port);
    }

    [Fact]
    public async Task Every_request_without_the_session_cookie_is_refused_401_the_health_included()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        foreach (var url in new[] { "/api/health", "/api/me", "/api/projects", "/" })
            Assert.Equal(401, (await SendAsync(client, HttpMethod.Get, url, null)).Status);
        Assert.Equal(401, (await SendAsync(client, HttpMethod.Get, "/api/me", "desktop_session=forged")).Status);
    }

    /// <summary>Security m-1 of the gate P7: no answer names the server, whatever its status (ASVS V14.3.3).</summary>
    [Fact]
    public async Task No_answer_of_the_desktop_mode_names_the_server()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        var anonymous = await SendAsync(client, HttpMethod.Get, "/", null);
        var launch = await SendAsync(client, HttpMethod.Get, $"/launch?token={run.LaunchToken}", null);
        var cookie = launch.Response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        var me = await SendAsync(client, HttpMethod.Get, "/api/me", cookie);
        var foreignHost = await SendAsync(client, HttpMethod.Get, "/api/me", cookie, ("Host", $"localhost:{run.Origin.Port}"));
        var answers = new[] { anonymous, launch, me, foreignHost };
        Assert.Equal([401, 302, 200, 400], answers.Select(answer => answer.Status));
        Assert.All(answers, answer => Assert.False(answer.Response.Headers.Contains("Server")));
    }

    [Fact]
    public async Task The_launch_token_is_exchanged_once_for_an_http_only_strict_session_cookie_and_the_browser_sent_to_the_application()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        var token = run.LaunchToken!;
        var (status, _, response) = await SendAsync(client, HttpMethod.Get, $"/launch?token={token}", null);
        Assert.Equal((302, "/"), (status, response.Headers.Location!.OriginalString));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.StartsWith("desktop_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("expires", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain(token.ToLowerInvariant(), cookie, StringComparison.Ordinal);
        Assert.Null(run.LaunchToken);
        var again = await SendAsync(client, HttpMethod.Get, $"/launch?token={token}", null);
        Assert.Equal(401, again.Status);
        Assert.False(again.Response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task With_the_cookie_the_user_is_the_account_of_the_process_administrator_of_his_file()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        var cookie = await run.SignInAsync(client);
        var me = JsonDocument.Parse((await SendAsync(client, HttpMethod.Get, "/api/me", cookie)).Body).RootElement;
        Assert.Equal(run.Services.GetRequiredService<IProcessAccount>().Login, me.GetProperty("login").GetString());
        Assert.True(me.GetProperty("is_admin").GetBoolean());
        var health = await SendAsync(client, HttpMethod.Get, "/api/health", cookie);
        Assert.Equal((200, "ok"), (health.Status, JsonDocument.Parse(health.Body).RootElement.GetProperty("database").GetString()));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("rebound.example")]
    public async Task A_host_other_than_the_loopback_address_and_port_is_refused_against_dns_rebinding(string name)
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        var cookie = await run.SignInAsync(client);
        var refused = await SendAsync(client, HttpMethod.Get, "/api/me", cookie, ("Host", $"{name}:{run.Origin.Port}"));
        Assert.Equal((400, "REQUETE_INVALIDE"), (refused.Status, JsonDocument.Parse(refused.Body).RootElement.GetProperty("code").GetString()));
        var otherPort = await SendAsync(client, HttpMethod.Get, "/api/me", cookie, ("Host", $"127.0.0.1:{run.Origin.Port + 1}"));
        Assert.Equal(400, otherPort.Status);
    }

    [Fact]
    public async Task A_write_from_another_origin_or_another_site_is_refused_and_one_from_the_application_is_accepted()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        var cookie = await run.SignInAsync(client);
        var origin = run.Origin.GetLeftPart(UriPartial.Authority);
        var foreign = await SendAsync(client, HttpMethod.Patch, "/api/me", cookie, ("Origin", "http://attacker.example"));
        var crossSite = await SendAsync(client, HttpMethod.Patch, "/api/me", cookie, ("Sec-Fetch-Site", "cross-site"));
        var sameSite = await SendAsync(client, HttpMethod.Patch, "/api/me", cookie, ("Sec-Fetch-Site", "same-site"));
        Assert.Equal([403, 403, 403], new[] { foreign.Status, crossSite.Status, sameSite.Status });
        Assert.Equal("ACCES_REFUSE", JsonDocument.Parse(foreign.Body).RootElement.GetProperty("code").GetString());
        var accepted = await SendAsync(client, HttpMethod.Patch, "/api/me", cookie, ("Origin", origin), ("Sec-Fetch-Site", "same-origin"));
        Assert.Equal(200, accepted.Status);
        var read = await SendAsync(client, HttpMethod.Get, "/api/me", cookie, ("Sec-Fetch-Site", "cross-site"));
        Assert.Equal(200, read.Status);
    }

    [Fact]
    public async Task A_write_without_the_anti_csrf_header_is_refused_before_the_authentication_and_a_read_needs_none()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        var cookie = await run.SignInAsync(client);
        var origin = run.Origin.GetLeftPart(UriPartial.Authority);
        var withoutHeader = await SendAsync(client, HttpMethod.Patch, "/api/me", cookie, antiCsrf: false, ("Origin", origin), ("Sec-Fetch-Site", "same-origin"));
        Assert.Equal((400, "EN_TETE_MANQUANT"), (withoutHeader.Status, JsonDocument.Parse(withoutHeader.Body).RootElement.GetProperty("code").GetString()));
        var anonymous = await SendAsync(client, HttpMethod.Post, "/api/desktop/quit", null, antiCsrf: false);
        Assert.Equal(400, anonymous.Status);
        var empty = await SendAsync(client, HttpMethod.Patch, "/api/me", cookie, antiCsrf: false, ("X-Requested-With", " "));
        Assert.Equal(400, empty.Status);
        Assert.Equal(200, (await SendAsync(client, HttpMethod.Get, "/api/me", cookie, antiCsrf: false)).Status);
        Assert.Equal(200, (await SendAsync(client, HttpMethod.Patch, "/api/me", cookie, antiCsrf: false, ("X-Requested-With", "any value"))).Status);
    }

    [Fact]
    public async Task A_body_sent_without_length_is_cut_at_the_limit_by_the_server_and_refused_413()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        var cookie = await run.SignInAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Patch, new Uri("/api/me", UriKind.Relative));
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("{\"display_name\":\"" + new string('a', 2 * 1024 * 1024) + "\"}")));
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.TransferEncodingChunked = true;
        using var response = await client.SendAsync(request, Token);
        var text = await response.Content.ReadAsStringAsync(Token);
        Assert.True((int)response.StatusCode == 413 && text.Length > 0, $"{(int)response.StatusCode}: {text}");
        Assert.Equal("REQUETE_TROP_GROSSE", JsonDocument.Parse(text).RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_write_is_logged_with_its_route_status_and_user_identifier_and_a_read_is_not_and_no_login_is_written()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        var cookie = await run.SignInAsync(client);
        var me = JsonDocument.Parse((await SendAsync(client, HttpMethod.Get, "/api/me", cookie)).Body).RootElement;
        Assert.Equal(404, (await SendAsync(client, HttpMethod.Put, $"/api/projects/{Guid.CreateVersion7()}/sharing-group-label?q=secret", cookie)).Status);
        await run.StopAsync();
        var log = Assert.Single(Directory.GetFiles(Path.Combine(run.Folder.FullName, "logs"), "topogenia-*.log"));
        var text = await File.ReadAllTextAsync(log, Token);
        Assert.Contains($"PUT /api/projects/{{id:guid}}/sharing-group-label 404", text, StringComparison.Ordinal);
        Assert.Contains($"(user {me.GetProperty("id").GetGuid()})", text, StringComparison.Ordinal);
        Assert.DoesNotContain("GET /api/me", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain(me.GetProperty("login").GetString()!, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_start_creates_the_data_file_the_backup_of_the_day_and_a_log_without_path_in_the_folder_of_the_product()
    {
        await using var run = await DesktopRun.StartAsync();
        Assert.True(File.Exists(Path.Combine(run.Folder.FullName, "data", "topogenia.db")));
        Assert.Single(Directory.GetFiles(Path.Combine(run.Folder.FullName, "backups"), "topogenia_*.db"));
        // A write is logged by the request log (the framework writes its errors only).
        using var client = run.Client();
        var cookie = await run.SignInAsync(client);
        await SendAsync(client, HttpMethod.Patch, "/api/me", cookie);
        var log = Assert.Single(Directory.GetFiles(Path.Combine(run.Folder.FullName, "logs"), "topogenia-*.log"));
        using (var reader = new StreamReader(new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            Assert.DoesNotContain(run.Folder.FullName, await reader.ReadToEndAsync(Token), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_log_is_the_file_alone_without_the_paths_nor_the_query_strings_that_the_framework_writes()
    {
        await using var run = await DesktopRun.StartAsync(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Trace" });
        Assert.Equal([typeof(Topogenia.Host.Logging.FileLoggingExtensions).Assembly.GetType("Topogenia.Host.Logging.FileLoggerProvider")],
            run.Services.GetServices<ILoggerProvider>().Select(provider => provider.GetType()));
        Assert.Equal(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), run.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath.TrimEnd(Path.DirectorySeparatorChar));
        using var client = run.Client();
        await run.SignInAsync(client);
        await run.StopAsync();
        var log = Assert.Single(Directory.GetFiles(Path.Combine(run.Folder.FullName, "logs"), "topogenia-*.log"));
        var text = await File.ReadAllTextAsync(log, Token);
        Assert.DoesNotContain("token=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Content root", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_kestrel_section_of_the_configuration_adds_no_address_and_the_keys_stay_in_the_folder_of_the_product()
    {
        await using var run = await DesktopRun.StartAsync(new Dictionary<string, string?> { ["Kestrel:Endpoints:Extra:Url"] = "http://0.0.0.0:0" });
        var address = new Uri(Assert.Single(run.Addresses));
        Assert.Equal(IPAddress.Loopback.ToString(), address.Host);
        var keys = run.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>>().Value;
        var repository = Assert.IsType<Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository>(keys.XmlRepository);
        Assert.Equal(Path.Combine(run.Folder.FullName, "keys"), repository.Directory.FullName);
    }

    [Fact]
    public async Task The_command_quit_stops_the_application_after_answering()
    {
        await using var run = await DesktopRun.StartAsync();
        using var client = run.Client();
        var cookie = await run.SignInAsync(client);
        var stopping = run.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        Assert.Equal(202, (await SendAsync(client, HttpMethod.Post, "/api/desktop/quit", cookie)).Status);
        Assert.True(stopping.IsCancellationRequested);
    }

    [Fact]
    public void Each_start_draws_a_new_launch_token_of_256_bits_that_is_not_the_session_secret()
    {
        var first = new DesktopSession();
        var second = new DesktopSession();
        Assert.NotEqual(first.LaunchToken, second.LaunchToken);
        Assert.Equal(32, Convert.FromBase64String(first.LaunchToken!.Replace('-', '+').Replace('_', '/') + "=").Length);
        Assert.False(first.IsSession(first.LaunchToken));
        Assert.Null(first.Exchange(second.LaunchToken));
        var secret = first.Exchange(first.LaunchToken);
        Assert.True(first.IsSession(secret));
        Assert.False(second.IsSession(secret));
    }

    [Fact]
    public async Task At_the_stop_the_log_of_sqlite_is_written_back_into_the_data_file_and_no_log_survives()
    {
        await using var run = await DesktopRun.StartAsync();
        using (var client = run.Client())
        {
            var cookie = await run.SignInAsync(client);
            Assert.Equal(200, (await SendAsync(client, HttpMethod.Patch, "/api/me", cookie)).Status);
        }
        var log = Path.Combine(run.Folder.FullName, "data", "topogenia.db-wal");
        await run.StopAsync();
        Assert.True(!File.Exists(log) || new FileInfo(log).Length == 0);
    }
}
