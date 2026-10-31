// ProductOptions.cs
// The configuration of the host, read from the section named after the product: mode, database, desktop, administrators, languages.

using Topogenia.Application;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Composition;

/// <summary>The mode of the host (spec 6.2, `Mode` of the section of the product): one binary serves both.</summary>
public enum HostMode
{
    /// <summary>One user on his workstation: SQLite file, 127.0.0.1, launch token (spec 5.2, 6.1).</summary>
    Desktop,

    /// <summary>Several users behind IIS: SQL Server, Windows authentication, HTTPS (spec 5.3, 6.2).</summary>
    Server,
}

/// <summary>The engine of the database (`Database:Provider` of the section of the product, spec 6.2).</summary>
public enum DatabaseKind
{
    Sqlite,
    SqlServer,
}

/// <summary>The database of the server mode; the desktop mode always opens its own SQLite file (<see cref="DesktopOptions"/>).</summary>
public sealed class DatabaseOptions
{
    public DatabaseKind Provider { get; set; } = DatabaseKind.SqlServer;

    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// The delay of the migrations and of the wait for their lock, in minutes (ADR 041): 10 by default, from 1 to 1440
    /// (one day), longer for a large database migrated by `--migrate`; any other value refuses the start.
    /// </summary>
    public int MigrationTimeoutMinutes { get; set; } = 10;

    /// <summary>The bounds of <see cref="MigrationTimeoutMinutes"/>: one minute, one day.</summary>
    public static (int Minimum, int Maximum) MigrationTimeoutBounds { get; } = (1, 24 * 60);
}

/// <summary>
/// The desktop mode (spec 5.2, 6.1): the folder of the product (by default the folder named after the product under
/// `%LOCALAPPDATA%`, which holds `data`, `backups`, `logs` and the optional `settings.json`), the port (0: a free port
/// chosen at the start), the data file (by default in `data\` of the folder) and the opening of the browser at the start.
/// </summary>
public sealed class DesktopOptions
{
    public string? Folder { get; set; }

    public int Port { get; set; }

    public string? DataFile { get; set; }

    public bool OpenBrowser { get; set; } = true;
}

/// <summary>
/// The server mode (spec 5.3, 6.2): the names the server answers to, an explicit list without `*` (A-4-19: the start is
/// refused without it), and the folder of the daily log (A-4-25; by default `logs` in the folder of the application).
/// </summary>
public sealed class ServerOptions
{
    public List<string> AllowedHosts { get; set; } = [];

    public string? LogsFolder { get; set; }
}

/// <summary>
/// The languages of the interface (AR-29, AR-51) and the name of the first lane of a new project in each (A-2.1-09). A
/// value the configuration does not give takes its default here, in the code: the delivered `appsettings.json` carries
/// no section named after the product, so that renaming the product (D13, ADR 013) leaves nothing unread.
/// </summary>
public sealed class LanguageOptions
{
    /// <summary>
    /// The languages of the interface and of the translations by default (ADR 083): the eighteen whose files the
    /// application has, in the order of their own names, the Latin script first, as the lists of the interface show them.
    /// </summary>
    public static IReadOnlyList<string> DefaultSupported { get; } =
        ["cs", "de", "en", "es", "fr", "it", "hu", "nl", "pl", "pt", "ro", "sv", "tr", "el", "ru", "uk", "zh", "ja"];

    /// <summary>The language used when neither the profile nor the browser gives a supported one, by default (AR-51: French).</summary>
    public const string DefaultLanguage = "fr";

    /// <summary>The name of the first lane of a new project by default, in each language of <see cref="DefaultSupported"/> (the `map.new_lane` of its file).</summary>
    public static IReadOnlyDictionary<string, string> DefaultFirstLaneNames { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["cs"] = "Aktér 1", ["de"] = "Akteur 1", ["en"] = "Actor 1", ["es"] = "Actor 1", ["fr"] = "Acteur 1", ["it"] = "Attore 1",
        ["hu"] = "Szereplő 1", ["nl"] = "Actor 1", ["pl"] = "Aktor 1", ["pt"] = "Ator 1", ["ro"] = "Actor 1", ["sv"] = "Aktör 1",
        ["tr"] = "Aktör 1", ["el"] = "Φορέας 1", ["ru"] = "Исполнитель 1", ["uk"] = "Виконавець 1", ["zh"] = "执行者 1", ["ja"] = "担当 1",
    };

    public List<string> Supported { get; set; } = [];

    public string Default { get; set; } = "";

    public Dictionary<string, string> FirstLaneNames { get; set; } = [];

    public LanguageSettings ToSettings()
    {
        IReadOnlyList<string> supported = Supported.Count > 0 ? Supported : DefaultSupported;
        var names = new Dictionary<string, string>(DefaultFirstLaneNames.Where(pair => supported.Contains(pair.Key)), StringComparer.Ordinal);
        foreach (var (language, name) in FirstLaneNames)
            names[language] = name;
        var fallback = Supported.Count > 0 ? supported[0] : DefaultLanguage;
        return new([.. supported], string.IsNullOrWhiteSpace(Default) ? fallback : Default, names);
    }
}

/// <summary>
/// The section of the configuration named after the product (ADR 013: it follows the display name), read at the start;
/// the administrators are read again at each change of the file (spec 5.3, <see cref="Users.ConfiguredAdministrators"/>).
/// The desktop mode is the default: it needs no configuration (spec 6.1); the server mode is chosen explicitly.
/// </summary>
public sealed class ProductOptions
{
    /// <summary>The name of the section: the product name (D13).</summary>
    public const string Section = ProductIdentity.Name;

    public HostMode Mode { get; set; } = HostMode.Desktop;

    public DatabaseOptions Database { get; set; } = new();

    public DesktopOptions Desktop { get; set; } = new();

    public ServerOptions Server { get; set; } = new();

    public List<string> Administrators { get; set; } = [];

    public LanguageOptions Languages { get; set; } = new();

    public int RecommendedDepth { get; set; } = AppSettings.DefaultRecommendedDepth;

    public string RaciLetters { get; set; } = AppSettings.Default.RaciLetters;

    public int TrashRetentionDays { get; set; } = AppSettings.Default.TrashRetentionDays;

    /// <summary>The retention of the audit trail in days (spec 5.5: configurable, a year by default).</summary>
    public int AuditRetentionDays { get; set; } = Application.Audit.AuditTrail.DefaultRetentionDays;

    public AppSettings ToSettings() => new(RecommendedDepth, RaciLetters, TrashRetentionDays);

    /// <summary>The options of the configuration, bound once (for what is decided before the services exist).</summary>
    public static ProductOptions Of(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new ProductOptions();
        configuration.GetSection(Section).Bind(options);
        return options;
    }
}
