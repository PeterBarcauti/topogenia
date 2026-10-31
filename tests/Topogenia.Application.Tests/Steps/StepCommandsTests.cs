// StepCommandsTests.cs
// Create, update and insert a step through the template: written, logged with the 1.x kinds, undone and redone (A 2.4, B 2.6-16 to 20).

using Topogenia.Application.OperationLog;
using Topogenia.Application.Steps;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Steps;

public sealed class StepCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_created_step_is_logged_with_the_kind_of_a_creation_and_its_undo_puts_the_shifted_step_back()
    {
        var project = new TestProject();
        var existing = project.Step(0);
        var outcome = await project.Runner.RunAsync(new CreateStepCommand(new CreateStepRequest(project.RootMapId, project.LaneId, 0, "Commander")), Token);
        var created = outcome.Result.Id;
        Assert.Equal(OperationKinds.CreateStep, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal((0, "Commander"), outcome.View!.Steps.Where(step => step.Id == created).Select(step => (step.Column, step.Label)).Single());
        Assert.Equal(1, project.StepOf(existing).Column);
        await project.UndoAsync();
        Assert.False(project.IsLiveStep(created));
        Assert.Equal(0, project.StepOf(existing).Column);
        await project.RedoAsync();
        Assert.True(project.IsLiveStep(created));
        Assert.Equal((0, 1), (project.StepOf(created).Column, project.StepOf(existing).Column));
    }

    [Fact]
    public async Task A_step_is_created_only_in_a_lane_of_its_map_and_nothing_is_written_otherwise()
    {
        var project = new TestProject();
        var parent = project.Step(0);
        var (_, otherLane) = project.SubMap(parent);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(
            new CreateStepCommand(new CreateStepRequest(project.RootMapId, otherLane, 1, "Commander")), Token));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)StructureEdits.LaneField), (refusal.Code, refusal.Details["field"]));
        Assert.Single(project.Database.All<Step>());
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task An_updated_step_checks_the_version_read_and_its_undo_and_redo_swap_the_label_and_the_type()
    {
        var project = new TestProject();
        var step = project.Step(0);
        UpdateStepRequest Request() => new(step) { Label = new FieldChange<string?>("Valider"), Type = new FieldChange<string?>("decision") };
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(UpdateStepCommand.WithVersion(Request(), 3), Token));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)"step"), (refusal.Code, refusal.Details["object"]));
        Assert.Equal("Étape 0", project.StepOf(step).Label);
        var outcome = await project.Runner.RunAsync(UpdateStepCommand.WithVersion(Request(), 1), Token);
        Assert.Equal(OperationKinds.UpdateStep, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal(("Valider", StepType.Decision), outcome.View!.Steps.Where(view => view.Id == step).Select(view => (view.Label, view.Type)).Single());
        await project.UndoAsync();
        Assert.Equal(("Étape 0", StepType.Task), (project.StepOf(step).Label, project.StepOf(step).StepType));
        await project.RedoAsync();
        Assert.Equal(("Valider", StepType.Decision), (project.StepOf(step).Label, project.StepOf(step).StepType));
    }

    [Fact]
    public async Task A_step_updated_without_if_match_is_not_checked()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var command = UpdateStepCommand.WithVersion(new UpdateStepRequest(step) { Label = new FieldChange<string?>("Valider") }, null);
        Assert.Empty(command.Versions);
        await project.Runner.RunAsync(command, Token);
        Assert.Equal("Valider", project.StepOf(step).Label);
    }

    [Fact]
    public async Task An_update_that_changes_nothing_writes_no_entry_in_the_log()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var outcome = await project.Runner.RunAsync(new UpdateStepCommand(new UpdateStepRequest(step) { Label = new FieldChange<string?>("Étape 0") }), Token);
        Assert.Null(outcome.OperationId);
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task The_assignee_must_be_a_member_and_a_change_of_cell_is_undone_with_the_assignee()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var second = project.SecondLane();
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(new UpdateStepCommand(
            new UpdateStepRequest(step) { AssigneeUserId = new FieldChange<Guid?>(project.Stranger.Id) }), Token));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)StepOperations.AssigneeField), (refusal.Code, refusal.Details["field"]));
        await project.Runner.RunAsync(new UpdateStepCommand(new UpdateStepRequest(step)
        {
            AssigneeUserId = new FieldChange<Guid?>(project.Contributor.Id), LaneId = new FieldChange<Guid>(second), Column = new FieldChange<int>(4),
        }), Token);
        Assert.Equal((project.Contributor.Id, (second, 4, 0)), (project.StepOf(step).AssigneeUserId!.Value, project.CellOf(step)));
        await project.UndoAsync();
        Assert.Equal(((Guid?)null, (project.LaneId, 0, 0)), (project.StepOf(step).AssigneeUserId, project.CellOf(step)));
    }

    [Fact]
    public async Task An_insertion_before_a_step_is_logged_as_inserer_and_its_undo_restores_the_cut_flow_and_the_positions()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var cut = project.Flow(a, b, "oui");
        var outcome = await project.Runner.RunAsync(new InsertStepCommand(new InsertStepRequest(b, InsertPosition.Before, "Vérifier")), Token);
        var x = outcome.Result.Id;
        Assert.Equal(OperationKinds.InsertStep, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal((1, 2), (project.StepOf(x).Column, project.StepOf(b).Column));
        Assert.False(project.IsLiveFlow(cut));
        Assert.Equal([(a, x, "oui"), (x, b, null)], project.LiveFlows().OrderBy(flow => flow.Source == a ? 0 : 1));
        await project.UndoAsync();
        Assert.False(project.IsLiveStep(x));
        Assert.Equal([(a, b, "oui")], project.LiveFlows());
        Assert.Equal(1, project.StepOf(b).Column);
        await project.RedoAsync();
        Assert.True(project.IsLiveStep(x));
        Assert.False(project.IsLiveFlow(cut));
        Assert.Equal((1, 2), (project.StepOf(x).Column, project.StepOf(b).Column));
        Assert.Equal(2, project.LiveFlows().Count);
    }

    [Fact]
    public async Task An_insertion_below_the_last_lane_is_refused_on_the_position_and_nothing_is_written()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new InsertStepCommand(new InsertStepRequest(step, InsertPosition.Below, "Vérifier")), Token));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"position"), (refusal.Code, refusal.Details["field"]));
        Assert.Single(project.Database.All<Step>());
        Assert.Empty(project.Database.Entries);
    }
}
