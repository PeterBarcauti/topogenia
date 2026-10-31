// TestDatabase.cs
// A throw-away database for one test: a SQL Server base Topogenia_Test_<n> or a SQLite file in a temporary folder (ADR 018).

using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>The two engines of the matrix (ADR 018): every persistence test runs on both.</summary>
public enum EngineKind
{
    Sqlite,
    SqlServer,
}

/// <summary>The theory data of the matrix.</summary>
public static class Engines
{
    public static TheoryData<EngineKind> All => [EngineKind.Sqlite, EngineKind.SqlServer];
}

/// <summary>
/// A database that the test creates and removes. SQL Server: the base `Topogenia_Test_&lt;process&gt;_&lt;n&gt;` on the
/// instance of <see cref="TestServer"/>; <see cref="Create"/> only names it (the code under test creates it),
/// <see cref="CreatedAsync"/>, <see cref="MigratedAsync"/> and <see cref="PreparedAsync"/> create it and remove it again
/// if their preparation fails, and <see cref="DisposeAsync"/> drops it. SQLite: a file in a folder created for the test
/// and deleted with it. No other database is ever touched (prompt 4-2).
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    /// <summary>Prefix of every SQL Server base of the tests.</summary>
    public const string Prefix = "Topogenia_Test_";

    /// <summary>Prefix of every temporary folder of a SQLite test database, followed by the process number and a dash.</summary>
    public const string FolderPrefix = "topogenia-test-";

    private static readonly Lazy<IReadOnlyList<string>> FoldersCleaned =
        new(() => StaleDatabases.DropFolders(Path.GetTempPath()), LazyThreadSafetyMode.ExecutionAndPublication);

    private static int counter;
    private readonly DirectoryInfo? folder;

    private TestDatabase(EngineKind kind, string name, string connectionString, DatabaseEngine engine, DirectoryInfo? folder)
    {
        Kind = kind;
        Name = name;
        ConnectionString = connectionString;
        Engine = engine;
        this.folder = folder;
    }

    public EngineKind Kind { get; }

    /// <summary>The SQL Server base name, or the SQLite file path.</summary>
    public string Name { get; }

    /// <summary>The connection string of the engine: the SQLite file without pooling, or the SQL Server base with the delays of the tests.</summary>
    public string ConnectionString { get; }

    public DatabaseEngine Engine { get; }

    /// <summary>The dialect of the engine: errors and transactions of the unit of work (P3-T04).</summary>
    public EngineDialect Dialect => Kind == EngineKind.Sqlite ? new SqliteDialect() : new SqlServerDialect();

    /// <summary>
    /// A new database, not created yet: the migrations create it. SQLite: the file of a new folder
    /// `topogenia-test-&lt;process&gt;-&lt;random&gt;` of the temporary folder of the account; the folders left by a process
    /// that no longer runs are removed before the first one of the process (<see cref="StaleDatabases.DropFolders"/>).
    /// </summary>
    public static TestDatabase Create(EngineKind kind)
    {
        var number = Interlocked.Increment(ref counter);
        if (kind == EngineKind.Sqlite)
        {
            _ = FoldersCleaned.Value;
            var folder = Directory.CreateTempSubdirectory($"{FolderPrefix}{Environment.ProcessId}-");
            var file = Path.Combine(folder.FullName, "data.db");
            var sqlite = new SqliteConnectionStringBuilder { DataSource = file, Pooling = false };
            return new TestDatabase(kind, file, sqlite.ConnectionString, new SqliteEngine(sqlite.ConnectionString), folder);
        }
        var name = $"{Prefix}{Environment.ProcessId}_{number}";
        var connection = TestServer.ConnectionString(name);
        return new TestDatabase(kind, name, connection, new SqlServerEngine(connection), null);
    }

    /// <summary>A new database, created empty: SQL Server through <see cref="TestServer"/> (bounded, retried), SQLite by EF Core.</summary>
    public static async Task<TestDatabase> CreatedAsync(EngineKind kind)
    {
        var database = Create(kind);
        try
        {
            if (kind == EngineKind.SqlServer)
            {
                await TestServer.CreateAsync(database.Name, Token);
            }
            else
            {
                await using var context = database.Context();
                await context.GetService<IRelationalDatabaseCreator>().CreateAsync(Token);
            }
            return database;
        }
        catch (Exception failure)
        {
            throw await DisposedAfterAsync(database, failure);
        }
    }

    /// <summary>A new database migrated to the last migration by the code under test (<see cref="SchemaMigrations"/>).</summary>
    public static Task<TestDatabase> MigratedAsync(EngineKind kind) =>
        PreparedAsync(kind, database => new SchemaMigrations(database.Engine, new FixedClock()).MigrateAsync(Token));

    /// <summary>
    /// A new database, created empty then brought to the state the test starts from by <paramref name="prepare"/>. A
    /// preparation that fails removes its database before the failure goes up: no base is left. On SQL Server, a passing
    /// failure of the instance (<see cref="TransientFailures.OfSetup"/>) starts again with a new database, a bounded
    /// number of times and within one budget shared with the retries of its creations and removals
    /// (<see cref="TransientFailures.Budget"/>); any other failure is thrown at once.
    /// </summary>
    public static Task<TestDatabase> PreparedAsync(EngineKind kind, Func<TestDatabase, Task> prepare)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        return TransientFailures.WithinBudgetAsync(TransientFailures.Budget, async () =>
        {
            for (var attempt = 1; ; attempt++)
            {
                var database = await CreatedAsync(kind);
                try
                {
                    await prepare(database);
                    return database;
                }
                catch (Exception failure)
                {
                    var thrown = await DisposedAfterAsync(database, failure);
                    if (kind != EngineKind.SqlServer || !TransientFailures.MayRetry(attempt) || !TransientFailures.OfSetup(failure))
                        throw thrown;
                }
                await Task.Delay(TransientFailures.DelayAfter(attempt), Token);
            }
        });
    }

    /// <summary>
    /// Runs a step of the test that creates this database itself (a migration of a new database by the code under test);
    /// on SQL Server, one at a time with the other creations of the process, and again on a passing failure of the
    /// creation, after removing what it created (<see cref="TestServer.CreatingAsync{T}"/>).
    /// </summary>
    public Task<T> CreatingAsync<T>(Func<Task<T>> creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        return Kind == EngineKind.SqlServer ? TestServer.CreatingAsync(Name, creation, Token) : creation();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public AppDbContext Context() => Engine.CreateContext();

    /// <summary>
    /// Another engine on this database with the given delay of the migrations (ADR 041) and, on SQL Server, the given
    /// ordinary command timeout instead of the one of the tests (SQLite has its own, ADR 029). Its connections use another
    /// pool on SQL Server: the removal of the base rolls them back.
    /// </summary>
    public DatabaseEngine EngineWith(TimeSpan migrationTimeout, int? commandTimeoutSeconds = null)
    {
        if (Kind == EngineKind.Sqlite)
            return new SqliteEngine(ConnectionString) { MigrationTimeout = migrationTimeout };
        var connection = new SqlConnectionStringBuilder(ConnectionString);
        if (commandTimeoutSeconds is { } seconds)
            connection.CommandTimeout = seconds;
        return new SqlServerEngine(connection.ConnectionString) { MigrationTimeout = migrationTimeout };
    }

    /// <summary>A session of one request on this database (unit of work, workspace store, counters).</summary>
    public StoreSession Session(RetryPolicy? retry = null, params IInterceptor[] interceptors) => new(Engine, Dialect, retry, interceptors);

    /// <summary>Removes the database after a failure of its preparation; the failure goes up, with the one of the removal if any.</summary>
    private static async Task<Exception> DisposedAfterAsync(TestDatabase database, Exception failure)
    {
        try
        {
            await database.DisposeAsync();
            return failure;
        }
        catch (Exception removal)
        {
            return new AggregateException(failure, removal);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (folder is null)
        {
            // Only a base of the tests is ever dropped (prompt 4-2); the pool of this base only is cleared.
            await TestServer.DropAsync(Name, CancellationToken.None);
            return;
        }
        // A host of the tests of the API may still give back its last connection to the pool while it stops: the
        // folder is removed once the handle is gone, within a bounded wait.
        for (var attempt = 1; ; attempt++)
        {
            SqliteConnection.ClearAllPools();
            folder.Refresh();
            if (!folder.Exists)
                return;
            try
            {
                folder.Delete(recursive: true);
                return;
            }
            catch (IOException) when (attempt < FolderRemovalAttempts)
            {
                await Task.Delay(FolderRemovalDelay);
            }
        }
    }

    /// <summary>The attempts to remove the folder of a SQLite base, and the delay between two: 5 s in all.</summary>
    private const int FolderRemovalAttempts = 50;

    private static readonly TimeSpan FolderRemovalDelay = TimeSpan.FromMilliseconds(100);
}
