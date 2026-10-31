// ServerSettings.cs
// The configuration the server mode cannot start without, and the names of the server its requests may give (spec 5.3, 6.2, A-4-19).

using Microsoft.Extensions.Options;
using Topogenia.Host.Composition;
using Topogenia.Host.Security;
using Topogenia.Host.Startup;

namespace Topogenia.Host.Server;

/// <summary>
/// The start of the server mode is refused (spec 5.1: a missing setting takes the safest value, here none) without SQL
/// Server (MODE_SERVEUR_SQLITE), without the connection string (CHAINE_CONNEXION_ABSENTE), with a delay of the migrations
/// out of its bounds (DELAI_MIGRATIONS_INVALIDE, ADR 050), or without the explicit list of
/// the names of the server: an empty list, an empty name or a name with `*` is HOTES_AUTORISES_INVALIDES (A-4-19; the
/// 1.x answered any name, `AllowedHosts:"*"`).
/// </summary>
public static class ServerSettings
{
    public static StartupRefusedException? Refusal(ProductOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Database.Provider == DatabaseKind.Sqlite)
            return new StartupRefusedException(StartupCodes.ServerModeWithSqlite);
        if (string.IsNullOrWhiteSpace(options.Database.ConnectionString))
            return new StartupRefusedException(StartupCodes.MissingConnectionString);
        var (minimum, maximum) = DatabaseOptions.MigrationTimeoutBounds;
        if (options.Database.MigrationTimeoutMinutes < minimum || options.Database.MigrationTimeoutMinutes > maximum)
            return new StartupRefusedException(StartupCodes.InvalidMigrationTimeout);
        var hosts = options.Server.AllowedHosts;
        if (hosts.Count == 0 || hosts.Any(host => string.IsNullOrWhiteSpace(host) || host.Contains('*', StringComparison.Ordinal)))
            return new StartupRefusedException(StartupCodes.InvalidAllowedHosts);
        return null;
    }
}

/// <summary>
/// The names of the server mode: `Host` must give one of <see cref="ServerOptions.AllowedHosts"/>, case ignored, any port
/// (IIS chooses it); the origin of the application is the scheme and the authority of the request itself.
/// </summary>
public sealed class ServerAuthority(IOptions<ProductOptions> options) : IRequestAuthority
{
    public bool Accepts(HostString host) =>
        host.HasValue && options.Value.Server.AllowedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase);

    public string OriginOf(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return $"{request.Scheme}://{request.Host.Value}";
    }
}
