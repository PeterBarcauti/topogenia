// SheetCommandsTests.cs
// The writes of a sheet through the template: right to fill, If-Match on the sheet, view of the map, nothing logged (annex A 2.10, B 2.6-37).

using Topogenia.Application.Sheets;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Sheets.SheetFixture;

namespace Topogenia.Application.Tests.Sheets;

public sealed class SheetCommandsTests
{
    [Fact]
    public async Task A_saved_draft_is_written_logs_nothing_and_the_reply_carries_the_gauge_of_its_step()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var outcome = await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Payer les factures")), Token);
        Assert.Equal((SheetStatus.Draft, true), (outcome.Result.Status, outcome.Result.Changed));
        Assert.Equal("Payer les factures", SheetOf(project, step).Purpose);
        Assert.Null(outcome.OperationId);
        var summary = outcome.View!.Steps.Single(view => view.Id == step).Sheet;
        Assert.Equal((SheetStatus.Draft, 14), (summary.Status, summary.Completeness));
        Assert.Empty(outcome.History!.Undo);
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task An_empty_draft_creates_no_sheet()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var outcome = await project.Runner.RunAsync(new SaveSheetCommand(step, new Domain.Sheets.SheetInput(null, null, null)), Token);
        Assert.Equal((SheetStatus.Empty, false), (outcome.Result.Status, outcome.Result.Changed));
        Assert.Empty(project.Database.All<StepSheet>());
    }

    [Fact]
    public async Task The_sheet_is_filled_by_the_facilitator_the_administrator_the_assigned_contributor_and_the_holder_of_the_lane_only()
    {
        var project = new TestProject();
        var free = project.Step(0);
        var assigned = project.Step(1, assignee: project.Contributor.Id);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Contributor), new SaveSheetCommand(free, Purpose("A")))).Code);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Reader), new SaveSheetCommand(free, Purpose("A")))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project.ActingAs(project.Stranger), new SaveSheetCommand(free, Purpose("A")))).Code);
        Assert.Empty(project.Database.All<StepSheet>());
        await project.ActingAs(project.Contributor).Runner.RunAsync(new SaveSheetCommand(assigned, Purpose("B")), Token);
        await project.ActingAs(project.Admin).Runner.RunAsync(new SaveSheetCommand(free, Purpose("C")), Token);
        HoldLane(project, project.Contributor.Id);
        await project.ActingAs(project.Contributor).Runner.RunAsync(new SaveSheetCommand(free, Purpose("D")), Token);
        Assert.Equal(("B", "D"), (SheetOf(project, assigned).Purpose, SheetOf(project, free).Purpose));
    }

    [Fact]
    public async Task A_stale_if_match_on_the_sheet_is_a_version_conflict_and_nothing_is_written()
    {
        var project = new TestProject();
        var step = project.Step(0);
        await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Avant")), Token);
        var read = SheetOf(project, step).Version;
        await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Entre-temps")), Token);
        var refusal = await Refused(project, SaveSheetCommand.WithVersion(step, Purpose("Après"), read));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)project.Facilitator.Id), (refusal.Code, refusal.Details["updated_by"]));
        Assert.Equal("Entre-temps", SheetOf(project, step).Purpose);
        await project.Runner.RunAsync(SaveSheetCommand.WithVersion(step, Purpose("Après"), SheetOf(project, step).Version), Token);
        Assert.Equal(("Après", read + 2), (SheetOf(project, step).Purpose, SheetOf(project, step).Version));
    }

    [Fact]
    public async Task An_if_match_on_a_step_that_has_no_sheet_yet_has_nothing_to_compare()
    {
        var project = new TestProject();
        var step = project.Step(0);
        await project.Runner.RunAsync(SaveSheetCommand.WithVersion(step, Purpose("Premier"), 7), Token);
        Assert.Equal("Premier", SheetOf(project, step).Purpose);
        var refusal = await Refused(project, ValidateSheetCommand.WithVersion(project.Step(1), 7));
        Assert.Equal(ErrorCodes.EmptySheet, refusal.Code);
    }

    [Fact]
    public async Task A_step_without_content_and_a_deleted_step_have_no_sheet()
    {
        var project = new TestProject();
        var start = StepOfType(project, 0, StepType.Start);
        Assert.Equal(ErrorCodes.StepWithoutContent, (await Refused(project, new SaveSheetCommand(start, Purpose("A")))).Code);
        var deleted = project.Step(1);
        Delete(project, deleted);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new SaveSheetCommand(deleted, Purpose("A")))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new ValidateSheetCommand(deleted))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new RequestClarificationCommand(deleted, "Pourquoi ?"))).Code);
        Assert.Empty(project.Database.All<StepSheet>());
    }

    [Fact]
    public async Task Validating_freezes_a_version_checks_the_version_read_and_logs_nothing()
    {
        var project = new TestProject();
        var step = project.Step(0, assignee: project.Contributor.Id);
        project.ActingAs(project.Contributor);
        await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Payer")), Token);
        var read = SheetOf(project, step).Version;
        await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Payer vite")), Token);
        Assert.Equal(ErrorCodes.VersionConflict, (await Refused(project, ValidateSheetCommand.WithVersion(step, read))).Code);
        Assert.Empty(project.Database.All<StepSheetVersion>());
        var outcome = await project.Runner.RunAsync(ValidateSheetCommand.WithVersion(step, SheetOf(project, step).Version), Token);
        Assert.Equal((1, true), (outcome.Result.VersionNumber, outcome.Result.Created));
        Assert.Equal(SheetStatus.Validated, SheetOf(project, step).Status);
        Assert.Equal(SheetStatus.Validated, outcome.View!.Steps.Single(view => view.Id == step).Sheet.Status);
        Assert.Null(outcome.OperationId);
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task Only_the_one_who_may_fill_validates_and_an_empty_sheet_is_refused()
    {
        var project = new TestProject();
        var step = project.Step(0);
        Assert.Equal(ErrorCodes.EmptySheet, (await Refused(project, new ValidateSheetCommand(step))).Code);
        await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Payer")), Token);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Contributor), new ValidateSheetCommand(step))).Code);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Reader), new ValidateSheetCommand(step))).Code);
        Assert.Empty(project.Database.All<StepSheetVersion>());
    }

    [Fact]
    public async Task The_facilitator_alone_asks_for_a_clarification_on_a_validated_sheet_which_goes_to_needs_review()
    {
        var project = new TestProject();
        var step = project.Step(0, assignee: project.Contributor.Id);
        await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Payer")), Token);
        Assert.Equal(ErrorCodes.InvalidSheetState, (await Refused(project, new RequestClarificationCommand(step, "Pourquoi ?"))).Code);
        await project.Runner.RunAsync(new ValidateSheetCommand(step), Token);
        Assert.Equal(ErrorCodes.AccessDenied,
            (await Refused(project.ActingAs(project.Contributor), new RequestClarificationCommand(step, "Pourquoi ?"))).Code);
        project.ActingAs(project.Facilitator);
        var empty = await Refused(project, new RequestClarificationCommand(step, " "));
        Assert.Equal((ErrorCodes.RequiredField, (object?)"text"), (empty.Code, empty.Details["field"]));
        Assert.Equal(ErrorCodes.VersionConflict,
            (await Refused(project, RequestClarificationCommand.WithVersion(step, "Pourquoi ?", SheetOf(project, step).Version - 1))).Code);
        var outcome = await project.Runner.RunAsync(RequestClarificationCommand.WithVersion(step, "Pourquoi ?", SheetOf(project, step).Version), Token);
        Assert.Equal((SheetStatus.NeedsReview, "Pourquoi ?"), (SheetOf(project, step).Status, SheetOf(project, step).ClarificationRequest));
        Assert.Equal(SheetStatus.NeedsReview, outcome.View!.Steps.Single(view => view.Id == step).Sheet.Status);
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task The_facilitator_alone_changes_or_withdraws_the_question_asked_with_the_version_of_the_sheet_and_nothing_to_undo()
    {
        var project = new TestProject();
        var step = project.Step(0, assignee: project.Contributor.Id);
        await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Payer")), Token);
        await project.Runner.RunAsync(new ValidateSheetCommand(step), Token);
        Assert.Equal(ErrorCodes.InvalidSheetState, (await Refused(project, new ChangeClarificationCommand(step, "Pourquoi ?"))).Code);
        Assert.Equal(ErrorCodes.InvalidSheetState, (await Refused(project, new WithdrawClarificationCommand(step))).Code);
        await project.Runner.RunAsync(new RequestClarificationCommand(step, "Pourquoi ?"), Token);
        foreach (var other in new[] { project.Contributor, project.Reader })
        {
            Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(other), new ChangeClarificationCommand(step, "Comment ?"))).Code);
            Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(other), new WithdrawClarificationCommand(step))).Code);
        }
        project.ActingAs(project.Facilitator);
        Assert.Equal(ErrorCodes.VersionConflict,
            (await Refused(project, ChangeClarificationCommand.WithVersion(step, "Comment ?", SheetOf(project, step).Version - 1))).Code);
        await project.Runner.RunAsync(ChangeClarificationCommand.WithVersion(step, "Comment ?", SheetOf(project, step).Version), Token);
        Assert.Equal((SheetStatus.NeedsReview, "Comment ?"), (SheetOf(project, step).Status, SheetOf(project, step).ClarificationRequest));
        Assert.Equal(ErrorCodes.VersionConflict,
            (await Refused(project, WithdrawClarificationCommand.WithVersion(step, SheetOf(project, step).Version - 1))).Code);
        var outcome = await project.Runner.RunAsync(WithdrawClarificationCommand.WithVersion(step, SheetOf(project, step).Version), Token);
        Assert.Equal((SheetStatus.Validated, (string?)null), (SheetOf(project, step).Status, SheetOf(project, step).ClarificationRequest));
        Assert.Equal(SheetStatus.Validated, outcome.View!.Steps.Single(view => view.Id == step).Sheet.Status);
        await AssertNothingToUndo(project);
    }
}
