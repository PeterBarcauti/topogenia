// ModelPortedTests.cs
// Integration tests of the 1.x on batches, deleted steps, sheets, uses and the undo of a flow, ported to the persistence.

using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Model;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Structure;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Porting;

/// <summary>
/// The 1.x tests that read or wrote the database directly, ported to the unit of work on both engines (the table of
/// `docs/portage-tests.md`, persistence part A). The restoration of a batch by the 1.x route becomes the undo of the
/// server (spec 4.6).
/// </summary>
public sealed class ModelPortedTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    /// <summary>T-MODE-01.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_batch_keeps_who_deleted_and_who_restored_and_is_restored_once(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        var deletion = await scenario.RunAsync((workspace, context) => MacroStepDeletion.DeleteStep(workspace, context, steps[1]));
        var deleted = Assert.Single((await scenario.AssertStoredAsync(deletion)).All<DeletionBatch>());
        Assert.Equal((scenario.Actor.Id, (DateTime?)null), (deleted.DeletedBy, deleted.RestoredAt));

        var undo = await scenario.RunAsync((workspace, context) => UndoReplay.Undo(workspace, context, deletion.Changes), scenario.Other);
        var restored = await scenario.AssertStoredAsync(undo);
        var again = await Assert.ThrowsAsync<DomainException>(() => scenario.RunAsync((workspace, context) =>
            UndoReplay.Undo(workspace, context, deletion.Changes)));

        Assert.Single(restored.FlowsOf(restored.RootMap.Id));
        var batch = Assert.Single(restored.All<DeletionBatch>());
        Assert.Equal((deleted.Id, scenario.Other.Id), (batch.Id, batch.RestoredBy));
        Assert.Equal(ErrorCodes.OperationOutdated, again.Code);
        WorkspaceRecords.AssertSame(restored, await scenario.LoadAsync());
    }

    /// <summary>T-MODE-02.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_operation_that_deletes_nothing_leaves_no_batch(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];

        var transfer = await scenario.RunAsync((workspace, context) => BlockTransfer.Transfer(workspace, context, workspace.RootMap.Id,
            new TransferRequest([new StepPlacement(step, workspace.FirstLane(), 3)], Cut: true)));

        var stored = await scenario.AssertStoredAsync(transfer);
        Assert.Equal(3, stored.LiveStep(step).Column);
        Assert.Empty(stored.All<DeletionBatch>());
    }

    /// <summary>
    /// T-MODE-13, D04: the 1.x trigger is gone; the domain deletes a step with its flows in the same batch, and that is
    /// what the database holds.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_deleted_step_keeps_no_live_flow_in_the_database_its_flows_go_in_its_batch(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(3);

        var deletion = await scenario.RunAsync((workspace, context) => MacroStepDeletion.DeleteStep(workspace, context, steps[1]));

        var stored = await scenario.AssertStoredAsync(deletion);
        var batch = stored.Step(steps[1])!.DeletionBatchId;
        Assert.NotNull(batch);
        Assert.All(stored.All<Flow>(), flow => Assert.Equal(batch, flow.DeletionBatchId));
        Assert.Empty(FlowLinks.BrokenFlows(stored));
    }

    /// <summary>
    /// T-MODE-13, second half (D04, A-6-44): the database keeps structural guards only, no trigger; a deleted step with a
    /// live flow written outside the domain is accepted by both engines and seen by the invariants of the domain.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_database_has_no_trigger_a_deleted_step_with_a_live_flow_is_only_refused_by_the_domain(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        var raw = await scenario.LoadAsync();
        var context = scenario.Context();
        var batch = new DeletionBatch(Guid.CreateVersion7(), scenario.ProjectId, null, context.Now, scenario.Actor.Id, null, null);
        var step = raw.LiveStep(steps[1]);

        await scenario.WriteAsync(new ChangeSet([StoreScenario.Created(batch),
            new RecordChange(ChangeKind.Modified, step, step with { DeletedAt = context.Now, DeletionBatchId = batch.Id, Version = step.Version + 1 })]));

        var stored = await scenario.LoadAsync();
        Assert.Single(FlowLinks.BrokenFlows(stored));
        Assert.Equal(ErrorCodes.MapModified, Assert.Throws<DomainException>(() => StructureInvariants.Ensure(stored)).Code);
    }

    /// <summary>T-FICI-09 (part of the database).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_sheet_saved_or_validated_on_a_stale_reading_is_refused_with_the_author_of_the_change(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];
        await scenario.RunAsync((workspace, context) => SheetWorkflow.Save(workspace, context, step, ScenarioSteps.Sheet("A")));
        var staleSave = await scenario.PrepareAsync((workspace, context) => SheetWorkflow.Save(workspace, context, step, ScenarioSteps.Sheet("C")));
        var staleValidation = await scenario.PrepareAsync((workspace, context) => SheetWorkflow.Validate(workspace, context, step));
        await scenario.RunAsync((workspace, context) => SheetWorkflow.Save(workspace, context, step, ScenarioSteps.Sheet("B")), scenario.Other);

        var save = await Assert.ThrowsAsync<DomainException>(() => scenario.WriteAsync(staleSave.Changes));
        var validation = await Assert.ThrowsAsync<DomainException>(() => scenario.WriteAsync(staleValidation.Changes));

        Assert.All(new[] { save, validation }, refusal =>
            Assert.Equal((ErrorCodes.VersionConflict, (object?)"sheet", (object?)scenario.Other.Id),
                (refusal.Code, refusal.Details[StoreRefusals.ObjectDetail], refusal.Details[StoreRefusals.UpdatedByDetail])));
        var stored = await scenario.LoadAsync();
        Assert.Equal("B", Assert.Single(stored.All<StepSheet>()).Purpose);
        Assert.Empty(stored.All<StepSheetVersion>());
    }

    /// <summary>T-DOCF-08.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_use_ticked_by_the_neighbour_step_does_not_change_the_version_of_the_sheet_of_the_producer(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        var document = await scenario.OutputOfAsync(steps[0], "SAP extraction");
        await scenario.RunAsync((workspace, context) => SheetWorkflow.Save(workspace, context, steps[0], ScenarioSteps.Sheet("Extract")));
        var producerSave = await scenario.PrepareAsync((workspace, context) =>
            SheetWorkflow.Save(workspace, context, steps[0], ScenarioSteps.Sheet("Extract the sales")));

        await scenario.RunAsync((workspace, context) => DocumentUses.Use(workspace, context, steps[1], document, new DocumentUseInput(2)), scenario.Other);
        await scenario.WriteAsync(producerSave.Changes);

        var stored = await scenario.LoadAsync();
        var sheet = Assert.Single(stored.All<StepSheet>());
        Assert.Equal(("Extract the sales", 2), (sheet.Purpose, sheet.Version));
        Assert.Equal(steps[1], Assert.Single(stored.All<DocumentUse>()).StepId);
    }

    /// <summary>T-SCAN-18 (the restoration is the undo).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Undoing_a_flow_deletion_after_the_flow_was_drawn_again_never_doubles_it(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var (x, y) = (await scenario.LinkedStepsAsync(2)) switch { var ids => (ids[0], ids[1]) };
        var deletion = await scenario.RunAsync((workspace, context) =>
            FlowRules.Delete(workspace, context, Assert.Single(workspace.FlowsOf(workspace.RootMap.Id)).Id));
        await scenario.RunAsync((workspace, context) => FlowRules.Create(workspace, context, new CreateFlowRequest(workspace.RootMap.Id, x, y, null)));

        var undo = await Record.ExceptionAsync(() => scenario.RunAsync((workspace, context) => UndoReplay.Undo(workspace, context, deletion.Changes)));

        Assert.Null(undo);
        var stored = await scenario.LoadAsync();
        Assert.Single(stored.FlowsOf(stored.RootMap.Id), flow => flow.SourceStepId == x && flow.TargetStepId == y);
        Assert.Empty(FlowLinks.Duplicates(stored));
    }
}
