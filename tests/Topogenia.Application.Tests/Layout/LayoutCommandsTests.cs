// LayoutCommandsTests.cs
// The grouped layout (deplacer or aligner) and the grouped move of a drag, one operation each, undone and redone (A 2.4, B 2.6-21 to 24).

using Topogenia.Application.Layout;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Tests.Steps;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Layout;
using Topogenia.Domain.Model;
using Topogenia.Domain.Structure;

namespace Topogenia.Application.Tests.Layout;

public sealed class LayoutCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static List<Guid> LaneOrder(TestProject project) =>
        project.Database.All<Lane>().Where(lane => lane.DeletedAt is null && lane.MapId == project.RootMapId)
            .OrderBy(lane => lane.SortOrder).Select(lane => lane.Id).ToList();

    [Theory]
    [InlineData(LayoutPurpose.Align, OperationKinds.Align)]
    [InlineData(LayoutPurpose.Move, OperationKinds.Move)]
    public async Task A_layout_is_logged_with_the_kind_of_its_purpose_and_its_undo_and_redo_swap_the_cells(LayoutPurpose purpose, string kind)
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(3));
        var outcome = await project.Runner.RunAsync(
            new ApplyLayoutCommand(new LayoutRequest(project.RootMapId, Steps: [new StepPlacement(b, project.LaneId, 1)]), purpose), Token);
        Assert.Equal([b], outcome.Result);
        Assert.Equal(kind, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal(1, outcome.View!.Steps.Single(step => step.Id == b).Column);
        await project.UndoAsync();
        Assert.Equal((0, 3), (project.StepOf(a).Column, project.StepOf(b).Column));
        await project.RedoAsync();
        Assert.Equal((0, 1), (project.StepOf(a).Column, project.StepOf(b).Column));
    }

    [Fact]
    public async Task A_layout_places_the_cells_exactly_and_takes_the_lane_order_back_on_undo()
    {
        var project = new TestProject();
        var second = project.SecondLane();
        var (a, b) = (project.Step(0), project.Step(1));
        await project.Runner.RunAsync(new ApplyLayoutCommand(new LayoutRequest(project.RootMapId, [second, project.LaneId],
            [new StepPlacement(a, second, 1), new StepPlacement(b, second, 0)]), LayoutPurpose.Move), Token);
        Assert.Equal([second, project.LaneId], LaneOrder(project));
        Assert.Equal(((second, 1, 0), (second, 0, 0)), (project.CellOf(a), project.CellOf(b)));
        await project.UndoAsync();
        Assert.Equal([project.LaneId, second], LaneOrder(project));
        Assert.Equal(((project.LaneId, 0, 0), (project.LaneId, 1, 0)), (project.CellOf(a), project.CellOf(b)));
    }

    [Fact]
    public async Task A_layout_whose_final_state_puts_two_steps_in_one_cell_is_refused_and_writes_nothing()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(
            new ApplyLayoutCommand(new LayoutRequest(project.RootMapId, Steps: [new StepPlacement(a, project.LaneId, 1)]), LayoutPurpose.Align), Token));
        Assert.Equal(ErrorCodes.CellTaken, refusal.Code);
        Assert.Equal((0, 1), (project.StepOf(a).Column, project.StepOf(b).Column));
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_layout_of_a_step_of_another_map_is_refused_as_outside_the_map()
    {
        var project = new TestProject();
        var parent = project.Step(0);
        var (subMap, subLane) = project.SubMap(parent);
        var inner = project.Step(0, laneId: subLane, mapId: subMap);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(
            new ApplyLayoutCommand(new LayoutRequest(project.RootMapId, Steps: [new StepPlacement(inner, project.LaneId, 4)]), LayoutPurpose.Move), Token));
        Assert.Equal(ErrorCodes.StepOutsideMap, refusal.Code);
        Assert.Empty(project.Database.Entries);
    }

    [Theory]
    [InlineData(false, 4, StructureEdits.LaneField)]
    [InlineData(true, 10_001, "column")]
    public async Task A_layout_into_a_lane_of_another_map_or_out_of_the_grid_is_refused_on_its_field(bool ownLane, int column, string field)
    {
        var project = new TestProject();
        var step = project.Step(0);
        var (_, subLane) = project.SubMap(step);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(new ApplyLayoutCommand(
            new LayoutRequest(project.RootMapId, Steps: [new StepPlacement(step, ownLane ? project.LaneId : subLane, column)]), LayoutPurpose.Move), Token));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)field), (refusal.Code, refusal.Details["field"]));
        Assert.Equal((project.LaneId, 0, 0), project.CellOf(step));
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_drag_of_several_steps_is_one_move_operation_undone_and_redone_by_one_call()
    {
        var project = new TestProject();
        var second = project.SecondLane();
        var (a, b) = (project.Step(0), project.Step(1));
        var outcome = await project.Runner.RunAsync(new MoveStepsCommand(new GroupMoveRequest(project.RootMapId,
            [new StepPlacement(b, second, 6), new StepPlacement(a, second, 5)])), Token);
        Assert.Equal([a, b], outcome.Result);
        Assert.Equal(OperationKinds.Move, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal(((second, 5, 0), (second, 6, 0)), (project.CellOf(a), project.CellOf(b)));
        await project.UndoAsync();
        Assert.Equal(((project.LaneId, 0, 0), (project.LaneId, 1, 0)), (project.CellOf(a), project.CellOf(b)));
        await project.RedoAsync();
        Assert.Equal(((second, 5, 0), (second, 6, 0)), (project.CellOf(a), project.CellOf(b)));
    }

    [Fact]
    public async Task A_drag_onto_a_taken_cell_shifts_the_block_and_stays_one_operation()
    {
        var project = new TestProject();
        var (a, b, c) = (project.Step(0), project.Step(1), project.Step(4));
        await project.Runner.RunAsync(new MoveStepsCommand(new GroupMoveRequest(project.RootMapId,
            [new StepPlacement(a, project.LaneId, 4), new StepPlacement(b, project.LaneId, 5)])), Token);
        Assert.Equal((4, 5, 6), (project.StepOf(a).Column, project.StepOf(b).Column, project.StepOf(c).Column));
        Assert.Single(project.Database.Entries);
        await project.UndoAsync();
        Assert.Equal((0, 1, 4), (project.StepOf(a).Column, project.StepOf(b).Column, project.StepOf(c).Column));
    }

    [Fact]
    public async Task A_drag_that_moves_nothing_writes_no_entry()
    {
        var project = new TestProject();
        var a = project.Step(0);
        var outcome = await project.Runner.RunAsync(new MoveStepsCommand(new GroupMoveRequest(project.RootMapId, [new StepPlacement(a, project.LaneId, 0)])), Token);
        Assert.Empty(outcome.Result);
        Assert.Empty(project.Database.Entries);
    }
}
