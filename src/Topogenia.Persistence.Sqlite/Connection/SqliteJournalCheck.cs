// SqliteJournalCheck.cs
// Refuses to open a data file in the rollback journal next to a write-ahead log: the log of another file, which SQLite would read with it (data B1).

using Microsoft.Data.Sqlite;

namespace Topogenia.Persistence.Sqlite.Connection;

/// <summary>
/// SQLite reads the write-ahead log found next to a file whatever the header of the file says: a backup (rollback
/// journal, ADR 029) copied by hand in place of a data file that was not closed cleanly is read with the log of the
/// replaced file, which shows work done after the backup or pages of another layout (a corrupt file). A file whose header
/// says the rollback journal (bytes 18 and 19 at 1) never has a log of its own: SQLite writes 2 there, in the file
/// itself, before it starts a log. Such a file next to a log that is not empty is refused before SQLite opens it, by a
/// <see cref="SqliteException"/> `SQLITE_NOTADB` (26) that the check of the start reads as <c>BASE_INACCESSIBLE</c>;
/// <see cref="Backup.SqliteBackupService.RestoreAsync"/> is the way to restore. A file in the write-ahead log copied
/// over another one with the log of the latter is told apart by its stamp (<see cref="SqliteDataFileGuard"/>, which calls
/// this check first, ADR 029).
/// </summary>
internal static class SqliteJournalCheck
{
    /// <summary>`SQLITE_NOTADB`: the code given to the refusal, read as an unreachable database by the start (ADR 036).</summary>
    public const int ForeignJournalCode = 26;

    private const int HeaderLength = 20;
    private const int WriteVersion = 18;
    private const int ReadVersion = 19;
    private const byte RollbackJournal = 1;

    /// <summary>Throws when <paramref name="dataFile"/> is in the rollback journal next to a log that is not empty.</summary>
    public static void EnsureOwnJournal(string dataFile)
    {
        if (HasForeignJournal(dataFile))
        {
            throw new SqliteException(
                "The data file is a file in the rollback journal next to a write-ahead log that is not its own (a backup copied by hand): restore it with the backup service.",
                ForeignJournalCode, ForeignJournalCode);
        }
    }

    /// <summary>True when <paramref name="dataFile"/> says the rollback journal and a write-ahead log that is not empty lies next to it.</summary>
    public static bool HasForeignJournal(string dataFile)
    {
        var log = new FileInfo(dataFile + "-wal");
        if (!log.Exists || log.Length == 0)
            return false;
        Span<byte> header = stackalloc byte[HeaderLength];
        try
        {
            using var file = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (file.ReadAtLeast(header, HeaderLength, throwOnEndOfStream: false) < HeaderLength)
                return false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be read here is left to SQLite and its own failure.
            return false;
        }
        return header[..16].SequenceEqual("SQLite format 3\0"u8)
            && header[WriteVersion] == RollbackJournal && header[ReadVersion] == RollbackJournal;
    }
}
