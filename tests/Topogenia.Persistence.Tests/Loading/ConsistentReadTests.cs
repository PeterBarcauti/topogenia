// ConsistentReadTests.cs
// A loading reads one state of the database: an operation committed during it is never half seen, never waits for it, and is not seen (spec 3.4, faiblesse 10).

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Application.Ports;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Bi;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Loading;

/// <summary>
/// A-4-51, A-5-11 (faiblesse 10 of the 1.x: `EnLecture` opened no transaction, so the ten queries of a map could read
/// different states). Right after the loading has read the project, another request locks the project, adds a lane and
/// moves a step into it, and commits. The loading must give the state before that operation, whole: SQLite reads in a
/// deferred transaction whose snapshot is taken at its first read; SQL Server reads in a snapshot transaction (spec 3.4,
/// ADR 031), whose snapshot is taken at its first read too and which takes no shared lock: on both engines the operation
/// commits during the loading, without waiting for it, and the loading does not see it.
/// </summary>
public sealed class ConsistentReadTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    /// <summary>
    /// How long the loading pauses at most for the concurrent operation to commit: the pause ends as soon as it has
    /// committed, and the test checks that it did (a pause too short would let the loading go on before the write, and
    /// prove nothing). Long enough for an instance loaded by other runs.
    /// </summary>
    private static readonly TimeSpan Concurrency = TimeSpan.FromSeconds(60);

    /// <summary>How long a write may take while a loading runs: a read that held it would make it wait until the loading ends.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Runs an action once, when the first reader of the context has been read whole (the project row).</summary>
    private sealed class AfterFirstRead(Func<Task> action) : DbCommandInterceptor
    {
        private int reads;

        public override async ValueTask<InterceptionResult> DataReaderClosingAsync(DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
        {
            if (Interlocked.Increment(ref reads) == 1)
                await action();
            return result;
        }
    }

    /// <summary>A scenario with one more step on the first lane of the root map; the step and the state after it.</summary>
    private static async Task<(StoreScenario Scenario, Guid Step, Outcome Before)> PreparedAsync(TestDatabase database)
    {
        var scenario = await StoreScenario.CreateAsync(database);
        var step = Guid.Empty;
        var before = await scenario.RunAsync((workspace, context) =>
            step = StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 0, "Moved")).Id);
        return (scenario, step, before);
    }

    /// <summary>The structure operation of the other request: a new lane, and the step moved into it.</summary>
    private static Task<Outcome> MoveIntoNewLaneAsync(StoreScenario scenario, Guid step) =>
        scenario.RunAsync((workspace, context) =>
        {
            var lane = LaneOperations.Add(workspace, context, new AddLaneRequest(workspace.RootMap.Id, "New lane"));
            StepOperations.Update(workspace, context, new UpdateStepRequest(step) { LaneId = new(lane.Id) });
        }, scenario.Other);

    private static async Task<ProjectWorkspace> LoadAsync(StoreScenario scenario, params IInterceptor[] interceptors)
    {
        await using var session = scenario.Database.Session(null, interceptors);
        return (await new WorkspaceStore(session).LoadAsync(scenario.ProjectId, WorkspaceContent.All, Token))!;
    }

    /// <summary>The loading gave the state before the operation, whole; the database holds the state after it.</summary>
    private static async Task AssertBeforeLoadedAndAfterStoredAsync(StoreScenario scenario, Guid step, Outcome before, ProjectWorkspace loaded, Outcome after)
    {
        WorkspaceRecords.AssertSame(before.Workspace, loaded);
        Assert.Empty(StructureInvariants.Violations(loaded));
        var stored = await scenario.AssertStoredAsync(after);
        Assert.Equal((2, 2), (stored.Project.StructureVersion, stored.LanesOf(stored.RootMap.Id).Count));
        Assert.NotEqual(before.Workspace.LiveStep(step).LaneId, stored.LiveStep(step).LaneId);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_structure_operation_committed_during_a_loading_is_never_half_seen(EngineKind kind)
    {
        var (scenario, step, before) = await PreparedAsync(await databases.OfAsync(kind));
        Task<Outcome>? concurrent = null;
        var committedDuringThePause = false;
        var interceptor = new AfterFirstRead(async () =>
        {
            concurrent = MoveIntoNewLaneAsync(scenario, step);
            // The operation commits during this pause; the loading goes on after it.
            await Task.WhenAny(concurrent, Task.Delay(Concurrency, Token));
            committedDuringThePause = concurrent.IsCompletedSuccessfully;
        });

        var loaded = await LoadAsync(scenario, interceptor);
        var after = await concurrent!.WaitAsync(Patience, Token);

        // The race really happened: the loading read the rest of the project after the commit of the operation. (The
        // provider of SQLite runs its statements synchronously: there, the operation has committed before the pause.)
        Assert.True(committedDuringThePause, "The concurrent operation did not commit while the loading paused.");
        await AssertBeforeLoadedAndAfterStoredAsync(scenario, step, before, loaded, after);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_loading_neither_makes_a_concurrent_write_wait_nor_sees_it_once_committed(EngineKind kind)
    {
        var (scenario, step, before) = await PreparedAsync(await databases.OfAsync(kind));
        Outcome? during = null;
        // The write runs whole, and is awaited, inside the loading: a read that kept shared locks (serializable) would make
        // it wait until the loading ends, and the wait would end in a timeout.
        var interceptor = new AfterFirstRead(async () => during = await MoveIntoNewLaneAsync(scenario, step).WaitAsync(Patience, Token));

        var loaded = await LoadAsync(scenario, interceptor);

        Assert.NotNull(during);
        await AssertBeforeLoadedAndAfterStoredAsync(scenario, step, before, loaded, during);
    }

    [Fact]
    public async Task On_sql_server_the_application_role_loads_in_a_snapshot()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var (scenario, step, before) = await PreparedAsync(database);
        var user = RoleSession.NewUser("app");
        await using (var owner = database.Context())
        {
            await owner.Database.OpenConnectionAsync(Token);
            await SqlServerAccessProbe.CreateMemberAsync(owner.Database.GetDbConnection(), user, SqlServerAccess.ApplicationRole, Token);
        }
        var impersonation = new RoleImpersonation(user);
        Outcome? during = null;
        var interceptor = new AfterFirstRead(async () => during = await MoveIntoNewLaneAsync(scenario, step).WaitAsync(Patience, Token));

        // The loading itself runs as the application role: a snapshot transaction needs no right beyond reading the tables.
        var loaded = await LoadAsync(scenario, impersonation, interceptor);

        Assert.Equal([$"{SqlServerEngine.Schema}.{TableNames.SchemaHistory}|SELECT"], impersonation.HeldOnSchemaHistory);
        Assert.NotNull(during);
        await AssertBeforeLoadedAndAfterStoredAsync(scenario, step, before, loaded, during);
    }
}
