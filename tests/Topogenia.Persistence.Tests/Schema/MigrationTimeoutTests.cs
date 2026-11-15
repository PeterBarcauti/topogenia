// MigrationTimeoutTests.cs
// The delay of the migrations: their statements and the wait for their lock are not cut by the ordinary command timeout (ADR 041).

using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Schema;

public sealed class MigrationTimeoutTests
{
    /// <summary>The ordinary command timeout of the engine of the waiting process on SQL Server, short on purpose.</summary>
    private const int OrdinarySeconds = 2;

    /// <summary>How much longer than the ordinary timeout the lock is held while the other process waits.</summary>
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(3);

    /// <summary>How long a test waits for a step of the other process before it fails.</summary>
    private static readonly TimeSpan Rendezvous = TimeSpan.FromSeconds(60);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The longest that an ordinary statement waits for a lock: its command timeout, or the busy wait of SQLite (ADR 029).</summary>
    private static TimeSpan Ordinary(EngineKind kind) =>
        kind == EngineKind.SqlServer ? TimeSpan.FromSeconds(OrdinarySeconds) : SqliteConnectionSettings.Default.MaximumBusyWait;

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_migration_waits_for_a_migration_elsewhere_longer_than_the_ordinary_command_timeout(EngineKind kind)
    {
        await using var database = await TestDatabase.CreatedAsync(kind);
        var engine = database.EngineWith(DatabaseEngine.DefaultMigrationTimeout, OrdinarySeconds);
        var waiter = new CommandTimeouts();
        Task<SchemaState> migrating;
        await using (var other = await HeldLock.TakeAsync(database, writing: true))
        {
            // On a thread of its own: Microsoft.Data.Sqlite runs synchronously, its wait for a busy database included.
            migrating = Task.Run(() => new SchemaMigrations(engine, new FixedClock(), waiter).MigrateAsync(Token), Token);
            await Task.WhenAny(waiter.LockAsked.Task, migrating).WaitAsync(Rendezvous, Token);
            Assert.True(waiter.LockAsked.Task.IsCompleted, $"The migration ended before asking for the lock: {migrating.Exception}");
            await Task.Delay(Ordinary(kind) + Margin, Token);
            Assert.False(migrating.IsCompleted, $"The migration did not wait for the lock held elsewhere: {migrating.Exception}");
        }
        // Bounded by what the product guarantees, the delay of the migrations (ADR 041, round 3): a whole migration of a
        // test database on SQL Server, under the load of several runs, takes more than five minutes.
        var state = await migrating.WaitAsync(engine.MigrationTimeout, Token);
        Assert.Equal(SchemaStatus.UpToDate, state.Status);
        await using var context = database.Context();
        Assert.Equal(context.Database.GetMigrations(),
            (await context.Set<SchemaHistoryEntry>().ToListAsync(Token)).Select(entry => entry.MigrationId).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_command_of_the_migration_and_of_the_check_has_the_delay_of_the_migrations_and_an_ordinary_context_keeps_its_own(EngineKind kind)
    {
        await using var database = await TestDatabase.CreatedAsync(kind);
        // Counted in whole seconds, rounded up.
        var engine = database.EngineWith(TimeSpan.FromSeconds(1234.2));
        Assert.Equal(1235, engine.MigrationTimeoutSeconds);
        var migration = new CommandTimeouts();
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock(), migration).MigrateAsync(Token)).Status);
        var check = new CommandTimeouts();
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock(), check).CheckAsync(Token)).Status);
        foreach (var log in new[] { migration, check })
        {
            Assert.True(log.LockAsked.Task.IsCompleted);
            Assert.NotEmpty(log.OfCommands);
            Assert.All(log.OfCommands, timeout => Assert.Equal(1235, timeout));
            Assert.NotEmpty(log.OfConnections);
            Assert.All(log.OfConnections, timeout => Assert.Equal(1235, timeout));
        }
        var ordinary = new CommandTimeouts();
        await using (var context = engine.CreateContext(ordinary))
            Assert.NotEqual(0, await context.Set<SchemaHistoryEntry>().CountAsync(Token));
        var expected = kind == EngineKind.SqlServer ? (int)TestServer.CommandTimeout.TotalSeconds : SqliteConnectionSettings.CommandTimeoutSeconds;
        Assert.All(ordinary.OfCommands, timeout => Assert.Equal(expected, timeout));
        Assert.All(ordinary.OfConnections, timeout => Assert.Equal(expected, timeout));
        Assert.NotEmpty(ordinary.OfCommands);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lock_of_the_migrations_held_beyond_the_delay_of_the_migrations_is_refused_as_busy_or_as_left_where_it_outlives_its_holder(EngineKind kind)
    {
        await using var database = await TestDatabase.MigratedAsync(kind);
        var engine = database.EngineWith(TimeSpan.FromSeconds(2));
        // SQLite: the row of a migration that stopped without removing it; SQL Server: a session that keeps the lock
        // (SQL Server releases it with its session, a stopped process never leaves it).
        await using var other = await HeldLock.TakeAsync(database, writing: false);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bound.CancelAfter(Rendezvous);
        var watch = Stopwatch.StartNew();
        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => new SchemaMigrations(engine, new FixedClock()).CheckAsync(bound.Token));
        // SQL Server: the lock goes with the session that holds it, a new run succeeds once it ends. SQLite: the row stays
        // after its holder, a new run fails again; the refusal says so and is not transient (ADR 041, revision).
        Assert.Equal(kind == EngineKind.SqlServer ? SchemaCodes.DatabaseBusy : SchemaCodes.MigrationLockHeld, refusal.Code);
        Assert.Equal(kind == EngineKind.SqlServer, refusal.IsTransient);
        Assert.InRange(watch.Elapsed, engine.MigrationTimeout - TimeSpan.FromMilliseconds(100), Rendezvous);
        Assert.False(bound.IsCancellationRequested);
        AssertGivesTheDelayOnly(refusal, engine);
    }

    /// <summary>
    /// A live migration elsewhere holds the row of the lock and the write lock of SQLite, longer than the delay: the
    /// refusal is <c>BASE_OCCUPEE</c>, transient, never <c>VERROU_MIGRATIONS</c>, whose advice (remove the row) would let
    /// two migrations run at once (ADR 041, round 3).
    /// </summary>
    [Fact]
    public async Task On_sqlite_a_lock_held_by_a_migration_that_writes_is_refused_as_busy_not_as_left()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.Sqlite);
        var engine = database.EngineWith(TimeSpan.FromSeconds(2));
        await using var other = await HeldLock.TakeAsync(database, writing: true);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
        bound.CancelAfter(Rendezvous);

        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => Task.Run(() => new SchemaMigrations(engine, new FixedClock()).CheckAsync(bound.Token), Token));

        Assert.Equal((SchemaCodes.DatabaseBusy, true), (refusal.Code, refusal.IsTransient));
        Assert.False(bound.IsCancellationRequested);
        AssertGivesTheDelayOnly(refusal, engine);
    }

    /// <summary>
    /// The row of the lock, left before the check, is removed and taken again by another process during the wait (its
    /// holder is alive, the mark of the row changed), which then keeps it past the delay without writing:
    /// <c>BASE_OCCUPEE</c>, transient.
    /// </summary>
    [Fact]
    public async Task On_sqlite_a_lock_taken_again_during_the_wait_is_refused_as_busy_not_as_left()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.Sqlite);
        var engine = database.EngineWith(TimeSpan.FromSeconds(3));
        HeldLock? first = await HeldLock.TakeAsync(database, writing: false);
        HeldLock? second = null;
        var again = new OnFirstLockAttempt(async () =>
        {
            var released = first;
            first = null;
            await released.DisposeAsync();
            second = await HeldLock.TakeAsync(database, writing: false);
        });
        try
        {
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(Token);
            bound.CancelAfter(Rendezvous);
            var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => new SchemaMigrations(engine, new FixedClock(), again).CheckAsync(bound.Token));

            Assert.True(again.Done);
            Assert.Equal((SchemaCodes.DatabaseBusy, true), (refusal.Code, refusal.IsTransient));
            AssertGivesTheDelayOnly(refusal, engine);
        }
        finally
        {
            if (first is not null)
                await first.DisposeAsync();
            if (second is not null)
                await second.DisposeAsync();
        }
    }

    /// <summary>The refusal of the delay gives the delay only, never the failure of the engine, whose message names the server or the file (ADR 036).</summary>
    private static void AssertGivesTheDelayOnly(SchemaRefusedException refusal, DatabaseEngine engine)
    {
        var timeout = Assert.IsType<TimeoutException>(refusal.InnerException);
        Assert.Null(timeout.InnerException);
        Assert.Contains(engine.MigrationTimeout.ToString(), timeout.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runs <paramref name="once"/> after the first command that a context sends: for a context of the check of
    /// <c>SchemaMigrations</c>, the first step of its request of the lock of the migrations, once the catalogue read the mark
    /// of the lock (its commands are made on the connection, unseen by EF Core, <see cref="CommandTimeouts"/>).
    /// </summary>
    private sealed class OnFirstLockAttempt(Func<Task> once) : DbCommandInterceptor
    {
        private int done;

        public bool Done => Volatile.Read(ref done) == 1;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            await AfterAsync();
            return result;
        }

        public override async ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        {
            await AfterAsync();
            return result;
        }

        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            await AfterAsync();
            return result;
        }

        private async Task AfterAsync()
        {
            if (Interlocked.Exchange(ref done, 1) == 0)
                await once();
        }
    }

    /// <summary>
    /// The lock of the migrations taken by another process (a context of the ordinary engine). With <c>writing</c>, the
    /// other process also holds the write lock of SQLite as a migration does during its transactions (SQL Server needs
    /// nothing more: its lock is an application lock of the session). Without it, on SQLite, the connection is closed and the row of the lock
    /// stays, as a stopped migration leaves it. Disposing commits and releases the lock.
    /// </summary>
    private sealed class HeldLock : IAsyncDisposable
    {
        private readonly AppDbContext context;
        private readonly IMigrationsDatabaseLock exclusive;
        private readonly IDbContextTransaction? transaction;

        private HeldLock(AppDbContext context, IMigrationsDatabaseLock exclusive, IDbContextTransaction? transaction)
        {
            this.context = context;
            this.exclusive = exclusive;
            this.transaction = transaction;
        }

        public static async Task<HeldLock> TakeAsync(TestDatabase database, bool writing)
        {
            var context = database.Context();
            await context.Database.OpenConnectionAsync(Token);
            var exclusive = await context.GetService<IHistoryRepository>().AcquireDatabaseLockAsync(Token);
            IDbContextTransaction? transaction = null;
            if (database.Kind == EngineKind.Sqlite && writing)
                transaction = await context.Database.BeginTransactionAsync(Token);
            else if (database.Kind == EngineKind.Sqlite)
                await context.Database.CloseConnectionAsync();
            return new HeldLock(context, exclusive, transaction);
        }

        public async ValueTask DisposeAsync()
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(CancellationToken.None);
                await transaction.DisposeAsync();
            }
            await exclusive.DisposeAsync();
            await context.DisposeAsync();
        }
    }
}
