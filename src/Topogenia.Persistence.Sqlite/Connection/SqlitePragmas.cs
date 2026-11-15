// SqlitePragmas.cs
// The SQL of the settings and of the checks of a SQLite file: every PRAGMA of the provider is written here (ADR 025).

using System.Data.Common;
using System.Globalization;

namespace Topogenia.Persistence.Sqlite.Connection;

/// <summary>
/// Statements on one SQLite connection: the settings of a connection, their reading, the journal mode, the stamp and the
/// checkpoints of the data file, and the two checks of a file (`integrity_check`, `foreign_key_check`). Values are
/// numbers or names of closed lists, never text from outside: nothing is concatenated that a user could write.
/// </summary>
internal static class SqlitePragmas
{
    /// <summary>
    /// The per-connection settings, the busy timeout first; the journal mode is set apart (it answers a row). `LIKE` is
    /// made case-sensitive, as in the binary collation of SQL Server: EF Core translates `StartsWith` and `EndsWith` by
    /// `LIKE`, which SQLite otherwise compares without the case of the ASCII letters (ADR 038). Deleted content is
    /// overwritten with zeros (`secure_delete`): a purged record leaves no trace in the free pages of the file, nor in a
    /// backup, which copies the file page by page (ADR 035, security round 4).
    /// </summary>
    public static IEnumerable<string> Settings(SqliteConnectionSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settings.BusyTimeoutMilliseconds);
        yield return "PRAGMA busy_timeout = " + settings.BusyTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture);
        yield return "PRAGMA foreign_keys = " + (settings.ForeignKeys ? "ON" : "OFF");
        yield return "PRAGMA synchronous = " + ((int)settings.Synchronous).ToString(CultureInfo.InvariantCulture);
        yield return "PRAGMA case_sensitive_like = ON";
        yield return "PRAGMA secure_delete = ON";
    }

    public static void Execute(DbConnection connection, string statement)
    {
        using var command = Command(connection, statement);
        command.ExecuteNonQuery();
    }

    public static async Task ExecuteAsync(DbConnection connection, string statement, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, statement);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Sets the journal mode and checks the answer: SQLite keeps its mode, without error, when it cannot change it.</summary>
    public static void SetJournalMode(DbConnection connection, SqliteJournalMode mode)
    {
        using var command = Command(connection, "PRAGMA journal_mode = " + Name(mode));
        EnsureJournalMode(mode, command.ExecuteScalar());
    }

    /// <inheritdoc cref="SetJournalMode"/>
    public static async Task SetJournalModeAsync(DbConnection connection, SqliteJournalMode mode, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, "PRAGMA journal_mode = " + Name(mode));
        EnsureJournalMode(mode, await command.ExecuteScalarAsync(cancellationToken));
    }

    public static async Task<SqliteConnectionSettings> ReadAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var foreignKeys = await ScalarAsync(connection, "PRAGMA foreign_keys", cancellationToken);
        var journal = await ScalarAsync(connection, "PRAGMA journal_mode", cancellationToken);
        var busy = await ScalarAsync(connection, "PRAGMA busy_timeout", cancellationToken);
        var synchronous = await ScalarAsync(connection, "PRAGMA synchronous", cancellationToken);
        return new SqliteConnectionSettings(
            Number(foreignKeys) != 0,
            ModeOf(Convert.ToString(journal, CultureInfo.InvariantCulture)),
            (int)Number(busy),
            (SqliteSynchronous)Number(synchronous));
    }

    /// <summary>
    /// The settings of the checkpoints of the data file (data B1): no automatic checkpoint (`wal_autocheckpoint = 0`, the
    /// engine runs its own, <see cref="SqliteDataFileGuard"/>) and a log cut back to nothing each time it starts again
    /// (`journal_size_limit = 0`), so that its length tells the work not yet written back.
    /// </summary>
    public static IEnumerable<string> CheckpointSettings()
    {
        yield return "PRAGMA wal_autocheckpoint = 0";
        yield return "PRAGMA journal_size_limit = 0";
    }

    /// <summary>Writes the stamp of the data file in the header of its first page (`user_version`, a number only).</summary>
    public static void SetStamp(DbConnection connection, int stamp) =>
        Execute(connection, "PRAGMA user_version = " + stamp.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Writes back the log into the data file: <paramref name="truncate"/> waits for the readers (busy timeout) and empties
    /// the log (`TRUNCATE`), else what no reader still needs is written back at once (`PASSIVE`). True when every frame
    /// of the log was written back.
    /// </summary>
    public static bool Checkpoint(DbConnection connection, bool truncate)
    {
        using var command = Command(connection, "PRAGMA wal_checkpoint(" + (truncate ? "TRUNCATE" : "PASSIVE") + ")");
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return false;
        // One row: busy (1 when it could not finish), frames in the log, frames written back (-1 and -1 outside WAL).
        var busy = reader.GetInt64(0);
        var log = reader.GetInt64(1);
        var written = reader.GetInt64(2);
        return busy == 0 && log == written;
    }

    /// <summary>The lines of `integrity_check` other than its single `ok`: empty when the file is sound.</summary>
    public static async Task<IReadOnlyList<string>> IntegrityProblemsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var lines = await RowsAsync(connection, "PRAGMA integrity_check", cancellationToken);
        return lines is ["ok"] ? [] : lines;
    }

    /// <summary>One line per row of `foreign_key_check` (table, row, parent table): empty when every reference holds.</summary>
    public static Task<IReadOnlyList<string>> ForeignKeyProblemsAsync(DbConnection connection, CancellationToken cancellationToken) =>
        RowsAsync(connection, "PRAGMA foreign_key_check", cancellationToken);

    private static async Task<IReadOnlyList<string>> RowsAsync(DbConnection connection, string statement, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, statement);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            lines.Add(string.Join(" ", values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))));
        }
        return lines;
    }

    private static async Task<object?> ScalarAsync(DbConnection connection, string statement, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, statement);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static long Number(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);

    private static string Name(SqliteJournalMode mode) => mode switch
    {
        SqliteJournalMode.Delete => "DELETE",
        SqliteJournalMode.Wal => "WAL",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    private static SqliteJournalMode ModeOf(string? name)
    {
        foreach (var mode in Enum.GetValues<SqliteJournalMode>())
        {
            if (string.Equals(Name(mode), name, StringComparison.OrdinalIgnoreCase))
                return mode;
        }
        throw new InvalidOperationException($"Unexpected SQLite journal mode {name}.");
    }

    private static void EnsureJournalMode(SqliteJournalMode mode, object? answer)
    {
        if (ModeOf(Convert.ToString(answer, CultureInfo.InvariantCulture)) != mode)
            throw new InvalidOperationException($"SQLite kept the journal mode {answer} instead of {Name(mode)}.");
    }

    private static DbCommand Command(DbConnection connection, string statement)
    {
        var command = connection.CreateCommand();
        command.CommandText = statement;
        return command;
    }
}
