// SqliteBackupService.cs
// Online backup of the SQLite data file (spec 5.2, 4.4): API of SQLite, checks of the copy, 7 daily files, the files before a migration kept for a while.

using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Sqlite.Files;

namespace Topogenia.Persistence.Sqlite.Backup;

/// <summary>
/// Stable codes that start <see cref="BackupResult.Failure"/>, followed by `: ` and the detail: codes and identifiers only,
/// never a path (it holds the login of the user) nor content (spec 5.2).
/// </summary>
public static class BackupFailures
{
    /// <summary>The data file does not exist: nothing to back up (the backup never creates it).</summary>
    public const string DataFileMissing = "SAUVEGARDE_BASE_ABSENTE";

    /// <summary>The copy could not be made: folder or file not writable, disk full, SQLite error.</summary>
    public const string CopyFailed = "SAUVEGARDE_IMPOSSIBLE";

    /// <summary>The copy fails `integrity_check` or `foreign_key_check`: it is thrown away.</summary>
    public const string CopyCorrupted = "SAUVEGARDE_CORROMPUE";

    /// <summary>The backup folder is on a share or in a synchronised folder (rule of the data file, ADR 035): nothing is written.</summary>
    public const string FolderRefused = "SAUVEGARDE_DOSSIER_REFUSE";
}

/// <summary>
/// <see cref="IBackupService"/> on the data file of a <see cref="SqliteEngine"/>. The copy is made by the online backup
/// API of SQLite (`sqlite3_backup`, <see cref="SqliteConnection.BackupDatabase(SqliteConnection)"/>) from a connection of
/// the engine: a consistent state of the file, rows still in the write-ahead log included, while other connections keep
/// working; never a copy of the file. It is written to a partial file of the backup folder, checked there
/// (`integrity_check`, `foreign_key_check`), switched to the rollback journal so that it stands alone, and only then put in
/// place of the previous file of the same name. The daily file of the day is not written again (A-4-30); the file before
/// a migration always is (A-4-31). The day is the UTC day of the clock (D08). The backup folder follows the rule of the
/// data file (<see cref="DesktopDataFile.Check"/>): never on a share nor in a synchronised folder. The files before a
/// migration are kept at most <see cref="BeforeMigrationKept"/> and at most <see cref="BeforeMigrationRetention"/> (ADR
/// 029). A failure is returned, never thrown, except a cancellation: the caller decides (AR-14); its exception stays in
/// <see cref="BackupResult.Cause"/> for the local diagnosis.
/// </summary>
public sealed class SqliteBackupService : IBackupService
{
    /// <summary>The files before a migration kept at most: the one of the last migration and the one before it.</summary>
    public const int BeforeMigrationKept = 2;

    private const string PartialSuffix = ".partial";

    /// <summary>`SQLITE_CORRUPT` and `SQLITE_NOTADB`: the file read is damaged.</summary>
    private const int CorruptCode = 11;
    private const int NotADatabaseCode = 26;

    private readonly SqliteEngine engine;
    private readonly string folder;
    private readonly IClock clock;
    private readonly DataFileEnvironment environment;

