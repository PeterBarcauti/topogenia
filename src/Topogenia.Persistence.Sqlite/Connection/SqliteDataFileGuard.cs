// SqliteDataFileGuard.cs
// The data file and its write-ahead log kept together: checked at the first opening, written back only by the engine, under a stamp (data B1).

using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Topogenia.Persistence.Sqlite.Connection;

/// <summary>
/// SQLite reads the write-ahead log found next to a file whatever file it is, and writes it back into that file at a
/// checkpoint: a file put by hand in place of the data file, while the application runs or next to the log of a file
/// that was not closed cleanly, is read and then overwritten with the pages of another file (work shown that the copy
/// never held, or a corrupt file, data B1). Nothing in the formats of SQLite ties a log to its file; the engine ties
/// them by a stamp, a random number written in the header of the first page (`user_version`):
/// <list type="bullet">
/// <item>the file is written back from its log only by the engine (no automatic checkpoint, none when the last
/// connection closes, `SQLITE_DBCONFIG_NO_CKPT_ON_CLOSE`), each time under a new stamp written first, so that the
/// file and every page 1 of the log that follows carry it; the log starts again with the stamp as its first frame;</item>
/// <item>at the first opening of the file by the process, before SQLite reads it: a log with committed frames must hold
/// a page 1 whose stamp is the one of the file (else <see cref="ForeignLogCode"/>, or <see cref="UnverifiableLogCode"/>
/// when no page 1 lets it tell); then the file is checked (`integrity_check`, `foreign_key_check`, else
/// <see cref="DamagedCode"/>) before anything is written; then the first connection that may write starts a log of its
/// own (stamp, checkpoint, stamp again);</item>
/// <item>before each checkpoint: the same test of the stamp; a file replaced while the application runs is never
/// written back, the process refuses every later opening (<see cref="ReplacedCode"/>) and its next start refuses the
/// file.</item>
/// </list>
/// A copy of the file taken since the last checkpoint carries the stamp of the log and is the very state the log was
/// written on: it is read with it, nothing lost. The refusals are a <see cref="SqliteException"/> `SQLITE_NOTADB` (26),
/// the case in its extended code, which the start reads as <c>BASE_INACCESSIBLE</c> (ADR 036); restoring is
/// <see cref="Backup.SqliteBackupService.RestoreAsync"/>. The limits are in ADR 029.
/// </summary>
internal static class SqliteDataFileGuard
{
    /// <summary>The log next to the file holds committed pages 1 whose stamp is not the one of the file: the log of another file.</summary>
    public const int ForeignLogCode = SqliteJournalCheck.ForeignJournalCode | (1 << 8);

    /// <summary>The log holds committed frames but no page 1: whether it belongs to the file cannot be told.</summary>
    public const int UnverifiableLogCode = SqliteJournalCheck.ForeignJournalCode | (2 << 8);

    /// <summary>The file fails `integrity_check` or `foreign_key_check` at its first opening by the process.</summary>
    public const int DamagedCode = SqliteJournalCheck.ForeignJournalCode | (3 << 8);

    /// <summary>The file was replaced while the process had it open: its log was not written back into it.</summary>
    public const int ReplacedCode = SqliteJournalCheck.ForeignJournalCode | (4 << 8);

    /// <summary>The length of the log past which a connection that closes writes it back (about the 1 000 pages of SQLite).</summary>
    public const long CheckpointLength = 4 * 1024 * 1024;

    private const int NoCheckpointOnClose = 1006;

    private static readonly ConcurrentDictionary<string, FileState> States = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Before SQLite opens the file: the log of another file is refused, and so is a file replaced while open.</summary>
    public static void Opening(DbConnection connection)
    {
        if (DataFileOf(connection) is not { } file)
            return;
        SqliteJournalCheck.EnsureOwnJournal(file);
        var state = States.GetOrAdd(file, _ => new FileState());
        lock (state)
        {
            if (state.Replaced)
                throw Refused(ReplacedCode, "The data file was replaced while it was open: its log was kept apart from it.");
            if (!state.Checked)
                EnsureLogOfFile(file);
        }
    }

