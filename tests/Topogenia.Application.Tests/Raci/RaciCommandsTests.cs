// RaciCommandsTests.cs
// The RACI of a step through the template: logged as raci, the approval given back on undo, the refusals (annex A 2.13, B 2.6-34).

using Topogenia.Application.Maps;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Raci;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Selection.SelectionFixture;

namespace Topogenia.Application.Tests.Raci;

public sealed class RaciCommandsTests
{
    private static List<(Guid Lane, RaciRole Role)> Roles(TestProject project, Guid stepId) =>
        project.Database.All<StepRaci>().Where(raci => raci.StepId == stepId).Select(raci => (raci.LaneId, raci.Role)).Order().ToList();

    [Fact]
    public async Task A_role_is_logged_as_raci_shown_in_the_view_and_undo_and_redo_remove_and_set_it_again()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var qualite = AddLane(project, "Qualité", 1);
        var outcome = await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, qualite, "C")), Token);
        Assert.Equal((qualite, RaciRole.C), (outcome.Result!.LaneId, outcome.Result.Role));
        Assert.Equal([new RaciView(step, qualite, RaciRole.C)], outcome.View!.Raci);
        Assert.Equal([OperationKinds.Raci], Kinds(project));

        await UndoAsync(project);
        Assert.Empty(Roles(project, step));
        await RedoAsync(project);
        Assert.Equal([(qualite, RaciRole.C)], Roles(project, step));
    }

    [Fact]
    public async Task Moving_the_approval_and_undoing_it_gives_the_A_back_to_the_former_approver()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var (qualite, achats) = (AddLane(project, "Qualité", 1), AddLane(project, "Achats", 2));
        await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, qualite, "A")), Token);
        await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, achats, "A")), Token);
        Assert.Equal([(achats, RaciRole.A)], Roles(project, step));

        await UndoAsync(project);
        Assert.Equal([(qualite, RaciRole.A)], Roles(project, step));
        await RedoAsync(project);
        Assert.Equal([(achats, RaciRole.A)], Roles(project, step));
    }

    [Fact]
    public async Task Moving_the_approval_to_a_lane_that_held_another_role_and_undoing_it_gives_each_lane_its_former_role()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var (quality, purchasing) = (AddLane(project, "Qualité", 1), AddLane(project, "Achats", 2));
        await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, quality, "A")), Token);
        await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, purchasing, "C")), Token);
        List<(Guid Lane, RaciRole Role)> before = [(quality, RaciRole.A), (purchasing, RaciRole.C)];
        Assert.Equal(before.Order(), Roles(project, step));

        await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, purchasing, "A")), Token);
        Assert.Equal([(purchasing, RaciRole.A)], Roles(project, step));
        await UndoAsync(project);
        Assert.Equal(before.Order(), Roles(project, step));
        await RedoAsync(project);
        Assert.Equal([(purchasing, RaciRole.A)], Roles(project, step));
    }

    [Fact]
    public async Task A_role_removed_with_null_comes_back_on_undo()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var qualite = AddLane(project, "Qualité", 1);
        await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, qualite, "I")), Token);
        var outcome = await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, qualite, null)), Token);
        Assert.Null(outcome.Result);
        Assert.Empty(Roles(project, step));
        await UndoAsync(project);
        Assert.Equal([(qualite, RaciRole.I)], Roles(project, step));
    }

    [Fact]
    public async Task Setting_the_role_a_lane_already_holds_changes_and_logs_nothing()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var qualite = AddLane(project, "Qualité", 1);
        await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, qualite, "C")), Token);
        var again = await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, qualite, "C")), Token);
        Assert.Null(again.OperationId);
        Assert.Equal([OperationKinds.Raci], Kinds(project));
        Assert.Equal(2, StepOf(project, step).Version);
    }

    [Fact]
    public async Task A_role_on_a_step_without_content_on_the_lane_of_the_step_or_with_an_unknown_letter_is_refused()
    {
        var project = new TestProject();
        var (step, start) = (project.Step(0), project.Step(1));
        var qualite = AddLane(project, "Qualité", 1);
        Retype(project, start, StepType.Start);
        var noContent = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(start, qualite, "C")), Token));
        var ownLane = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, project.LaneId, "C")), Token));
        var letter = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, qualite, "S")), Token));
        Assert.Equal(ErrorCodes.StepWithoutContent, noContent.Code);
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"lane_id"), (ownLane.Code, ownLane.Details["field"]));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"role"), (letter.Code, letter.Details["field"]));
        Assert.Empty(project.Database.All<StepRaci>());
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_contributor_sets_no_role()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var qualite = AddLane(project, "Qualité", 1);
        project.ActingAs(project.Contributor);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(step, qualite, "C")), Token));
        Assert.Equal(ErrorCodes.AccessDenied, refusal.Code);
        Assert.Empty(project.Database.All<StepRaci>());
    }

    [Fact]
    public async Task A_role_checks_the_version_read_of_the_step()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var qualite = AddLane(project, "Qualité", 1);
        var stale = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(SetRaciCommand.WithVersion(new SetRaciRequest(step, qualite, "C"), 3), Token));
        Assert.Equal(ErrorCodes.VersionConflict, stale.Code);
        Assert.Empty(project.Database.All<StepRaci>());
        Assert.Empty(SetRaciCommand.WithVersion(new SetRaciRequest(step, qualite, "C"), null).Versions);
        await project.Runner.RunAsync(SetRaciCommand.WithVersion(new SetRaciRequest(step, qualite, "C"), 1), Token);
        Assert.Equal([(qualite, RaciRole.C)], Roles(project, step));
    }
}
