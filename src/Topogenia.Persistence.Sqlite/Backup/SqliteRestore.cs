// SqliteRestore.cs
// Restoration of a backup in place of the SQLite data file, the application stopped: the file and its journal replaced together, the copy checked first (data B1).

using Microsoft.Data.Sqlite;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Sqlite.Files;

namespace Topogenia.Persistence.Sqlite.Backup;

/// <summary>
/// Stable codes that start <see cref="RestoreResult.Failure"/>, followed by `: ` and the detail: codes only, never a path
/// nor content (spec 5.2). The data file is left as it was.
/// </summary>
public static class RestoreFailures
{
    /// <summary>The backup file does not exist.</summary>
    public const string BackupMissing = "RESTAURATION_SAUVEGARDE_ABSENTE";

    /// <summary>The backup is not a sound database: `integrity_check`, `foreign_key_check`, or not a database at all.</summary>
    public const string BackupCorrupted = "RESTAURATION_SAUVEGARDE_CORROMPUE";

    /// <summary>The backup is a sound file that the start would refuse (no migration applied, newer, changed): its code follows.</summary>
    public const string BackupRefused = "RESTAURATION_SAUVEGARDE_REFUSEE";

    /// <summary>The data file or its journal is open, in this process or elsewhere: the application is not stopped.</summary>
    public const string DataFileOpen = "RESTAURATION_FICHIER_OUVERT";

    /// <summary>
    /// The path of the backup is a form that the check of the data file does not read (relative, a device or a volume by
    /// its name, a character that no path holds: <see cref="DataFileRefusals.UnsupportedPath"/>), never opened.
    /// </summary>
    public const string UnsupportedPath = "RESTAURATION_CHEMIN_NON_PRIS_EN_CHARGE";

    /// <summary>The restoration could not be made: the backup is the data file itself, folder not writable, disk full.</summary>
    public const string Impossible = "RESTAURATION_IMPOSSIBLE";
}

/// <summary>
/// What <see cref="SqliteBackupService.RestoreAsync"/> does. The path of the backup must be in a form that the check of
/// the data file reads, and the backup another file than the data file, by their identity on the disk
/// (`GetFileInformationByHandle`), whatever the text of the two paths (`\\?\`, `\\.\`, a short name, a hard link). The backup is copied by the online backup API of SQLite, which reads its
/// write-ahead log if it has one, to a partial file next to the data file, and checked there: `integrity_check` and
/// `foreign_key_check`, then the check of the start (<see cref="SchemaMigrations.CheckAsync"/>: a file with no migration,
/// newer or changed is refused, a file behind is accepted and migrated by the start), then put in the rollback journal.
/// The idle connections of the engine are closed; the data file, its write-ahead log and its shared memory must then
/// open with no sharing (no connection left anywhere). The replaced data file and its journals are moved aside next to
/// it (<see cref="BackupFileNames.Refused"/>, AR-96: the work left in a refused log is never destroyed), the shared memory
/// is deleted, the partial file takes the place of the data file, and the restored file is checked again. A copy by hand would leave the log of the
/// replaced file, which SQLite reads with the restored one (data B1). Every failure is a code; no partial file is left.
/// </summary>
internal sealed class SqliteRestore(SqliteEngine engine, IClock clock)
{
    private const string PartialSuffix = ".restoring";

    /// <summary>`SQLITE_DBCONFIG_NO_CKPT_ON_CLOSE`: the backup read is never written back from its own log.</summary>
    private const int NoCheckpointOnClose = 1006;

    /// <summary>The files that SQLite keeps next to a data file: its write-ahead log and its shared memory, and the rollback journal.</summary>
    private static readonly IReadOnlyList<string> JournalSuffixes = DataFileUse.JournalSuffixes;

    /// <summary>
    /// The move of a file (source, target), `File.Move` itself; the tests of the persistence make it fail on one file to
    /// prove that a failed restoration puts the kept files back (AR-96).
    /// </summary>
    internal Action<string, string> MoveFile { get; init; } = (source, target) => File.Move(source, target);

    /// <summary>A machine without synchronised folder nor network drive: only the form of the path of the backup is checked.</summary>
    private static readonly DataFileEnvironment FormOnly = new([], _ => false) { LinkCountOf = _ => 1 };

