// DesktopConnectionTests.cs
// The connections of the desktop mode: one of a process of another account is closed unanswered, even with the session cookie; the owner of a connection as Windows tells it (spec 5.2, ADR 075).

using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topogenia.Host.Desktop;
using Topogenia.Host.Security;

namespace Topogenia.Host.Tests.Desktop;

[SupportedOSPlatform("windows")]
public sealed class DesktopConnectionTests
{
    /// <summary>The number of a process of another account, as the simulated owner gives it.</summary>
    private const int ForeignProcess = 4242;

    /// <summary>The SID of an account of a domain, not the one that runs the tests.</summary>
    private const string ForeignAccount = "S-1-5-21-1004336348-1177238915-682003330-1105";

    /// <summary>The line the guard writes for each connection it closes: no login, no account, no process number.</summary>
    private const string RefusalLine =
        " Warning Topogenia.Host.Desktop.DesktopConnectionGuard A connection is closed unread: its client process runs under another account, or its account cannot be read.";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string OwnAccount
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User!.Value;
        }
    }

    /// <summary>The owner of every connection, as the test sets it: the process and account of the tests, another one, or none.</summary>
    private sealed class SimulatedOwners : IConnectionOwners
    {
        private volatile ConnectionOwner? owner;

        public ConnectionOwner? Owner
        {
            get => owner;
            set => owner = value;
        }

        public ConnectionOwner? OwnerOf(IPEndPoint server, IPEndPoint client) => owner;
    }

    [Fact]
    public async Task A_connection_of_a_process_of_another_account_is_closed_unanswered_even_with_the_session_cookie_of_the_run()
    {
        var owners = new SimulatedOwners { Owner = new ConnectionOwner(Environment.ProcessId, OwnAccount) };
        await using var run = await DesktopRun.StartAsync(owners: owners);
        using var client = run.Client();
        var cookie = await run.SignInAsync(client);
        Assert.StartsWith("HTTP/1.1 200 ", await RawAsync(run.Origin, Read(run.Origin, cookie)), StringComparison.Ordinal);

        // The cookie of this run, replayed by a client of another account (security M-1), or by one whose account cannot be read.
        owners.Owner = new ConnectionOwner(ForeignProcess, ForeignAccount);
        Assert.Empty(await RawAsync(run.Origin, Read(run.Origin, cookie)));
        Assert.Empty(await RawAsync(run.Origin, Write(run.Origin, cookie, "Replayed")));
        owners.Owner = null;
        Assert.Empty(await RawAsync(run.Origin, Write(run.Origin, cookie, "Unknown")));

        // The same bytes from a process of the account are answered: the write refused above was a valid one.
        owners.Owner = new ConnectionOwner(Environment.ProcessId, OwnAccount);
        using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/me", UriKind.Relative)))
        {
            request.Headers.Add("Cookie", cookie);
            using var response = await client.SendAsync(request, Token);
            var me = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement;
            Assert.DoesNotContain(me.GetProperty("display_name").GetString(), new[] { "Replayed", "Unknown" });
        }
        Assert.StartsWith("HTTP/1.1 200 ", await RawAsync(run.Origin, Write(run.Origin, cookie, "Accepted")), StringComparison.Ordinal);

        await run.StopAsync();
        var log = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(Path.Combine(run.Folder.FullName, "logs"), "topogenia-*.log")), Token);
        var refusals = log.Split('\n').Where(line => line.Contains("DesktopConnectionGuard", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, refusals.Count);
        Assert.All(refusals, line => Assert.EndsWith(RefusalLine, line, StringComparison.Ordinal));
        Assert.DoesNotContain(ForeignAccount, log, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnAccount, log, StringComparison.Ordinal);
    }

    [Fact]
    public void The_guard_admits_a_connection_only_when_its_owner_is_known_and_runs_under_the_account_of_the_application()
    {
        var owners = new SimulatedOwners();
        var guard = new DesktopConnectionGuard(owners, NullLogger<DesktopConnectionGuard>.Instance);
        var server = new IPEndPoint(IPAddress.Loopback, 5212);
        var client = new IPEndPoint(IPAddress.Loopback, 50123);
        owners.Owner = new ConnectionOwner(ForeignProcess, OwnAccount);
        Assert.True(guard.Admits(server, client));
        owners.Owner = new ConnectionOwner(Environment.ProcessId, ForeignAccount);
        Assert.False(guard.Admits(server, client));
        owners.Owner = null;
        Assert.False(guard.Admits(server, client));
        owners.Owner = new ConnectionOwner(Environment.ProcessId, OwnAccount);
        Assert.False(guard.Admits(null, client));
        Assert.False(guard.Admits(server, null));
        Assert.True(guard.Admits(server, client));
    }

    /// <summary>The client socket in IPv4 only, or in both families (`TcpClient()`, `HttpClient`), whose end is then IPv4 mapped to IPv6.</summary>
    [Theory]
    [InlineData(AddressFamily.InterNetwork)]
    [InlineData(AddressFamily.InterNetworkV6)]
    public async Task Windows_tells_the_process_and_the_account_of_the_client_end_of_a_loopback_connection_and_no_owner_for_an_end_of_none(AddressFamily family)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        using var other = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        other.Start();
        using var client = family == AddressFamily.InterNetwork ? new TcpClient(family) : new TcpClient();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, Token);
        using var accepted = await listener.AcceptTcpClientAsync(Token);
        var server = (IPEndPoint)accepted.Client.LocalEndPoint!;
        var remote = (IPEndPoint)accepted.Client.RemoteEndPoint!;
        Assert.Equal((family, ((IPEndPoint)client.Client.LocalEndPoint!).Port), (client.Client.AddressFamily, remote.Port));
        var owners = new WindowsConnectionOwners();

        Assert.Equal(new ConnectionOwner(Environment.ProcessId, OwnAccount), owners.OwnerOf(server, remote));
        // A client end of no connection, the client end towards another server, an end that is not IPv4: no owner.
        var elsewhere = (IPEndPoint)other.LocalEndpoint;
        Assert.Null(owners.OwnerOf(server, elsewhere));
        Assert.Null(owners.OwnerOf(elsewhere, remote));
        Assert.Null(owners.OwnerOf(new IPEndPoint(IPAddress.IPv6Loopback, server.Port), remote));
    }

    /// <summary>A read of the API with the cookie, on a connection that the server closes once it has answered.</summary>
    private static string Read(Uri origin, string cookie) =>
        $"GET /api/me HTTP/1.1\r\nHost: {origin.Authority}\r\nCookie: {cookie}\r\nConnection: close\r\n\r\n";

    /// <summary>A write of the API with the cookie and the anti-CSRF header, which renames the user.</summary>
    private static string Write(Uri origin, string cookie, string name)
    {
        var body = $"{{\"display_name\":\"{name}\"}}";
        return $"PATCH /api/me HTTP/1.1\r\nHost: {origin.Authority}\r\nCookie: {cookie}\r\n{RequestGuard.CsrfHeader}: XMLHttpRequest\r\n"
            + $"Content-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
    }

    /// <summary>Sends <paramref name="request"/> on a connection of its own and gives all that the server answers until it closes it.</summary>
    private static async Task<string> RawAsync(Uri origin, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, origin.Port, Token);
        using var stream = client.GetStream();
        using var answer = new MemoryStream();
        try
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(request), Token);
            await stream.CopyToAsync(answer, Token);
        }
        catch (IOException reset) when (reset.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted })
        {
            // The server closed the connection under the request: what it sent before, if anything, is all of its answer.
        }
        return Encoding.UTF8.GetString(answer.ToArray());
    }
}
