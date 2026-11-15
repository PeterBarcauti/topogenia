// DataFileUse.cs
// Whether a data file of the desktop mode, its log or its shared memory is held open by a connection, here or in another process.

using System.Globalization;

namespace Topogenia.Persistence.Sqlite.Files;

/// <summary>
/// Windows refuses to open with no sharing a file that some connection holds: the data file, its write-ahead log and its
/// shared memory are tried in turn. The restoration needs no connection left anywhere (ADR 029); `--check` of the desktop
/// mode does not open a file that the running application holds (two processes on one file, ADR 029).
/// </summary>
public static class DataFileUse
{
    /// <summary>The files that SQLite keeps next to a data file: its write-ahead log, its shared memory and its rollback journal.</summary>
    public static IReadOnlyList<string> JournalSuffixes { get; } = ["-wal", "-shm", "-journal"];

    /// <summary>
    /// The first of the data file and its journals that some connection holds open, by its suffix and the code of the
    /// failure (`data file-wal 0x80070020`); null when none is open or none exists.
    /// </summary>
    public static string? HeldOpen(string dataFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFile);
        foreach (var suffix in new[] { "" }.Concat(JournalSuffixes))
        {
            var file = dataFile + suffix;
            if (!File.Exists(file))
                continue;
            try
            {
                using var alone = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException error)
            {
                return $"data file{suffix} 0x{error.HResult.ToString("X8", CultureInfo.InvariantCulture)}";
            }
            catch (UnauthorizedAccessException)
            {
                // A file this account may not write (read-only, rights): whether it is open cannot be told here; the
                // check of the start that follows refuses it for its rights.
            }
        }
        return null;
    }
}
