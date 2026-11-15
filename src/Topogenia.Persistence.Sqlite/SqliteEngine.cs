// SqliteEngine.cs
// The SQLite engine of the desktop mode: options of the common context, tables without schema prefix, settings of each connection.

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite.Connection;

namespace Topogenia.Persistence.Sqlite;

/// <summary>
/// A SQLite data file. SQLite has no schema: the tables have no prefix (spec 4.1). Every connection that a context opens
/// receives the settings of spec 4.3-6 (<see cref="SqliteConnectionSettings.Default"/>) from
/// <see cref="SqliteSettingsInterceptor"/>, placed before the interceptors given by the caller. The command timeout of
/// an ordinary context is <see cref="SqliteConnectionSettings.CommandTimeoutSeconds"/>, whatever the connection string
/// says, so that a statement waits for a lock about the busy timeout of SQLite and no more (spec 4.3-5, ADR 029). A
/// context of the migrations has the delay of the migrations instead, in its options and on its connection (the
/// commands of the catalogue and the `BEGIN` of the transactions of the migrations), so that a statement waits that long
/// for the write lock held by a migration elsewhere (ADR 041). The online backup of the file is
/// <see cref="Backup.SqliteBackupService"/>.
/// </summary>
public sealed class SqliteEngine : DatabaseEngine
{
    private static readonly SqliteSettingsInterceptor Settings = new(SqliteConnectionSettings.Default);

    public SqliteEngine(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
        DataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
    }

    public override SchemaCatalog Catalog { get; } = new SqliteSchemaCatalog();

    /// <summary>The data file named by the connection string, as written there.</summary>
    public string DataSource { get; }

    internal string ConnectionString { get; }

    /// <summary>
    /// The clean stop of the desktop mode, once every context is disposed: the write-ahead log is written back into the
    /// data file and emptied, so that no log survives a normal stop. The engine never lets SQLite write the log back by
    /// itself (data B1): without this call the log stays, and the next start reads it with the file. Throws a
    /// <see cref="SqliteException"/> 26 when the file was replaced by hand while the application ran (its log is not
    /// its own, <see cref="SqliteDataFileGuard"/>): the log is kept and the next start refuses the file.
    /// </summary>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await using (var context = CreateContext())
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            SqliteDataFileGuard.Close(context.Database.GetDbConnection());
        }
        SqliteConnection.ClearPool(new SqliteConnection(ConnectionString));
    }

    protected override DbContextOptions<AppDbContext> Options(IReadOnlyList<IInterceptor> interceptors, int? commandTimeoutSeconds) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ConnectionString, sqlite => sqlite
                .MigrationsAssembly(typeof(SqliteEngine).Assembly.GetName().Name)
                .MigrationsHistoryTable(TableNames.MigrationsHistory)
                .CommandTimeout(commandTimeoutSeconds ?? SqliteConnectionSettings.CommandTimeoutSeconds))
            .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
            .ReplaceService<IMigrationsSqlGenerator, AtomicRebuildSqlGenerator>()
            .UseTextSearchParity(typeof(SqliteTextSearch))
            .AddInterceptors(commandTimeoutSeconds is { } seconds
                ? [Settings, new MigrationCommandTimeout(seconds), .. interceptors]
                : [Settings, .. interceptors])
            .Options;

    /// <summary>
    /// Placed right after <see cref="Settings"/> on a context of the migrations: gives the delay of the migrations to the
    /// commands that the connection creates itself (those of the catalogue, the `BEGIN` of a transaction), in place of the
    /// ordinary timeout that the settings have just set.
    /// </summary>
    private sealed class MigrationCommandTimeout(int seconds) : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Apply(connection);

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Apply(connection);
            return Task.CompletedTask;
        }

        private void Apply(DbConnection connection)
        {
            if (connection is SqliteConnection sqlite)
                sqlite.DefaultTimeout = seconds;
        }
    }
}
