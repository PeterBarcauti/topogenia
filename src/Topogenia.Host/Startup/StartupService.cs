// StartupService.cs
// What the host does before the first request and after the last one: checks of the mode, opening of the data file, clean stop (spec 4.4, 5.2, 6.2).

using System.Diagnostics;
using Microsoft.Extensions.Options;
using Topogenia.Domain.Errors;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;
using Topogenia.Host.Server;
using Topogenia.Persistence.Sqlite;

namespace Topogenia.Host.Startup;

/// <summary>
/// Runs before the server accepts a request (a hosted service starts before the server):
/// <list type="bullet">
/// <item>the server mode with SQLite is refused (MODE_SERVEUR_SQLITE, spec 6.2), as a server mode without the list of
/// its names or without its connection string (<see cref="ServerSettings"/>);</item>
/// <item>in server mode, the database is checked, never migrated (<see cref="ServerDatabase"/>);</item>
/// <item>in desktop mode, the named lock of the data file is taken; held by an application that answers, the start asks it
/// to open the browser on itself and ends (<see cref="DesktopReopen"/>, spec 6.1), else INSTANCE_DEJA_LANCEE (<see cref="DesktopInstance"/>),
/// then the data file is opened (<see cref="DesktopDatabase"/>); a refusal that a backup can mend is
/// followed by the offer to restore one (<see cref="DesktopRecovery"/>, ADR 029), then the start runs again; a folder
/// of the data file or of the backups readable by the other accounts of the machine is only warned of (AR-98).</item>
/// </list>
/// A refusal is written to the console in the language of the user and thrown as <see cref="StartupRefusedException"/>:
/// the host does not start and the process ends with a non-zero code (AR-15). At the stop, once the server no longer
/// serves, the SQLite engine of the desktop mode writes its log back into the file and closes it (ADR 029, data B1).
/// </summary>
public sealed class StartupService(IServiceProvider services, IOptions<ProductOptions> options, StartupConsole console)
    : IHostedService
{
    private SqliteEngine? desktopEngine;
    private DesktopInstance? desktopInstance;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (options.Value.Mode == HostMode.Server)
            {
                if (ServerSettings.Refusal(options.Value) is { } refusal)
                    throw refusal;
                await services.GetRequiredService<ServerDatabase>().CheckAsync(cancellationToken);
            }
            else
            {
                await OpenDesktopAsync(cancellationToken);
            }
        }
        catch (StartupRefusedException refused)
        {
            console.Write(refused.MessageKey, refused.Details);
            if (options.Value.Mode == HostMode.Desktop && DesktopRecovery.RestorableCodes.Contains(refused.Code))
                console.Write("SAUVEGARDES_DOSSIER", new Dictionary<string, object?> { ["folder"] = services.GetRequiredService<DesktopPaths>().BackupsFolder });
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (desktopEngine is { } sqlite)
            await sqlite.CloseAsync(cancellationToken);
        desktopInstance?.Release();
    }

    private async Task OpenDesktopAsync(CancellationToken cancellationToken)
    {
        // Taken now: at the stop, the services may already be released.
        desktopEngine = services.GetRequiredService<SqliteEngine>();
        desktopInstance = services.GetRequiredService<DesktopInstance>();
        if (!desktopInstance.TryAcquire())
        {
            // Spec 6.1: a second start opens the browser on the application already running, then ends.
            if (DesktopReopen.Ask(services.GetRequiredService<DesktopPaths>().DataFile, cancellationToken))
            {
                console.Write("DEJA_LANCEE_OUVERTE");
                throw new StartupHandedOverException();
            }
            throw new StartupRefusedException(StartupCodes.AlreadyRunning);
        }
        var database = services.GetRequiredService<DesktopDatabase>();
        StartupRefusedException refused;
        try
        {
            WarnOfExposedFolders(console, services.GetRequiredService<DesktopPaths>(), await database.OpenAsync(cancellationToken));
            return;
        }
        catch (StartupRefusedException first)
        {
            refused = first;
        }
        if (!await services.GetRequiredService<DesktopRecovery>().OfferAsync(refused, cancellationToken))
            throw refused;
        WarnOfExposedFolders(console, services.GetRequiredService<DesktopPaths>(), await database.OpenAsync(cancellationToken));
    }

    /// <summary>AR-98: the console names each folder readable by others and recommends the place by default; the start goes on.</summary>
    public static void WarnOfExposedFolders(StartupConsole console, DesktopPaths paths, IReadOnlyList<string> places)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(places);
        foreach (var place in places)
        {
            console.Write(place == DesktopDatabase.DataPlace ? "DOSSIER_DONNEES_EXPOSE" : "DOSSIER_SAUVEGARDES_EXPOSE",
                new Dictionary<string, object?> { ["folder"] = place == DesktopDatabase.DataPlace ? Path.GetDirectoryName(paths.DataFile) : paths.BackupsFolder });
        }
    }
}

/// <summary>Opens the default browser of the user on an address (spec 2 D10).</summary>
public interface IBrowserLauncher
{
    void Open(string address);
}

/// <summary>The default browser of the system, opened by the shell.</summary>
public sealed class ShellBrowserLauncher : IBrowserLauncher
{
    public void Open(string address)
    {
        using var process = Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
    }
}

/// <summary>
/// Once the server listens in desktop mode: the console says the address (without the launch token, which only the
/// browser receives) and how to quit, and the default browser is opened on the exchange of the launch token, unless the
/// configuration says not to (spec 5.2, 6.1).
/// </summary>
public sealed class DesktopLaunchService(IHostApplicationLifetime lifetime, IOptions<ProductOptions> options, DesktopAddress address,
    DesktopSession session, IBrowserLauncher browser, StartupConsole console) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() =>
        {
            console.Write("PRET", new Dictionary<string, object?> { ["address"] = address.Origin });
            console.Write("QUITTER");
            if (options.Value.Desktop.OpenBrowser && session.LaunchToken is { } token)
                browser.Open(address.LaunchAddress(token));
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>The command « Quit » of the interface in desktop mode (spec 5.2): the host stops after answering.</summary>
public static class DesktopQuit
{
    public static void Stop(IHostApplicationLifetime lifetime, IOptions<ProductOptions> options)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Value.Mode != HostMode.Desktop)
            throw DomainException.NotFound();
        lifetime.StopApplication();
    }
}
