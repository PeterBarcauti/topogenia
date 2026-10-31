// SubMapCommandsTests.cs
// Detail a step, delete the detail and reattach a sub-map through the template, and the map that their undo answers (annex A 2.2, A 2.15, B 2.6-27, 28, 41).

using Topogenia.Application.OperationLog;
using Topogenia.Application.Steps;
using Topogenia.Application.SubMaps;
using Topogenia.Application.Tests.Dissolutions;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Selection.SelectionFixture;

namespace Topogenia.Application.Tests.SubMaps;

public sealed class SubMapCommandsTests
{
    [Fact]
    public async Task Detailing_a_step_is_logged_on_its_map_and_undo_and_redo_remove_and_bring_back_the_sub_map()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var outcome = await project.Runner.RunAsync(new DetailStepCommand(step), Token);
        var subMap = outcome.Result.MapId;
        Assert.False(outcome.Result.Reopened);
        Assert.Equal(step, MapOf(project, subMap).ParentStepId);
        Assert.Equal("Acteur 1", project.Database.Get<Lane>(Lane.KeyOf(outcome.Result.FirstLaneId!.Value)).Name);
        Assert.Equal((project.RootMapId, subMap, true), (outcome.View!.Id, outcome.View.Steps.Single().SubMapId, outcome.View.Steps.Single().SubMapEmpty));
        Assert.Equal([OperationKinds.Detail], Kinds(project));
        Assert.Empty(outcome.Warnings);

