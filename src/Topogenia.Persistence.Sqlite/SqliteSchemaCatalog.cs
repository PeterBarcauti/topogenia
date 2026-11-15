// SqliteSchemaCatalog.cs
// History of the schema and description of the catalogue on SQLite (spec 4.4): the SQL of the schema check lives here (ADR 025).

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;

namespace Topogenia.Persistence.Sqlite;

/// <summary>
/// Reads and writes `schema_history` and describes the schema from `sqlite_master`, whose `sql` column keeps the exact
/// statement of each table, index, view and trigger (columns, types, keys, CHECK constraints, foreign keys, filters);
/// the internal objects of SQLite (named `sqlite_` and something) and the two tables of EF Core (history, lock) aside,
/// but not what a person attaches to them. A failure of SQLite met by the check or the migration is read by its result
/// code (<see cref="RefusalOf"/>).
/// </summary>
internal sealed class SqliteSchemaCatalog : SchemaCatalog
{
    private const string History = "\"" + TableNames.SchemaHistory + "\"";

    /// <summary>`SQLITE_CANTOPEN` and `SQLITE_NOTADB`: the file cannot be opened, or is not a database.</summary>
    private static readonly int[] Unreachable = [14, 26];

    /// <summary>`SQLITE_PERM` and `SQLITE_READONLY`: the account may not open the file as asked, or may not write it.</summary>
    private static readonly int[] Refused = [3, 8];

    /// <summary>`SQLITE_BUSY` and `SQLITE_LOCKED`: another connection holds the file (ADR 029).</summary>
    private static readonly int[] Transient = [SqliteDialect.Busy, SqliteDialect.Locked];

