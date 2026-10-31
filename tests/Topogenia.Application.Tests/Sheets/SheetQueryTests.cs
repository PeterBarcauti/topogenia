// SheetQueryTests.cs
// The read of a sheet: visibility of the draft, rights, responsible, suggestions for the one who may fill, documents (annex A 1, A 2.10).

using Topogenia.Application.Documents;
using Topogenia.Application.Ports;
using Topogenia.Application.Sheets;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Sheets.SheetFixture;

namespace Topogenia.Application.Tests.Sheets;

public sealed class SheetQueryTests
{
    private static async Task<SheetView> ReadAsync(TestProject project, Guid stepId) =>
        await project.Queries.RunAsync(new GetSheetQuery(stepId), Token);

    /// <summary>A step whose sheet was validated with « Payer », then changed into the draft « Payer vite ».</summary>
    private static async Task<Guid> ValidatedThenChangedAsync(TestProject project)
    {
        var step = project.Step(0, assignee: project.Contributor.Id);
        await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Payer")), Token);
        await project.Runner.RunAsync(new ValidateSheetCommand(step), Token);
        await project.Runner.RunAsync(new SaveSheetCommand(step, Purpose("Payer vite")), Token);
        return step;
    }

    [Fact]
    public async Task A_reader_sees_the_latest_validated_version_without_lock_writing_nor_map_view()
    {
        var project = new TestProject();
        var step = await ValidatedThenChangedAsync(project);
        var calls = project.Database.Calls.Count;
        var commits = project.Database.Commits;
        var sheet = await ReadAsync(project.ActingAs(project.Reader), step);
        Assert.Equal((SheetStatus.Validated, false, "Payer"), (sheet.Status, sheet.IsDraft, sheet.Content.Purpose));
        Assert.Equal((1, project.Facilitator.Id), (sheet.Validated!.Number, sheet.Validated.ValidatedBy));
        Assert.Null(sheet.Version);
        Assert.Equal(new SheetPermissions(false, false, false, false), sheet.Permissions);
        Assert.Equal(["access", "load"], project.Database.Calls.Skip(calls));
        Assert.Equal(commits, project.Database.Commits);
        Assert.Equal(WorkspaceContent.Sheets | WorkspaceContent.Documents, project.Database.LoadedContents[^1]);
        Assert.False(new GetSheetQuery(step).ReadsMapView);
    }

    [Fact]
    public async Task The_one_who_may_fill_sees_the_draft_with_the_version_to_send_back_and_his_rights()
    {
        var project = new TestProject();
        var step = await ValidatedThenChangedAsync(project);
        var sheet = await ReadAsync(project.ActingAs(project.Contributor), step);
        Assert.Equal((SheetStatus.Draft, true, "Payer vite"), (sheet.Status, sheet.IsDraft, sheet.Content.Purpose));
        Assert.Equal(SheetOf(project, step).Version, sheet.Version);
        Assert.Equal(1, sheet.Validated!.Number);
        Assert.Equal(new SheetPermissions(true, true, false, false), sheet.Permissions);
        Assert.Equal(new SheetResponsible(project.Contributor.Id, ResponsibleOrigin.Step), sheet.Responsible);
        var facilitator = await ReadAsync(project.ActingAs(project.Facilitator), step);
        Assert.Equal(new SheetPermissions(true, true, true, true), facilitator.Permissions);
    }

    [Fact]
    public async Task The_suggestions_are_given_only_to_the_one_who_may_fill()
    {
        var project = new TestProject();
        var before = project.Step(0);
        var step = project.Step(1, assignee: project.Contributor.Id);
        await project.Runner.RunAsync(new SaveSheetCommand(before, PurposeAndFrequency("Recevoir", "weekly")), Token);
        await project.Runner.RunAsync(new ValidateSheetCommand(before), Token);
        var filler = await ReadAsync(project.ActingAs(project.Contributor), step);
        Assert.Equal(new FrequencySuggestion(Frequency.Weekly, "PRJ-E001"), filler.Suggestions.Frequency);
        Assert.Equal(["Messagerie", "Tableur"], filler.Suggestions.Tools);
        var reader = await ReadAsync(project.ActingAs(project.Reader), step);
        Assert.Null(reader.Suggestions.Frequency);
        Assert.Empty(reader.Suggestions.Tools);
    }

    [Fact]
    public async Task The_sheet_shows_its_documents_and_their_pain_points_to_every_member_and_its_completeness()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var step = project.Step(1);
        Flow(project, producer, step);
        var document = (await project.Runner.RunAsync(new AddDocumentCommand(producer, Output("Facture")), Token)).Result;
        await project.Runner.RunAsync(new UseDocumentCommand(step, document, new(2m, 50, "Illisible")), Token);
        var sheet = await ReadAsync(project.ActingAs(project.Reader), step);
        var input = Assert.Single(sheet.Inputs);
        Assert.Equal((document, true, true), (input.Document.Id, input.Near, input.Used));
        Assert.Equal(("PRJ-I001", "Illisible"), (Assert.Single(sheet.PainPoints).Code, sheet.PainPoints[0].Description));
        Assert.Equal(SheetCompleteness.Percent(0, true, false), sheet.Completeness);
        var produced = await ReadAsync(project, producer);
        Assert.Equal(step, Assert.Single(Assert.Single(produced.Outputs).UsedBy).StepId);
    }

    [Fact]
    public async Task The_sheet_of_a_deleted_step_is_not_found_and_a_stranger_learns_nothing()
    {
        var project = new TestProject();
        var step = project.Step(0);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => ReadAsync(project.ActingAs(project.Stranger), step))).Code);
        Delete(project, step);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => ReadAsync(project.ActingAs(project.Reader), step))).Code);
    }
}
