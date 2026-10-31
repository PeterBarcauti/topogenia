// MigrationRollbackTests.cs
// The way back of the migrations on both engines: down to an empty schema and up again, and a failed migration that never leaves the lock.

using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// The application never migrates down; the tools of EF Core do (`database update 0`), and a migration may fail half way.
/// Both must leave a database that the start reads at once: the way down of every migration runs on both engines (the
/// SQLite one without the operations its provider cannot run, data M2; a file whose detailed map closes a round of
/// references is refused by SQLite, its initial migration left as it was, the file then behind), and a failed migration releases the lock of the migrations, so
/// that the next check neither waits the delay of the migrations nor is refused for its lock (ADR 041).
/// The way down has the delay of the tests (on SQL Server, removing the snapshot isolation waits for the snapshots of the
/// whole instance); the check after it has a short delay: a lock left behind fails the test after it, not after 10
/// minutes.
/// </summary>
public sealed class MigrationRollbackTests
{
    private static readonly TimeSpan ShortDelay = TimeSpan.FromSeconds(20);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_migration_goes_down_to_an_empty_schema_and_the_database_is_migrated_again(EngineKind kind)
    {
        await using var database = await TestDatabase.MigratedAsync(kind);
        var engine = database.EngineWith(ShortDelay);

        var known = await DownAsync(database.Engine);

        await AssertEmptyAsync(kind, engine);
        await AssertBehindAtOnceAsync(engine, known);
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock()).MigrateAsync(Token)).Status);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_database_with_a_detailed_map_goes_down_on_sql_server_and_is_refused_by_sqlite_with_its_lock_given_back(EngineKind kind)
    {
        await using var database = await TestDatabase.MigratedAsync(kind);
        var graph = ProjectGraph.New();
        await using (var context = database.Context())
        {
            // Rows in every table, a detailed map among them: on SQLite the map refers to the step it details, and the
            // step to its map, a round that a foreign key without its drop cannot leave.
            context.AddRange([.. graph.Records]);
            await context.SaveChangesAsync(Token);
        }
        var engine = database.EngineWith(ShortDelay);

        if (kind == EngineKind.SqlServer)
        {
            var known = await DownAsync(database.Engine);
            await AssertEmptyAsync(kind, engine);
            await AssertBehindAtOnceAsync(engine, known);
            return;
        }
        var migrations = await KnownAsync(database.Engine);
        var refused = await Assert.ThrowsAnyAsync<Exception>(() => DownAsync(database.Engine));
        Assert.Equal(19, Assert.IsType<Microsoft.Data.Sqlite.SqliteException>(refused.GetBaseException()).SqliteErrorCode);
        // The initial migration is left as it was by its transaction; the way down of each migration after it ran in its
        // own transaction, its record removed with it and the statements of the initial migration given back (data M2):
        // the start reads the file as behind at once (the lock was given back), migrates it, and every row is there.
        var watch = Stopwatch.StartNew();
        var check = await new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token);
        Assert.True(watch.Elapsed < ShortDelay, $"The check waited {watch.Elapsed}: the lock of the migrations was left.");
        Assert.Equal(SchemaStatus.Behind, check.Status);
        Assert.Equal([migrations[0]], check.Applied);
        Assert.Equal(migrations[1..], check.Pending);
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock()).MigrateAsync(Token)).Status);
        await using var read = engine.CreateContext();
        Assert.Equal([graph.Project.Id], await read.Set<Topogenia.Domain.Model.Project>().Select(project => project.Id).ToListAsync(Token));
        Assert.Equal(graph.Of<Topogenia.Domain.Model.Step>().Count, await read.Set<Topogenia.Domain.Model.Step>().CountAsync(Token));
    }

    private static async Task<List<string>> KnownAsync(DatabaseEngine engine)
    {
        await using var context = engine.CreateMigrationContext();
        return context.Database.GetMigrations().ToList();
    }

    /// <summary>Every migration down, by the tools of EF Core; the migrations of the application, in order.</summary>
    private static async Task<List<string>> DownAsync(DatabaseEngine engine)
    {
        await using var context = engine.CreateMigrationContext();
        var known = context.Database.GetMigrations().ToList();
        await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase, Token);
        return known;
    }

    /// <summary>
    /// What is left is what a new empty database holds; SQL Server keeps the schema `topogenia`, which the way down of EF
    /// Core does not drop (it is created again as it is).
    /// </summary>
    private static async Task AssertEmptyAsync(EngineKind kind, DatabaseEngine engine)
    {
        await using (var fresh = await TestDatabase.CreatedAsync(kind))
        {
            var left = (await DescriptionAsync(engine)).Where(line => !(kind == EngineKind.SqlServer && line == "S|topogenia|dbo"));
            Assert.Equal(await DescriptionAsync(fresh.Engine), left);
        }
        await using var context = engine.CreateContext();
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync(Token));
    }

    /// <summary>The check answers at once, every migration pending: no lock of the migrations was left.</summary>
    private static async Task AssertBehindAtOnceAsync(DatabaseEngine engine, List<string> known)
    {
        var watch = Stopwatch.StartNew();
        var behind = await new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token);
        Assert.True(watch.Elapsed < ShortDelay, $"The check waited {watch.Elapsed}: the lock of the migrations was left.");
        Assert.Equal(SchemaStatus.Behind, behind.Status);
        Assert.Empty(behind.Applied);
        Assert.Equal(known, behind.Pending);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_script_of_every_migration_up_and_down_is_generated_by_the_provider(EngineKind kind)
    {
        // A migration that its provider cannot generate fails after EF Core took the lock of the migrations; on SQLite the
        // row of the lock stayed behind (data M2). The migrations added later are held to the same rule here.
        // Nothing is created: the scripts are generated without the database.
        await using var database = TestDatabase.Create(kind);
        await using var context = database.Engine.CreateMigrationContext();
        var migrator = context.GetService<IMigrator>();
        var last = context.Database.GetMigrations().Last();

        Assert.NotEmpty(migrator.GenerateScript(Migration.InitialDatabase, last));
        Assert.NotEmpty(migrator.GenerateScript(last, Migration.InitialDatabase));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_migration_that_fails_half_way_leaves_no_lock_and_the_next_run_succeeds(EngineKind kind)
    {
        await using var database = await TestDatabase.CreatedAsync(kind);
        var engine = database.EngineWith(ShortDelay);

        var failure = await Assert.ThrowsAnyAsync<Exception>(
            () => new SchemaMigrations(engine, new FixedClock(), new FailingStatement(12)).MigrateAsync(Token));
        Assert.IsType<SimulatedEngineFailure>(failure.GetBaseException());

        var watch = Stopwatch.StartNew();
        var state = await new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token);
        Assert.True(watch.Elapsed < ShortDelay, $"The check waited {watch.Elapsed}: the lock of the migrations was left.");
        Assert.Equal(SchemaStatus.Behind, state.Status);
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock()).MigrateAsync(Token)).Status);
    }

    /// <summary>The schema of the database as its catalogue describes it.</summary>
    private static async Task<IReadOnlyList<string>> DescriptionAsync(DatabaseEngine engine)
    {
        await using var context = engine.CreateContext();
        await context.Database.OpenConnectionAsync(Token);
        return await engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token);
    }

    /// <summary>
    /// Fails the statement of the given rank among those that change the database, as the engine would fail it (disk
    /// full, lost connection): a statement of the migration, past the lock and the history tables.
    /// </summary>
    private sealed class FailingStatement(int rank) : DbCommandInterceptor
    {
        private int seen;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Fails() ? throw new SimulatedEngineFailure() : ValueTask.FromResult(result);

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) =>
            Fails() ? throw new SimulatedEngineFailure() : result;

        private bool Fails() => Interlocked.Increment(ref seen) == rank;
    }
}
