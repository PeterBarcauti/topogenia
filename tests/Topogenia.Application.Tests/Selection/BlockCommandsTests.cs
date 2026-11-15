// BlockCommandsTests.cs
// Transfer a block and detail a selection through the template: the map logged, the cut flows, undo and redo (annex A 2.8, B 2.6-30, 31, 42).

using Topogenia.Application.OperationLog;
using Topogenia.Application.Raci;
using Topogenia.Application.Selection;
using Topogenia.Application.Dissolutions;
using Topogenia.Application.Tests.Dissolutions;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Structure;
using static Topogenia.Application.Tests.Selection.SelectionFixture;

namespace Topogenia.Application.Tests.Selection;

public sealed class BlockCommandsTests
{
    /// <summary>On the root map A, B and C in a row, and the macro step D whose sub-map is empty.</summary>
    private static (TestProject Project, Guid A, Guid B, Guid C, Guid SubMap, Guid SubLane) Row()
    {
        var project = new TestProject();
        var (a, b, c, d) = (project.Step(0), project.Step(1), project.Step(2), project.Step(3));
        Flow(project, a, b);
        Flow(project, b, c);
        var (subMap, subLane) = project.SubMap(d);
        return (project, a, b, c, subMap, subLane);
    }

    [Fact]
    public async Task A_block_cut_to_a_sub_map_is_logged_on_the_target_map_and_undo_there_brings_it_back_stitches_removed()
    {
        var (project, a, b, c, subMap, subLane) = Row();
        var outcome = await project.Runner.RunAsync(new TransferBlockCommand(subMap, new TransferRequest([new StepPlacement(b, subLane, 0)], Cut: true)), Token);
        Assert.Equal(subMap, outcome.View!.Id);
        Assert.Equal([b], outcome.View.Steps.Select(step => step.Id));
        Assert.Equal([OperationKinds.MoveBlock], Kinds(project, subMap));
        Assert.Empty(Kinds(project));
        Assert.Equal([(a, c)], LiveFlows(project));
        Assert.Equal("PRJ-E002", StepOf(project, b).Code);

        await UndoAsync(project, subMap);
        Assert.Equal((project.RootMapId, 1), (StepOf(project, b).MapId, StepOf(project, b).Column));
        Assert.Equal(new[] { (a, b), (b, c) }.Order(), LiveFlows(project));

        await RedoAsync(project, subMap);
        Assert.Equal(subMap, StepOf(project, b).MapId);
        Assert.Equal([(a, c)], LiveFlows(project));
    }

    [Fact]
    public async Task The_stack_of_the_map_of_origin_is_kept_and_its_operations_stay_protected_by_their_versions()
    {
        var (project, _, b, _, subMap, subLane) = Row();
        var qualite = AddLane(project, "Qualité", 1);
        await project.Runner.RunAsync(new SetRaciCommand(new SetRaciRequest(b, qualite, "C")), Token);
        await project.Runner.RunAsync(new TransferBlockCommand(subMap, new TransferRequest([new StepPlacement(b, subLane, 0)], Cut: true)), Token);
        Assert.Equal([OperationKinds.Raci], Kinds(project));

        var refusal = await Assert.ThrowsAsync<DomainException>(() => UndoAsync(project));
        Assert.Equal(ErrorCodes.OperationOutdated, refusal.Code);
        Assert.Empty(Kinds(project));
        Assert.Equal([OperationKinds.MoveBlock], Kinds(project, subMap));
    }