    public async Task<RestoreResult> RestoreAsync(string backupFile, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFile);
        // Only the form of the path: a backup may be read from a share or a synchronised folder, never written there.
        if (DesktopDataFile.Check(backupFile, FormOnly) is { Code: DataFileRefusals.UnsupportedPath } refusal)
            return Failed(RestoreFailures.UnsupportedPath, refusal.Code, null);
        var backup = Path.GetFullPath(backupFile);
        var dataFile = Path.GetFullPath(engine.DataSource);
        if (!File.Exists(backup))
            return Failed(RestoreFailures.BackupMissing, "no backup file", null);
        if (string.Equals(backup, dataFile, StringComparison.OrdinalIgnoreCase)
            || (FileIdentity.Of(backup) is { } source && FileIdentity.Of(dataFile) is { } target && source.IsSameFileAs(target)))
            return Failed(RestoreFailures.Impossible, "the backup is the data file", null);
        var partial = dataFile + PartialSuffix;
        try
        {
            DeleteWithJournal(partial);
            if (await CopyAsync(backup, partial, cancellationToken) is { } unreadable)
                return unreadable;
            if (await ProblemOfCopyAsync(partial, cancellationToken) is { } problem)
                return problem;
            SqliteConnection.ClearPool(new SqliteConnection(engine.ConnectionString));
            if (DataFileUse.HeldOpen(dataFile) is { } open)
                return Failed(RestoreFailures.DataFileOpen, open, null);
            var kept = KeepAside(dataFile);
            try
            {
                MoveFile(partial, dataFile);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                PutBack(kept, dataFile);
                throw;
            }
            SqliteDataFileGuard.Forget(dataFile);
            var keptName = kept is null ? null : Path.GetFileName(kept);
            return await DamageAsync(dataFile, cancellationToken) is { } damage
                ? Failed(RestoreFailures.BackupCorrupted, damage, null) with { KeptFile = keptName }
                : new RestoreResult(true, null) { KeptFile = keptName };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException)
        {
            return Failed(RestoreFailures.Impossible, SqliteBackupService.Describe(error), error);
        }
        finally
        {
            TryDeleteWithJournal(partial);
        }
    }

    /// <summary>
    /// Copies the backup to the partial file by the online backup API, from a read-only connection that reads the log of
    /// the backup if it has one and never writes it back; null when done, the refusal of a file that is no database.
    /// </summary>
    private static async Task<RestoreResult?> CopyAsync(string backup, string partial, CancellationToken cancellationToken)
    {
        var options = new SqliteConnectionStringBuilder { DataSource = backup, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var source = new SqliteConnection(options.ConnectionString);
        await using var copy = new SqliteConnection(Plain(partial, SqliteOpenMode.ReadWriteCreate));
        try
        {
            await source.OpenAsync(cancellationToken);
            _ = SQLitePCL.raw.sqlite3_db_config(source.Handle, NoCheckpointOnClose, 1, out _);
            await copy.OpenAsync(cancellationToken);
            source.BackupDatabase(copy);
            return null;
        }
        catch (SqliteException error) when (SqliteBackupService.IsDamage(error))
        {
            return Failed(RestoreFailures.BackupCorrupted, SqliteBackupService.Describe(error), error);
        }
    }

    /// <summary>The refusal of the copy of the backup, or null when it may become the data file (left in the rollback journal).</summary>
    private async Task<RestoreResult?> ProblemOfCopyAsync(string partial, CancellationToken cancellationToken)
    {
        if (await DamageAsync(partial, cancellationToken) is { } damage)
            return Failed(RestoreFailures.BackupCorrupted, damage, null);
        var copy = new SqliteEngine(Plain(partial, SqliteOpenMode.ReadWrite));
        try
        {
            var state = await new SchemaMigrations(copy, clock).CheckAsync(cancellationToken);
            if (state.Applied.Count == 0)
                return Failed(RestoreFailures.BackupRefused, "no migration applied", null);
        }
        catch (SchemaRefusedException refusal)
        {
            return Failed(RestoreFailures.BackupRefused, refusal.Code, refusal);
        }
        finally
        {
            // The check opened the copy through an engine: its log is written back and the copy closed before it moves.
            await copy.CloseAsync(CancellationToken.None);
            SqliteDataFileGuard.Forget(partial);
        }
        await using var connection = new SqliteConnection(Plain(partial, SqliteOpenMode.ReadWrite));
        await connection.OpenAsync(cancellationToken);
        await SqlitePragmas.SetJournalModeAsync(connection, SqliteJournalMode.Delete, cancellationToken);
        return null;
    }

    /// <summary>The first problem of `integrity_check` or `foreign_key_check` of a file, or null when it is sound.</summary>
    private static async Task<string?> DamageAsync(string file, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(Plain(file, SqliteOpenMode.ReadWrite));
        await connection.OpenAsync(cancellationToken);
        if (await SqliteBackupService.IntegrityProblemAsync(connection, cancellationToken) is { } damage)
            return "integrity_check: " + damage;
        try
        {
            if (await SqlitePragmas.ForeignKeyProblemsAsync(connection, cancellationToken) is { Count: > 0 } broken)
                return $"foreign_key_check: {broken.Count} broken reference(s), first {broken[0]}";
        }
        catch (SqliteException error) when (SqliteBackupService.IsDamage(error))
        {
            return SqliteBackupService.Describe(error);
        }
        return null;
    }

    /// <summary>
    /// Moves the data file and its log or rollback journal aside under <see cref="BackupFileNames.Refused"/>, in the same
    /// folder (a rename: no copy, no space); deletes the shared memory, an index of the log that SQLite rebuilds. Without a
    /// data file, its journals are deleted as before (nothing reads them without it) and null is given.
    /// </summary>
    private string? KeepAside(string dataFile)
    {
        File.Delete(dataFile + "-shm");
        if (!File.Exists(dataFile))
        {
            foreach (var suffix in JournalSuffixes)
                File.Delete(dataFile + suffix);
            return null;
        }
        var folder = Path.GetDirectoryName(dataFile)!;
        var now = clock.UtcNow;
        var index = 1;
        string kept;
        do
            kept = Path.Combine(folder, BackupFileNames.Refused(dataFile, now, index++));
        while (WithJournals(kept).Any(File.Exists));
        var moved = new List<string>();
        try
        {
            foreach (var suffix in new[] { "" }.Concat(JournalSuffixes).Where(suffix => File.Exists(dataFile + suffix)))
            {
                MoveFile(dataFile + suffix, kept + suffix);
                moved.Add(suffix);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            foreach (var suffix in moved)
                MoveFile(kept + suffix, dataFile + suffix);
            throw;
        }
        return kept;
    }

    /// <summary>The kept files put back in place when the partial file could not take it: the data file is left as it was.</summary>
    private void PutBack(string? kept, string dataFile)
    {
        if (kept is null)
            return;
        foreach (var suffix in new[] { "" }.Concat(JournalSuffixes).Where(suffix => File.Exists(kept + suffix)))
            MoveFile(kept + suffix, dataFile + suffix);
    }

    /// <summary>A file and the journals that SQLite would keep next to it.</summary>
    private static IEnumerable<string> WithJournals(string file) => new[] { "" }.Concat(JournalSuffixes).Select(suffix => file + suffix);

    private static void DeleteWithJournal(string file)
    {
        foreach (var suffix in new[] { "" }.Concat(JournalSuffixes))
        {
            if (File.Exists(file + suffix))
            {
                File.SetAttributes(file + suffix, FileAttributes.Normal);
                File.Delete(file + suffix);
            }
        }
    }

    /// <summary>The partial file removed at the end whatever happened: a failure here never hides the result (data m1).</summary>
    private static void TryDeleteWithJournal(string file)
    {
        try
        {
            DeleteWithJournal(file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left to the next restoration, which deletes it first.
        }
    }

    /// <summary>A connection string of its own on a file: no pool, no setting of the application (the file is not the data file yet).</summary>
    private static string Plain(string file, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder { DataSource = file, Mode = mode, Pooling = false }.ConnectionString;

    private static RestoreResult Failed(string code, string detail, Exception? cause) =>
        new(false, code + ": " + detail) { Cause = cause };
}
