// UnitOfWorkTests.cs
// The unit of work: the portable project lock (structure_version + 1), real concurrency on both engines, rollback without commit (spec 4.7).

using System.Collections.Concurrent;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Writing;

/// <summary>
/// Spec 4.7, A-2.16-08, A-2.16-09, A-5-16: before any structure operation the unit moves the structure version of the
/// project on by one, in its transaction; two structure operations on one project never interleave, on SQL Server (the
/// write lock of the row of the project) as on SQLite (the write lock of the database), without any hint of an engine.
/// </summary>
public sealed class UnitOfWorkTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Locking_moves_the_structure_version_on_by_one_in_the_transaction_and_returns_it(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        await using var session = scenario.Database.Session();
        var unit = new UnitOfWork(session);

        await unit.BeginAsync(Token);
        var first = await unit.LockProjectAsync(scenario.ProjectId, Token);
        var second = await unit.LockProjectAsync(scenario.ProjectId, Token);
        await unit.CommitAsync(Token);

        Assert.Equal((1, 2), (first, second));
        Assert.Equal(2, (await scenario.LoadAsync(WorkspaceContent.None)).Project.StructureVersion);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Locking_a_project_that_does_not_exist_is_refused_as_not_found(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        await using var session = scenario.Database.Session();
        var unit = new UnitOfWork(session);
        await unit.BeginAsync(Token);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => unit.LockProjectAsync(Guid.CreateVersion7(), Token));

        Assert.Equal((ErrorCodes.NotFound, RefusalKind.NotFound), (refusal.Code, refusal.Kind));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_unit_disposed_without_its_commit_leaves_nothing_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        await using (var session = scenario.Database.Session())
        {
            var unit = new UnitOfWork(session);
            var store = new WorkspaceStore(session);
            await unit.BeginAsync(Token);
            await unit.LockProjectAsync(scenario.ProjectId, Token);
            var workspace = (await store.LoadAsync(scenario.ProjectId, WorkspaceContent.None, Token))!;
            var context = scenario.Context();
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 0, "Never kept"));
            await store.SaveAsync(workspace.Commit(context), Token);
            await unit.DisposeAsync();
        }

        var stored = await scenario.LoadAsync();
        Assert.Empty(stored.All<Step>());
        Assert.Equal(0, stored.Project.StructureVersion);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_change_set_is_written_only_inside_a_unit_of_work(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        await using var session = scenario.Database.Session();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new WorkspaceStore(session).SaveAsync(new ChangeSet([]), Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new UnitOfWork(session).LockProjectAsync(scenario.ProjectId, Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Structure_operations_on_one_project_run_at_the_same_time_never_interleave(EngineKind kind)
    {
        const int operations = 4;
        var scenario = await ScenarioAsync(kind);
        var sections = new ConcurrentBag<(DateTime Start, DateTime End)>();
        var start = new TaskCompletionSource();

        var runs = Enumerable.Range(0, operations).Select(i => Task.Run(async () =>
        {
            await start.Task;
            await using var session = scenario.Database.Session();
            var unit = new UnitOfWork(session);
            var store = new WorkspaceStore(session);
            return await unit.RunAsync(async token =>
            {
                var locked = await unit.LockProjectAsync(scenario.ProjectId, token);
                var entered = DateTime.UtcNow;
                var workspace = (await store.LoadAsync(scenario.ProjectId, WorkspaceContent.None, token))!;
                // A long operation: another one that did not wait for the lock would read the same counter meanwhile.
                await Task.Delay(150, token);
                var context = scenario.Context();
                var map = workspace.RootMap.Id;
                var step = StepOperations.Create(workspace, context, new CreateStepRequest(map, workspace.LanesOf(map)[0].Id, i, $"Concurrent {i}"));
                await store.SaveAsync(workspace.Commit(context), token);
                sections.Add((entered, DateTime.UtcNow));
                return (locked, step.Code);
            }, Token);
        }, Token)).ToList();
        start.SetResult();
        var results = await Task.WhenAll(runs);

        Assert.Equal(Enumerable.Range(1, operations), results.Select(result => result.locked).Order());
        Assert.Equal(operations, results.Select(result => result.Code).Distinct().Count());
        var ordered = sections.OrderBy(section => section.Start).ToList();
        Assert.All(ordered.Zip(ordered.Skip(1)), pair => Assert.True(pair.First.End <= pair.Second.Start, "Two structure operations interleaved."));
        var stored = await scenario.LoadAsync(WorkspaceContent.None);
        Assert.Equal((operations, operations), (stored.Project.StructureVersion, stored.All<ProjectCounter>().Single(counter => counter.Counter == CounterKind.Step).LastValue));
    }
}
