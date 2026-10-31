// DeleteStepCommandTests.cs
// Deletion of one step: stitching, sub-map deleted or reattached (RG-C09), logged as supprimer, undone and redone (A 2.2, A 2.8).

using Topogenia.Application.OperationLog;
using Topogenia.Application.Steps;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.SubMaps;

namespace Topogenia.Application.Tests.Steps;

public sealed class DeleteStepCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ProcessMap MapOf(TestProject project, Guid mapId) => project.Database.Get<ProcessMap>(ProcessMap.KeyOf(mapId));

    /// <summary>A step with a sub-map that holds one live step.</summary>
    private static (Guid MapId, Guid InnerStep) FilledSubMap(TestProject project, Guid step)
    {
        var (mapId, laneId) = project.SubMap(step);
        return (mapId, project.Step(0, laneId: laneId, mapId: mapId));
    }

    [Fact]
    public async Task A_step_deleted_with_stitching_leaves_its_predecessor_linked_to_its_successor_until_the_undo()
    {
        var project = new TestProject();
        var (a, x, b) = (project.Step(0), project.Step(1), project.Step(2));
        project.Flow(a, x, "oui");
        project.Flow(x, b);
        await project.Runner.RunAsync(new DeleteStepCommand(x, stitch: true), Token);
        Assert.Equal(OperationKinds.Delete, Assert.Single(project.Database.Entries).Kind);
        Assert.False(project.IsLiveStep(x));
        Assert.Equal([(a, b, "oui")], project.LiveFlows());
        await project.UndoAsync();
        Assert.True(project.IsLiveStep(x));
        Assert.Equal(2, project.LiveFlows().Count);
        Assert.DoesNotContain(project.LiveFlows(), flow => flow.Source == a && flow.Target == b);
        await project.RedoAsync();
        Assert.False(project.IsLiveStep(x));
        Assert.Equal([(a, b, "oui")], project.LiveFlows());
    }

    [Fact]
    public async Task A_step_deleted_without_stitching_takes_its_flows_and_creates_none()
    {
        var project = new TestProject();
        var (a, x, b) = (project.Step(0), project.Step(1), project.Step(2));
        project.Flow(a, x);
        project.Flow(x, b);
        var outcome = await project.Runner.RunAsync(new DeleteStepCommand(x), Token);
        Assert.Equal(x, outcome.Result);
        Assert.False(project.IsLiveStep(x));
        Assert.Empty(project.LiveFlows());
        Assert.DoesNotContain(outcome.View!.Steps, step => step.Id == x);
    }

    [Fact]
    public async Task A_step_with_a_filled_sub_map_asks_for_a_choice_and_its_deletion_with_the_sub_map_is_undone_whole()
    {
        var project = new TestProject();
        var macro = project.Step(0);
        var (subMap, inner) = FilledSubMap(project, macro);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(new DeleteStepCommand(macro), Token));
        Assert.Equal((ErrorCodes.MacroChoiceRequired, RefusalKind.Conflict), (refusal.Code, refusal.Kind));
        Assert.True(project.IsLiveStep(macro));
        Assert.Empty(project.Database.Entries);
        await project.Runner.RunAsync(new DeleteStepCommand(macro, SubMapChoice.Delete), Token);
        Assert.Equal((false, false), (project.IsLiveStep(macro), project.IsLiveStep(inner)));
        Assert.NotNull(MapOf(project, subMap).DeletedAt);
        await project.UndoAsync();
        Assert.Equal((true, true), (project.IsLiveStep(macro), project.IsLiveStep(inner)));
        Assert.Null(MapOf(project, subMap).DeletedAt);
    }

    [Fact]
    public async Task A_sub_map_reattached_to_an_heir_comes_back_to_the_deleted_step_on_undo()
    {
        var project = new TestProject();
        var (macro, heir) = (project.Step(0), project.Step(1));
        var (subMap, inner) = FilledSubMap(project, macro);
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            project.Runner.RunAsync(new DeleteStepCommand(macro, SubMapChoice.Attach), Token));
        Assert.Equal(ErrorCodes.InvalidReattachment, refusal.Code);
        await project.Runner.RunAsync(new DeleteStepCommand(macro, SubMapChoice.Attach, heir), Token);
        Assert.Equal((false, true), (project.IsLiveStep(macro), project.IsLiveStep(inner)));
        Assert.Equal(heir, MapOf(project, subMap).ParentStepId);
        await project.UndoAsync();
        Assert.True(project.IsLiveStep(macro));
        Assert.Equal(macro, MapOf(project, subMap).ParentStepId);
    }

    [Fact]
    public async Task A_macro_step_deleted_with_stitching_settles_its_sub_map_first()
    {
        var project = new TestProject();
        var (a, macro, b, heir) = (project.Step(0), project.Step(1), project.Step(2), project.Step(5));
        project.Flow(a, macro);
        project.Flow(macro, b);
        var (subMap, _) = FilledSubMap(project, macro);
        await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(new DeleteStepCommand(macro, stitch: true), Token));
        Assert.Equal(2, project.LiveFlows().Count);
        await project.Runner.RunAsync(new DeleteStepCommand(macro, SubMapChoice.Attach, heir, stitch: true), Token);
        Assert.Equal(heir, MapOf(project, subMap).ParentStepId);
        Assert.Equal([(a, b, null)], project.LiveFlows());
        Assert.Single(project.Database.Entries);
    }
}
