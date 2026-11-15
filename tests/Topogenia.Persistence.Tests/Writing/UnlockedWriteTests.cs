// UnlockedWriteTests.cs
// A record without version is modified or removed only under the lock of its project: otherwise the change set is refused before any write (ADR 030).

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Writing;

/// <summary>
/// The statement that writes a record without version (here the counter of the step codes) has no condition, and the
/// record is read back before under row versioning in SQL Server: without the lock of its project, a concurrent unit
/// could change it in between and one write would be lost. The contract of ADR 030 is enforced on both engines: a unit
/// that holds no lock, or the lock of another project only, is refused with <see cref="UnlockedWriteException"/> and
/// writes nothing; under the lock of the project the change set is written.
/// </summary>
public sealed class UnlockedWriteTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The change set of a new step: the step created and the counter of the step codes moved on.</summary>
    private static async Task<ChangeSet> NewStepAsync(StoreScenario scenario)
    {
        var outcome = await scenario.PrepareAsync((workspace, context) =>
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.FirstLane(), 0, "Counted")));
        Assert.Contains(outcome.Changes.Changes, change => change.Kind == ChangeKind.Modified && change.Before is ProjectCounter);
        return outcome.Changes;
    }

    /// <summary>Writes the change set in a unit that locks <paramref name="locked"/> first.</summary>
    private static async Task WriteLockingAsync(TestDatabase database, Guid locked, ChangeSet changes)
    {
        await using var session = database.Session();
        var unit = new UnitOfWork(session);
        await unit.RunAsync(async token =>
        {
            await unit.LockProjectAsync(locked, token);
            await new WorkspaceStore(session).SaveAsync(changes, token);
            return true;
        }, Token);
    }

    private static async Task AssertNothingWrittenAsync(StoreScenario scenario)
    {
        var stored = await scenario.LoadAsync(WorkspaceContent.None);
        Assert.Empty(stored.All<Step>());
        Assert.Equal(0, stored.All<ProjectCounter>().Single(counter => counter.Counter == CounterKind.Step).LastValue);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_record_without_version_written_without_any_lock_is_refused_and_nothing_is_written(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var changes = await NewStepAsync(scenario);

        var refusal = await Assert.ThrowsAsync<UnlockedWriteException>(() => scenario.WriteAsync(changes));

        Assert.Contains("counter", refusal.Message, StringComparison.Ordinal);
        await AssertNothingWrittenAsync(scenario);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_record_without_version_written_under_the_lock_of_another_project_is_refused_and_nothing_is_written(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var other = await StoreScenario.CreateAsync(database);
        var changes = await NewStepAsync(scenario);

        await Assert.ThrowsAsync<UnlockedWriteException>(() => WriteLockingAsync(database, other.ProjectId, changes));

        await AssertNothingWrittenAsync(scenario);
        // The lock taken on the other project is rolled back with the unit.
        Assert.Equal(0, (await other.LoadAsync(WorkspaceContent.None)).Project.StructureVersion);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_record_without_version_written_under_the_lock_of_its_project_is_written(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var scenario = await StoreScenario.CreateAsync(database);
        var changes = await NewStepAsync(scenario);

        await WriteLockingAsync(database, scenario.ProjectId, changes);

        var stored = await scenario.LoadAsync(WorkspaceContent.None);
        Assert.Single(stored.All<Step>());
        Assert.Equal(1, stored.All<ProjectCounter>().Single(counter => counter.Counter == CounterKind.Step).LastValue);
    }
}
