// DissolutionCommandsTests.cs
// Dissolve a detail and work its remainder through the template: kinds, undo and redo, what is not logged (annex A 2.9, B 2.6-35, 39, 40, B 2.9-35).

using Topogenia.Application.Dissolutions;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Selection;
using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Selection;
using static Topogenia.Application.Tests.Selection.SelectionFixture;

namespace Topogenia.Application.Tests.Dissolutions;

public sealed class DissolutionCommandsTests
{
    private static async Task<(DetailedFlow Flow, Guid Frame)> DissolvedAsync(bool remainder = true)
    {
        var flow = new DetailedFlow(remainder);
        var outcome = await flow.RunAsync(new DissolveCommand(flow.Macro));
        return (flow, outcome.Result.DissolutionId);
    }

    [Fact]
    public async Task A_dissolution_is_logged_as_remonter_its_result_names_the_frame_and_undo_and_redo_replay_it()
    {
        var flow = new DetailedFlow();
        var outcome = await flow.RunAsync(new DissolveCommand(flow.Macro));
        var frame = Assert.Single(flow.Project.Database.All<Dissolution>());
        Assert.Equal((frame.Id, frame.Id), (outcome.Result.DissolutionId, outcome.View!.Dissolution!.Id));
        Assert.Equal([flow.First, flow.Second], outcome.Result.FrameStepIds);
        Assert.Equal([OperationKinds.Dissolve], Kinds(flow.Project));
        Assert.Equal((flow.Project.RootMapId, 3), (StepOf(flow.Project, flow.First).MapId, StepOf(flow.Project, flow.After).Column));
        Assert.False(IsLiveStep(flow.Project, flow.Macro) || IsLiveMap(flow.Project, flow.SubMap));

        await UndoAsync(flow.Project);
        Assert.True(IsLiveStep(flow.Project, flow.Macro) && IsLiveMap(flow.Project, flow.SubMap));
        Assert.Equal((flow.SubMap, 0, 2), (StepOf(flow.Project, flow.First).MapId, StepOf(flow.Project, flow.First).Column, StepOf(flow.Project, flow.After).Column));
        Assert.Equal(new[] { (flow.Before, flow.Macro), (flow.Macro, flow.After) }.Order(), LiveFlows(flow.Project));
        Assert.Null(flow.OpenFrame);

        await RedoAsync(flow.Project);
        Assert.Equal(frame.Id, flow.OpenFrame!.Id);
        Assert.False(IsLiveStep(flow.Project, flow.Macro));
    }