    /// <summary>
    /// Once SQLite opened the file, before its journal mode is set: no checkpoint but those of the engine, and at the first
    /// opening by the process the checks of the file.
    /// </summary>
    public static void Opened(DbConnection connection)
    {
        if (DataFileOf(connection) is not { } file || connection is not SqliteConnection sqlite)
            return;
        _ = raw.sqlite3_db_config(sqlite.Handle, NoCheckpointOnClose, 1, out _);
        foreach (var statement in SqlitePragmas.CheckpointSettings())
            SqlitePragmas.Execute(connection, statement);
        var state = States.GetOrAdd(file, _ => new FileState());
        lock (state)
        {
            if (!state.Checked)
            {
                EnsureSound(connection);
                state.Checked = true;
            }
        }
    }

    /// <summary>Once the journal mode is set: the first connection of the process that may write starts a log of its own.</summary>
    public static void Ready(DbConnection connection)
    {
        if (DataFileOf(connection) is not { } file || IsReadOnly(connection))
            return;
        var state = States.GetOrAdd(file, _ => new FileState());
        lock (state)
        {
            if (state.Started || state.Replaced)
                return;
            if (!OwnLog(file))
            {
                state.Replaced = true;
                throw Refused(ReplacedCode, "The data file was replaced while it was open: its log was kept apart from it.");
            }
            WriteBack(connection, truncate: true);
            state.Started = true;
            state.LastLength = LogLength(file);
        }
    }

    /// <summary>
    /// When a connection closes: the log is written back once it passed <see cref="CheckpointLength"/>. The closing of a
    /// connection follows a commit or a read that is done: it never fails on a busy file, the work of the connection stays
    /// as it is (a failure there would make a committed unit run again, data B1 of round 4). Write-back refused because
    /// another connection holds the write lock is tried again by the next connection that closes.
    /// </summary>
    public static void Closing(DbConnection connection)
    {
        if (DataFileOf(connection) is not { } file || IsReadOnly(connection) || connection is not SqliteConnection { Handle: { } handle })
            return;
        var state = States.GetOrAdd(file, _ => new FileState());
        var length = LogLength(file);
        if (!state.Started || state.Replaced || length < CheckpointLength || length == state.LastLength || raw.sqlite3_get_autocommit(handle) == 0)
            return;
        lock (state)
        {
            if (state.Replaced || !OwnLog(file))
            {
                state.Replaced = true;
                return;
            }
            try
            {
                WriteBack(connection, truncate: false);
            }
            catch (SqliteException busy) when (busy.SqliteErrorCode is SqliteDialect.Busy or SqliteDialect.Locked)
            {
                // Left for the next connection that closes: the length of the log stays the one it was not written back at.
                return;
            }
            state.LastLength = LogLength(file);
        }
    }

    /// <summary>
    /// The clean stop: the log written back into the file and emptied, after the test of the stamp; a file replaced while
    /// open is refused (<see cref="ReplacedCode"/>) and its log kept.
    /// </summary>
    public static void Close(DbConnection connection)
    {
        if (DataFileOf(connection) is not { } file)
            return;
        var state = States.GetOrAdd(file, _ => new FileState());
        lock (state)
        {
            if (state.Replaced || !OwnLog(file))
            {
                state.Replaced = true;
                throw Refused(ReplacedCode, "The data file was replaced while it was open: its log was kept apart from it.");
            }
            var stamp = NewStamp();
            SqlitePragmas.SetStamp(connection, stamp);
            SqlitePragmas.Checkpoint(connection, truncate: true);
            state.LastLength = LogLength(file);
        }
    }

    /// <summary>Forgets what the process knows of a file: a restored file is checked again at its next opening.</summary>
    public static void Forget(string dataFile) => States.TryRemove(Path.GetFullPath(dataFile), out _);

