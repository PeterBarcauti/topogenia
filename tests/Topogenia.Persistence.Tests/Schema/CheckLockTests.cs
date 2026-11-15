// CheckLockTests.cs
// On SQLite the check of the start writes no row of the lock of the migrations: a stop during it never leaves a lock behind (data MAJ-1 of round 4).

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Schema;

/// <summary>
/// The probe of the gate (round 4, data MAJ-1): the check of the start, killed while it held the lock of the migrations
/// of EF Core, left its row in `__EFMigrationsLock`, and the next start refused the sound file as
/// <c>VERROU_MIGRATIONS</c> after the whole delay of the migrations (603 s measured). The check now holds the write lock
/// of SQLite, which the engine releases with the process: at every statement of the check, no row of the lock exists, so
/// that no stop can leave one. A migration still writes its row; the waits for it are proven by
/// <c>MigrationTimeoutTests</c>. SQLite only: on SQL Server the lock is an application lock of the session.
/// </summary>
public sealed class CheckLockTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>At each statement that EF Core sends for the check, the row of `__EFMigrationsLock` read by a connection of its own.</summary>
    private sealed class LockRows(DatabaseEngine engine, string file) : DbCommandInterceptor
    {
        public List<string?> Seen { get; } = [];

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            Seen.Add(await MarkAsync());
            return result;
        }

        public override async ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        {
            Seen.Add(await MarkAsync());
            return result;
        }

        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Seen.Add(await MarkAsync());
            return result;
        }

        /// <summary>
        /// The mark of the row of the lock, null when there is none, read by the catalogue of the engine on a plain
        /// connection, read-only and never written back on close: the file stays as the check leaves it.
        /// </summary>
        private async Task<string?> MarkAsync()
        {
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString);
            await connection.OpenAsync(Token);
            _ = SQLitePCL.raw.sqlite3_db_config(connection.Handle, 1006, 1, out _);
            return await engine.Catalog.LockMarkAsync(connection, Token);
        }
    }

    [Fact]
    public async Task On_sqlite_the_check_of_the_start_never_writes_a_row_of_the_lock_of_the_migrations()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.Sqlite);
        var rows = new LockRows(database.Engine, database.Name);

        var state = await new SchemaMigrations(database.Engine, new FixedClock(), rows).CheckAsync(Token);

        Assert.Equal(SchemaStatus.UpToDate, state.Status);
        Assert.NotEmpty(rows.Seen);
        Assert.All(rows.Seen, Assert.Null);
    }
}
