// SqliteConnectionSettings.cs
// The settings that every SQLite connection receives (spec 4.3-6): foreign keys, write-ahead log, busy timeout, synchronous, command timeout, decimal collation.

using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Topogenia.Persistence.Sqlite.Connection;

/// <summary>The journal modes used by the application: the write-ahead log for the data file, the rollback journal for a backup.</summary>
public enum SqliteJournalMode
{
    Delete,
    Wal,
}

/// <summary>The levels of `PRAGMA synchronous`, with the numbers SQLite gives them.</summary>
public enum SqliteSynchronous
{
    Off = 0,
    Normal = 1,
    Full = 2,
    Extra = 3,
}

/// <summary>
/// The settings of a SQLite connection. <see cref="Default"/> holds the values of spec 4.3-6 (A-5-28, the 1.x `db/003`):
/// foreign keys on, write-ahead log, 5 seconds of busy timeout, synchronous NORMAL (safe with the write-ahead log, a power
/// cut loses at most the last commits, never the file). Foreign keys, busy timeout and synchronous belong to the
/// connection and are lost when it closes; the journal mode is written in the file. They are applied at every opening by
/// <see cref="SqliteSettingsInterceptor"/>, a connection given back by the pool included. Two settings of the provider
/// itself, not of SQLite, go with them: the command timeout (<see cref="CommandTimeoutSeconds"/>) and the decimal
/// collation (<see cref="SqliteDecimalCollation"/>).
/// </summary>
public sealed record SqliteConnectionSettings(bool ForeignKeys, SqliteJournalMode JournalMode, int BusyTimeoutMilliseconds, SqliteSynchronous Synchronous)
{
    /// <summary>
    /// The command timeout of Microsoft.Data.Sqlite, in seconds: the smallest it accepts (0 would mean no limit). The
    /// provider runs again a statement that SQLite answers `SQLITE_BUSY` or `SQLITE_LOCKED`, every 150 ms, as long as the
    /// command is younger than this timeout; SQLite itself already waited `busy_timeout` (5 s) before answering. With
    /// the default of the provider (30 s), a statement on a database held elsewhere waited about 34 s, and the three
    /// attempts of a unit (spec 4.3-5) about 100 s. With 1 s: a lock held for good fails a statement after the 5 s of
    /// SQLite (the provider gives up at once, its timeout being already past), and a busy answer given at once (a snapshot
    /// too old in the write-ahead log) is tried again for at most 1 s, then waits at most the 5 s of SQLite: at most
    /// <see cref="MaximumBusyWait"/> per statement (ADR 029).
    /// </summary>
    public const int CommandTimeoutSeconds = 1;

    /// <summary>The settings of spec 4.3-6, given to every connection of the application.</summary>
    public static SqliteConnectionSettings Default { get; } = new(true, SqliteJournalMode.Wal, 5000, SqliteSynchronous.Normal);

    /// <summary>
    /// The longest a statement of the application waits for a lock before it fails as busy: the command timeout, one
    /// pause of 150 ms of the provider and the busy timeout of SQLite (6.15 s with <see cref="Default"/>). Read by the tests
    /// of the waits only (Architecture m6): no type of the application needs it.
    /// </summary>
    internal TimeSpan MaximumBusyWait =>
        TimeSpan.FromSeconds(CommandTimeoutSeconds) + TimeSpan.FromMilliseconds(150) + TimeSpan.FromMilliseconds(BusyTimeoutMilliseconds);

    /// <summary>
    /// Applies the settings to an open connection, the busy timeout first so that the others wait for a lock. The journal
    /// mode is set by a connection that may write only: EF Core opens a read-only connection to see whether the file
    /// exists, which cannot change the mode written in the file, and a restored backup is in the rollback journal (ADR
    /// 029) until the first connection that may write takes it to the write-ahead log.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="beforeJournalMode">Run after the other settings and before the journal mode, which may write the file
    /// (the checks of the data file, <see cref="SqliteDataFileGuard"/>).</param>
    public void Apply(DbConnection connection, Action<DbConnection>? beforeJournalMode = null)
    {
        Prepare(connection);
        foreach (var statement in SqlitePragmas.Settings(this))
            SqlitePragmas.Execute(connection, statement);
        beforeJournalMode?.Invoke(connection);
        if (!ReadOnly(connection))
            SqlitePragmas.SetJournalMode(connection, JournalMode);
    }

    /// <inheritdoc cref="Apply"/>
    public Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken) => ApplyAsync(connection, null, cancellationToken);

    /// <inheritdoc cref="Apply"/>
    public async Task ApplyAsync(DbConnection connection, Action<DbConnection>? beforeJournalMode, CancellationToken cancellationToken)
    {
        Prepare(connection);
        foreach (var statement in SqlitePragmas.Settings(this))
            await SqlitePragmas.ExecuteAsync(connection, statement, cancellationToken);
        beforeJournalMode?.Invoke(connection);
        if (!ReadOnly(connection))
            await SqlitePragmas.SetJournalModeAsync(connection, JournalMode, cancellationToken);
    }

    /// <summary>A connection opened read-only by its connection string (`Mode=ReadOnly`).</summary>
    private static bool ReadOnly(DbConnection connection) =>
        connection is SqliteConnection sqlite && new SqliteConnectionStringBuilder(sqlite.ConnectionString).Mode == SqliteOpenMode.ReadOnly;

    /// <summary>
    /// The settings of the provider, before any statement: the command timeout of the commands created from now on, those
    /// of the pragmas and of `BEGIN` included, and the invariant decimal collation in place of the one of EF Core.
    /// </summary>
    private static void Prepare(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection is not SqliteConnection sqlite)
            return;
        sqlite.DefaultTimeout = CommandTimeoutSeconds;
        SqliteDecimalCollation.Register(sqlite);
    }
}
