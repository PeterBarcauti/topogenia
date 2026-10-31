// ServerServices.cs
// The parts of the server mode: SQL Server, Windows authentication, names of the server, HTTPS and HSTS, directory, daily log (spec 5.3, 6.2).

using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.Options;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;
using Topogenia.Host.Logging;
using Topogenia.Host.Security;
using Topogenia.Host.Users;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;

namespace Topogenia.Host.Server;

/// <summary>
/// Registers the server mode: the SQL Server database of the configuration, checked at the start
/// (<see cref="ServerDatabase"/>); Windows authentication (Negotiate: Kerberos, else NTLM; behind IIS, the Windows
/// authentication of IIS), anonymous access refused by the fallback policy; the administrators of the configuration,
/// read again at each change of the file (spec 5.3); the names of the server (<see cref="ServerAuthority"/>); HSTS for a
/// year and the redirection of HTTP to HTTPS by 308, which keeps the method of a write (spec 5.3); the Windows directory;
/// the daily log in the configured folder, else `logs` in the folder of the application (A-4-25).
/// </summary>
public static class ServerServices
{
    /// <summary>The duration of HSTS: one year, the value the browsers expect of a server that stays in HTTPS.</summary>
    public static readonly TimeSpan HstsMaxAge = TimeSpan.FromDays(365);

    public static void AddServer(this WebApplicationBuilder builder, ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        builder.Logging.AddDailyLogFile(LogsFolder(options, builder.Environment.ContentRootPath));
        var services = builder.Services;
        // Resolved after the check of the settings (ServerSettings), which refuses a delay out of its bounds.
        services.AddSingleton<DatabaseEngine>(provider =>
        {
            var database = provider.GetRequiredService<IOptions<ProductOptions>>().Value.Database;
            return new SqlServerEngine(database.ConnectionString) { MigrationTimeout = TimeSpan.FromMinutes(database.MigrationTimeoutMinutes) };
        });
        services.AddSingleton<ServerDatabase>();
        services.AddSingleton<IAdministrators, ConfiguredAdministrators>();
        services.AddSingleton<IUserDirectory, WindowsDirectory>();
        services.AddSingleton<IRequestAuthority, ServerAuthority>();
        services.AddHsts(hsts => hsts.MaxAge = HstsMaxAge);
        services.AddHttpsRedirection(redirection => redirection.RedirectStatusCode = StatusCodes.Status308PermanentRedirect);
        services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
    }

    /// <summary>The folder of the daily log: the configured one, relative to the folder of the application when it is not rooted.</summary>
    public static string LogsFolder(ServerOptions options, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Path.GetFullPath(string.IsNullOrWhiteSpace(options.LogsFolder) ? "logs" : options.LogsFolder, contentRoot);
    }
}
