// CommandRightsTests.cs
// The declared rights of the template: 404 for a non-member, 403 for a weaker role, the administrator as facilitator (annex A 1).

using Topogenia.Application.Authorization;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Commands;

public sealed class CommandRightsTests
{
    private static async Task<DomainException> Refused<T>(TestProject project, Topogenia.Application.Commands.ProjectCommand<T> command) =>
        await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(command, TestContext.Current.CancellationToken));

    [Fact]
    public async Task An_anonymous_request_is_refused_before_any_unit_of_work()
    {
        var project = new TestProject();
        project.CurrentUser.User = null;
        var refusal = await Refused(project, new AddLaneProbe(project.RootMapId, "Achats"));
        Assert.Equal(ErrorCodes.NotAuthenticated, refusal.Code);
        Assert.Empty(project.Database.Calls);
        Assert.Equal(0, project.Unit.Attempts);
    }

    [Fact]
    public async Task A_command_that_declares_no_right_is_never_run()
    {
        var project = new TestProject();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            project.Runner.RunAsync(new UndeclaredCommand(project.ProjectId), TestContext.Current.CancellationToken));
        Assert.Empty(project.Database.Calls);
    }

    [Fact]
    public void Every_command_of_the_tests_declares_exactly_one_right()
    {
        var commands = typeof(AddLaneProbe).Assembly.GetTypes()
            .Where(type => type.BaseType is { IsGenericType: true } baseType
                           && baseType.GetGenericTypeDefinition() == typeof(Topogenia.Application.Commands.ProjectCommand<>)
                           && type != typeof(UndeclaredCommand));
        Assert.All(commands, type => Assert.IsAssignableFrom<AccessRequirementAttribute>(AccessPolicy.RequirementOf(type)));
    }

    [Fact]
    public async Task A_non_member_gets_not_found_and_the_project_is_neither_locked_nor_loaded()
    {
        var project = new TestProject();
        project.ActingAs(project.Stranger);
        var refusal = await Refused(project, new AddLaneProbe(project.RootMapId, "Achats"));
        Assert.Equal(ErrorCodes.NotFound, refusal.Code);
        Assert.Equal(RefusalKind.NotFound, refusal.Kind);
        Assert.Equal(["access"], project.Database.Calls);
        Assert.Equal(0, project.Project.StructureVersion);
    }

    [Fact]
    public async Task A_non_member_gets_not_found_even_for_a_command_open_to_readers()
    {
        var project = new TestProject();
        project.ActingAs(project.Stranger);
        var refusal = await Refused(project, new ReadOnlyCommand(AccessTarget.Map(project.RootMapId)));
        Assert.Equal(ErrorCodes.NotFound, refusal.Code);
    }

    [Theory]
    [InlineData(TargetKind.Map)]
    [InlineData(TargetKind.Lane)]
    [InlineData(TargetKind.Step)]
    [InlineData(TargetKind.Project)]
    public async Task An_unknown_target_is_not_found(TargetKind kind)
    {
        var project = new TestProject();
        var refusal = await Refused(project, new ReadOnlyCommand(new AccessTarget(kind, Guid.NewGuid())));
        Assert.Equal(ErrorCodes.NotFound, refusal.Code);
    }

    [Fact]
    public async Task A_reader_and_a_contributor_are_forbidden_a_facilitator_command()
    {
        var project = new TestProject();
        foreach (var user in new[] { project.Reader, project.Contributor })
        {
            project.ActingAs(user);
            var refusal = await Refused(project, new AddLaneProbe(project.RootMapId, "Achats"));
            Assert.Equal(ErrorCodes.AccessDenied, refusal.Code);
            Assert.Equal(RefusalKind.Forbidden, refusal.Kind);
        }
        Assert.Single(project.Database.All<Lane>());
        Assert.Equal(0, project.Project.StructureVersion);
    }

    [Fact]
    public async Task A_reader_may_run_a_reader_command()
    {
        var project = new TestProject();
        project.ActingAs(project.Reader);
        var command = new ReadOnlyCommand(AccessTarget.Map(project.RootMapId));
        await project.Runner.RunAsync(command, TestContext.Current.CancellationToken);
        Assert.True(command.Ran);
    }

    [Fact]
    public async Task An_administrator_who_is_not_a_member_acts_as_facilitator()
    {
        var project = new TestProject();
        project.ActingAs(project.Admin);
        var outcome = await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), TestContext.Current.CancellationToken);
        Assert.Equal(2, project.Database.All<Lane>().Count());
        Assert.Equal(new Topogenia.Application.Maps.MapRightsView(true, MemberRole.Facilitator), outcome.View!.Rights);
    }

    [Fact]
    public async Task A_project_in_the_trash_is_not_found_by_a_live_command_even_for_an_administrator()
    {
        var project = new TestProject();
        project.PutInTrash();
        foreach (var user in new[] { project.Facilitator, project.Admin })
        {
            project.ActingAs(user);
            var refusal = await Refused(project, new ReadOnlyCommand(AccessTarget.Project(project.ProjectId)));
            Assert.Equal(ErrorCodes.NotFound, refusal.Code);
        }
    }

    [Fact]
    public async Task A_command_of_the_trash_reaches_only_a_project_in_the_trash()
    {
        var project = new TestProject();
        var live = await Refused(project, new FacilitatorInTrashCommand(project.ProjectId));
        Assert.Equal(ErrorCodes.NotFound, live.Code);
        project.PutInTrash();
        Assert.True((await project.Runner.RunAsync(new FacilitatorInTrashCommand(project.ProjectId), TestContext.Current.CancellationToken)).Result);
        project.ActingAs(project.Reader);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project, new FacilitatorInTrashCommand(project.ProjectId))).Code);
    }

    [Fact]
    public async Task An_administrator_command_is_forbidden_to_a_facilitator_and_open_to_an_administrator()
    {
        var project = new TestProject();
        var refusal = await Refused(project, new AdminCommand(project.ProjectId));
        Assert.Equal(ErrorCodes.AccessDenied, refusal.Code);
        Assert.Empty(project.Database.Calls);
        project.ActingAs(project.Admin);
        var command = new AdminCommand(project.ProjectId);
        await project.Runner.RunAsync(command, TestContext.Current.CancellationToken);
        Assert.True(command.Ran);
    }

    [Fact]
    public async Task An_administrator_command_on_a_missing_project_is_not_found()
    {
        var project = new TestProject();
        project.ActingAs(project.Admin);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new AdminCommand(Guid.NewGuid()))).Code);
    }

    [Fact]
    public async Task The_facilitator_fills_every_sheet()
    {
        var project = new TestProject();
        var command = new FillSheetCommand(project.Step(0));
        await project.Runner.RunAsync(command, TestContext.Current.CancellationToken);
        Assert.True(command.Ran);
    }

    [Fact]
    public async Task A_contributor_fills_the_sheet_of_a_step_assigned_to_him_and_no_other()
    {
        var project = new TestProject();
        var assigned = project.Step(0, assignee: project.Contributor.Id);
        var other = project.Step(1);
        project.ActingAs(project.Contributor);
        var command = new FillSheetCommand(assigned);
        await project.Runner.RunAsync(command, TestContext.Current.CancellationToken);
        Assert.True(command.Ran);
        var refused = new FillSheetCommand(other);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project, refused)).Code);
        Assert.False(refused.Ran);
    }

    [Fact]
    public async Task A_contributor_fills_the_sheets_of_the_lane_he_holds()
    {
        var project = new TestProject();
        var lane = project.Database.Get<Lane>(Lane.KeyOf(project.LaneId));
        project.Database.Add(lane with { ActorUserId = project.Contributor.Id });
        var step = project.Step(0);
        project.ActingAs(project.Contributor);
        var command = new FillSheetCommand(step);
        await project.Runner.RunAsync(command, TestContext.Current.CancellationToken);
        Assert.True(command.Ran);
    }

    [Fact]
    public async Task A_reader_never_fills_a_sheet_even_when_the_step_is_assigned_to_him()
    {
        var project = new TestProject();
        var step = project.Step(0, assignee: project.Reader.Id);
        project.ActingAs(project.Reader);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project, new FillSheetCommand(step))).Code);
    }

    [Fact]
    public async Task The_sheet_of_a_deleted_step_is_not_found()
    {
        var project = new TestProject();
        var id = project.Step(0);
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(id)) with { DeletedAt = TestProject.Now });
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new FillSheetCommand(id))).Code);
    }

    [Fact]
    public async Task A_command_that_requires_the_right_to_fill_must_name_its_step()
    {
        var project = new TestProject();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            project.Runner.RunAsync(new FillWithoutStepCommand(project.Step(0)), TestContext.Current.CancellationToken));
        Assert.Empty(project.Database.Calls);
    }
}
