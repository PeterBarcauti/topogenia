// VersionConflictTests.cs
// A change set computed on records changed or removed since its loading is refused whole with CONFLIT_VERSION (D06, A-2.16-06, A-2.16-09).

using Topogenia.Domain.Errors;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Writing;

/// <summary>
/// Two changes of one object: the one written on a version read before the other is refused cleanly (409), never a lost
/// write (A-2.16-09). The details give the author and the time of the change that blocks (A-2.16-06: the host formats
/// them). Nothing of the refused change set is written: the unit rolls it back.
/// </summary>
public sealed class VersionConflictTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(StoreScenario Scenario, Guid Step)> ScenarioWithAStepAsync(EngineKind kind)
    {
        var scenario = await StoreScenario.CreateAsync(await databases.OfAsync(kind));
        var step = Guid.Empty;
        await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            step = StepOperations.Create(workspace, context, new CreateStepRequest(map, workspace.LanesOf(map)[0].Id, 0, "Receive")).Id;
            LaneOperations.Add(workspace, context, new AddLaneRequest(map, "Second lane"));
        });
        return (scenario, step);
    }

    /// <summary>Writes, in a unit of its own, what a rule of the domain does on a workspace loaded before.</summary>
    private static async Task<DomainException> RefusedAsync(StoreScenario scenario, ProjectWorkspace stale, Action<ProjectWorkspace, OperationContext> operation)
    {
        await using var session = scenario.Database.Session();
        var unit = new UnitOfWork(session);
        var store = new WorkspaceStore(session);
        return await Assert.ThrowsAsync<DomainException>(() => unit.RunAsync(async token =>
        {
            await unit.LockProjectAsync(scenario.ProjectId, token);
            var context = scenario.Context();
            operation(stale, context);
            await store.SaveAsync(stale.Commit(context), token);
            return true;
        }, Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_record_changed_since_the_loading_is_refused_with_the_author_and_time_of_the_change(EngineKind kind)
    {
        var (scenario, step) = await ScenarioWithAStepAsync(kind);
        var stale = await scenario.LoadAsync();
        var other = await scenario.RunAsync((workspace, context) =>
            StepOperations.Update(workspace, context, new UpdateStepRequest(step) { Label = new FieldChange<string?>("Renamed by the other") }), scenario.Other);

        var refusal = await RefusedAsync(scenario, stale, (workspace, context) =>
        {
            StepOperations.Update(workspace, context, new UpdateStepRequest(step) { Label = new FieldChange<string?>("Renamed late") });
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 3, "Never kept"));
        });

        Assert.Equal((ErrorCodes.VersionConflict, RefusalKind.Conflict), (refusal.Code, refusal.Kind));
        Assert.Equal("step", refusal.Details[StoreRefusals.ObjectDetail]);
        Assert.Equal(scenario.Other.Id, refusal.Details[StoreRefusals.UpdatedByDetail]);
        Assert.Equal(other.Workspace.LiveStep(step).UpdatedAt, refusal.Details[StoreRefusals.UpdatedAtDetail]);
        var stored = await scenario.AssertStoredAsync(other);
        Assert.Equal("Renamed by the other", stored.LiveStep(step).Label);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_record_only_touched_since_the_loading_still_refuses_a_change_written_on_its_old_version(EngineKind kind)
    {
        var (scenario, step) = await ScenarioWithAStepAsync(kind);
        var stale = await scenario.LoadAsync();
        // A role set on the step touches it: its version moves on without any change of its fields (spec 4.7).
        var other = await scenario.RunAsync((workspace, context) =>
            RaciRules.SetRole(workspace, context, new SetRaciRequest(step, workspace.LanesOf(workspace.RootMap.Id)[1].Id, "C")), scenario.Other);

        var refusal = await RefusedAsync(scenario, stale, (workspace, context) =>
            StepOperations.Update(workspace, context, new UpdateStepRequest(step) { Label = new FieldChange<string?>("Renamed late") }));

        Assert.Equal(ErrorCodes.VersionConflict, refusal.Code);
        await scenario.AssertStoredAsync(other);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_record_removed_on_a_version_read_before_another_change_is_refused(EngineKind kind)
    {
        var (scenario, step) = await ScenarioWithAStepAsync(kind);
        await scenario.RunAsync((workspace, context) =>
            RaciRules.SetRole(workspace, context, new SetRaciRequest(step, workspace.LanesOf(workspace.RootMap.Id)[1].Id, "C")));
        var stale = await scenario.LoadAsync();
        var other = await scenario.RunAsync((workspace, context) =>
            RaciRules.SetRole(workspace, context, new SetRaciRequest(step, workspace.LanesOf(workspace.RootMap.Id)[1].Id, "I")), scenario.Other);

        // Only the removal, without the touch of the step that the rule adds: the delete itself must hold the version read.
        var refusal = await RefusedAsync(scenario, stale, (workspace, _) => workspace.Remove(workspace.All<StepRaci>().Single()));

        Assert.Equal(ErrorCodes.VersionConflict, refusal.Code);
        var stored = await scenario.AssertStoredAsync(other);
        Assert.Equal(RaciRole.I, Assert.Single(stored.All<StepRaci>()).Role);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_record_without_version_changed_since_the_loading_is_refused(EngineKind kind)
    {
        var (scenario, _) = await ScenarioWithAStepAsync(kind);
        var stale = await scenario.LoadAsync();
        var other = await scenario.RunAsync((workspace, context) =>
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 5, "Other")),
            scenario.Other);

        // The counter of the steps has no version: its row is compared with the one the change set started from.
        var refusal = await RefusedAsync(scenario, stale, (workspace, context) =>
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 7, "Late")));

        Assert.Equal((ErrorCodes.VersionConflict, "counter"), (refusal.Code, refusal.Details[StoreRefusals.ObjectDetail]));
        await scenario.AssertStoredAsync(other);
    }
}
