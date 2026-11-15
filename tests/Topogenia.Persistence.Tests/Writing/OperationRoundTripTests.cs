// OperationRoundTripTests.cs
// Load, operation of the domain, save, load again: the database holds what the operation left, on both engines (ADR 015, 016, D06).

using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Writing;

/// <summary>
/// Representative operations of each kind of change: creations (steps, flows, sub-map), moves that exchange cells,
/// logical deletion in a batch and its restoration by the undo, a dissolution (deletions, moves to another map, new
/// lanes, flows and a frame) and a sheet (procedure rows without version, a validated version). After each, the
/// workspace read again equals the workspace left in memory, and every touched record is in its expected state.
/// </summary>
public sealed class OperationRoundTripTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    /// <summary>Three steps in the first lane of the root map, at the columns 0, 1 and 2, linked in this order.</summary>
    private static async Task<(Outcome Outcome, Guid[] Steps)> ThreeStepsAsync(StoreScenario scenario)
    {
        var steps = new Guid[3];
        var outcome = await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            var lane = workspace.LanesOf(map)[0].Id;
            for (var i = 0; i < 3; i++)
                steps[i] = StepOperations.Create(workspace, context, new CreateStepRequest(map, lane, i, $"Step {i}")).Id;
            FlowRules.Create(workspace, context, new CreateFlowRequest(map, steps[0], steps[1], "ok"));
            FlowRules.Create(workspace, context, new CreateFlowRequest(map, steps[1], steps[2], null));
        });
        return (outcome, steps);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_project_created_by_the_domain_is_read_back_with_its_map_lane_counters_and_member(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);

        var stored = await scenario.LoadAsync();

        Assert.Equal(scenario.Actor.Id, Assert.Single(stored.All<ProjectMember>()).UserId);
        Assert.Equal("Acteur", Assert.Single(stored.LanesOf(stored.RootMap.Id)).Name);
        Assert.Equal(new[] { CounterKind.Step, CounterKind.Lane, CounterKind.PainPoint }, stored.All<ProjectCounter>().Select(counter => counter.Counter).Order());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Steps_and_flows_created_by_the_domain_are_read_back_as_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);

        var (outcome, steps) = await ThreeStepsAsync(scenario);

        var stored = await scenario.AssertStoredAsync(outcome);
        Assert.Equal(2, stored.FlowsOf(stored.RootMap.Id).Count);
        Assert.Equal(3, stored.All<ProjectCounter>().Single(counter => counter.Counter == CounterKind.Step).LastValue);
        Assert.All(steps, id => Assert.NotNull(stored.Step(id)));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_move_that_turns_three_steps_round_their_cells_is_written_through_the_parking(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (_, steps) = await ThreeStepsAsync(scenario);

        // The last step goes to the first cell: RG-C04 shifts the two others, each step takes the cell of another.
        var outcome = await scenario.RunAsync((workspace, _) =>
        {
            var last = workspace.LiveStep(steps[2]);
            GridRules.MoveTo(workspace, last, last.Cell with { Column = 0 });
        });

        var stored = await scenario.AssertStoredAsync(outcome);
        Assert.Equal(new[] { 1, 2, 0 }, steps.Select(id => stored.LiveStep(id).Column));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_steps_that_exchange_their_cells_in_one_change_set_are_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (_, steps) = await ThreeStepsAsync(scenario);

        var outcome = await scenario.RunAsync((workspace, _) =>
        {
            var (first, second) = (workspace.LiveStep(steps[0]), workspace.LiveStep(steps[1]));
            workspace.Put(first with { Column = second.Column });
            workspace.Put(second with { Column = first.Column });
        });

        var stored = await scenario.AssertStoredAsync(outcome);
        Assert.Equal((1, 0), (stored.LiveStep(steps[0]).Column, stored.LiveStep(steps[1]).Column));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_group_deleted_in_a_batch_then_undone_is_restored_in_the_database(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (_, steps) = await ThreeStepsAsync(scenario);

        var deletion = await scenario.RunAsync((workspace, context) =>
            GroupDeletion.Delete(workspace, context, workspace.RootMap.Id, new GroupDeletionRequest([steps[0], steps[1]], null)));
        var deleted = await scenario.AssertStoredAsync(deletion);
        var batch = Assert.Single(deleted.All<DeletionBatch>());
        Assert.All(steps[..2], id => Assert.Equal(batch.Id, deleted.Step(id)!.DeletionBatchId));
        Assert.All(deleted.All<Flow>(), flow => Assert.Equal(batch.Id, flow.DeletionBatchId));

        var undo = await scenario.RunAsync((workspace, context) => UndoReplay.Undo(workspace, context, deletion.Changes));

        var restored = await scenario.AssertStoredAsync(undo);
        Assert.All(steps, id => Assert.Null(restored.Step(id)!.DeletedAt));
        Assert.Equal(2, restored.FlowsOf(restored.RootMap.Id).Count);
        Assert.NotNull(restored.All<DeletionBatch>().Single().RestoredAt);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_detail_dissolved_into_its_map_is_read_back_as_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (_, steps) = await ThreeStepsAsync(scenario);
        var detailed = await scenario.RunAsync((workspace, context) =>
        {
            var detail = SubMapRules.Detail(workspace, context, steps[1]);
            var first = StepOperations.Create(workspace, context, new CreateStepRequest(detail.MapId, detail.FirstLaneId!.Value, 0, "Prepare"));
            var second = StepOperations.Create(workspace, context, new CreateStepRequest(detail.MapId, detail.FirstLaneId!.Value, 1, "Check"));
            FlowRules.Create(workspace, context, new CreateFlowRequest(detail.MapId, first.Id, second.Id, null));
        });
        await scenario.AssertStoredAsync(detailed);

        var dissolution = await scenario.RunAsync((workspace, context) => Dissolve.Run(workspace, context, steps[1]));

        var stored = await scenario.AssertStoredAsync(dissolution);
        Assert.NotNull(stored.Step(steps[1])!.DeletedAt);
        Assert.True(Assert.Single(stored.All<Dissolution>()).IsOpen);
        Assert.Equal(4, stored.StepsOf(stored.RootMap.Id).Count);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_sheet_saved_validated_then_changed_is_read_back_as_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (_, steps) = await ThreeStepsAsync(scenario);
        static SheetInput Input(params string[] rows) => new(
            new Dictionary<string, SheetFieldValue?>
            {
                [SheetContentReader.ActorKey] = new("Buyer", null),
                [SheetContentReader.FrequencyKey] = new("daily", null),
                [SheetContentReader.DurationKey] = new(null, 12.5m),
            },
            rows.Select(row => (ProcedureLine?)new ProcedureLine(row, "ERP", "check")).ToList(),
            "Free text");

        var saved = await scenario.RunAsync((workspace, context) => SheetWorkflow.Save(workspace, context, steps[0], Input("Open", "Check", "File")));
        await scenario.AssertStoredAsync(saved);
        var validated = await scenario.RunAsync((workspace, context) => SheetWorkflow.Validate(workspace, context, steps[0]));
        await scenario.AssertStoredAsync(validated);
        var changed = await scenario.RunAsync((workspace, context) => SheetWorkflow.Save(workspace, context, steps[0], Input("Open", "Send")));

        var stored = await scenario.AssertStoredAsync(changed);
        Assert.Equal(new[] { "Open", "Send" }, stored.All<SheetOperation>().OrderBy(row => row.Position).Select(row => row.Text));
        Assert.Equal(Frequency.Daily, Assert.Single(stored.All<StepSheetVersion>()).Frequency);
        Assert.Contains(changed.Changes.Changes, change => change.Kind == ChangeKind.Removed && change.Before is SheetOperation);
    }
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_approval_moved_to_another_lane_of_the_step_is_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (_, steps) = await ThreeStepsAsync(scenario);
        var (approver, consulted) = (Guid.Empty, Guid.Empty);
        await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            approver = LaneOperations.Add(workspace, context, new AddLaneRequest(map, "Approver")).Id;
            consulted = LaneOperations.Add(workspace, context, new AddLaneRequest(map, "Consulted")).Id;
            RaciRules.SetRole(workspace, context, new SetRaciRequest(steps[0], approver, "A"));
            RaciRules.SetRole(workspace, context, new SetRaciRequest(steps[0], consulted, "C"));
        });

        // The single A moves: the role of the approver is removed and the role C of the other lane becomes A.
        var moved = await scenario.RunAsync((workspace, context) => RaciRules.SetRole(workspace, context, new SetRaciRequest(steps[0], consulted, "A")));

        var stored = await scenario.AssertStoredAsync(moved);
        Assert.Equal((consulted, RaciRole.A), stored.All<StepRaci>().Select(role => (role.LaneId, role.Role)).Single());
    }
    public static TheoryData<EngineKind, bool> EveryOrderOfKeys()
    {
        var data = new TheoryData<EngineKind, bool>();
        foreach (var kind in Enum.GetValues<EngineKind>())
        {
            data.Add(kind, true);
            data.Add(kind, false);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryOrderOfKeys))]
    public async Task A_step_deleted_logically_frees_its_cell_for_a_step_moved_into_it_in_the_same_change_set(EngineKind kind, bool movedFirst)
    {
        var scenario = await ScenarioAsync(kind);
        var (_, steps) = await ThreeStepsAsync(scenario);
        // The step that leaves the cell and the one that takes it, in both orders of their keys (EF Core orders by key).
        var (deleted, moved) = movedFirst ? (steps[1], steps[0]) : (steps[0], steps[1]);

        var outcome = await scenario.RunAsync((workspace, context) =>
        {
            var leaving = workspace.LiveStep(deleted);
            workspace.SoftDelete(leaving, context);
            workspace.Put(workspace.LiveStep(moved) with { Column = leaving.Column });
        });

        var stored = await scenario.AssertStoredAsync(outcome);
        Assert.Equal(stored.Step(deleted)!.Column, stored.LiveStep(moved).Column);
    }
}