    [Fact]
    public async Task A_step_without_detail_is_not_dissolved_and_a_contributor_dissolves_nothing()
    {
        var flow = new DetailedFlow();
        var absent = await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(new DissolveCommand(flow.Before)));
        flow.Project.ActingAs(flow.Project.Contributor);
        var denied = await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(new DissolveCommand(flow.Macro)));
        Assert.Equal((ErrorCodes.NoDetail, ErrorCodes.AccessDenied), (absent.Code, denied.Code));
        Assert.Empty(flow.Project.Database.All<Dissolution>());
    }

    [Fact]
    public async Task The_undo_of_a_dissolution_whose_frame_step_was_deleted_by_another_user_is_refused_and_clears_the_stack()
    {
        var (flow, _) = await DissolvedAsync();
        flow.Project.ActingAs(flow.Project.Admin);
        await flow.RunAsync(new DeleteSelectionCommand(flow.Project.RootMapId, new GroupDeletionRequest([flow.Second], null)));
        flow.Project.ActingAs(flow.Project.Facilitator);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => UndoAsync(flow.Project));
        Assert.Equal(ErrorCodes.OperationOutdated, refusal.Code);
        Assert.DoesNotContain(flow.Project.Database.Entries, entry => entry.UserId == flow.Project.Facilitator.Id);
        Assert.NotNull(flow.OpenFrame);
    }

    [Fact]
    public async Task An_assignment_is_logged_as_affecter_and_undo_brings_the_document_back_to_the_remainder()
    {
        var (flow, frame) = await DissolvedAsync();
        var outcome = await flow.RunAsync(new AssignRemainderCommand(frame, new AssignRequest("document", flow.Document, flow.First)));
        Assert.Equal(flow.First, flow.HolderOfDocument);
        Assert.Empty(outcome.Result!.Documents);
        Assert.Single(outcome.Result.PainPoints);
        Assert.Equal([OperationKinds.Dissolve, OperationKinds.Assign], Kinds(flow.Project));

        await UndoAsync(flow.Project);
        Assert.Equal(flow.Macro, flow.HolderOfDocument);
        await RedoAsync(flow.Project);
        Assert.Equal(flow.First, flow.HolderOfDocument);
    }

    [Fact]
    public async Task An_assignment_names_a_known_frame_a_step_that_holds_content_and_a_facilitator()
    {
        var (flow, frame) = await DissolvedAsync();
        var unknown = await Assert.ThrowsAsync<DomainException>(() =>
            flow.RunAsync(new AssignRemainderCommand(flow.Project.NextId(), new AssignRequest("document", flow.Document, flow.First))));
        var missing = await Assert.ThrowsAsync<DomainException>(() =>
            flow.RunAsync(new AssignRemainderCommand(frame, new AssignRequest("usage", flow.Document, null))));
        flow.Project.ActingAs(flow.Project.Contributor);
        var denied = await Assert.ThrowsAsync<DomainException>(() =>
            flow.RunAsync(new AssignRemainderCommand(frame, new AssignRequest("document", flow.Document, flow.First))));
        Assert.Equal((ErrorCodes.NotFound, ErrorCodes.RequiredField, ErrorCodes.AccessDenied), (unknown.Code, missing.Code, denied.Code));
        Assert.Equal(flow.Macro, flow.HolderOfDocument);
        Assert.Equal([OperationKinds.Dissolve], Kinds(flow.Project));
    }

    [Fact]
    public async Task Removing_an_element_of_the_remainder_is_for_good_and_logs_nothing()
    {
        var (flow, frame) = await DissolvedAsync();
        var outcome = await flow.RunAsync(new RemoveRemainderCommand(frame, new RemoveRequest("pain_point", flow.PainPoint)));
        Assert.Null(outcome.OperationId);
        Assert.DoesNotContain(flow.Project.Database.All<PainPoint>(), painPoint => painPoint.Id == flow.PainPoint);
        Assert.Empty(outcome.Result!.PainPoints);
        Assert.Equal([OperationKinds.Dissolve], Kinds(flow.Project));
        var absent = await Assert.ThrowsAsync<DomainException>(() =>
            flow.RunAsync(new RemoveRemainderCommand(frame, new RemoveRequest("pain_point", flow.PainPoint))));
        Assert.Equal(ErrorCodes.NotFound, absent.Code);
    }

    [Fact]
    public async Task Nothing_is_removed_from_a_closed_frame()
    {
        var (flow, frame) = await DissolvedAsync();
        await flow.RunAsync(new FinishDissolutionCommand(frame));
        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            flow.RunAsync(new RemoveRemainderCommand(frame, new RemoveRequest("document", flow.Document))));
        Assert.Equal(ErrorCodes.DissolutionFinished, refusal.Code);
        Assert.Equal(flow.Second, flow.HolderOfDocument);
    }

    [Fact]
    public async Task Finishing_stores_the_remainder_is_logged_and_undo_reopens_the_frame_with_its_remainder()
    {
        var (flow, frame) = await DissolvedAsync();
        var outcome = await flow.RunAsync(new FinishDissolutionCommand(frame));
        Assert.Equal([RemainderKind.Document, RemainderKind.PainPoint], outcome.Result.Select(arrangement => arrangement.Kind));
        Assert.Equal(flow.Second, flow.HolderOfDocument);
        Assert.Null(flow.OpenFrame);
        Assert.Null(outcome.View!.Dissolution);
        Assert.Equal([OperationKinds.Dissolve, OperationKinds.FinishDissolution], Kinds(flow.Project));
        Assert.Empty((await flow.RunAsync(new FinishDissolutionCommand(frame))).Result);
        Assert.Equal(2, flow.Project.Database.Entries.Count);

        await UndoAsync(flow.Project);
        Assert.Equal((frame, flow.Macro), (flow.OpenFrame!.Id, flow.HolderOfDocument!.Value));
        await RedoAsync(flow.Project);
        Assert.Null(flow.OpenFrame);
        Assert.Equal(flow.Second, flow.HolderOfDocument);
    }

    /// <summary>
    /// B-2.9-35: the frame holds two steps (First, Second), so no single step remains: the pain point is stored on the map
    /// (its arrangement names no step and it loses its step), while the output document goes to the last step of the frame.
    /// </summary>
    [Fact]
    public async Task Finishing_a_frame_of_several_steps_stores_the_pain_points_on_the_map()
    {
        var flow = new DetailedFlow();
        var dissolved = await flow.RunAsync(new DissolveCommand(flow.Macro));
        Assert.Equal([flow.First, flow.Second], dissolved.Result.FrameStepIds);
        var outcome = await flow.RunAsync(new FinishDissolutionCommand(dissolved.Result.DissolutionId));

        var painPoint = Assert.Single(outcome.Result, arrangement => arrangement.Kind == RemainderKind.PainPoint);
        Assert.Equal((flow.PainPoint, (Guid?)null, (string?)null), (painPoint.ObjectId, painPoint.StepId, painPoint.StepCode));
        Assert.Null(PainPointOf(flow).StepId);
        var document = Assert.Single(outcome.Result, arrangement => arrangement.Kind == RemainderKind.Document);
        Assert.Equal((flow.Second, flow.Second), (document.StepId, flow.HolderOfDocument));

        await UndoAsync(flow.Project);
        Assert.Equal(flow.Macro, PainPointOf(flow).StepId);
    }

    private static PainPoint PainPointOf(DetailedFlow flow) => flow.Project.Database.Get<PainPoint>(PainPoint.KeyOf(flow.PainPoint));

    [Fact]
    public async Task Closing_an_empty_frame_logs_nothing_and_the_next_undo_undoes_the_dissolution()
    {
        var (flow, frame) = await DissolvedAsync(remainder: false);
        var outcome = await flow.RunAsync(new FinishDissolutionCommand(frame));
        Assert.Empty(outcome.Result);
        Assert.Null(outcome.OperationId);
        Assert.Null(flow.OpenFrame);
        Assert.Equal([OperationKinds.Dissolve], Kinds(flow.Project));

        var undone = await UndoAsync(flow.Project);
        Assert.Equal(OperationKinds.Dissolve, undone.Result.Kind);
        Assert.True(IsLiveStep(flow.Project, flow.Macro));
    }

    [Fact]
    public async Task A_dissolution_checks_the_version_read_of_the_dissolved_step()
    {
        var flow = new DetailedFlow();
        var stale = await Assert.ThrowsAsync<DomainException>(() => flow.RunAsync(DissolveCommand.WithVersion(flow.Macro, 2)));
        Assert.Equal(ErrorCodes.VersionConflict, stale.Code);
        Assert.True(IsLiveStep(flow.Project, flow.Macro));
        Assert.Empty(DissolveCommand.WithVersion(flow.Macro, null).Versions);
        await flow.RunAsync(DissolveCommand.WithVersion(flow.Macro, 1));
        Assert.NotNull(flow.OpenFrame);
    }
}
