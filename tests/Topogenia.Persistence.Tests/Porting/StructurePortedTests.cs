// StructurePortedTests.cs
// Integration tests of the 1.x `StructureTests.cs` ported to the persistence: codes, moves through the parking, versions, flows, layout.

using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Layout;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Porting;

/// <summary>
/// The 1.x tests (`api.Tests/Integration/StructureTests.cs`) that ran on a real database, ported to the unit of work and the
/// workspace store on both engines (the table of `docs/portage-tests.md`, persistence part A). The HTTP part (status codes,
/// `If-Match`, `VERSION_INVALIDE` of an unreadable version) is ported in `Host.Tests` (phase 4).
/// </summary>
public sealed class StructurePortedTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    /// <summary>T-STRU-24: ten creations at once in one cell all succeed.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Ten_creations_in_one_cell_at_the_same_time_all_succeed_with_distinct_columns_and_codes(EngineKind kind)
    {
        const int operations = 10;
        var scenario = await ScenarioAsync(kind);

        var attempts = await Concurrently.RunAsync(operations, index => scenario.RunAsync((workspace, context) =>
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.FirstLane(), 0, $"Parallel {index}"))));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        var steps = (await scenario.LoadAsync()).All<Step>().ToList();
        Assert.Equal(Enumerable.Range(0, operations), steps.Select(step => step.Column).Order());
        Assert.Equal(operations, steps.Select(step => step.Code).Distinct().Count());
        Assert.Equal(operations, Assert.Single((await scenario.LoadAsync()).All<ProjectCounter>(), counter => counter.Counter == CounterKind.Step).LastValue);
    }

    /// <summary>T-STRU-13.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Step_codes_are_sequential_kept_on_a_change_and_never_given_again_after_a_deletion(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        await scenario.RunAsync((workspace, context) => StepOperations.Update(workspace, context,
            new UpdateStepRequest(steps[1]) { Label = new("Sales extraction"), Column = new(5) }));
        var changed = (await scenario.LoadAsync()).LiveStep(steps[1]);
        await scenario.RunAsync((workspace, context) => MacroStepDeletion.DeleteStep(workspace, context, steps[1]));

        var created = await scenario.RunAsync((workspace, context) =>
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.FirstLane(), 1, "New")));

        var stored = await scenario.AssertStoredAsync(created);
        var code = stored.Project.Code;
        Assert.Equal(new[] { $"{code}-E001", $"{code}-E002" }, steps.Select(id => stored.Step(id)!.Code));
        Assert.Equal(("Sales extraction", 5), (changed.Label, changed.Column));
        Assert.Equal($"{code}-E003", Assert.Single(stored.StepsOf(stored.RootMap.Id), step => step.Label == "New").Code);
    }

    /// <summary>T-STRU-19.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_moved_left_then_right_in_its_lane_shifts_its_neighbours_without_blocking_itself(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (a, b, c) = (await scenario.LinkedStepsAsync(3)) switch { var ids => (ids[0], ids[1], ids[2]) };

        var left = await scenario.RunAsync((workspace, context) => StepOperations.Update(workspace, context, new UpdateStepRequest(c) { Column = new(0) }));
        var afterLeft = await scenario.AssertStoredAsync(left);
        var right = await scenario.RunAsync((workspace, context) => StepOperations.Update(workspace, context, new UpdateStepRequest(c) { Column = new(2) }));

        var stored = await scenario.AssertStoredAsync(right);
        Assert.Equal((1, 2, 0), (afterLeft.LiveStep(a).Column, afterLeft.LiveStep(b).Column, afterLeft.LiveStep(c).Column));
        Assert.Equal((1, 3, 2), (stored.LiveStep(a).Column, stored.LiveStep(b).Column, stored.LiveStep(c).Column));
    }

    /// <summary>T-STRU-21 (part of the database: the stale version).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_changed_since_its_reading_is_refused_with_the_author_and_keeps_the_first_change(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];
        var stale = await scenario.PrepareAsync((workspace, context) =>
            StepOperations.Update(workspace, context, new UpdateStepRequest(step) { Label = new("Facilitator") }));
        await scenario.RunAsync((workspace, context) =>
            StepOperations.Update(workspace, context, new UpdateStepRequest(step) { Label = new("Admin") }), scenario.Other);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => scenario.WriteAsync(stale.Changes));

        AssertConflict(refusal, "step", scenario.Other.Id);
        var stored = (await scenario.LoadAsync()).LiveStep(step);
        Assert.Equal(("Admin", 2), (stored.Label, stored.Version));
    }

    /// <summary>T-STRU-06 (part of the database).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_changed_since_its_reading_is_refused_with_the_author_and_the_time_of_the_change(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var lane = (await scenario.LoadAsync()).FirstLane();
        var stale = await scenario.PrepareAsync((workspace, context) =>
            LaneOperations.Update(workspace, context, new UpdateLaneRequest(lane) { Name = new("Too late") }));
        var first = await scenario.RunAsync((workspace, context) =>
            LaneOperations.Update(workspace, context, new UpdateLaneRequest(lane) { Name = new("By the admin") }), scenario.Other);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => scenario.WriteAsync(stale.Changes));

        AssertConflict(refusal, "lane", scenario.Other.Id);
        Assert.Equal(first.Workspace.Lane(lane)!.UpdatedAt, refusal.Details[StoreRefusals.UpdatedAtDetail]);
        Assert.Equal("By the admin", (await scenario.LoadAsync()).Lane(lane)!.Name);
    }

    /// <summary>T-STRU-26.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_duplicate_flow_is_refused_and_nothing_written_while_the_reverse_flow_is_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (x, y) = (await scenario.LinkedStepsAsync(2)) switch { var ids => (ids[0], ids[1]) };

        var duplicate = await Assert.ThrowsAsync<DomainException>(() => scenario.RunAsync((workspace, context) =>
            FlowRules.Create(workspace, context, new CreateFlowRequest(workspace.RootMap.Id, x, y, null))));
        var reverse = await scenario.RunAsync((workspace, context) =>
            FlowRules.Create(workspace, context, new CreateFlowRequest(workspace.RootMap.Id, y, x, "back")));
        var loop = await Assert.ThrowsAsync<DomainException>(() => scenario.RunAsync((workspace, context) =>
            FlowRules.Create(workspace, context, new CreateFlowRequest(workspace.RootMap.Id, x, x, null))));

        Assert.Equal((ErrorCodes.DuplicateFlow, ErrorCodes.ReflexiveFlow), (duplicate.Code, loop.Code));
        var stored = await scenario.AssertStoredAsync(reverse);
        Assert.Equal(2, stored.FlowsOf(stored.RootMap.Id).Count);
    }

    /// <summary>T-STRU-30.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_layout_that_exchanges_two_steps_and_reorders_the_lanes_is_written_in_one_change_set(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (x, y) = (await scenario.LinkedStepsAsync(2)) switch { var ids => (ids[0], ids[1]) };
        var b = Guid.Empty;
        await scenario.RunAsync((workspace, context) => b = LaneOperations.Add(workspace, context, new AddLaneRequest(workspace.RootMap.Id, "B")).Id);

        var layout = await scenario.RunAsync((workspace, _) =>
        {
            var a = workspace.FirstLane();
            StepLayout.Apply(workspace, new LayoutRequest(workspace.RootMap.Id, [b, a], [new StepPlacement(x, a, 1), new StepPlacement(y, a, 0)]));
        });

        var stored = await scenario.AssertStoredAsync(layout);
        Assert.Equal(new[] { "B", "Acteur" }, stored.LanesOf(stored.RootMap.Id).Select(lane => lane.Name));
        Assert.Equal((1, 0), (stored.LiveStep(x).Column, stored.LiveStep(y).Column));
    }

    private static void AssertConflict(DomainException refusal, string type, Guid author)
    {
        Assert.Equal((ErrorCodes.VersionConflict, RefusalKind.Conflict), (refusal.Code, refusal.Kind));
        Assert.Equal(type, refusal.Details[StoreRefusals.ObjectDetail]);
        Assert.Equal(author, refusal.Details[StoreRefusals.UpdatedByDetail]);
    }
}
