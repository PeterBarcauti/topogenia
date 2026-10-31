// BackupFileNames.cs
// Names of the backup files of a data file: one per day of the week, one per target migration (spec 5.2, 1.x Sauvegarde.cs).

namespace Topogenia.Persistence.Sqlite.Backup;

/// <summary>
/// The names follow the 1.x (`Ariane_1_lundi.bak`, `Ariane_avant_v5.bak`), from the name of the data file: for
/// `data.db`, the daily files `data_1_monday.db` to `data_7_sunday.db` (ISO number, then the English name, so that they
/// sort in the order of the week) and `data_before_&lt;migration&gt;.db` before the migration to the last migration of
/// the application. The extension of the data file is kept (`.db` when it has none).
/// </summary>
public static class BackupFileNames
{
    private static readonly string[] Days = ["7_sunday", "1_monday", "2_tuesday", "3_wednesday", "4_thursday", "5_friday", "6_saturday"];

    /// <summary>The daily file of the day of the week of <paramref name="utcDay"/> (the UTC day of the clock, D08).</summary>
    public static string Daily(string dataFile, DateTime utcDay) => Stem(dataFile) + "_" + Days[(int)utcDay.DayOfWeek] + Extension(dataFile);

    /// <summary>The file written before migrating to <paramref name="targetMigration"/>, the last migration of the application.</summary>
    public static string BeforeMigration(string dataFile, string targetMigration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetMigration);
        return BeforeMigrationPrefix(dataFile) + targetMigration + Extension(dataFile);
    }

    /// <summary>The start of the name of every file before a migration of <paramref name="dataFile"/> (`data_before_`).</summary>
    public static string BeforeMigrationPrefix(string dataFile) => Stem(dataFile) + "_before_";

    /// <summary>
    /// The name under which a restoration keeps the refused data file next to it (AR-96): `data.db.refused-20261024-201500`
    /// (UTC time of the clock), then `-2`, `-3`... when the name is taken. Its journals keep their suffix after it, so that
    /// SQLite reads them with it. The name never matches the backups (`data_*.db`).
    /// </summary>
    public static string Refused(string dataFile, DateTime utcNow, int index = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 1);
        var name = Path.GetFileName(dataFile) + ".refused-" + utcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        return index == 1 ? name : name + "-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The extension of the data file, kept by its backups (`.db` when it has none).</summary>
    public static string Extension(string dataFile) => Path.GetExtension(dataFile) is { Length: > 0 } extension ? extension : ".db";

    private static string Stem(string dataFile)
    {
        var stem = Path.GetFileNameWithoutExtension(dataFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(stem, nameof(dataFile));
        return stem;
    }
}
