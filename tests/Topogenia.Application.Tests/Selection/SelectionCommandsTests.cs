// SelectionCommandsTests.cs
// Delete and paste a selection through the template: written, logged with the 1.x kinds, undone and redone (annex A 2.8, B 2.6-25, 29).

using Topogenia.Application.OperationLog;
using Topogenia.Application.Selection;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Selection;
using static Topogenia.Application.Tests.Selection.SelectionFixture;

namespace Topogenia.Application.Tests.Selection;

public sealed class SelectionCommandsTests
{
    [Fact]
    public async Task A_deleted_selection_is_stitched_logged_as_a_deletion_and_undo_brings_it_back_without_the_stitch()
    {
        var project = new TestProject();
        var (a, b, c) = (project.Step(0), project.Step(1), project.Step(2));
        var ab = Flow(project, a, b);
        var bc = Flow(project, b, c);
        var outcome = await project.Runner.RunAsync(new DeleteSelectionCommand(project.RootMapId, new GroupDeletionRequest([b], null, Stitch: true)), Token);
        Assert.Equal([b], outcome.Result.Ids);
        Assert.Single(outcome.Result.CreatedFlows);
        Assert.Equal([OperationKinds.Delete], Kinds(project));
        Assert.Equal([a, c], outcome.View!.Steps.Select(step => step.Id).Order());
        Assert.Equal([(a, c)], LiveFlows(project));

        await UndoAsync(project);
        Assert.True(IsLiveStep(project, b));
        Assert.Equal(new[] { (a, b), (b, c) }.Order(), LiveFlows(project));
        Assert.Equal((1, 2), (StepOf(project, b).Column, StepOf(project, c).Column));
        Assert.Contains(project.Database.All<Flow>(), flow => flow.Id == ab && flow.DeletedAt is null);
        Assert.Contains(project.Database.All<Flow>(), flow => flow.Id == bc && flow.DeletedAt is null);

        await RedoAsync(project);
        Assert.False(IsLiveStep(project, b));
        Assert.Equal([(a, c)], LiveFlows(project));
    }

