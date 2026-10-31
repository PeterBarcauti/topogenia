// DesktopPaths.cs
// The folders and files of the desktop mode: product folder, data file, backups, logs, user settings (spec 5.2, 6.1).

using Microsoft.Data.Sqlite;
using Topogenia.Application;
using Topogenia.Host.Composition;
using Topogenia.Persistence.Sqlite.Files;

namespace Topogenia.Host.Desktop;

/// <summary>
/// Where the desktop mode reads and writes, nowhere else (spec 6.1): the folder of the product, by default the folder named
/// after the product under `%LOCALAPPDATA%` (NTFS rights of the profile), with the data file in `data\`, `backups\`,
/// `logs\` and the optional `settings.json`. The data file may be moved by the configuration; a relative path is resolved
/// against the folder of the product. Whether the file may be opened there (share, synchronised folder) is checked at the start, not here.
/// </summary>
public sealed record DesktopPaths(string Folder, string DataFile)
{
    public const string BackupsFolderName = "backups";

    public const string LogsFolderName = "logs";

    public const string SettingsFileName = "settings.json";

    public string BackupsFolder => Path.Combine(Folder, BackupsFolderName);

    public string LogsFolder => Path.Combine(Folder, LogsFolderName);

    /// <summary>The connection string of the data file, read by the SQLite engine of the persistence.</summary>
    public string ConnectionString => new SqliteConnectionStringBuilder { DataSource = DataFile }.ConnectionString;

    /// <summary>
    /// The settings file of the user, in the folder of the product. It is read before the configuration is complete, so
    /// from the folder alone: the data file may still be moved by the settings themselves.
    /// </summary>
    public static string SettingsFileOf(DesktopOptions options) => Path.Combine(FolderOf(options), SettingsFileName);

    public static DesktopPaths Of(DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var folder = FolderOf(options);
        var dataFile = string.IsNullOrWhiteSpace(options.DataFile)
            ? DesktopDataFile.DefaultPathIn(folder)
            : Path.GetFullPath(options.DataFile, folder);
        return new DesktopPaths(folder, dataFile);
    }

    /// <summary>The folder of the product of the configuration, else the default one of the user.</summary>
    private static string FolderOf(DesktopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return string.IsNullOrWhiteSpace(options.Folder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductIdentity.Name)
            : Path.GetFullPath(options.Folder);
    }
}