    [Fact]
    public async Task A_contributor_does_not_transfer_and_a_block_into_its_own_detail_or_an_unknown_step_is_refused()
    {
        var (project, _, b, _, subMap, subLane) = Row();
        var macro = MapOf(project, subMap).ParentStepId!.Value;
        var cycle = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new TransferBlockCommand(subMap, new TransferRequest([new StepPlacement(macro, subLane, 0)])), Token));
        var unknown = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new TransferBlockCommand(subMap, new TransferRequest([new StepPlacement(project.NextId(), subLane, 0)])), Token));
        project.ActingAs(project.Contributor);
        var denied = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new TransferBlockCommand(subMap, new TransferRequest([new StepPlacement(b, subLane, 0)])), Token));
        Assert.Equal((ErrorCodes.TransferCycle, ErrorCodes.NotFound, ErrorCodes.AccessDenied), (cycle.Code, unknown.Code, denied.Code));
        Assert.Equal(project.RootMapId, StepOf(project, b).MapId);
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task Detailing_a_selection_logs_a_grouping_with_the_cut_flows_and_the_new_step_remembers_the_operation()
    {
        var (project, a, b, c, _, _) = Row();
        var outcome = await project.Runner.RunAsync(new GroupSelectionCommand(project.RootMapId, new GroupRequest([b], "Sous-processus")), Token);
        var macro = outcome.Result.StepId!.Value;
        var subMap = Assert.Single(outcome.Result.Ids);
        Assert.Equal(2, outcome.Result.CutFlows.Count);
        Assert.Equal((subMap, 1), (StepOf(project, b).MapId, StepOf(project, macro).Column));
        Assert.Equal(new[] { (a, macro), (macro, c) }.Order(), LiveFlows(project));
        Assert.Equal(outcome.OperationId, StepOf(project, macro).OriginOperationId);
        var entry = Assert.Single(project.Database.Entries);
        Assert.Equal(OperationKinds.Group, entry.Kind);
        var logged = ChangeSetJson.Read(entry.ForwardJson).Changes.Where(change => change.After is Flow { DeletedAt: not null })
            .Select(change => ((Flow)change.After!).Id);
        Assert.Equal(outcome.Result.CutFlows.Order(), logged.Order());
        Assert.Empty(outcome.Warnings);

        await UndoAsync(project);
        Assert.Equal((project.RootMapId, 1), (StepOf(project, b).MapId, StepOf(project, b).Column));
        Assert.False(IsLiveStep(project, macro) || IsLiveMap(project, subMap));
        Assert.Equal(new[] { (a, b), (b, c) }.Order(), LiveFlows(project));

        await RedoAsync(project);
        Assert.Equal(subMap, StepOf(project, b).MapId);
        Assert.True(IsLiveStep(project, macro));
    }

    [Fact]
    public async Task The_undo_of_a_grouping_is_refused_once_its_sub_map_was_completed_and_the_stack_is_cleared()
    {
        var (project, _, b, _, _, _) = Row();
        var outcome = await project.Runner.RunAsync(new GroupSelectionCommand(project.RootMapId, new GroupRequest([b], "Sous-processus")), Token);
        var subMap = outcome.Result.Ids[0];
        var lane = project.Database.All<Lane>().Single(record => record.MapId == subMap).Id;
        await project.Runner.RunAsync(new AddStepProbe(subMap, lane, 2), Token);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => UndoAsync(project));
        Assert.Contains(refusal.Code, new[] { ErrorCodes.OperationOutdated, ErrorCodes.MapModified });
        Assert.Empty(Kinds(project));
        Assert.Equal(subMap, StepOf(project, b).MapId);
    }

    [Fact]
    public async Task A_grouping_without_label_with_a_step_of_another_map_or_of_an_open_frame_is_refused_and_a_deep_one_warns()
    {
        var (project, _, b, _, subMap, subLane) = Row();
        var label = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new GroupSelectionCommand(project.RootMapId, new GroupRequest([b], " ")), Token));
        Assert.Equal((ErrorCodes.RequiredField, (object?)"label"), (label.Code, label.Details["field"]));
        var elsewhere = project.Step(5, laneId: subLane, mapId: subMap);
        var outside = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new GroupSelectionCommand(project.RootMapId, new GroupRequest([b, elsewhere], "Sous-processus")), Token));
        Assert.Equal((ErrorCodes.StepOutsideMap, (object?)"PRJ-E006"), (outside.Code, outside.Details["code"]));
        Assert.Empty(project.Database.Entries);

        var flow = new DetailedFlow(settings: new AppSettings(1, "ACI", 30));
        await flow.RunAsync(new DissolveCommand(flow.Macro));
        var framed = await Assert.ThrowsAsync<DomainException>(() =>
            flow.RunAsync(new GroupSelectionCommand(flow.Project.RootMapId, new GroupRequest([flow.First], "Sous-processus"))));
        Assert.Equal(ErrorCodes.DissolutionInProgress, framed.Code);
        Assert.NotNull(flow.OpenFrame);

        var inner = flow.Project.SubMap(flow.Before);
        var deep = flow.Project.Step(0, laneId: inner.LaneId, mapId: inner.MapId);
        var outcome = await flow.RunAsync(new GroupSelectionCommand(inner.MapId, new GroupRequest([deep], "Sous-processus")));
        Assert.Equal([WarningCodes.DepthExceeded], outcome.Warnings);
    }
}