    public override async Task<IReadOnlyList<SchemaHistoryEntry>> HistoryAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var exists = Command(connection, transaction,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @table", ("@table", TableNames.SchemaHistory));
        if (Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 0)
            return [];
        await using var select = Command(connection, transaction,
            $"SELECT migration_id, migration_fingerprint, schema_fingerprint, application_version, applied_at FROM {History}");
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        var entries = new List<SchemaHistoryEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new SchemaHistoryEntry(
                reader.GetString(0),
                reader.GetString(1),
                await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetString(2),
                reader.GetString(3),
                DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)));
        }
        return entries;
    }

    public override async Task RecordAsync(DbConnection connection, DbTransaction transaction, SchemaHistoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var insert = Command(connection, transaction,
            $"INSERT INTO {History} (migration_id, migration_fingerprint, schema_fingerprint, application_version, applied_at) " +
            "VALUES (@id, @migration, @schema, @version, @at)",
            ("@id", entry.MigrationId), ("@migration", entry.MigrationFingerprint), ("@schema", entry.SchemaFingerprint),
            ("@version", entry.ApplicationVersion), ("@at", UtcDates.ToStore(entry.AppliedAt)));
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<IReadOnlyList<string>> DescribeAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        // The underscore is escaped (in LIKE it matches any character): only the names that SQLite keeps for itself are
        // left out. Only the two tables of EF Core are, never what is attached to them (an index, a trigger).
        await using var command = Command(connection, transaction,
            "SELECT type || '|' || name || '|' || tbl_name || '|' || COALESCE(sql, '') FROM sqlite_master " +
            "WHERE name NOT LIKE 'sqlite\\_%' ESCAPE '\\' AND NOT (type = 'table' AND name IN (@history, @lock))",
            ("@history", TableNames.MigrationsHistory), ("@lock", TableNames.MigrationsLock));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            lines.Add(reader.GetString(0));
        return lines;
    }

    /// <summary>
    /// The first failure of SQLite in the chain, read by its primary result code, that of its extended code as well (the
    /// low byte: `SQLITE_BUSY_RECOVERY` 261 is a busy file): busy or locked first, <c>BASE_OCCUPEE</c>, the only
    /// transient code; then a file that cannot be opened or is not a database, <c>BASE_INACCESSIBLE</c>; then a file that
    /// the account may not write, <c>DROITS_INSUFFISANTS</c>. Any other failure (corrupt file, full disk, input or output)
    /// stays the one of the engine (ADR 036, revision of the desktop mode).
    /// </summary>
    public override string? RefusalOf(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqliteException failure)
                continue;
            var codes = new[] { failure.SqliteErrorCode & 0xFF, failure.SqliteExtendedErrorCode & 0xFF };
            if (codes.Intersect(Transient).Any())
                return SchemaCodes.DatabaseBusy;
            if (codes.Intersect(Unreachable).Any())
                return SchemaCodes.DatabaseUnreachable;
            return codes.Intersect(Refused).Any() ? SchemaCodes.InsufficientRights : null;
        }
        return null;
    }

    /// <summary>The result code and the extended code of the first failure of SQLite in the chain.</summary>
    public override IReadOnlyList<int> NumbersOf(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException failure)
                return [failure.SqliteErrorCode, failure.SqliteExtendedErrorCode];
        }
        return [];
    }

    /// <summary>The time written by EF Core in the row of `__EFMigrationsLock` when it took the lock, null when there is none.</summary>
    public override async Task<string?> LockMarkAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var exists = Command(connection, null,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @table", ("@table", TableNames.MigrationsLock));
        if (Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 0)
            return null;
        await using var select = Command(connection, null, $"SELECT \"Timestamp\" FROM \"{TableNames.MigrationsLock}\" WHERE \"Id\" = 1");
        return await select.ExecuteScalarAsync(cancellationToken) is string mark ? mark : null;
    }

    /// <summary>
    /// The lock of the migrations of EF Core on SQLite is a row of `__EFMigrationsLock`, which a migration stopped before
    /// it removes it leaves for good (ADR 041). A process that holds it is alive when the row is gone or taken again
    /// during the wait (its mark changed), or when it writes the file now (a migration holds the write lock of SQLite during
    /// its transactions: `BEGIN IMMEDIATE` is refused at once). Otherwise the row stayed through the whole delay and nobody
    /// writes: left by a holder that stopped. A migration alive that held the row since before the wait, longer than the
    /// whole delay, and is between two of its transactions at that instant, is taken for a stopped one: the host says to
    /// close any other instance first (P4-T05).
    /// </summary>
    public override async Task<bool> LockLeftByAStoppedHolderAsync(DbConnection connection, string? before, CancellationToken cancellationToken)
    {
        var now = await LockMarkAsync(connection, cancellationToken);
        return now is not null && now == before && !await WrittenElsewhereAsync(connection, cancellationToken);
    }

    /// <summary>
    /// The check holds the write lock of SQLite (`BEGIN IMMEDIATE`, nothing written) instead of the row of
    /// `__EFMigrationsLock`: SQLite releases it by itself when the process stops, where a row written by a check that a
    /// stop cut would refuse a sound file at the next start (`VERROU_MIGRATIONS`, data MAJ-1 of round 4). A migration
    /// writes its row before its first step and holds the write lock during each of its transactions: when the row is
    /// there or the file is written, the check waits for the lock of the migrations as before (null). A file that the
    /// connection may not write takes the lock of EF Core too, whose row it cannot write: the check refuses it for its
    /// rights (`SQLITE_READONLY`, A-5-10), where `BEGIN IMMEDIATE` alone would pass.
    /// </summary>
    public override async Task<IAsyncDisposable?> CheckLockAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not SqliteConnection { Handle: { } handle } || SQLitePCL.raw.sqlite3_db_readonly(handle, "main") != 0
            || !await TryBeginImmediateAsync(connection, cancellationToken))
            return null;
        var held = new RolledBack(connection);
        if (await LockMarkAsync(connection, cancellationToken) is null)
            return held;
        await held.DisposeAsync();
        return null;
    }

    /// <summary>
    /// True when another connection holds the write lock of the file; otherwise the transaction is rolled back at once,
    /// nothing written.
    /// </summary>
    private static async Task<bool> WrittenElsewhereAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!await TryBeginImmediateAsync(connection, cancellationToken))
            return true;
        await new RolledBack(connection).DisposeAsync();
        return false;
    }

    /// <summary>
    /// `BEGIN IMMEDIATE`, false when another connection holds the write lock: SQLite answers busy after the busy timeout of
    /// the connection (5 s), never tried again by the provider past its smallest command timeout (1 s).
    /// </summary>
    private static async Task<bool> TryBeginImmediateAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var begin = Command(connection, null, "BEGIN IMMEDIATE");
        begin.CommandTimeout = 1;
        try
        {
            await begin.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (SqliteException failure) when (Transient.Contains(failure.SqliteErrorCode & 0xFF))
        {
            return false;
        }
    }

    private static DbCommand Command(DbConnection connection, DbTransaction? transaction, string text, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = text;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    /// <summary>The write transaction of the check, rolled back when released.</summary>
    private sealed class RolledBack(DbConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await using var rollback = Command(connection, null, "ROLLBACK");
            await rollback.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
