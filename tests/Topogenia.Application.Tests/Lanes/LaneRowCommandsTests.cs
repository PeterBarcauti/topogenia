// LaneRowCommandsTests.cs
// Rows inserted into a lane and an empty row taken out of it through the template (3.1, lot G, ADR 096): one operation of the log each, its steps and the passage along its rows undone and redone together; a row that holds something refused, nothing logged.

using Topogenia.Application.Lanes;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.Lanes;

public sealed class LaneRowCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Two steps on the row 0 of the first lane, joined by a flow pinned to the middle of its row 1.</summary>
    private static (TestProject Project, Guid First, Guid Second, Guid Flow) Pinned()
    {
        var project = new TestProject();
        var first = project.Step(0);
        var second = project.Step(2);
        var flow = project.NextId();
        project.Database.Add(new Flow(flow, project.RootMapId, first, second, null, TestProject.Now, project.Facilitator.Id, null, null, null, null, 1)
        {
            ViaLaneId = project.LaneId,
            ViaRow = 1,
        });
        return (project, first, second, flow);
    }

    private static (int, int, int?) RowsOf(TestProject project, Guid first, Guid second, Guid flow) =>
        (project.Database.Get<Step>(Step.KeyOf(first)).Row, project.Database.Get<Step>(Step.KeyOf(second)).Row, project.Database.Get<Flow>(Flow.KeyOf(flow)).ViaRow);

    private static Task Undo(TestProject project) => project.Runner.RunAsync(new UndoCommand(project.RootMapId), Token);

    private static Task Redo(TestProject project) => project.Runner.RunAsync(new RedoCommand(project.RootMapId), Token);

    [Fact]
    public async Task A_row_inserted_at_the_top_is_one_operation_its_steps_and_pinned_passage_going_down_undone_and_redone_together()
    {
        var (project, first, second, flow) = Pinned();
        var outcome = await project.Runner.RunAsync(new InsertRowsCommand(new InsertRowsRequest(project.LaneId, 0)), Token);
        Assert.Equal([first, second], outcome.Result.Order());
        Assert.Equal(OperationKinds.InsertRows, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal((1, 1, 2), RowsOf(project, first, second, flow));
        Assert.Equal(project.LaneId, project.Database.Get<Flow>(Flow.KeyOf(flow)).ViaLaneId);
        await Undo(project);
        Assert.Equal((0, 0, 1), RowsOf(project, first, second, flow));
        await Redo(project);
        Assert.Equal((1, 1, 2), RowsOf(project, first, second, flow));
    }

    [Fact]
    public async Task An_empty_row_taken_out_is_one_operation_what_is_under_it_coming_up_and_going_back_on_undo()
    {
        var (project, first, second, flow) = Pinned();
        await project.Runner.RunAsync(new InsertRowsCommand(new InsertRowsRequest(project.LaneId, 0)), Token);
        await project.Runner.RunAsync(new RemoveRowCommand(new RemoveRowRequest(project.LaneId, 0)), Token);
        Assert.Equal(OperationKinds.RemoveRow, project.Database.Entries[^1].Kind);
        Assert.Equal((0, 0, 1), RowsOf(project, first, second, flow));
        await Undo(project);
        Assert.Equal((1, 1, 2), RowsOf(project, first, second, flow));
    }

    [Fact]
    public async Task A_row_that_holds_a_step_or_a_passage_is_refused_and_nothing_is_logged()
    {
        var (project, _, _, _) = Pinned();
        foreach (var row in new[] { 0, 1 })
        {
            var refusal = await Assert.ThrowsAsync<DomainException>(() =>
                project.Runner.RunAsync(new RemoveRowCommand(new RemoveRowRequest(project.LaneId, row)), Token));
            Assert.Equal((ErrorCodes.RowNotEmpty, RefusalKind.Conflict), (refusal.Code, refusal.Kind));
        }
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task Rows_inserted_where_nothing_goes_down_change_nothing_and_log_nothing()
    {
        var (project, _, _, _) = Pinned();
        var outcome = await project.Runner.RunAsync(new InsertRowsCommand(new InsertRowsRequest(project.LaneId, 5, 2)), Token);
        Assert.Empty(outcome.Result);
        Assert.Empty(project.Database.Entries);
    }
}