    [Fact]
    public async Task A_selection_with_a_filled_sub_map_asks_for_the_choice_and_its_deletion_takes_the_descent_back_on_undo()
    {
        var project = new TestProject();
        var macro = project.Step(0);
        var (subMap, subLane) = project.SubMap(macro);
        var inside = project.Step(0, laneId: subLane, mapId: subMap);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new DeleteSelectionCommand(project.RootMapId, new GroupDeletionRequest([macro], null)), Token));
        Assert.Equal(ErrorCodes.MacroChoiceRequired, refusal.Code);
        Assert.Empty(project.Database.Entries);
        Assert.True(IsLiveStep(project, inside));

        await project.Runner.RunAsync(new DeleteSelectionCommand(project.RootMapId, new GroupDeletionRequest([macro], null, DeleteSubMaps: true)), Token);
        var batch = StepOf(project, macro).DeletionBatchId;
        Assert.NotNull(batch);
        Assert.Equal((batch, batch), (StepOf(project, inside).DeletionBatchId, MapOf(project, subMap).DeletionBatchId));

        await UndoAsync(project);
        Assert.True(IsLiveStep(project, macro) && IsLiveStep(project, inside) && IsLiveMap(project, subMap));
    }

    [Fact]
    public async Task A_step_of_another_map_in_the_selection_is_refused_with_its_code_and_nothing_is_written()
    {
        var project = new TestProject();
        var macro = project.Step(0);
        var (subMap, subLane) = project.SubMap(macro);
        var other = project.Step(3, laneId: subLane, mapId: subMap);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new DeleteSelectionCommand(project.RootMapId, new GroupDeletionRequest([macro, other], null, DeleteSubMaps: true)), Token));
        Assert.Equal((ErrorCodes.StepOutsideMap, (object?)"PRJ-E004"), (refusal.Code, refusal.Details["code"]));
        Assert.True(IsLiveStep(project, macro));
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_pasted_copy_is_logged_as_a_paste_and_undo_and_redo_remove_and_bring_back_the_same_steps()
    {
        var project = new TestProject();
        var request = new PasteRequest(
            [new PastedStep("c0", "Contrôler", null, project.LaneId, 3), new PastedStep("c1", "Archiver", "wait", project.LaneId, 4)],
            [new PastedFlow("c0", "c1", "ok")]);
        var outcome = await project.Runner.RunAsync(new PasteCommand(project.RootMapId, request), Token);
        var pasted = outcome.Result.Ids;
        Assert.Equal(["Contrôler", "Archiver"], pasted.Select(id => StepOf(project, id).Label));
        Assert.Equal([(pasted[0], pasted[1])], LiveFlows(project));
        Assert.Equal([OperationKinds.Paste], Kinds(project));
        Assert.Equal(2, outcome.View!.Steps.Count);

        await UndoAsync(project);
        Assert.Empty(LiveSteps(project));
        Assert.Empty(LiveFlows(project));

        await RedoAsync(project);
        Assert.Equal(pasted, LiveSteps(project));
        Assert.Equal([(pasted[0], pasted[1])], LiveFlows(project));
    }

    [Fact]
    public async Task A_paste_after_an_anchor_cuts_its_single_outgoing_flow_and_undo_puts_the_flow_back()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var ab = Flow(project, a, b);
        var request = new PasteRequest([new PastedStep("c0", "Contrôler", null, project.LaneId, 1)], null, new AnchorRequest(a, "after"));
        var outcome = await project.Runner.RunAsync(new PasteCommand(project.RootMapId, request), Token);
        var pasted = Assert.Single(outcome.Result.Ids);
        Assert.Equal([ab], outcome.Result.CutFlows);
        Assert.Equal(new[] { (a, pasted), (pasted, b) }.Order(), LiveFlows(project));
        Assert.Equal(2, StepOf(project, b).Column);

        await UndoAsync(project);
        Assert.Equal([(a, b)], LiveFlows(project));
        Assert.Equal([a, b], LiveSteps(project));
        Assert.Equal(1, StepOf(project, b).Column);
    }

    [Fact]
    public async Task A_paste_with_an_unknown_anchor_position_is_refused_on_position_and_nothing_is_written()
    {
        var project = new TestProject();
        var a = project.Step(0);
        var request = new PasteRequest([new PastedStep("c0", "Contrôler", null, project.LaneId, 1)], null, new AnchorRequest(a, "below"));
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(new PasteCommand(project.RootMapId, request), Token));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"position"), (refusal.Code, refusal.Details["field"]));
        Assert.Equal([a], LiveSteps(project));
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_pasted_step_on_an_unknown_lane_is_refused()
    {
        var project = new TestProject();
        var request = new PasteRequest([new PastedStep("c0", "Contrôler", null, project.NextId(), 1)], null);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(new PasteCommand(project.RootMapId, request), Token));
        Assert.Equal(ErrorCodes.InvalidValue, refusal.Code);
        Assert.Empty(LiveSteps(project));
    }

    [Fact]
    public async Task A_contributor_neither_deletes_nor_pastes_a_selection()
    {
        var project = new TestProject();
        var a = project.Step(0);
        project.ActingAs(project.Contributor);
        var deletion = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new DeleteSelectionCommand(project.RootMapId, new GroupDeletionRequest([a], null)), Token));
        var paste = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(
            new PasteCommand(project.RootMapId, new PasteRequest([new PastedStep("c0", "Contrôler", null, project.LaneId, 1)], null)), Token));
        Assert.Equal((ErrorCodes.AccessDenied, ErrorCodes.AccessDenied), (deletion.Code, paste.Code));
        Assert.Equal([a], LiveSteps(project));
    }
}
