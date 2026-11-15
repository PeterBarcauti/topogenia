// RealBusyTests.cs
// A real busy SQLite file with the settings of production: wait of each attempt measured, retried unit, unit that fails after three attempts (spec 4.3-5/6).

using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// A-2.16-10, A-5-09, data M4: a file whose write lock another connection holds, opened with the engine as the
/// application opens it (no timeout of the tests in the connection string). Each attempt of a unit waits the busy timeout
/// of SQLite (5 s) and at most the documented 6.15 s (ADR 029: 5 s, then the 1 s of the command timeout of the provider
/// and one pause of 150 ms), never the 30 s of the provider; a lock held for good fails the unit as busy after its three
/// attempts. The values are those of the ADR, fixed here: the test checks the product against them, never against its own
/// constants. What SQLite guarantees is proven exactly: the delay set on each connection of the unit (1 s), the number and
/// the kind of the attempts, their pauses (50 and 100 ms). The 5 s of SQLite are the sum of the sleeps that its busy
/// handler asks for (<see cref="BusySleeps"/>), not a time read on a clock: on a loaded machine each sleep ends later,
/// when its thread wakes up, and the threads of the suite wait for a free thread of the pool between two statements. So
/// the wait of an attempt is measured where SQLite waits, from the opening of its connection to its closing after the
/// refusal of `BEGIN IMMEDIATE` (one thread, no wait for the pool). It is at least the busy timeout of each attempt, and
/// at most <see cref="LoadFactor"/> times the documented wait of each (ADR 040, revision of 25/10/2026): the same sleeps
/// slept at the same time on a thread of the test are named in the failure, to tell a stretched sleep from a wait for a
/// thread. The class runs alone, after the other tests of its process. The waits on the clock of the test keep a wide
/// bound, below the 30 s per statement of the provider before ADR 029 (ADR 040). SQLite only: the real deadlock of SQL
/// Server is proven in `Writing.RetryTests`.
/// </summary>
[Collection(nameof(RealBusyCollection))]
public sealed class RealBusyTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(150);

    /// <summary>The busy timeout of SQLite documented by ADR 029.</summary>
    private const int BusyTimeoutMilliseconds = 5000;

    private static readonly TimeSpan BusyTimeout = TimeSpan.FromMilliseconds(BusyTimeoutMilliseconds);

    /// <summary>The command timeout of the provider documented by ADR 029, in seconds.</summary>
    private const int CommandTimeoutSeconds = 1;

    /// <summary>What the provider may add to one busy wait of SQLite (ADR 029): its command timeout and one pause of 150 ms.</summary>
    private static readonly TimeSpan ProviderPart = TimeSpan.FromSeconds(CommandTimeoutSeconds) + TimeSpan.FromMilliseconds(150);

    /// <summary>The documented wait of one busy attempt (ADR 029): the busy timeout of SQLite, then the part of the provider (6.15 s).</summary>
    private static readonly TimeSpan DocumentedWait = BusyTimeout + ProviderPart;

    /// <summary>
    /// How many times its documented wait a busy attempt may last on a machine loaded by other runs (ADR 040, revision of
    /// 25/10/2026): 2.46 times measured at worst, and 4 times stays below the 30 s per statement before ADR 029.
    /// </summary>
    private const int LoadFactor = 4;

    /// <summary>
    /// The command timeout of the provider by default, the one before ADR 029: a busy statement then waited at least this
    /// long (34 s measured). A measured wait stays below it, however loaded the machine.
    /// </summary>
    private static readonly TimeSpan ProviderDefault = TimeSpan.FromSeconds(30);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The attempts of a unit as they happen: their number, failure and wait, and when each failure came.</summary>
    private sealed class Attempts
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();

        public ConcurrentQueue<(RetryAttempt Attempt, TimeSpan At)> Retries { get; } = new();

        public TaskCompletionSource<RetryAttempt> First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TimeSpan Elapsed => clock.Elapsed;

        public RetryPolicy Policy() => RetryPolicy.Standard(retrying: attempt =>
        {
            Retries.Enqueue((attempt, clock.Elapsed));
            First.TrySetResult(attempt);
        });
    }

    /// <summary>
    /// The connections of a unit, one per attempt: how long each stayed open, on the thread that opened it, and the command
    /// timeout of the provider on it when it closed. An attempt refused busy opens, waits in `BEGIN IMMEDIATE`, fails and
    /// closes on one thread: its time open is the wait of SQLite and of the provider, without the waits for the pool.
    /// </summary>
    private sealed class Connections : DbConnectionInterceptor
    {
        private readonly ConcurrentDictionary<DbConnection, long> opened = new();

        public ConcurrentQueue<(TimeSpan Open, int CommandTimeout)> Closed { get; } = new();

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => opened[connection] = Stopwatch.GetTimestamp();

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            ConnectionOpened(connection, eventData);
            return Task.CompletedTask;
        }

        public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            if (opened.TryRemove(connection, out var start))
                Closed.Enqueue((Stopwatch.GetElapsedTime(start), ((SqliteConnection)connection).DefaultTimeout));
            return result;
        }

        public override ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
            ValueTask.FromResult(ConnectionClosing(connection, eventData, result));
    }

    /// <summary>
    /// The sleeps of the busy handler of SQLite on Windows (`sqliteDefaultBusyCallback`) for a busy timeout: 1, 2, 5, 10,
    /// 15, 20, 25, 25, 25, 50, 50 ms, then 100 ms, the last one cut so that they sum to the timeout (59 sleeps for 5 s).
    /// </summary>
    internal static IReadOnlyList<int> BusySleeps(int timeoutMilliseconds)
    {
        int[] delays = [1, 2, 5, 10, 15, 20, 25, 25, 25, 50, 50, 100];
        var sleeps = new List<int>();
        for (var prior = 0; prior < timeoutMilliseconds; prior += sleeps[^1])
            sleeps.Add(Math.Min(delays[Math.Min(sleeps.Count, delays.Length - 1)], timeoutMilliseconds - prior));
        return sleeps;
    }

    /// <summary>How long the sleeps of <paramref name="waits"/> busy waits of SQLite last on this machine now, slept on a thread of their own.</summary>
    private static Task<TimeSpan> SleptAsync(int waits) => Task.Factory.StartNew(() =>
    {
        var clock = Stopwatch.StartNew();
        for (var wait = 0; wait < waits; wait++)
        {
            foreach (var sleep in BusySleeps(BusyTimeoutMilliseconds))
                Thread.Sleep(sleep);
        }
        return clock.Elapsed;
    }, Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>
    /// The time open of <paramref name="attempts"/> busy attempts: at least their busy timeouts, at most
    /// <see cref="LoadFactor"/> times their documented waits; the failure names the sleeps of SQLite slept meanwhile.
    /// </summary>
    private static async Task AssertBusyWaitAsync(TimeSpan open, int attempts, Task<TimeSpan> slept)
    {
        var sleeps = await slept.WaitAsync(Patience, Token);
        Assert.True(open >= attempts * BusyTimeout, $"{open} < {attempts * BusyTimeout}");
        var bound = LoadFactor * attempts * DocumentedWait;
        Assert.True(open <= bound, $"{open} > {bound}, {LoadFactor} times the documented wait; the same sleeps slept meanwhile: {sleeps}");
    }

    /// <summary>A migrated file with the settings of production and a project; the connection string names no timeout.</summary>
    private static async Task<(TestDatabase Database, StoreScenario Scenario)> CreateAsync()
    {
        var database = TestDatabase.Create(EngineKind.Sqlite);
        try
        {
            await new SchemaMigrations(database.Engine, new FixedClock()).MigrateAsync(Token);
            return (database, await StoreScenario.CreateAsync(database));
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    /// <summary>Takes the write lock of the file on another connection, by a write in a transaction kept open.</summary>
    private static async Task<IDbContextTransaction> HoldWriteLockAsync(AppDbContext blocker, StoreScenario scenario)
    {
        var held = await blocker.Database.BeginTransactionAsync(Token);
        await blocker.Set<Project>().Where(project => project.Id == scenario.ProjectId)
            .ExecuteUpdateAsync(row => row.SetProperty(project => project.Name, project => project.Name), Token);
        return held;
    }

    /// <summary>One structure operation (lock, load, new step, save) in a unit of the session.</summary>
    private static Task<string> CreateStepAsync(StoreScenario scenario, StoreSession session) =>
        new UnitOfWork(session).RunAsync(async token =>
        {
            var unit = new UnitOfWork(session);
            var store = new WorkspaceStore(session);
            await unit.LockProjectAsync(scenario.ProjectId, token);
            var workspace = (await store.LoadAsync(scenario.ProjectId, WorkspaceContent.None, token))!;
            var context = scenario.Context();
            var step = StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 0, "Retried"));
            await store.SaveAsync(workspace.Commit(context), token);
            return step.Code;
        }, Token);

    [Fact]
    public void The_sleeps_of_the_busy_handler_of_sqlite_sum_to_its_busy_timeout()
    {
        var sleeps = BusySleeps(BusyTimeoutMilliseconds);
        Assert.Equal(BusyTimeoutMilliseconds, sleeps.Sum());
        Assert.Equal(59, sleeps.Count);
        Assert.Equal([1, 2, 5, 10, 15, 20, 25, 25, 25, 50, 50, 100], sleeps.Take(12));
        Assert.Equal(72, sleeps[^1]);
    }

    [Fact]
    public async Task On_sqlite_a_real_busy_database_is_retried_once_its_write_lock_is_released()
    {
        var (database, scenario) = await CreateAsync();
        await using var _ = database;
        await using var blocker = database.Context();
        await using var held = await HoldWriteLockAsync(blocker, scenario);
        var attempts = new Attempts();
        var connections = new Connections();

        await using var session = database.Session(attempts.Policy(), connections);
        var slept = SleptAsync(1);
        var unit = CreateStepAsync(scenario, session);
        var retry = await attempts.First.Task.WaitAsync(Patience, Token);
        await held.RollbackAsync(Token);
        var code = await unit.WaitAsync(Patience, Token);

        Assert.Equal((1, StoreErrorKind.Busy), (retry.Attempt, retry.Error.Kind));
        var (_, failedAt) = Assert.Single(attempts.Retries);
        var (open, timeout) = connections.Closed.First();
        Assert.All(connections.Closed, connection => Assert.Equal(CommandTimeoutSeconds, connection.CommandTimeout));
        Assert.Equal(CommandTimeoutSeconds, timeout);
        await AssertBusyWaitAsync(open, 1, slept);
        Assert.InRange(failedAt, BusyTimeout, ProviderDefault);
        Assert.Equal(code, Assert.Single((await scenario.LoadAsync(WorkspaceContent.None)).All<Step>()).Code);
    }

    [Fact]
    public async Task A_write_lock_held_for_good_fails_the_unit_as_busy_after_three_attempts_within_the_documented_wait()
    {
        var (database, scenario) = await CreateAsync();
        await using var _ = database;
        await using var blocker = database.Context();
        await using var held = await HoldWriteLockAsync(blocker, scenario);
        var attempts = new Attempts();
        var connections = new Connections();
        var slept = SleptAsync(3);

        await using (var session = database.Session(attempts.Policy(), connections))
        {
            var failure = await Assert.ThrowsAsync<SqliteException>(() => CreateStepAsync(scenario, session).WaitAsync(Patience, Token));
            Assert.Equal(SqliteDialect.Busy, failure.SqliteErrorCode);
        }
        var total = attempts.Elapsed;
        await held.RollbackAsync(Token);

        Assert.Equal(
            new[] { (1, StoreErrorKind.Busy, TimeSpan.FromMilliseconds(50)), (2, StoreErrorKind.Busy, TimeSpan.FromMilliseconds(100)) },
            attempts.Retries.Select(retry => (retry.Attempt.Attempt, retry.Attempt.Error.Kind, retry.Attempt.Delay)));
        Assert.Equal(3, connections.Closed.Count);
        Assert.All(connections.Closed, connection => Assert.Equal(CommandTimeoutSeconds, connection.CommandTimeout));
        Assert.All(connections.Closed, connection => Assert.True(connection.Open >= BusyTimeout, $"{connection.Open} < {BusyTimeout}"));
        var open = TimeSpan.FromTicks(connections.Closed.Sum(connection => connection.Open.Ticks));
        await AssertBusyWaitAsync(open, 3, slept);
        var waits = TimeSpan.FromMilliseconds(150);
        Assert.InRange(total, 3 * BusyTimeout + waits, 3 * ProviderDefault + waits);
        Assert.Empty((await scenario.LoadAsync(WorkspaceContent.None)).All<Step>());
    }
}

/// <summary>The tests that measure real waits of SQLite: run after the others of the process, alone (ADR 040).</summary>
[CollectionDefinition(nameof(RealBusyCollection), DisableParallelization = true)]
public sealed class RealBusyCollection;
