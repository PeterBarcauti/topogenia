// StructureRoundTripTests.cs
// Load, operate, save, load again for the structure: block moves, hidden approvals, grouping, dissolution and its undo.

using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Layout;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.RoundTrips;

/// <summary>
/// The part of the persistence of the structure rules (annex A 2.4, 2.9, 2.13, 6): each operation of the domain is written
/// through the parking and the freeing of the partial unique indexes (ADR 016, 030), read back equal, and an operation
/// written on a map changed since its reading is refused by a guard.
/// </summary>
public sealed class StructureRoundTripTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_block_that_turns_round_its_own_cells_is_written_through_the_parking(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(3);

        // Each step of the block takes the cell of the next one: no order of the statements avoids a duplicate cell.
        var moved = await scenario.RunAsync((workspace, _) =>
        {
            var lane = workspace.FirstLane();
            StepLayout.MoveGroup(workspace, new GroupMoveRequest(workspace.RootMap.Id, steps.Select((id, i) => new StepPlacement(id, lane, (i + 1) % 3)).ToList()));
        });

        var stored = await scenario.AssertStoredAsync(moved);
        Assert.Equal(new[] { 1, 2, 0 }, steps.Select(id => stored.LiveStep(id).Column));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_block_shifted_onto_its_own_cells_and_its_neighbour_is_written(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(4);

        var moved = await scenario.RunAsync((workspace, _) =>
        {
            var lane = workspace.FirstLane();
            StepLayout.MoveGroup(workspace, new GroupMoveRequest(workspace.RootMap.Id, steps[..3].Select((id, i) => new StepPlacement(id, lane, i + 1)).ToList()));
        });

        var stored = await scenario.AssertStoredAsync(moved);
        Assert.Equal(new[] { 1, 2, 3, 4 }, steps.Select(id => stored.LiveStep(id).Column));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_database_bounds_the_column_below_only_for_the_parking_and_the_domain_bounds_it_at_ten_thousand(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];
        var parked = (await scenario.LoadAsync()).LiveStep(step);

        await scenario.WriteAsync(new ChangeSet([new RecordChange(ChangeKind.Modified, parked,
            (parked with { Column = WritePlan.ParkingColumn + 7 }).Stamp(parked.Version + 1, FixedClock.Time, null))]));
        var beyond = await Assert.ThrowsAsync<DomainException>(() => scenario.RunAsync((workspace, context) =>
            StepOperations.Update(workspace, context, new UpdateStepRequest(step) { Column = new(Cell.MaxColumn + 1) })));

        Assert.Equal(WritePlan.ParkingColumn + 7, (await scenario.LoadAsync()).LiveStep(step).Column);
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"column"), (beyond.Code, beyond.Details["field"]));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Setting_an_approval_replaces_in_the_database_a_hidden_approval_left_on_a_lane_of_another_map(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        var (hidden, visible) = (Guid.Empty, Guid.Empty);
        await scenario.RunAsync((workspace, context) =>
        {
            hidden = SubMapRules.Detail(workspace, context, steps[1]).FirstLaneId!.Value;
            visible = LaneOperations.Add(workspace, context, new AddLaneRequest(workspace.RootMap.Id, "Approver")).Id;
        });
        await scenario.WriteAsync(new ChangeSet([StoreScenario.Created(new StepRaci(steps[0], hidden, RaciRole.A, FixedClock.Time, scenario.Actor.Id))]));

        var moved = await scenario.RunAsync((workspace, context) => RaciRules.SetRole(workspace, context, new SetRaciRequest(steps[0], visible, "A")));

        var stored = await scenario.AssertStoredAsync(moved);
        Assert.Equal((visible, RaciRole.A), Assert.Single(stored.All<StepRaci>()) switch { var role => (role.LaneId, role.Role) });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_step_created_by_a_grouping_keeps_the_identifier_of_its_operation(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(3);
        var (operation, created) = (Guid.Empty, (Guid?)null);

        var grouped = await scenario.RunAsync((workspace, context) =>
        {
            operation = context.OperationId;
            created = GroupIntoSubMap.Group(workspace, context, workspace.RootMap.Id, new GroupRequest([steps[0], steps[1]], "Prepare")).StepId;
        });

        var stored = await scenario.AssertStoredAsync(grouped);
        Assert.Equal(operation, stored.LiveStep(created!.Value).OriginOperationId);
        Assert.Equal(2, stored.StepsOf(stored.SubMapOf(created.Value)!.Id).Count);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_use_written_after_its_document_was_removed_is_refused_as_an_integrity_conflict(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        var document = await scenario.OutputOfAsync(steps[0]);
        var stale = await scenario.PrepareAsync((workspace, context) => DocumentUses.Use(workspace, context, steps[1], document, new DocumentUseInput(1)));
        await scenario.RunAsync((workspace, _) => DocumentEdits.Delete(workspace, steps[0], document), scenario.Other);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => scenario.WriteAsync(stale.Changes));

        Assert.Equal(ErrorCodes.IntegrityConflict, refusal.Code);
        Assert.Equal(kind == EngineKind.SqlServer ? "FK_document_use_document" : null, refusal.Details[StoreRefusals.ConstraintDetail]);
        var stored = await scenario.LoadAsync();
        Assert.Empty(stored.All<Document>());
        Assert.Empty(stored.All<DocumentUse>());
    }

    /// <summary>Three linked steps whose middle one is detailed by two linked steps; the first flow is labelled and translated.</summary>
    private static async Task<Guid[]> DetailedAsync(StoreScenario scenario)
    {
        var steps = await scenario.LinkedStepsAsync(3);
        await scenario.RunAsync((workspace, context) =>
        {
            var flow = workspace.FlowsOf(workspace.RootMap.Id).Single(candidate => candidate.TargetStepId == steps[1]);
            FlowRules.Relabel(workspace, context, new RelabelFlowRequest(flow.Id, "ok"));
            TranslationRules.WriteManual(workspace, context, TranslationObject.Flow, flow.Id, [new ManualTranslation("en", "fine")]);
            var detail = SubMapRules.Detail(workspace, context, steps[1]);
            var first = StepOperations.Create(workspace, context, new CreateStepRequest(detail.MapId, detail.FirstLaneId!.Value, 0, "Prepare", Row: 1));
            var second = StepOperations.Create(workspace, context, new CreateStepRequest(detail.MapId, detail.FirstLaneId!.Value, 1, "Check"));
            FlowRules.Create(workspace, context, new CreateFlowRequest(detail.MapId, first.Id, second.Id, null));
        });
        return steps;
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_dissolution_records_its_frame_the_original_cells_of_its_steps_and_the_objects_it_created(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await DetailedAsync(scenario);
        var before = await scenario.LoadAsync();
        var detail = before.SubMapOf(steps[1])!.Id;
        var origins = before.StepsOf(detail).ToDictionary(step => step.Id, step => (step.LaneId, step.Column, step.Row));

        var dissolution = await scenario.RunAsync((workspace, context) => Dissolve.Run(workspace, context, steps[1]));

        var stored = await scenario.AssertStoredAsync(dissolution);
        var frame = Assert.Single(stored.All<Dissolution>());
        Assert.Equal((stored.RootMap.Id, steps[1], true), (frame.MapId, frame.StepId, frame.IsOpen));
        Assert.Equal(origins.OrderBy(pair => pair.Key), stored.All<DissolutionStep>().ToDictionary(step => step.StepId, step => (step.OriginLaneId, step.OriginColumn, step.OriginRow)).OrderBy(pair => pair.Key));
        var created = stored.All<DissolutionObject>().Where(item => item.ObjectType == DissolutionObjectTypes.Flow).Select(item => item.ObjectId).ToList();
        Assert.NotEmpty(created);
        Assert.All(created, flow => Assert.Null(stored.Flow(flow)!.DeletedAt));
        Assert.Contains(stored.All<Translation>(), translation => created.Contains(translation.ObjectId) && translation.Text == "fine");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Undoing_a_dissolution_removes_its_flows_their_translations_and_its_rows_from_the_database(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await DetailedAsync(scenario);
        var before = await scenario.LoadAsync();
        var dissolution = await scenario.RunAsync((workspace, context) => Dissolve.Run(workspace, context, steps[1]));
        var created = dissolution.Workspace.All<DissolutionObject>().Where(item => item.ObjectType == DissolutionObjectTypes.Flow).Select(item => item.ObjectId).ToList();

        var undo = await scenario.RunAsync((workspace, context) => UndoReplay.Undo(workspace, context, dissolution.Changes));

        // The 1.x removed the created flows physically; the 2.0 puts them in a batch of the undo (spec 4.6) and drops their copied translations.
        var stored = await scenario.AssertStoredAsync(undo);
        var frameBatch = Assert.Single(dissolution.Workspace.All<Dissolution>()).DeletionBatchId;
        Assert.All(created.Select(flow => stored.Flow(flow)!), flow =>
        {
            Assert.NotNull(flow.DeletedAt);
            Assert.NotEqual(frameBatch, flow.DeletionBatchId);
        });
        Assert.DoesNotContain(stored.All<Translation>(), translation => created.Contains(translation.ObjectId));
        Assert.Empty(stored.All<Dissolution>());
        Assert.Empty(stored.All<DissolutionStep>());
        Assert.Empty(stored.All<DissolutionObject>());
        Assert.Equal(before.StepsOf(before.SubMapOf(steps[1])!.Id).Select(step => step.Id).Order(), stored.StepsOf(stored.SubMapOf(steps[1])!.Id).Select(step => step.Id).Order());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Undoing_a_dissolution_whose_step_was_deleted_since_is_refused_and_writes_nothing(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await DetailedAsync(scenario);
        var dissolution = await scenario.RunAsync((workspace, context) => Dissolve.Run(workspace, context, steps[1]));
        var moved = dissolution.Workspace.All<DissolutionStep>().First().StepId;
        await scenario.RunAsync((workspace, context) => MacroStepDeletion.DeleteStep(workspace, context, moved), scenario.Other);
        var before = WorkspaceRecords.Of(await scenario.LoadAsync());

        var refusal = await Assert.ThrowsAsync<DomainException>(() => scenario.RunAsync((workspace, context) => UndoReplay.Undo(workspace, context, dissolution.Changes)));

        Assert.Equal(ErrorCodes.OperationOutdated, refusal.Code);
        Assert.Equal(before, WorkspaceRecords.Of(await scenario.LoadAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_frame_closed_frees_its_map_for_the_next_dissolution_in_the_database(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await DetailedAsync(scenario);
        var first = await scenario.RunAsync((workspace, context) => Dissolve.Run(workspace, context, steps[1]));
        var frame = Assert.Single(first.Workspace.All<Dissolution>()).Id;
        await scenario.RunAsync((workspace, context) => Remainder.Finish(workspace, context, frame));
        var other = steps[2];
        await scenario.RunAsync((workspace, context) =>
        {
            var detail = SubMapRules.Detail(workspace, context, other);
            StepOperations.Create(workspace, context, new CreateStepRequest(detail.MapId, detail.FirstLaneId!.Value, 0, "Archive"));
        });

        var second = await scenario.RunAsync((workspace, context) => Dissolve.Run(workspace, context, other));

        var stored = await scenario.AssertStoredAsync(second);
        Assert.Equal(2, stored.All<Dissolution>().Count());
        Assert.Single(stored.All<Dissolution>(), dissolution => dissolution.IsOpen);
    }
}
