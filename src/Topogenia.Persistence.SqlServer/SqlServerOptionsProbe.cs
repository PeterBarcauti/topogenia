// SqlServerOptionsProbe.cs
// Reads and changes by hand the options of the database that the snapshot reads need, and makes the record of one migration fail (tests only).

using System.Data.Common;
using Topogenia.Persistence.Model;

namespace Topogenia.Persistence.SqlServer;

/// <summary>
/// What the tests of the snapshot reads need from SQL Server (ADR 031), kept here because no SQL text lives outside the
/// providers (spec 3.2): the options of the current database, read in `sys.databases`; the same options removed by hand,
/// as an administrator could; and a guard added by hand to one of the two histories of the migrations that refuses the
/// record of one migration, so that the migrations fail after the statements that run outside their transaction. Visible
/// to the persistence tests only.
/// </summary>
internal static class SqlServerOptionsProbe
{
    /// <summary>The options of a database: the state of the snapshot isolation and the row versions of read committed.</summary>
    public sealed record DatabaseOptions(string SnapshotIsolation, bool ReadCommittedSnapshot);

    /// <summary>The name of the guard added by <see cref="RefuseRecordAsync"/>.</summary>
    public const string RefusalGuard = "CK_probe_refused_migration";

    public static async Task<DatabaseOptions> OptionsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = Command(connection,
            "SELECT snapshot_isolation_state_desc, is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID();");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("The current database is not visible in sys.databases.");
        return new DatabaseOptions(reader.GetString(0), reader.GetBoolean(1));
    }

    /// <summary>
    /// Sets or removes the snapshot isolation of the current database, as an administrator could by hand. Its removal
    /// waits until every transaction that writes in a versioned database of the instance when it starts has ended, other
    /// databases included; SQL Server refuses `WITH ROLLBACK IMMEDIATE` on a change of the versioning (5083): the caller
    /// gives the command the delay it needs.
    /// </summary>
    public static Task SetSnapshotIsolationAsync(DbConnection connection, bool on, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, $"ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION {(on ? "ON" : "OFF")};", cancellationToken);

    /// <summary>Sets or removes the row versions of read committed, the other sessions rolled back, as an administrator could by hand.</summary>
    public static Task SetReadCommittedSnapshotAsync(DbConnection connection, bool on, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, $"ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT {(on ? "ON" : "OFF")} WITH ROLLBACK IMMEDIATE;", cancellationToken);

    /// <summary>
    /// Adds to one of the two histories of the migrations (`schema_history` or the history of EF Core, named by its table)
    /// a guard that refuses the record of the migration, whose identifier is quoted by the server.
    /// </summary>
    public static async Task RefuseRecordAsync(DbConnection connection, string table, string migrationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationId);
        var column = HistoryColumn(table);
        await using var command = Command(connection,
            $"DECLARE @text nvarchar(max) = N'ALTER TABLE [{SqlServerEngine.Schema}].[{table}] ADD CONSTRAINT {RefusalGuard} " +
            $"CHECK ([{column}] <> ' + QUOTENAME(@id, N'''') + N');'; EXEC sys.sp_executesql @text;");
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = migrationId;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Removes the guard of <see cref="RefuseRecordAsync"/> from the history.</summary>
    public static Task AcceptRecordsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        _ = HistoryColumn(table);
        return ExecuteAsync(connection, $"ALTER TABLE [{SqlServerEngine.Schema}].[{table}] DROP CONSTRAINT {RefusalGuard};", cancellationToken);
    }

    private static string HistoryColumn(string table) => table switch
    {
        TableNames.SchemaHistory => "migration_id",
        TableNames.MigrationsHistory => "MigrationId",
        _ => throw new ArgumentException($"{table} is not a history of the migrations.", nameof(table)),
    };

    private static async Task ExecuteAsync(DbConnection connection, string text, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, text);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand Command(DbConnection connection, string text)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var command = connection.CreateCommand();
        command.CommandText = text;
        return command;
    }
}
