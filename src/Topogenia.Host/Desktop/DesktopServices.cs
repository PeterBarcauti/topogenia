// DesktopServices.cs
// The parts of the desktop mode: listening on 127.0.0.1 for the account of the process, session, data file, backups, start, browser (spec 5.2, 6.1).

using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;
using Topogenia.Host.Logging;
using Topogenia.Host.Security;
using Topogenia.Host.Startup;
using Topogenia.Host.Users;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Backup;
using Topogenia.Persistence.Sqlite.Files;

namespace Topogenia.Host.Desktop;

/// <summary>
/// Registers the desktop mode: the server listens on `127.0.0.1` only, in IPv4, never on all the addresses (spec 5.2), on
/// the port of the configuration or a free one, and names itself in no `Server` header; it keeps only the connections of
/// the processes of its own account (<see cref="DesktopConnectionGuard"/>, ADR 075); the session cookie authenticates the
/// account of the process, administrator of its file; the logs go to the `logs` folder of the product (ADR 021); the
/// data file of the folder of the product is opened by the SQLite engine, backed up by the online backup of SQLite, and
/// opened at the start by <see cref="DesktopDatabase"/>; the browser is opened once the server listens.
/// </summary>
public static class DesktopServices
{
    public static void AddDesktop(this WebApplicationBuilder builder, DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            // Spec 5.2: the loopback only. A `Kestrel` section of the configuration (`Kestrel:Endpoints`) would add an
            // address of its own, on the network as well: an empty configuration replaces it.
            kestrel.Configure(new ConfigurationBuilder().Build(), reloadOnChange: false);
            // No header names the server (ASVS V14.3.3), as the web.config of the server mode does under IIS.
            kestrel.AddServerHeader = false;
            // ADR 075: a connection whose client process runs under another account is closed before a byte is read.
            kestrel.Listen(IPAddress.Loopback, options.Port,
                listen => listen.Use(next => listen.ApplicationServices.GetRequiredService<DesktopConnectionGuard>().Use(next)));
        });
        builder.Logging.AddDailyLogFile(DesktopPaths.Of(options).LogsFolder);
        var services = builder.Services;
        services.AddSingleton(provider => DesktopPaths.Of(provider.GetRequiredService<IOptions<ProductOptions>>().Value.Desktop));
        services.AddSingleton(_ => DataFileEnvironment.Current());
        services.AddSingleton(provider => new SqliteEngine(provider.GetRequiredService<DesktopPaths>().ConnectionString));
        services.AddSingleton<DatabaseEngine>(provider => provider.GetRequiredService<SqliteEngine>());
        services.AddSingleton<IBackupService>(provider => new SqliteBackupService(provider.GetRequiredService<SqliteEngine>(),
            provider.GetRequiredService<DesktopPaths>().BackupsFolder, provider.GetRequiredService<IClock>(), provider.GetRequiredService<DataFileEnvironment>()));
        services.AddSingleton<DesktopDatabase>();
        services.AddSingleton<DesktopInstance>();
        services.AddSingleton<DesktopRecovery>();
        services.AddSingleton<DesktopSession>();
        services.AddSingleton<DesktopAddress>();
        services.AddSingleton<IRequestAuthority>(provider => provider.GetRequiredService<DesktopAddress>());
        services.AddSingleton<IProcessAccount, ProcessAccount>();
        // A test may give its own owners of the connections before (TryAdd).
        services.TryAddSingleton<IConnectionOwners, WindowsConnectionOwners>();
        services.AddSingleton<DesktopConnectionGuard>();
        services.AddSingleton<IAdministrators, DesktopAdministrators>();
        services.AddSingleton<IUserDirectory, EmptyDirectory>();
        // A test may give its own browser before (TryAdd).
        services.TryAddSingleton<IBrowserLauncher, ShellBrowserLauncher>();
        services.AddHostedService<DesktopLaunchService>();
        services.AddHostedService<DesktopReopenService>();
        // Spec 6.1: nothing written outside the folder of the product; the keys that ASP.NET Core may create are kept there,
        // protected for the account of the user.
        var protection = services.AddDataProtection().SetApplicationName(Application.ProductIdentity.Name)
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(DesktopPaths.Of(options).Folder, "keys")));
        if (OperatingSystem.IsWindows())
            protection.ProtectKeysWithDpapi();
        services.AddAuthentication(DesktopAuthenticationHandler.Name)
            .AddScheme<AuthenticationSchemeOptions, DesktopAuthenticationHandler>(DesktopAuthenticationHandler.Name, null);
    }
}
