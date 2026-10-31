// UndoRedoTests.cs
// Undo and redo through the template: stack order, entries of the log, refusal that clears the stacks (spec 4.6, AR-05).

using Topogenia.Application.OperationLog;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.OperationLog;

public sealed class UndoRedoTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<Topogenia.Application.Commands.CommandOutcome<JournalResult>> Undo(TestProject project) =>
        project.Runner.RunAsync(new UndoCommand(project.RootMapId), Token);

    private static Task<Topogenia.Application.Commands.CommandOutcome<JournalResult>> Redo(TestProject project) =>
        project.Runner.RunAsync(new RedoCommand(project.RootMapId), Token);

    private static List<string> LiveLanes(TestProject project) =>
        project.Database.All<Lane>().Where(lane => lane.DeletedAt is null).OrderBy(lane => lane.SortOrder).Select(lane => lane.Name).ToList();

    [Fact]
    public async Task An_undo_takes_back_the_operation_and_keeps_its_changes_in_the_entry()
    {
        var project = new TestProject();
        var done = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        var undone = await Undo(project);
        Assert.Equal(["Acteur 1"], LiveLanes(project));
        Assert.Equal(["Acteur 1"], undone.View!.Lanes.Select(lane => lane.Name));
        Assert.Equal(new JournalResult(done.OperationId!.Value, "lane.add"), undone.Result);
        Assert.Equal(done.OperationId, undone.OperationId);
        var entry = Assert.Single(project.Database.Entries);
        Assert.Equal(TestProject.Now, entry.UndoneAt);
        var inverse = ChangeSetJson.Read(entry.InverseJson);
        Assert.Contains(inverse.Changes, change => change.After is Lane { Name: "Achats", DeletedAt: not null });
    }

    [Fact]
    public async Task A_redo_brings_the_operation_back_in_a_new_entry_of_its_chain()
    {
        var project = new TestProject();
        var done = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        await Undo(project);
        var redone = await Redo(project);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
        Assert.Equal(done.OperationId, redone.OperationId);
        Assert.Equal(2, project.Database.Entries.Count);
        var redo = project.Database.Entries[^1];
        Assert.Equal((done.OperationId, 1, "lane.add", (DateTime?)null), (redo.RedoneFromId, redo.Sequence, redo.Kind, redo.UndoneAt));
        Assert.Equal(OperationRecorder.NotUndoneYet, redo.InverseJson);
    }

    [Fact]
    public async Task An_undo_after_a_redo_undoes_the_redo_and_a_second_redo_brings_it_back()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        await Undo(project);
        await Redo(project);
        await Undo(project);
        Assert.Equal(["Acteur 1"], LiveLanes(project));
        Assert.All(project.Database.Entries, entry => Assert.NotNull(entry.UndoneAt));
        await Redo(project);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
        Assert.Equal(3, project.Database.Entries.Count);
        Assert.All(project.Database.Entries.Skip(1), entry => Assert.Equal(project.Database.Entries[0].Id, entry.RedoneFromId));
    }

    [Fact]
    public async Task Undo_goes_down_the_stack_and_redo_comes_back_up_in_order()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Ventes"), Token);
        await Undo(project);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
        await Undo(project);
        Assert.Equal(["Acteur 1"], LiveLanes(project));
        await Redo(project);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
        await Redo(project);
        Assert.Equal(["Acteur 1", "Achats", "Ventes"], LiveLanes(project));
    }

    [Fact]
    public async Task A_redo_undone_again_keeps_its_place_and_is_the_next_one_redone()
    {
        var project = new TestProject();
        var first = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        var second = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Ventes"), Token);
        await Undo(project);
        await Undo(project);
        await Redo(project);
        var undoneAgain = await Undo(project);
        // The redo of the first operation has the highest sequence, yet the first one is still below the second in the stack.
        Assert.Equal([first.OperationId!.Value, second.OperationId!.Value], undoneAgain.History!.Redo.Select(item => item.OperationId));
        var redone = await Redo(project);
        Assert.Equal(first.OperationId, redone.OperationId);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
        await Redo(project);
        Assert.Equal(["Acteur 1", "Achats", "Ventes"], LiveLanes(project));
    }

    [Fact]
    public async Task The_batch_of_an_undo_belongs_to_the_operation_undone_so_that_its_purge_reaches_the_operation()
    {
        var project = new TestProject();
        var done = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        await Undo(project);
        var batch = Assert.Single(project.Database.All<DeletionBatch>());
        Assert.Equal(done.OperationId, batch.OperationId);
        Assert.Contains(project.Database.Entries, entry => entry.Id == batch.OperationId);
    }

    [Fact]
    public async Task Every_reply_carries_what_the_user_can_undo_and_redo_on_the_map_the_next_one_first()
    {
        var project = new TestProject();
        var first = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        var second = await project.Runner.RunAsync(new AddStepProbe(project.RootMapId, project.LaneId, 0), Token);
        Assert.Equal([second.OperationId!.Value, first.OperationId!.Value], second.History!.Undo.Select(item => item.OperationId));
        Assert.Empty(second.History.Redo);
        var undone = await Undo(project);
        Assert.Equal([new HistoryItem(first.OperationId.Value, "lane.add", TestProject.Now)], undone.History!.Undo);
        Assert.Equal([new HistoryItem(second.OperationId.Value, "step.create", TestProject.Now)], undone.History.Redo);
        project.ActingAs(project.Admin);
        var other = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Ventes"), Token);
        Assert.Equal([other.OperationId!.Value], other.History!.Undo.Select(item => item.OperationId));
        Assert.Empty(other.History.Redo);
    }

    [Fact]
    public async Task An_empty_stack_is_not_found_and_nothing_is_written()
    {
        var project = new TestProject();
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => Undo(project))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => Redo(project))).Code);
        Assert.Equal(0, project.Project.StructureVersion);
        Assert.Equal(0, project.Database.Commits);
    }

    [Fact]
    public async Task A_user_never_undoes_the_operation_of_another_user()
    {
        var project = new TestProject();
        project.ActingAs(project.Admin);
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        project.ActingAs(project.Facilitator);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => Undo(project))).Code);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
        Assert.Null(Assert.Single(project.Database.Entries).UndoneAt);
    }

    [Fact]
    public async Task A_new_operation_after_an_undo_leaves_nothing_to_redo()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        await Undo(project);
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Ventes"), Token);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => Redo(project))).Code);
        Assert.Equal(["Acteur 1", "Ventes"], LiveLanes(project));
        Assert.Single(project.Database.Entries);
    }

    [Fact]
    public async Task An_outdated_undo_is_refused_with_its_author_and_clears_only_the_stacks_of_the_user_on_the_map()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(Support.Commands.Rename(project.LaneId, "Achats", version: 1), Token);
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Ventes"), Token);
        await Undo(project);
        project.ActingAs(project.Admin);
        project.Clock.UtcNow = TestProject.Now.AddMinutes(5);
        await project.Runner.RunAsync(Support.Commands.Rename(project.LaneId, "Comptabilité", version: 2), Token);
        project.ActingAs(project.Facilitator);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => Undo(project));

        Assert.Equal(ErrorCodes.OperationOutdated, refusal.Code);
        Assert.Equal(project.Admin.Id, refusal.Details["updated_by"]);
        Assert.Equal(TestProject.Now.AddMinutes(5), refusal.Details["updated_at"]);
        Assert.Equal("Comptabilité", project.Database.Get<Lane>(Lane.KeyOf(project.LaneId)).Name);
        Assert.Equal([project.Admin.Id], project.Database.Entries.Select(entry => entry.UserId));
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => Redo(project))).Code);
    }

    /// <summary>
    /// Annex B 2.6-48, AR-05 (1.x `magasin.ts`, `vider()` on the failure of `retablir`): the refusal of a redo clears the
    /// redo stack and the undo stack of the user on the map, and changes nothing else on the map.
    /// </summary>
    [Fact]
    public async Task An_outdated_redo_is_refused_with_its_author_and_clears_the_undo_and_the_redo_stacks_of_the_user_on_the_map()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Ventes"), Token);
        await project.Runner.RunAsync(Support.Commands.Rename(project.LaneId, "Achats", version: 1), Token);
        var undone = await Undo(project);
        Assert.Equal((1, 1), (undone.History!.Undo.Count, undone.History.Redo.Count));
        project.ActingAs(project.Admin);
        project.Clock.UtcNow = TestProject.Now.AddMinutes(5);
        var version = project.Database.Get<Lane>(Lane.KeyOf(project.LaneId)).Version;
        await project.Runner.RunAsync(Support.Commands.Rename(project.LaneId, "Comptabilité", version), Token);
        project.ActingAs(project.Facilitator);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => Redo(project));

        Assert.Equal(ErrorCodes.OperationOutdated, refusal.Code);
        Assert.Equal(project.Admin.Id, refusal.Details["updated_by"]);
        Assert.Equal(TestProject.Now.AddMinutes(5), refusal.Details["updated_at"]);
        Assert.Equal(["Comptabilité", "Ventes"], LiveLanes(project));
        Assert.Equal([project.Admin.Id], project.Database.Entries.Select(entry => entry.UserId));
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => Redo(project))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => Undo(project))).Code);
        Assert.Equal(["Comptabilité", "Ventes"], LiveLanes(project));
    }

    [Fact]
    public async Task An_undo_that_would_break_the_map_is_refused_as_map_modified_and_clears_the_stacks()
    {
        var project = new TestProject();
        var lane = (await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token)).Result.Id;
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Ventes"), Token);
        await Undo(project);
        project.ActingAs(project.Admin);
        await project.Runner.RunAsync(new AddStepProbe(project.RootMapId, lane, 0), Token);
        project.ActingAs(project.Facilitator);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => Undo(project));

        Assert.Equal(ErrorCodes.MapModified, refusal.Code);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
        Assert.Equal([project.Admin.Id], project.Database.Entries.Select(entry => entry.UserId));
    }

    [Fact]
    public async Task A_refusal_of_the_right_never_clears_a_stack()
    {
        var project = new TestProject();
        project.Database.Add(project.Database.All<ProjectMember>().Single(member => member.UserId == project.Reader.Id)
            with { Role = Domain.Values.MemberRole.Facilitator });
        project.ActingAs(project.Reader);
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        project.Database.Add(project.Database.All<ProjectMember>().Single(member => member.UserId == project.Reader.Id)
            with { Role = Domain.Values.MemberRole.Reader });
        Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() => Undo(project))).Code);
        Assert.Single(project.Database.Entries);
    }

    [Fact]
    public async Task An_undo_run_again_after_a_failed_commit_undoes_once()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        project.Database.CommitFailures.Enqueue(new TimeoutException("deadlock"));
        await Undo(project);
        Assert.Equal(["Acteur 1"], LiveLanes(project));
        Assert.Single(project.Database.All<DeletionBatch>());
        Assert.NotNull(Assert.Single(project.Database.Entries).UndoneAt);
    }

    [Fact]
    public async Task The_undo_of_a_deletion_restores_the_lane_and_its_redo_deletes_it_again()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        await project.Runner.RunAsync(new DeleteLaneProbe(project.LaneId), Token);
        Assert.Equal(["Achats"], LiveLanes(project));
        await Undo(project);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
        await Redo(project);
        Assert.Equal(["Achats"], LiveLanes(project));
    }
}
