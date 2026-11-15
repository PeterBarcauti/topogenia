// DocumentCommandsTests.cs
// Documents and uses through the template: right to fill, If-Match, view of the map, pain points, nothing logged (annex A 2.11, B 2.6-38).

using Topogenia.Application.Documents;
using Topogenia.Application.Maps;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Sheets.SheetFixture;

namespace Topogenia.Application.Tests.Documents;

public sealed class DocumentCommandsTests
{
    private static Document DocumentOf(TestProject project, Guid id) => project.Database.Get<Document>(Document.KeyOf(id));

    private static async Task<Guid> AddAsync(TestProject project, Guid stepId, string name) =>
        (await project.Runner.RunAsync(new AddDocumentCommand(stepId, Output(name)), Token)).Result;

    [Fact]
    public async Task An_added_output_is_written_logs_nothing_and_the_view_shows_its_marker_and_the_gauge()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var outcome = await project.Runner.RunAsync(new AddDocumentCommand(step, Output("Facture")), Token);
        Assert.Equal(("Facture", DocumentDirection.Output), (DocumentOf(project, outcome.Result).Name, DocumentOf(project, outcome.Result).Direction));
        Assert.Null(outcome.OperationId);
        var view = outcome.View!.Steps.Single(item => item.Id == step);
        Assert.Equal(outcome.Result, Assert.Single(view.Markers.Documents).DocumentId);
        Assert.Equal(14, view.Sheet.Completeness);
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task The_view_gives_each_document_marker_the_medium_of_its_document_held_or_received()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var step = project.Step(1);
        var typed = (await project.Runner.RunAsync(new AddDocumentCommand(producer, Output("Suivi.xlsx") with { Medium = "spreadsheet" }), Token)).Result;
        await AddAsync(project, producer, "Facture");
        var outcome = await project.Runner.RunAsync(new UseDocumentCommand(step, typed, new DocumentUseInput(1m)), Token);
        static IEnumerable<(string, DocumentDirection, string?)> Markers(StepView view) =>
            view.Markers.Documents.Select(marker => (marker.Name, marker.Direction, marker.Medium));
        Assert.Equal([("Suivi.xlsx", DocumentDirection.Output, "spreadsheet"), ("Facture", DocumentDirection.Output, null)],
            Markers(outcome.View!.Steps.Single(item => item.Id == producer)));
        // A document received through a use keeps the medium entered on the step that produces it.
        Assert.Equal([("Suivi.xlsx", DocumentDirection.Input, "spreadsheet")], Markers(outcome.View.Steps.Single(item => item.Id == step)));
    }

    [Fact]
    public async Task Documents_are_written_by_the_one_who_may_fill_only()
    {
        var project = new TestProject();
        var step = project.Step(0);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Reader), new AddDocumentCommand(step, Output("A")))).Code);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Contributor), new AddDocumentCommand(step, Output("A")))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project.ActingAs(project.Stranger), new AddDocumentCommand(step, Output("A")))).Code);
        Assert.Empty(project.Database.All<Document>());
        var assigned = project.Step(1, assignee: project.Contributor.Id);
        await project.ActingAs(project.Contributor).Runner.RunAsync(new AddDocumentCommand(assigned, ExternalInput("Relevé")), Token);
        Assert.Equal(4m, Assert.Single(project.Database.All<Document>()).WaitHours);
    }

    [Fact]
    public async Task The_entered_values_are_checked_by_the_domain()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var wait = await Refused(project, new AddDocumentCommand(step, new DocumentInput("output", "Facture", null, 2m, false)));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)DocumentRules.WaitHoursField), (wait.Code, wait.Details["field"]));
        Assert.Equal(ErrorCodes.InvalidAddress, (await Refused(project, new AddDocumentCommand(step, new DocumentInput("output", "Facture", "ici", null, false)))).Code);
        Assert.Equal(ErrorCodes.StepWithoutContent, (await Refused(project, new AddDocumentCommand(StepOfType(project, 1, StepType.End), Output("A")))).Code);
    }

    [Fact]
    public async Task An_updated_document_checks_the_version_read_and_belongs_to_its_step()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var other = project.Step(1);
        var document = await AddAsync(project, step, "Facture");
        await project.Runner.RunAsync(new UpdateDocumentCommand(step, document, Output("Facture signée")), Token);
        var stale = await Refused(project, UpdateDocumentCommand.WithVersion(step, document, Output("Avoir"), 1));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)project.Facilitator.Id), (stale.Code, stale.Details["updated_by"]));
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new UpdateDocumentCommand(other, document, Output("Avoir")))).Code);
        Assert.Equal("Facture signée", DocumentOf(project, document).Name);
        var outcome = await project.Runner.RunAsync(UpdateDocumentCommand.WithVersion(step, document, Output("Avoir"), 2), Token);
        Assert.Equal(("Avoir", 3), (DocumentOf(project, document).Name, DocumentOf(project, document).Version));
        Assert.Equal("Avoir", outcome.View!.Steps.Single(item => item.Id == step).Markers.Documents[0].Name);
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task A_used_document_raises_its_pain_point_on_the_producing_step_and_the_flow_carries_it()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var step = project.Step(1, assignee: project.Contributor.Id);
        var flow = Flow(project, producer, step);
        var document = await AddAsync(project, producer, "Facture");
        var outcome = await project.ActingAs(project.Contributor).Runner.RunAsync(
            new UseDocumentCommand(step, document, new DocumentUseInput(3m, 80, "Souvent en retard", 2.25m)), Token);
        var painPoint = Assert.Single(project.Database.All<PainPoint>());
        Assert.Equal((producer, "PRJ-I001", PainPointStatus.Reported, 2.3m), (painPoint.StepId, painPoint.Code, painPoint.Status, painPoint.LostHoursPerMonth));
        Assert.Equal((painPoint.Id, "PRJ-I001"), (outcome.Result.PainPointId, outcome.Result.PainPointCode));
        Assert.Equal(["Facture"], outcome.View!.Flows.Single(item => item.Id == flow).Documents);
        Assert.Equal(painPoint.Id, Assert.Single(outcome.View.Steps.Single(item => item.Id == producer).Markers.PainPoints).PainPointId);
        Assert.Equal((3m, 80), (Assert.Single(project.Database.All<DocumentUse>()).WaitHours, project.Database.All<DocumentUse>().Single().PercentCompleteAccurate));
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task A_use_checks_the_version_read_and_the_producer_must_be_another_step_of_the_map()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var step = project.Step(1);
        var document = await AddAsync(project, producer, "Facture");
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new UseDocumentCommand(producer, document, new DocumentUseInput(1m)))).Code);
        var detail = project.SubMap(producer);
        var elsewhere = project.Step(0, laneId: detail.LaneId, mapId: detail.MapId);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new UseDocumentCommand(elsewhere, document, new DocumentUseInput(1m)))).Code);
        await project.Runner.RunAsync(new UseDocumentCommand(step, document, new DocumentUseInput(1m)), Token);
        var created = Assert.Single(project.Database.All<DocumentUse>()).Version;
        await project.Runner.RunAsync(new UseDocumentCommand(step, document, new DocumentUseInput(2m)), Token);
        Assert.Equal(created + 1, Assert.Single(project.Database.All<DocumentUse>()).Version);
        Assert.Equal(ErrorCodes.VersionConflict, (await Refused(project, UseDocumentCommand.WithVersion(step, document, new DocumentUseInput(5m), created))).Code);
        await project.Runner.RunAsync(UseDocumentCommand.WithVersion(step, document, new DocumentUseInput(5m), created + 1), Token);
        Assert.Equal(5m, Assert.Single(project.Database.All<DocumentUse>()).WaitHours);
    }

    [Fact]
    public async Task Unticking_is_idempotent_and_erases_a_reported_pain_point()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var step = project.Step(1);
        var document = await AddAsync(project, producer, "Facture");
        await project.Runner.RunAsync(new UseDocumentCommand(step, document, new DocumentUseInput(1m, 50, "Incomplet")), Token);
        await project.Runner.RunAsync(new StopUsingDocumentCommand(step, document), Token);
        var again = await project.Runner.RunAsync(new StopUsingDocumentCommand(step, document), Token);
        Assert.Equal(document, again.Result);
        Assert.Empty(project.Database.All<DocumentUse>());
        Assert.Empty(project.Database.All<PainPoint>());
        Assert.Empty(project.Database.All<PainPointStatusChange>());
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Reader), new StopUsingDocumentCommand(step, document))).Code);
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task A_deleted_document_takes_its_uses_on_every_step_and_their_reported_pain_points()
    {
        var project = new TestProject();
        var producer = project.Step(0);
        var first = project.Step(1);
        var second = project.Step(2);
        var document = await AddAsync(project, producer, "Facture");
        await project.Runner.RunAsync(new UseDocumentCommand(first, document, new DocumentUseInput(1m, 50, "Incomplet")), Token);
        await project.Runner.RunAsync(new UseDocumentCommand(second, document, new DocumentUseInput(1m)), Token);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new DeleteDocumentCommand(first, document))).Code);
        Assert.Equal(2, project.Database.All<DocumentUse>().Count());
        var outcome = await project.Runner.RunAsync(new DeleteDocumentCommand(producer, document), Token);
        Assert.Empty(project.Database.All<Document>());
        Assert.Empty(project.Database.All<DocumentUse>());
        Assert.Empty(project.Database.All<PainPoint>());
        Assert.Empty(outcome.View!.Steps.Single(item => item.Id == producer).Markers.Documents);
        Assert.Equal(ErrorCodes.NotFound, (await Refused(project, new DeleteDocumentCommand(producer, document))).Code);
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task The_other_window_lists_the_outputs_of_the_map_except_the_step_and_its_predecessors_for_a_reader()
    {
        var project = new TestProject();
        var predecessor = project.Step(0);
        var step = project.Step(1);
        var far = project.Step(2);
        Flow(project, predecessor, step);
        await AddAsync(project, predecessor, "Bon");
        var used = await AddAsync(project, far, "Planning");
        await AddAsync(project, step, "Facture");
        await project.Runner.RunAsync(new UseDocumentCommand(step, used, new DocumentUseInput(null)), Token);
        var calls = project.Database.Calls.Count;
        var available = await project.ActingAs(project.Reader).Queries.RunAsync(new GetAvailableDocumentsQuery(step), Token);
        var group = Assert.Single(available);
        Assert.Equal((far, used, true), (group.Producer.Id, Assert.Single(group.Documents).Document.Id, group.Documents[0].Used));
        Assert.Equal(["access", "load"], project.Database.Calls.Skip(calls));
        Assert.Equal(WorkspaceContent.Documents, project.Database.LoadedContents[^1]);
        Assert.False(new GetAvailableDocumentsQuery(step).ReadsMapView);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() =>
            project.ActingAs(project.Stranger).Queries.RunAsync(new GetAvailableDocumentsQuery(step), Token))).Code);
    }

    private static async Task<string> NotReadAsync(TestProject project, Guid stepId, Guid documentId) =>
        (await Assert.ThrowsAsync<DomainException>(() => project.Queries.RunAsync(new GetStepDocumentQuery(stepId, documentId), Token))).Code;

    /// <summary>ADR 104: what « Open the folder » of the desktop mode reads of a document, as a reader of the sheet reads it.</summary>
    [Fact]
    public async Task The_document_a_step_declared_is_read_by_a_reader_with_its_location_the_documents_alone_loaded_and_nothing_written()
    {
        var project = new TestProject();
        var step = project.Step(0);
        const string folder = @"\\serveur\partage\dossier\inventaire.xlsx";
        var output = (await project.Runner.RunAsync(new AddDocumentCommand(step, Output("Inventaire") with { Location = folder }), Token)).Result;
        var input = (await project.Runner.RunAsync(new AddDocumentCommand(step, ExternalInput("Releve")), Token)).Result;
        var calls = project.Database.Calls.Count;
        var commits = project.Database.Commits;
        var read = await project.ActingAs(project.Reader).Queries.RunAsync(new GetStepDocumentQuery(step, output), Token);
        Assert.Equal((output, folder, LocationType.Path), (read.Id, read.Location, read.LocationType));
        Assert.Equal(["access", "load"], project.Database.Calls.Skip(calls));
        Assert.Equal(commits, project.Database.Commits);
        Assert.Equal(WorkspaceContent.Documents, project.Database.LoadedContents[^1]);
        Assert.False(new GetStepDocumentQuery(step, output).ReadsMapView);
        Assert.Equal(LocationType.Url, (await project.Queries.RunAsync(new GetStepDocumentQuery(step, input), Token)).LocationType);
    }

    [Fact]
    public async Task The_document_of_another_step_of_a_stranger_of_a_deleted_step_or_of_a_project_in_the_trash_is_not_found()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var other = project.Step(1);
        var document = await AddAsync(project, step, "Inventaire");
        Assert.Equal(ErrorCodes.NotFound, await NotReadAsync(project, other, document));
        Assert.Equal(ErrorCodes.NotFound, await NotReadAsync(project, step, project.NextId()));
        Assert.Equal(ErrorCodes.NotFound, await NotReadAsync(project.ActingAs(project.Stranger), step, document));
        // A step that uses the document of another does not declare it: the document is read under its own step only.
        await project.ActingAs(project.Facilitator).Runner.RunAsync(new UseDocumentCommand(other, document, new DocumentUseInput(1m)), Token);
        Assert.Equal(ErrorCodes.NotFound, await NotReadAsync(project, other, document));
        Assert.Equal(document, (await project.Queries.RunAsync(new GetStepDocumentQuery(step, document), Token)).Id);
        Delete(project, step);
        Assert.Equal(ErrorCodes.NotFound, await NotReadAsync(project, step, document));

        var trashed = new TestProject();
        var kept = trashed.Step(0);
        var lost = await AddAsync(trashed, kept, "Inventaire");
        trashed.PutInTrash();
        Assert.Equal(ErrorCodes.NotFound, await NotReadAsync(trashed.ActingAs(trashed.Reader), kept, lost));
    }
}