    /// <summary>
    /// A new stamp, then the log written back, then the stamp again as the first frame of the next log: the file and
    /// every page 1 of the log carry the stamp until the next checkpoint.
    /// </summary>
    private static void WriteBack(DbConnection connection, bool truncate)
    {
        var stamp = NewStamp();
        SqlitePragmas.SetStamp(connection, stamp);
        SqlitePragmas.Checkpoint(connection, truncate);
        SqlitePragmas.SetStamp(connection, stamp);
    }

    /// <summary>Throws when the log next to the file holds committed frames that are not those of the file.</summary>
    private static void EnsureLogOfFile(string file)
    {
        var log = SqliteWalFile.Read(file + "-wal");
        if (!log.HasFrames)
            return;
        if (log.Stamps.Count == 0)
            throw Refused(UnverifiableLogCode, "The write-ahead log next to the data file holds no first page: it cannot be told to be the log of the file.");
        if (!log.Stamps.Contains(SqliteWalFile.StampOf(file)))
            throw Refused(ForeignLogCode, "The write-ahead log next to the data file is the log of another file (a file copied by hand).");
    }

    /// <summary>The log has no committed frame, or a page 1 among them carries the stamp of the file, or none at all.</summary>
    private static bool OwnLog(string file)
    {
        var log = SqliteWalFile.Read(file + "-wal");
        return !log.HasFrames || log.Stamps.Count == 0 || log.Stamps.Contains(SqliteWalFile.StampOf(file));
    }

    /// <summary>
    /// A failure of SQLite that means a damaged file (`SQLITE_CORRUPT`, `SQLITE_NOTADB`) met while the file opens for the
    /// first time in the process, before its checks passed, other than a refusal of this guard (whose extended codes
    /// carry the case above the low byte): the start refuses it as damaged. Later, SQLite reports it as it is.
    /// </summary>
    public static bool IsUnreadableAtFirstOpening(DbConnection connection, SqliteException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Backup.SqliteBackupService.IsDamage(error) && error.SqliteExtendedErrorCode >> 8 == 0
            && DataFileOf(connection) is { } file && !(States.TryGetValue(file, out var state) && state.Checked);
    }

    /// <summary>The refusal of a file that SQLite cannot read while it opens it, or that fails its checks.</summary>
    public static SqliteException Damaged() => Refused(DamagedCode, "The data file is damaged: restore a backup.");

    private static void EnsureSound(DbConnection connection)
    {
        if (SqlitePragmas.IntegrityProblemsAsync(connection, CancellationToken.None).GetAwaiter().GetResult() is { Count: > 0 }
            || SqlitePragmas.ForeignKeyProblemsAsync(connection, CancellationToken.None).GetAwaiter().GetResult() is { Count: > 0 })
            throw Damaged();
    }

    private static int NewStamp()
    {
        int stamp;
        do
            stamp = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        while (stamp == 0);
        return stamp;
    }

    private static long LogLength(string file)
    {
        var log = new FileInfo(file + "-wal");
        return log.Exists ? log.Length : 0;
    }

    private static SqliteException Refused(int code, string message) => new(message, SqliteJournalCheck.ForeignJournalCode, code);

    /// <summary>The full path of the data file of a connection, or null for a database in memory.</summary>
    private static string? DataFileOf(DbConnection connection)
    {
        if (connection is not SqliteConnection sqlite)
            return null;
        var options = new SqliteConnectionStringBuilder(sqlite.ConnectionString);
        return options.Mode == SqliteOpenMode.Memory || options.DataSource is "" or ":memory:" ? null : Path.GetFullPath(options.DataSource);
    }

    private static bool IsReadOnly(DbConnection connection) =>
        connection is SqliteConnection sqlite && new SqliteConnectionStringBuilder(sqlite.ConnectionString).Mode == SqliteOpenMode.ReadOnly;

    /// <summary>What the process knows of one data file; its lock is the object itself.</summary>
    private sealed class FileState
    {
        public bool Checked { get; set; }

        public bool Started { get; set; }

        public bool Replaced { get; set; }

        public long LastLength { get; set; } = -1;
    }
}
