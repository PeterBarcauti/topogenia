// IBackupService.cs
// Port of the backup of the data file of the desktop mode and of its restoration (spec 5.2, 4.4), implemented by Persistence.Sqlite (SqliteBackupService, ADR 029).

namespace Topogenia.Application.Ports;

/// <summary>Why a backup is made: the first start of the day (7 rotating files) or before a schema migration.</summary>
public enum BackupReason
{
    Daily,
    BeforeMigration,
}

/// <summary>
/// Result of a backup: done (with the file written and its integrity check passed), skipped (the daily backup of the day
/// already exists) or failed with the reason, a stable code and a detail without path nor content (spec 5.2). A failed
/// backup before a migration stops the migration (AR-14).
/// </summary>
public sealed record BackupResult(bool Done, bool Skipped, string? File, string? Failure)
{
    /// <summary>The exception that made the backup fail, for the local diagnosis only: its message may hold a path.</summary>
    public Exception? Cause { get; init; }
}

/// <summary>
/// Result of a restoration: done (the backup is the data file, the replaced file and its journal kept aside under
/// <see cref="KeptFile"/>, the file checked) or failed with the
/// reason, a stable code and a detail without path nor content (spec 5.2); a failed restoration leaves the data file as
/// it was, except a failure of the disk while the file is replaced (ADR 029).
/// </summary>
public sealed record RestoreResult(bool Done, string? Failure)
{
    /// <summary>The exception that made the restoration fail, for the local diagnosis only: its message may hold a path.</summary>
    public Exception? Cause { get; init; }

    /// <summary>The name (never the path) of the replaced data file kept next to it with its journal, null when there was none (AR-96).</summary>
    public string? KeptFile { get; init; }
}

/// <summary>Online backup of the database file with an integrity check of the copy (spec 5.2), and its restoration.</summary>
public interface IBackupService
{
    Task<BackupResult> BackupAsync(BackupReason reason, CancellationToken cancellationToken);

    /// <summary>
    /// Puts <paramref name="backupFile"/> in place of the data file, the application stopped: before the start of the
    /// desktop mode, no connection open on the data file in this process or elsewhere (ADR 029). The backup is read with
    /// its own journal if it has one; the data file itself under another path, or a path of a form that the check of the
    /// data file does not read, is refused by a code. The journal of the replaced file is removed with it, so that it is
    /// never read with the restored one. The start runs afterwards as usual (check, migration of a backup behind).
    /// </summary>
    Task<RestoreResult> RestoreAsync(string backupFile, CancellationToken cancellationToken);
}