        await UndoAsync(project);
        Assert.False(IsLiveMap(project, subMap));
        await RedoAsync(project);
        Assert.True(IsLiveMap(project, subMap));
    }

    [Fact]
    public async Task Reopening_an_empty_sub_map_warns_and_logs_nothing()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var first = await project.Runner.RunAsync(new DetailStepCommand(step), Token);
        var again = await project.Runner.RunAsync(new DetailStepCommand(step), Token);
        Assert.Equal((first.Result.MapId, true, (Guid?)null), (again.Result.MapId, again.Result.Reopened, again.OperationId));
        Assert.Equal([WarningCodes.ExistingSubMap], again.Warnings);
        Assert.Equal([OperationKinds.Detail], Kinds(project));
    }

    [Fact]
    public async Task A_detailed_step_is_not_detailed_again_a_start_is_never_detailed_and_a_deep_detail_warns()
    {
        var flow = new DetailedFlow(remainder: false, settings: new AppSettings(1, "ACI", 30));
        var again = await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(new DetailStepCommand(flow.Macro)));
        Retype(flow.Project, flow.Before, StepType.Start);
        var start = await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(new DetailStepCommand(flow.Before)));
        Assert.Equal((ErrorCodes.StepAlreadyDetailed, ErrorCodes.StepNotDetailable), (again.Code, start.Code));
        Assert.Empty(flow.Project.Database.Entries);

        var deep = await flow.RunAsync(new DetailStepCommand(flow.First));
        Assert.Equal([WarningCodes.DepthExceeded], deep.Warnings);
        Assert.Equal([OperationKinds.Detail], Kinds(flow.Project, flow.SubMap));
    }

    [Fact]
    public async Task The_undo_of_a_detail_completed_since_is_refused_and_clears_the_stack()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var outcome = await project.Runner.RunAsync(new DetailStepCommand(step), Token);
        await project.Runner.RunAsync(new AddStepProbe(outcome.Result.MapId, outcome.Result.FirstLaneId!.Value, 0), Token);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => UndoAsync(project));
        Assert.Equal(ErrorCodes.MapModified, refusal.Code);
        Assert.Empty(Kinds(project));
        Assert.True(IsLiveMap(project, outcome.Result.MapId));
    }

    [Fact]
    public async Task Deleting_the_detail_takes_the_descent_in_one_batch_is_logged_on_the_parent_map_and_undo_restores_it()
    {
        var flow = new DetailedFlow(remainder: false);
        var below = flow.Project.SubMap(flow.First);
        var outcome = await flow.RunAsync(new DeleteSubMapCommand(flow.SubMap));
        Assert.Equal([flow.SubMap, below.MapId], outcome.Result);
        var batch = MapOf(flow.Project, flow.SubMap).DeletionBatchId;
        Assert.NotNull(batch);
        Assert.Equal((batch, batch), (MapOf(flow.Project, below.MapId).DeletionBatchId, StepOf(flow.Project, flow.Second).DeletionBatchId));
        Assert.Equal((flow.Project.RootMapId, (Guid?)null), (outcome.View!.Id, outcome.View.Steps.Single(step => step.Id == flow.Macro).SubMapId));
        Assert.Equal([OperationKinds.DeleteDetail], Kinds(flow.Project));
        Assert.True(IsLiveStep(flow.Project, flow.Macro));

        await UndoAsync(flow.Project);
        Assert.True(IsLiveMap(flow.Project, flow.SubMap) && IsLiveMap(flow.Project, below.MapId) && IsLiveStep(flow.Project, flow.Second));
        await RedoAsync(flow.Project);
        Assert.False(IsLiveMap(flow.Project, flow.SubMap));
    }

    /// <summary>
    /// A-2.15-12, AR-109: the undo restores a batch that holds a map (the sub-map) and a step; the 1.x showed the restored map
    /// first, the 2.0 answers the map of the request, where the step comes back with the indicator of its filled sub-map.
    /// </summary>
    [Fact]
    public async Task The_undo_of_a_step_deleted_with_its_sub_map_answers_the_map_of_the_request_where_the_step_shows_its_sub_map_again()
    {
        var flow = new DetailedFlow(remainder: false);
        await flow.RunAsync(new DeleteStepCommand(flow.Macro, SubMapChoice.Delete));
        Assert.False(IsLiveStep(flow.Project, flow.Macro) || IsLiveMap(flow.Project, flow.SubMap));

        var undone = await flow.RunAsync(new UndoCommand(flow.Project.RootMapId));
        Assert.Equal(OperationKinds.Delete, undone.Result.Kind);
        Assert.True(IsLiveMap(flow.Project, flow.SubMap));
        Assert.Equal(flow.Project.RootMapId, undone.View!.Id);
        var macro = Assert.Single(undone.View.Steps, step => step.Id == flow.Macro);
        Assert.Equal((flow.SubMap, false), (macro.SubMapId, macro.SubMapEmpty));
    }

    /// <summary>A-2.15-12, AR-109: the undo of « Delete the detail », asked on the parent map, answers the parent map, not the sub-map it restores.</summary>
    [Fact]
    public async Task The_undo_of_a_deleted_detail_answers_the_map_of_the_request_where_the_step_shows_its_sub_map_again()
    {
        var flow = new DetailedFlow(remainder: false);
        await flow.RunAsync(new DeleteSubMapCommand(flow.SubMap));

        var undone = await flow.RunAsync(new UndoCommand(flow.Project.RootMapId));
        Assert.Equal(OperationKinds.DeleteDetail, undone.Result.Kind);
        Assert.True(IsLiveMap(flow.Project, flow.SubMap));
        Assert.Equal(flow.Project.RootMapId, undone.View!.Id);
        var macro = Assert.Single(undone.View.Steps, step => step.Id == flow.Macro);
        Assert.Equal((flow.SubMap, false), (macro.SubMapId, macro.SubMapEmpty));
    }

    [Fact]
    public async Task The_root_map_is_not_a_detail_and_a_contributor_deletes_no_detail()
    {
        var project = new TestProject();
        var root = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(new DeleteSubMapCommand(project.RootMapId), Token));
        var (subMap, _) = project.SubMap(project.Step(0));
        project.ActingAs(project.Contributor);
        var denied = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(new DeleteSubMapCommand(subMap), Token));
        Assert.Equal((ErrorCodes.RootMap, ErrorCodes.AccessDenied), (root.Code, denied.Code));
        Assert.True(IsLiveMap(project, subMap));
    }

    [Fact]
    public async Task A_reattached_sub_map_is_logged_as_a_detail_on_the_map_of_its_new_step_and_undo_gives_it_back()
    {
        var flow = new DetailedFlow(remainder: false);
        var outcome = await flow.RunAsync(new ReattachSubMapCommand(flow.SubMap, flow.After));
        Assert.Equal(flow.After, MapOf(flow.Project, flow.SubMap).ParentStepId);
        Assert.Equal(flow.SubMap, outcome.View!.Steps.Single(step => step.Id == flow.After).SubMapId);
        Assert.Equal([OperationKinds.Detail], Kinds(flow.Project));

        await UndoAsync(flow.Project);
        Assert.Equal(flow.Macro, MapOf(flow.Project, flow.SubMap).ParentStepId);
        await RedoAsync(flow.Project);
        Assert.Equal(flow.After, MapOf(flow.Project, flow.SubMap).ParentStepId);
    }

    [Fact]
    public async Task A_sub_map_is_not_reattached_to_an_unknown_step_a_start_or_its_own_descent()
    {
        var flow = new DetailedFlow(remainder: false);
        Retype(flow.Project, flow.After, StepType.Start);
        var codes = new List<string>();
        foreach (var target in new[] { flow.Project.NextId(), flow.After, flow.First })
            codes.Add((await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(new ReattachSubMapCommand(flow.SubMap, target)))).Code);
        Assert.Equal([ErrorCodes.InvalidReattachment, ErrorCodes.InvalidReattachment, ErrorCodes.InvalidReattachment], codes);
        Assert.Equal(flow.Macro, MapOf(flow.Project, flow.SubMap).ParentStepId);
        Assert.Empty(flow.Project.Database.Entries);
    }

    [Fact]
    public async Task Detailing_deleting_and_reattaching_check_the_version_read_of_the_object_of_their_route()
    {
        var flow = new DetailedFlow(remainder: false);
        var codes = new List<string>();
        codes.Add((await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(ReattachSubMapCommand.WithVersion(flow.SubMap, flow.After, 7)))).Code);
        codes.Add((await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(DeleteSubMapCommand.WithVersion(flow.SubMap, 7)))).Code);
        codes.Add((await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(DetailStepCommand.WithVersion(flow.After, 7)))).Code);
        Assert.Equal([ErrorCodes.VersionConflict, ErrorCodes.VersionConflict, ErrorCodes.VersionConflict], codes);
        Assert.Empty(flow.Project.Database.Entries);

        Assert.Empty(DetailStepCommand.WithVersion(flow.After, null).Versions);
        await flow.RunAsync(DetailStepCommand.WithVersion(flow.After, 1));
        await flow.RunAsync(ReattachSubMapCommand.WithVersion(flow.SubMap, flow.Before, 1));
        await flow.RunAsync(DeleteSubMapCommand.WithVersion(flow.SubMap, 2));
        Assert.False(IsLiveMap(flow.Project, flow.SubMap));
    }
}
