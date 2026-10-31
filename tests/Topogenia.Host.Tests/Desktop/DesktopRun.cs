// DesktopRun.cs
// The host started for real in desktop mode (Kestrel on the loopback, a free port) in a temporary folder of the product.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Startup;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Desktop;

/// <summary>
/// One run of the executable as a user starts it, built by <see cref="HostApplication"/> like the executable: desktop mode,
/// port 0 (a free port), no browser, a temporary folder of the product removed at the end. The client sends no cookie of
/// its own and follows no redirection, so that each test sees the headers the host writes.
/// </summary>
public sealed class DesktopRun : IAsyncDisposable
{
    private readonly WebApplication application;
    private bool stopped;

    private DesktopRun(WebApplication application, DirectoryInfo folder)
    {
        this.application = application;
        Folder = folder;
    }

    public DirectoryInfo Folder { get; }

    public IServiceProvider Services => application.Services;

    public IReadOnlyList<string> Addresses => [.. application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses];

    public Uri Origin => new(Addresses.Single());

    public string? LaunchToken => Services.GetRequiredService<DesktopSession>().LaunchToken;

    /// <summary>
    /// Starts the run; <paramref name="browser"/>, when given, is the browser the application opens (spec 6.1);
    /// <paramref name="owners"/>, when given, tells the owner of each connection in place of Windows (ADR 075).
    /// </summary>
    public static async Task<DesktopRun> StartAsync(IReadOnlyDictionary<string, string?>? settings = null, IBrowserLauncher? browser = null,
        IConnectionOwners? owners = null)
    {
        var folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-");
        var section = ProductOptions.Section;
        var configuration = new Dictionary<string, string?>
        {
            [$"{section}:Mode"] = nameof(HostMode.Desktop),
            [$"{section}:Desktop:Folder"] = folder.FullName,
            [$"{section}:Desktop:Port"] = "0",
            [$"{section}:Desktop:OpenBrowser"] = "false",
            [$"{section}:Languages:Supported:0"] = "fr",
            [$"{section}:Languages:Supported:1"] = "en",
            [$"{section}:Languages:Default"] = "fr",
            [$"{section}:Languages:FirstLaneNames:fr"] = "Acteur 1",
            [$"{section}:Languages:FirstLaneNames:en"] = "Actor 1",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            configuration[key] = value;
        var application = HostApplication.Build([], builder =>
        {
            builder.Configuration.AddInMemoryCollection(configuration);
            if (browser is not null)
                builder.Services.AddSingleton(browser);
            if (owners is not null)
                builder.Services.AddSingleton(owners);
        });
        try
        {
            await application.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await application.DisposeAsync();
            await RemoveAsync(folder);
            throw;
        }
        return new DesktopRun(application, folder);
    }

    /// <summary>A client of the application that keeps no cookie and follows no redirection.</summary>
    public HttpClient Client() =>
        new(new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = Origin };

    /// <summary>Exchanges the launch token and returns the cookie header to send.</summary>
    public async Task<string> SignInAsync(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var response = await client.GetAsync(new Uri($"{DesktopSession.LaunchPath}?{DesktopSession.TokenParameter}={LaunchToken}", UriKind.Relative),
            TestContext.Current.CancellationToken);
        return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }

    /// <summary>Stops the application as the user quits it; the folder stays until the run is disposed.</summary>
    public async Task StopAsync()
    {
        if (stopped)
            return;
        stopped = true;
        await application.StopAsync(CancellationToken.None);
        await application.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await RemoveAsync(Folder);
    }

    private static async Task RemoveAsync(DirectoryInfo folder)
    {
        for (var attempt = 1; ; attempt++)
        {
            SqliteConnection.ClearAllPools();
            folder.Refresh();
            if (!folder.Exists)
                return;
            try
            {
                folder.Delete(recursive: true);
                return;
            }
            catch (IOException) when (attempt < 50)
            {
                await Task.Delay(100);
            }
        }
    }
}