    public SqliteBackupService(SqliteEngine engine, string backupFolder, IClock clock, DataFileEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFolder);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(environment);
        this.engine = engine;
        folder = Path.GetFullPath(backupFolder);
        this.clock = clock;
        this.environment = environment;
    }

    /// <summary>
    /// The age after which a file before a migration is deleted, even the last one: the 30 days of the recycle bin
    /// (`trash_retention_days`), so that what a purge removed leaves the backups within 30 days more.
    /// </summary>
    public static TimeSpan BeforeMigrationRetention { get; } = TimeSpan.FromDays(30);

    public async Task<BackupResult> BackupAsync(BackupReason reason, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var dataFile = Path.GetFullPath(engine.DataSource);
        if (DesktopDataFile.Check(folder, environment) is { } refusal)
            return Failed(BackupFailures.FolderRefused, refusal.Code, null);
        if (!File.Exists(dataFile))
            return Failed(BackupFailures.DataFileMissing, "no data file", null);
        try
        {
            var target = Path.Combine(folder, reason == BackupReason.Daily
                ? BackupFileNames.Daily(dataFile, now)
                : BackupFileNames.BeforeMigration(dataFile, TargetMigration()));
            if (reason == BackupReason.Daily && WrittenOn(target, now))
                return Rotated(new BackupResult(false, true, target, null), dataFile, now);
            Directory.CreateDirectory(folder);
            var failure = await CopyAsync(target, now, cancellationToken);
            return failure is null
                ? Rotated(new BackupResult(true, false, target, null), dataFile, now)
                : Failed(BackupFailures.CopyCorrupted, failure, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException)
        {
            return Failed(BackupFailures.CopyFailed, Describe(error), error);
        }
    }

    /// <summary>
    /// Restores <paramref name="backupFile"/> in place of the data file, the application stopped (<see cref="SqliteRestore"/>):
    /// the file and its journal are replaced together, a damaged or refused backup leaves the data file as it was.
    /// </summary>
    public Task<RestoreResult> RestoreAsync(string backupFile, CancellationToken cancellationToken) =>
        new SqliteRestore(engine, clock).RestoreAsync(backupFile, cancellationToken);

    /// <summary>The type of a failure and its code (SQLite result code, or HRESULT): never its message, which may hold a path.</summary>
    internal static string Describe(Exception error) => error is SqliteException sqlite
        ? $"{nameof(SqliteException)} {sqlite.SqliteErrorCode.ToString(CultureInfo.InvariantCulture)}/{sqlite.SqliteExtendedErrorCode.ToString(CultureInfo.InvariantCulture)}"
        : $"{error.GetType().Name} 0x{error.HResult.ToString("X8", CultureInfo.InvariantCulture)}";

    /// <summary>The last migration of the application: the one a migration would bring the file to (1.x `avant_vN`).</summary>
    private string TargetMigration()
    {
        using var context = engine.CreateContext();
        return context.Database.GetMigrations().Last();
    }

    private static bool WrittenOn(string file, DateTime now) => File.Exists(file) && File.GetLastWriteTimeUtc(file).Date == now.Date;

    /// <summary>
    /// Deletes the files before a migration of this data file beyond the <see cref="BeforeMigrationKept"/> most recent and
    /// those older than <see cref="BeforeMigrationRetention"/>, by their date (the clock when they were written). A file
    /// that cannot be deleted now is deleted at a next backup: the backup itself stays done.
    /// </summary>
    private BackupResult Rotated(BackupResult result, string dataFile, DateTime now)
    {
        var prefix = BackupFileNames.BeforeMigrationPrefix(dataFile);
        var extension = BackupFileNames.Extension(dataFile);
        var files = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).Select(file => new FileInfo(file))
                .Where(file => file.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && file.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.LastWriteTimeUtc).ToList()
            : [];
        foreach (var (file, rank) in files.Select((file, rank) => (file, rank)))
        {
            if (rank < BeforeMigrationKept && now - file.LastWriteTimeUtc <= BeforeMigrationRetention)
                continue;
            try
            {
                file.Delete();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Kept until a next backup: an open file is not a failure of this one.
            }
        }
        return result;
    }

    /// <summary>Copies, checks and puts the copy in place; returns the problem found by the checks, or null.</summary>
    private async Task<string?> CopyAsync(string target, DateTime now, CancellationToken cancellationToken)
    {
        var partial = target + PartialSuffix;
        File.Delete(partial);
        try
        {
            var problem = await CopyCheckedAsync(partial, cancellationToken);
            if (problem is not null)
                return problem;
            File.Move(partial, target, overwrite: true);
            File.SetLastWriteTimeUtc(target, now);
            return null;
        }
        finally
        {
            File.Delete(partial);
        }
    }

    private async Task<string?> CopyCheckedAsync(string partial, CancellationToken cancellationToken)
    {
        var destination = new SqliteConnectionStringBuilder { DataSource = partial, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
        await using var copy = new SqliteConnection(destination.ConnectionString);
        await copy.OpenAsync(cancellationToken);
        await using (var context = engine.CreateContext())
        {
            // The connection of the engine, with its settings (busy timeout), reads a consistent state of the file.
            await context.Database.OpenConnectionAsync(cancellationToken);
            ((SqliteConnection)context.Database.GetDbConnection()).BackupDatabase(copy);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (await IntegrityProblemAsync(copy, cancellationToken) is { } damage)
            return "integrity_check: " + damage;
        if (await SqlitePragmas.ForeignKeyProblemsAsync(copy, cancellationToken) is { Count: > 0 } broken)
            return $"foreign_key_check: {broken.Count} broken reference(s), first {broken[0]}";
        await SqlitePragmas.SetJournalModeAsync(copy, SqliteJournalMode.Delete, cancellationToken);
        return null;
    }

    /// <summary>
    /// The first problem that `integrity_check` finds in the copy (pages, tables and indexes named by SQLite), or null.
    /// SQLite reports most damages as lines, and stops with an error on a page it cannot read at all: both mean a damaged
    /// copy, the error given by its codes.
    /// </summary>
    internal static async Task<string?> IntegrityProblemAsync(SqliteConnection copy, CancellationToken cancellationToken)
    {
        try
        {
            return (await SqlitePragmas.IntegrityProblemsAsync(copy, cancellationToken)).FirstOrDefault();
        }
        catch (SqliteException error) when (IsDamage(error))
        {
            return Describe(error);
        }
    }

    /// <summary>A failure of SQLite that means a damaged file: `SQLITE_CORRUPT` or `SQLITE_NOTADB`.</summary>
    internal static bool IsDamage(SqliteException error) => error.SqliteErrorCode is CorruptCode or NotADatabaseCode;

    private static BackupResult Failed(string code, string detail, Exception? cause) =>
        new(false, false, null, code + ": " + detail) { Cause = cause };
}
