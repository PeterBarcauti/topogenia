// FlowCommandsTests.cs
// Create, relabel, reverse and delete a flow and the command bar through the template, logged with the 1.x kinds and the kind of the reversal (A 2.6, B 2.6-32, AR-155).

using Topogenia.Application.Flows;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Tests.Steps;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;

namespace Topogenia.Application.Tests.Flows;

public sealed class FlowCommandsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_created_flow_is_logged_with_the_kind_of_a_new_flow_and_can_be_undone_and_redone()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var outcome = await project.Runner.RunAsync(new CreateFlowCommand(new CreateFlowRequest(project.RootMapId, a, b, "oui")), Token);
        Assert.Equal(OperationKinds.CreateFlow, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal((a, b, "oui"), outcome.View!.Flows.Where(flow => flow.Id == outcome.Result.Id).Select(flow => (flow.SourceStepId, flow.TargetStepId, flow.Label)).Single());
        await project.UndoAsync();
        Assert.False(project.IsLiveFlow(outcome.Result.Id));
        await project.RedoAsync();
        Assert.True(project.IsLiveFlow(outcome.Result.Id));
    }

    [Fact]
    public async Task A_duplicate_or_reflexive_flow_is_refused_and_nothing_is_logged()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        project.Flow(a, b);
        var duplicate = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new CreateFlowCommand(new CreateFlowRequest(project.RootMapId, a, b, null)), Token));
        Assert.Equal((ErrorCodes.DuplicateFlow, RefusalKind.Conflict), (duplicate.Code, duplicate.Kind));
        var reflexive = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new CreateFlowCommand(new CreateFlowRequest(project.RootMapId, a, a, null)), Token));
        Assert.Equal(ErrorCodes.ReflexiveFlow, reflexive.Code);
        Assert.Single(project.LiveFlows());
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_flow_towards_an_unknown_step_is_not_found()
    {
        var project = new TestProject();
        var a = project.Step(0);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new CreateFlowCommand(new CreateFlowRequest(project.RootMapId, a, project.NextId(), null)), Token));
        Assert.Equal((ErrorCodes.NotFound, RefusalKind.NotFound), (refusal.Code, refusal.Kind));
        Assert.Empty(project.LiveFlows());
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_relabelled_flow_checks_the_version_read_and_is_logged_with_the_kind_of_a_flow_update()
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1), "oui");
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(RelabelFlowCommand.WithVersion(new RelabelFlowRequest(flow, "non"), 2), Token));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)"flow"), (refusal.Code, refusal.Details["object"]));
        Assert.Equal("oui", project.FlowOf(flow).Label);
        await project.Runner.RunAsync(RelabelFlowCommand.WithVersion(new RelabelFlowRequest(flow, "non"), 1), Token);
        Assert.Equal(OperationKinds.UpdateFlow, Assert.Single(project.Database.Entries).Kind);
        await project.UndoAsync();
        Assert.Equal("oui", project.FlowOf(flow).Label);
        await project.RedoAsync();
        Assert.Equal("non", project.FlowOf(flow).Label);
    }

    [Fact]
    public async Task A_flow_relabelled_without_if_match_is_not_checked_and_an_unchanged_label_logs_nothing()
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1), "oui");
        var command = RelabelFlowCommand.WithVersion(new RelabelFlowRequest(flow, " oui "), null);
        Assert.Empty(command.Versions);
        var outcome = await project.Runner.RunAsync(command, Token);
        Assert.False(outcome.Result.Changed);
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_reversed_flow_checks_its_version_is_logged_with_its_own_kind_and_is_undone_and_redone()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var flow = project.Flow(a, b, "oui");
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(ReverseFlowCommand.WithVersion(flow, 2), Token));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)"flow"), (refusal.Code, refusal.Details["object"]));
        var outcome = await project.Runner.RunAsync(ReverseFlowCommand.WithVersion(flow, 1), Token);
        Assert.Equal(OperationKinds.ReverseFlow, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal((b, a, "oui"), outcome.View!.Flows.Where(item => item.Id == flow).Select(item => (item.SourceStepId, item.TargetStepId, item.Label)).Single());
        await project.UndoAsync();
        Assert.Equal((a, b), (project.FlowOf(flow).SourceStepId, project.FlowOf(flow).TargetStepId));
        await project.RedoAsync();
        Assert.Equal((b, a), (project.FlowOf(flow).SourceStepId, project.FlowOf(flow).TargetStepId));
    }

    [Fact]
    public async Task A_deleted_flow_is_logged_as_a_deletion_and_comes_back_on_undo()
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1));
        var outcome = await project.Runner.RunAsync(new DeleteFlowCommand(flow), Token);
        Assert.Equal(project.FlowOf(flow).DeletionBatchId, outcome.Result);
        Assert.Equal(OperationKinds.Delete, Assert.Single(project.Database.Entries).Kind);
        Assert.Empty(outcome.View!.Flows);
        await project.UndoAsync();
        Assert.True(project.IsLiveFlow(flow));
        await project.RedoAsync();
        Assert.False(project.IsLiveFlow(flow));
    }

    [Fact]
    public async Task A_command_bar_draws_its_flows_in_one_commande_operation_undone_and_redone_whole()
    {
        var project = new TestProject();
        var (a, b, c) = (project.Step(0), project.Step(1), project.Step(2));
        var outcome = await project.Runner.RunAsync(new ApplyCommandBarCommand(project.RootMapId, "e1 > E002 > PRJ-E003 : fin"), Token);
        Assert.Equal(2, outcome.Result.FlowIds.Count);
        Assert.Equal(OperationKinds.Command, Assert.Single(project.Database.Entries).Kind);
        Assert.Equal([(a, b, null), (b, c, "fin")], project.LiveFlows().OrderBy(flow => flow.Source == a ? 0 : 1));
        await project.UndoAsync();
        Assert.Empty(project.LiveFlows());
        await project.RedoAsync();
        Assert.Equal(2, project.LiveFlows().Count);
    }

    [Fact]
    public async Task A_command_bar_with_a_wrong_line_draws_nothing_and_returns_the_errors_by_line()
    {
        var project = new TestProject();
        project.Step(0);
        project.Step(1);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new ApplyCommandBarCommand(project.RootMapId, "E001 > E002\nE001 > E099"), Token));
        Assert.Equal(ErrorCodes.CommandErrors, refusal.Code);
        var error = Assert.Single((IReadOnlyList<CommandLineError>)refusal.Details[CommandBar.ErrorsDetail]!);
        Assert.Equal((2, ErrorCodes.UnknownStep), (error.Line, error.Code));
        Assert.Empty(project.LiveFlows());
        Assert.Empty(project.Database.Entries);
    }
}
