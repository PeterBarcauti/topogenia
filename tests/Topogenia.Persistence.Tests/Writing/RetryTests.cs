// RetryTests.cs
// New attempts of the whole unit on a deadlock or a busy database: 3 attempts, 50 then 100 ms; real deadlock (spec 4.3-5).

using System.Collections.Concurrent;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Writing;

/// <summary>
/// A-2.16-10, A-5-09: a unit chosen as the victim of a deadlock (SQL Server 1205) or refused by a busy database (SQLite
/// `SQLITE_BUSY`) runs again whole, from its opening, at most three times, after 50 then 100 ms (1.x `BaseDeDonnees.cs:30`);
/// the attempts that failed leave nothing. The count and the waits are proven on both engines with failures simulated at
/// the commit; the real path is proven by a real deadlock on SQL Server here, and by a real busy file with the settings
/// of production on SQLite in `Sqlite.RealBusyTests`.
/// </summary>
public sealed class RetryTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The passing failure that each dialect really produces: SQLite reads `SQLITE_BUSY` and `SQLITE_LOCKED` as busy and
    /// never gives a deadlock, SQL Server reads 1205 as a deadlock and never gives a busy database
    /// (<c>ErrorTranslationTests</c>, the real deadlock and the real busy database below). The policy itself does not
    /// look at the engine: it retries every passing kind (<see cref="The_policy_retries_every_passing_kind_and_no_other"/>).
    /// </summary>
    public static TheoryData<EngineKind, StoreErrorKind> Transients =>
        new() { { EngineKind.Sqlite, StoreErrorKind.Busy }, { EngineKind.SqlServer, StoreErrorKind.Deadlock } };

    public static TheoryData<StoreErrorKind> Kinds => [.. Enum.GetValues<StoreErrorKind>()];

    private sealed class Attempts
    {
        public ConcurrentQueue<TimeSpan> Waits { get; } = new();

        public ConcurrentQueue<RetryAttempt> Retries { get; } = new();

        public int Runs;

        public RetryPolicy Policy() => RetryPolicy.Standard((delay, _) =>
        {
            Waits.Enqueue(delay);
            return Task.CompletedTask;
        }, Retries.Enqueue);
    }

    /// <summary>One structure operation (lock, load, new step, save) in a unit of the given session; counts its runs.</summary>
    private static Task<string> CreateStepAsync(StoreScenario scenario, StoreSession session, Attempts attempts) =>
        new UnitOfWork(session).RunAsync(async token =>
        {
            Interlocked.Increment(ref attempts.Runs);
            var unit = new UnitOfWork(session);
            var store = new WorkspaceStore(session);
            await unit.LockProjectAsync(scenario.ProjectId, token);
            var workspace = (await store.LoadAsync(scenario.ProjectId, WorkspaceContent.None, token))!;
            var context = scenario.Context();
            var step = StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 0, "Retried"));
            await store.SaveAsync(workspace.Commit(context), token);
            return step.Code;
        }, Token);

    private static StoreSession Session(StoreScenario scenario, StoreErrorKind kind, Attempts attempts, ScriptedFailures failures) =>
        new(scenario.Database.Engine, new ScriptedDialect(scenario.Database.Dialect, kind), attempts.Policy(), failures);

    [Theory]
    [MemberData(nameof(Transients))]
    public async Task Two_transient_failures_run_the_whole_unit_again_after_50_then_100_ms(EngineKind engine, StoreErrorKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(engine));
        var attempts = new Attempts();
        await using var session = Session(scenario, kind, attempts, new ScriptedFailures(commits: 2));

        var code = await CreateStepAsync(scenario, session, attempts);

        Assert.Equal(3, attempts.Runs);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100) }, attempts.Waits);
        Assert.Equal(new[] { (1, kind), (2, kind) }, attempts.Retries.Select(retry => (retry.Attempt, retry.Error.Kind)));
        var stored = await scenario.LoadAsync(WorkspaceContent.None);
        Assert.Equal(code, Assert.Single(stored.All<Step>()).Code);
        Assert.Equal(1, stored.Project.StructureVersion);
    }

    [Theory]
    [MemberData(nameof(Transients))]
    public async Task A_third_transient_failure_is_thrown_and_leaves_nothing_written(EngineKind engine, StoreErrorKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(engine));
        var attempts = new Attempts();
        await using var session = Session(scenario, kind, attempts, new ScriptedFailures(commits: 3));

        await Assert.ThrowsAsync<SimulatedEngineFailure>(() => CreateStepAsync(scenario, session, attempts));

        Assert.Equal((3, 2), (attempts.Runs, attempts.Waits.Count));
        var stored = await scenario.LoadAsync(WorkspaceContent.None);
        Assert.Empty(stored.All<Step>());
        Assert.Equal(0, stored.Project.StructureVersion);
    }

    [Theory]
    [MemberData(nameof(Transients))]
    public async Task A_transient_failure_after_the_commit_is_thrown_and_the_committed_unit_never_runs_again(EngineKind engine, StoreErrorKind kind)
    {
        // The connection closes after the commit of the engine: inside the clean-up of the transaction on SQL Server, when
        // the unit ends on SQLite (data B1 of round 4). Both are after work that is done.
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(engine));
        var attempts = new Attempts();
        await using var session = Session(scenario, kind, attempts, new ScriptedFailures(closingsAfterCommit: 1));

        await Assert.ThrowsAsync<SimulatedEngineFailure>(() => CreateStepAsync(scenario, session, attempts));

        Assert.Equal((1, 0), (attempts.Runs, attempts.Waits.Count));
        var stored = await scenario.LoadAsync(WorkspaceContent.None);
        Assert.Single(stored.All<Step>());
        Assert.Equal(1, stored.Project.StructureVersion);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void The_policy_retries_every_passing_kind_and_no_other(StoreErrorKind kind)
    {
        // The retry of the unit (RetryPolicy) runs again on IsTransient, whatever the engine that classified the failure.
        var passing = kind is StoreErrorKind.Deadlock or StoreErrorKind.Busy or StoreErrorKind.CreationRace;
        Assert.Equal(passing, new StoreError(kind, null).IsTransient);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_business_refusal_is_never_retried(EngineKind engine)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(engine));
        var attempts = new Attempts();
        await using var session = Session(scenario, StoreErrorKind.Deadlock, attempts, new ScriptedFailures());

        await Assert.ThrowsAsync<DomainException>(() => new UnitOfWork(session).RunAsync<bool>(_ =>
        {
            Interlocked.Increment(ref attempts.Runs);
            throw DomainException.Conflict(ErrorCodes.UniqueConflict);
        }, Token));

        Assert.Equal((1, 0), (attempts.Runs, attempts.Waits.Count));
    }

    [Theory]
    [MemberData(nameof(Transients))]
    public async Task A_loading_outside_a_unit_is_retried_whole_on_a_transient_failure(EngineKind engine, StoreErrorKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(engine));
        var attempts = new Attempts();
        await using var session = Session(scenario, kind, attempts, new ScriptedFailures(queries: 2));

        var workspace = await new WorkspaceStore(session).LoadAsync(scenario.ProjectId, WorkspaceContent.All, Token);

        Assert.Equal(scenario.ProjectId, workspace!.ProjectId);
        Assert.Equal(2, attempts.Retries.Count);
    }

    [Fact]
    public async Task On_sql_server_a_real_deadlock_is_retried_and_both_units_commit()
    {
        var first = await StoreScenario.CreateAsync(await databases.OfAsync(EngineKind.SqlServer));
        var second = await StoreScenario.CreateAsync(first.Database);
        var (firstLocked, secondLocked) = (new TaskCompletionSource(), new TaskCompletionSource());
        var retries = new ConcurrentQueue<RetryAttempt>();

        // Each unit locks its project, waits for the other to hold its own, then locks the other project: a deadlock.
        Task<bool> Crossed(StoreScenario mine, StoreScenario theirs, TaskCompletionSource held, TaskCompletionSource other) => Task.Run(async () =>
        {
            await using var session = first.Database.Session(RetryPolicy.Standard(retrying: retries.Enqueue));
            var unit = new UnitOfWork(session);
            return await unit.RunAsync(async token =>
            {
                await unit.LockProjectAsync(mine.ProjectId, token);
                held.TrySetResult();
                await other.Task.WaitAsync(Patience, token);
                await unit.LockProjectAsync(theirs.ProjectId, token);
                return true;
            }, Token);
        }, Token);
        await Task.WhenAll(Crossed(first, second, firstLocked, secondLocked), Crossed(second, first, secondLocked, firstLocked)).WaitAsync(Patience, Token);

        var retry = Assert.Single(retries);
        Assert.Equal((1, StoreErrorKind.Deadlock), (retry.Attempt, retry.Error.Kind));
        Assert.Equal(2, (await first.LoadAsync(WorkspaceContent.None)).Project.StructureVersion);
        Assert.Equal(2, (await second.LoadAsync(WorkspaceContent.None)).Project.StructureVersion);
    }
}
