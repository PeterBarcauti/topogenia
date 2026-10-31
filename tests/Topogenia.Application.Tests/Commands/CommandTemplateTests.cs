// CommandTemplateTests.cs
// The order and the guarantees of the command template: lock, load, If-Match, domain, writing, log, reply (spec 3.4, 4.6, 4.7).

using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.Commands;

public sealed class CommandTemplateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_steps_run_in_the_order_of_the_template_inside_one_unit()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        Assert.Equal(["access", "lock", "load", "save", "log"], project.Database.Calls);
        Assert.Equal(1, project.Database.Commits);
    }

    [Fact]
    public async Task The_lock_moves_the_structure_version_on_and_the_reply_carries_it()
    {
        var project = new TestProject();
        var first = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        var second = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Ventes"), Token);
        Assert.Equal(1, first.StructureVersion);
        Assert.Equal(2, second.StructureVersion);
        Assert.Equal(2, project.Project.StructureVersion);
        Assert.Equal(2, second.View!.StructureVersion);
    }

    [Fact]
    public async Task The_workspace_holds_what_the_command_and_the_view_need()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        Assert.Equal([WorkspaceContent.Sheets | WorkspaceContent.Documents | WorkspaceContent.Translations], project.Database.LoadedContents);
    }

    [Fact]
    public async Task The_reply_carries_the_result_the_operation_and_the_view_up_to_date()
    {
        var project = new TestProject();
        var outcome = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        Assert.Equal("PRJ-C02", outcome.Result.Code);
        Assert.NotNull(outcome.OperationId);
        Assert.Equal(["Acteur 1", "Achats"], outcome.View!.Lanes.Select(lane => lane.Name));
        Assert.Contains(outcome.View.Lanes, lane => lane.Id == outcome.Result.Id);
        Assert.Empty(outcome.Warnings);
    }

    [Fact]
    public async Task A_stale_if_match_is_a_version_conflict_with_its_author_and_nothing_is_written()
    {
        var project = new TestProject();
        var lane = project.Database.Get<Lane>(Lane.KeyOf(project.LaneId));
        var changedAt = TestProject.Now.AddMinutes(-3);
        project.Database.Add(lane with { Version = 2, UpdatedAt = changedAt, UpdatedBy = project.Contributor.Id });
        var command = Support.Commands.Rename(project.LaneId, "Achats", version: 1);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(command, Token));
        Assert.Equal(ErrorCodes.VersionConflict, refusal.Code);
        Assert.Equal(RefusalKind.Conflict, refusal.Kind);
        Assert.Equal(project.Contributor.Id, refusal.Details["updated_by"]);
        Assert.Equal(changedAt, refusal.Details["updated_at"]);
        Assert.False(command.Ran);
        Assert.Equal("Acteur 1", project.Database.Get<Lane>(Lane.KeyOf(project.LaneId)).Name);
        Assert.Empty(project.Database.Entries);
        Assert.Equal(0, project.Project.StructureVersion);
    }

    [Fact]
    public async Task A_request_without_the_version_its_mode_requires_is_refused_428_after_the_rights_and_before_the_domain()
    {
        var project = new TestProject();
        project.Versions.IsMissing = true;
        async Task<DomainException> RefusedAsync(CurrentUser user, RenameLaneProbe command)
        {
            project.CurrentUser.User = user;
            return await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(command, Token));
        }
        Assert.Equal(RefusalKind.NotFound, (await RefusedAsync(project.Stranger, new RenameLaneProbe(project.LaneId, "Achats"))).Kind);
        Assert.Equal(RefusalKind.Forbidden, (await RefusedAsync(project.Reader, new RenameLaneProbe(project.LaneId, "Achats"))).Kind);
        var command = new RenameLaneProbe(project.LaneId, "Achats");
        var refusal = await RefusedAsync(project.Facilitator, command);
        Assert.Equal((ErrorCodes.VersionRequired, RefusalKind.PreconditionRequired), (refusal.Code, refusal.Kind));
        Assert.False(command.Ran);
        Assert.Equal("Acteur 1", project.Database.Get<Lane>(Lane.KeyOf(project.LaneId)).Name);
        Assert.Empty(project.Database.Entries);
        Assert.Equal(0, project.Project.StructureVersion);
        project.Versions.IsMissing = false;
        await project.Runner.RunAsync(new RenameLaneProbe(project.LaneId, "Achats"), Token);
        Assert.Equal("Achats", project.Database.Get<Lane>(Lane.KeyOf(project.LaneId)).Name);
    }

    [Fact]
    public async Task The_version_of_no_record_is_a_conflict_on_a_record_that_exists()
    {
        var project = new TestProject();
        var command = Support.Commands.Rename(project.LaneId, "Achats", version: VersionCheck.NoRecord);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(command, Token));
        Assert.Equal(ErrorCodes.VersionConflict, refusal.Code);
        Assert.False(command.Ran);
    }

    [Fact]
    public async Task An_if_match_on_a_record_never_changed_names_its_creator()
    {
        var project = new TestProject();
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(Support.Commands.Rename(project.LaneId, "Achats", version: 7), Token));
        Assert.Equal(project.Facilitator.Id, refusal.Details["updated_by"]);
        Assert.Equal(TestProject.Now, refusal.Details["updated_at"]);
    }

    [Fact]
    public async Task A_current_if_match_lets_the_command_run_and_moves_the_version_on()
    {
        var project = new TestProject();
        var outcome = await project.Runner.RunAsync(Support.Commands.Rename(project.LaneId, "Achats", version: 1), Token);
        var lane = project.Database.Get<Lane>(Lane.KeyOf(project.LaneId));
        Assert.Equal(("Achats", 2, project.Facilitator.Id), (lane.Name, lane.Version, lane.UpdatedBy));
        Assert.Equal(2, outcome.View!.Lanes.Single().Version);
    }

    [Fact]
    public async Task A_refusal_of_the_domain_writes_nothing_and_logs_nothing()
    {
        var project = new TestProject();
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new AddLaneThenRefuseCommand(project.RootMapId), Token));
        Assert.Equal(ErrorCodes.LastLane, refusal.Code);
        Assert.Single(project.Database.All<Lane>());
        Assert.Empty(project.Database.Entries);
        Assert.Equal(0, project.Project.StructureVersion);
        Assert.Equal(0, project.Database.Commits);
    }

    [Fact]
    public async Task A_command_that_changes_nothing_writes_nothing_and_logs_nothing()
    {
        var project = new TestProject();
        var outcome = await project.Runner.RunAsync(new ReadOnlyCommand(AccessTarget.Map(project.RootMapId)), Token);
        Assert.Null(outcome.OperationId);
        Assert.DoesNotContain("save", project.Database.Calls);
        Assert.Empty(project.Database.Entries);
        Assert.Null(outcome.View);
    }

    [Fact]
    public async Task A_command_that_cannot_be_undone_is_written_but_not_logged()
    {
        var project = new TestProject();
        var outcome = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats", kind: null), Token);
        Assert.Null(outcome.OperationId);
        Assert.Equal(2, project.Database.All<Lane>().Count());
        Assert.Empty(project.Database.Entries);
        Assert.NotNull(outcome.View);
    }

    [Fact]
    public async Task A_unit_run_again_after_a_failed_commit_does_the_work_once_with_a_new_operation()
    {
        var project = new TestProject();
        project.Database.CommitFailures.Enqueue(new TimeoutException("deadlock"));
        var command = new AddLaneProbe(project.RootMapId, "Achats");
        var outcome = await project.Runner.RunAsync(command, Token);
        Assert.Equal(2, command.Runs);
        Assert.Equal(2, command.OperationIds.Distinct().Count());
        Assert.Equal(2, project.Database.All<Lane>().Count());
        var entry = Assert.Single(project.Database.Entries);
        Assert.Equal(command.OperationIds[1], entry.Id);
        Assert.Equal(entry.Id, outcome.OperationId);
        Assert.Equal(1, project.Project.StructureVersion);
    }

    [Fact]
    public async Task The_entry_of_an_operation_holds_its_changes_and_carries_its_identifier()
    {
        var project = new TestProject();
        var outcome = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        var entry = Assert.Single(project.Database.Entries);
        Assert.Equal((outcome.OperationId!.Value, project.ProjectId, project.RootMapId, project.Facilitator.Id, "lane.add", 0),
            (entry.Id, entry.ProjectId, entry.MapId, entry.UserId, entry.Kind, entry.Sequence));
        Assert.Equal((TestProject.Now, (DateTime?)null, (Guid?)null), (entry.CreatedAt, entry.UndoneAt, entry.RedoneFromId));
        Assert.Equal(OperationRecorder.NotUndoneYet, entry.InverseJson);
        var forward = ChangeSetJson.Read(entry.ForwardJson);
        var created = Assert.Single(forward.Created<Lane>());
        Assert.Equal(project.Database.Get<Lane>(Lane.KeyOf(outcome.Result.Id)), created);
        Assert.Contains(forward.Changes, change => change.After is ProjectCounter { Counter: Domain.Values.CounterKind.Lane, LastValue: 2 });
        Assert.Contains($"\"key\":\"{outcome.Result.Id:N}\"", entry.TouchedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_batch_of_a_deletion_carries_the_identifier_of_the_logged_operation()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        var outcome = await project.Runner.RunAsync(new DeleteLaneProbe(project.LaneId), Token);
        var batch = Assert.Single(project.Database.All<DeletionBatch>());
        Assert.Equal(outcome.OperationId, batch.OperationId);
        Assert.Equal(["Achats"], outcome.View!.Lanes.Select(lane => lane.Name));
    }

    [Fact]
    public async Task The_sequence_follows_the_map_across_users()
    {
        var project = new TestProject();
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), Token);
        project.ActingAs(project.Admin);
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Ventes"), Token);
        Assert.Equal([(0, project.Facilitator.Id), (1, project.Admin.Id)],
            project.Database.Entries.Select(entry => (entry.Sequence, entry.UserId)));
    }
}
