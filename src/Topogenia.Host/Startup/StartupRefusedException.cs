// StartupRefusedException.cs
// The start of the host refused with a stable code and its values: the console and the exit code say why (AR-15, spec 4.4, 5.2).

using System.Globalization;

namespace Topogenia.Host.Startup;

/// <summary>The codes of the refusals of the start that are not those of the schema, the data file, the backup or the restore.</summary>
public static class StartupCodes
{
    /// <summary>The server mode with SQLite is forbidden (spec 6.2).</summary>
    public const string ServerModeWithSqlite = "MODE_SERVEUR_SQLITE";

    /// <summary>The backup before a migration failed: the database was not migrated (AR-14); `failure` gives the code of the backup.</summary>
    public const string MigrationWithoutBackup = "MIGRATION_SANS_SAUVEGARDE";

    /// <summary>The server mode needs the explicit list of the names of the server, without `*` (A-4-19).</summary>
    public const string InvalidAllowedHosts = "HOTES_AUTORISES_INVALIDES";

    /// <summary>The server mode needs the connection string of its SQL Server database (spec 6.2).</summary>
    public const string MissingConnectionString = "CHAINE_CONNEXION_ABSENTE";

    /// <summary>The delay of the migrations is out of its bounds, 1 to 1440 minutes (ADR 041, ADR 050).</summary>
    public const string InvalidMigrationTimeout = "DELAI_MIGRATIONS_INVALIDE";

    /// <summary>Another process of the application already runs on the data file of the desktop mode (ADR 029, <see cref="Desktop.DesktopInstance"/>).</summary>
    public const string AlreadyRunning = "INSTANCE_DEJA_LANCEE";
}

/// <summary>
/// The host does not start: <see cref="Code"/> is a code of <see cref="StartupCodes"/>, of the schema, of the data file or
/// of the settings, with its values (the migration, the place of a file); its message is the code and the values, never a
/// path nor the message of an engine. The console writes the sentence of the code (`Startup/messages.*.json`).
/// </summary>
public sealed class StartupRefusedException : Exception
{
    public StartupRefusedException(string code, IReadOnlyDictionary<string, object?>? details = null)
        : base(code + Describe(details))
    {
        Code = code;
        Details = details ?? new Dictionary<string, object?>();
    }

    public StartupRefusedException()
        : this(StartupCodes.ServerModeWithSqlite)
    {
    }

    public StartupRefusedException(string message, Exception inner)
        : base(message, inner)
    {
        Code = message;
        Details = new Dictionary<string, object?>();
    }

    public string Code { get; }

    /// <summary>The key of the sentence the console writes: the code, unless the mode words it its own way.</summary>
    public string MessageKey
    {
        get => messageKey ?? Code;
        init => messageKey = value;
    }

    public IReadOnlyDictionary<string, object?> Details { get; }

    private readonly string? messageKey;

    private static string Describe(IReadOnlyDictionary<string, object?>? details) =>
        details is null || details.Count == 0
            ? ""
            : " (" + string.Join(", ", details.Where(pair => pair.Key != "place" && pair.Key != "folder")
                .Select(pair => string.Create(CultureInfo.InvariantCulture, $"{pair.Key}={pair.Value}"))) + ")";
}

/// <summary>
/// The start ends without a refusal: another process of the application runs on the data file, and it opened the browser
/// on itself at the request of this one (spec 6.1, <see cref="Desktop.DesktopReopen"/>); the process ends with a zero code.
/// </summary>
public sealed class StartupHandedOverException : Exception
{
    public StartupHandedOverException()
        : base("The application already running opened itself.")
    {
    }
}
