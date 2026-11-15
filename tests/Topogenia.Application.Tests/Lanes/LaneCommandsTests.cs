// LaneCommandsTests.cs
// The lane commands through the template: written, logged with the 1.x kinds, undone and redone (annex A 2.3, annex B 2.6-33).

using Topogenia.Application.Lanes;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Structure;

namespace Topogenia.Application.Tests.Lanes;

public sealed class LaneCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static List<string> LiveLanes(TestProject project) =>
        project.Database.All<Lane>().Where(lane => lane.DeletedAt is null && lane.MapId == project.RootMapId)
            .OrderBy(lane => lane.SortOrder).Select(lane => lane.Name).ToList();

    private static async Task UndoAsync(TestProject project) => await project.Runner.RunAsync(new UndoCommand(project.RootMapId), Token);

    private static async Task RedoAsync(TestProject project) => await project.Runner.RunAsync(new RedoCommand(project.RootMapId), Token);

    [Fact]
    public async Task An_added_lane_is_logged_with_the_1x_kind_of_a_lane_creation_and_can_be_undone_and_redone()
    {
        var project = new TestProject();
        var outcome = await project.Runner.RunAsync(new AddLaneCommand(new AddLaneRequest(project.RootMapId, "Achats")), Token);
        Assert.Equal(OperationKinds.CreateLane, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal(["Acteur 1", "Achats"], outcome.View!.Lanes.Select(lane => lane.Name));
        await UndoAsync(project);
        Assert.Equal(["Acteur 1"], LiveLanes(project));
        await RedoAsync(project);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
    }

    [Fact]
    public async Task The_actor_of_a_lane_must_be_a_member_of_the_project()
    {
        var project = new TestProject();
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(
            new AddLaneCommand(new AddLaneRequest(project.RootMapId, "Achats", ActorUserId: project.Stranger.Id)), Token));
        Assert.Equal((ErrorCodes.InvalidValue, LaneOperations.ActorField), (refusal.Code, refusal.Details["field"]));
        var outcome = await project.Runner.RunAsync(
            new AddLaneCommand(new AddLaneRequest(project.RootMapId, "Achats", ActorUserId: project.Contributor.Id)), Token);
        Assert.Equal(project.Contributor.Id, outcome.View!.Lanes.Single(lane => lane.Id == outcome.Result.Id).ActorUserId);
    }

    [Fact]
    public async Task An_updated_lane_checks_the_version_read_and_is_logged_with_the_1x_kind_of_a_lane_update()
    {
        var project = new TestProject();
        var stale = UpdateLaneCommand.WithVersion(new UpdateLaneRequest(project.LaneId) { Name = new FieldChange<string?>("Achats") }, 4);
        Assert.Equal(ErrorCodes.VersionConflict, (await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(stale, Token))).Code);
        var current = UpdateLaneCommand.WithVersion(new UpdateLaneRequest(project.LaneId) { Name = new FieldChange<string?>("Achats") }, 1);
        await project.Runner.RunAsync(current, Token);
        Assert.Equal(OperationKinds.UpdateLane, Assert.Single(project.Database.Entries).Kind);
        await UndoAsync(project);
        Assert.Equal(["Acteur 1"], LiveLanes(project));
        await RedoAsync(project);
        Assert.Equal(["Achats"], LiveLanes(project));
    }

    [Fact]
    public async Task A_lane_updated_without_if_match_is_not_checked()
    {
        var project = new TestProject();
        var command = UpdateLaneCommand.WithVersion(new UpdateLaneRequest(project.LaneId) { Name = new FieldChange<string?>("Achats") }, null);
        Assert.Empty(command.Versions);
        await project.Runner.RunAsync(command, Token);
        Assert.Equal(["Achats"], LiveLanes(project));
    }

    [Fact]
    public async Task The_order_of_the_lanes_is_logged_with_the_1x_kind_of_a_lane_order_and_comes_back_on_undo()
    {
        var project = new TestProject();
        var second = (await project.Runner.RunAsync(new AddLaneCommand(new AddLaneRequest(project.RootMapId, "Achats")), Token)).Result.Id;
        var outcome = await project.Runner.RunAsync(new ReorderLanesCommand(project.RootMapId, [second, project.LaneId]), Token);
        Assert.Equal([second, project.LaneId], outcome.Result);
        Assert.Equal(OperationKinds.LaneOrder, project.Database.Entries[^1].Kind);
        await UndoAsync(project);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
        await RedoAsync(project);
        Assert.Equal(["Achats", "Acteur 1"], LiveLanes(project));
    }

    [Fact]
    public async Task An_incomplete_order_is_refused()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneCommand(new AddLaneRequest(project.RootMapId, "Achats")), Token);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new ReorderLanesCommand(project.RootMapId, [project.LaneId]), Token));
        Assert.Equal(ErrorCodes.InvalidLaneOrder, refusal.Code);
    }

    [Fact]
    public async Task A_lane_with_steps_asks_for_a_choice_and_its_deletion_with_them_is_undone_whole()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneCommand(new AddLaneRequest(project.RootMapId, "Achats")), Token);
        var step = project.Step(0);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new DeleteLaneCommand(new DeleteLaneRequest(project.LaneId)), Token));
        Assert.Equal((ErrorCodes.LaneNotEmpty, (object?)1), (refusal.Code, refusal.Details["step_count"]));
        await project.Runner.RunAsync(new DeleteLaneCommand(new DeleteLaneRequest(project.LaneId, LaneContentChoice.Delete)), Token);
        Assert.NotNull(project.Database.Get<Step>(Step.KeyOf(step)).DeletedAt);
        Assert.Equal(OperationKinds.Delete, project.Database.Entries[^1].Kind);
        await UndoAsync(project);
        Assert.Null(project.Database.Get<Step>(Step.KeyOf(step)).DeletedAt);
        Assert.Equal(["Acteur 1", "Achats"], LiveLanes(project));
    }

    [Fact]
    public async Task The_last_lane_of_a_map_is_never_deleted()
    {
        var project = new TestProject();
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new DeleteLaneCommand(new DeleteLaneRequest(project.LaneId)), Token));
        Assert.Equal(ErrorCodes.LastLane, refusal.Code);
    }
}
