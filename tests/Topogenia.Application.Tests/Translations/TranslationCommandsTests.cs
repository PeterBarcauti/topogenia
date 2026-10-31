// TranslationCommandsTests.cs
// The translations through the templates: workbook list, import in one pass under the lock, manual translations of steps and flows (annex A 2.14).

using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Translations;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Sheets.SheetFixture;

namespace Topogenia.Application.Tests.Translations;

public sealed class TranslationCommandsTests
{
    private static TranslationRow Row(int line, TranslationObject type, Guid id, string original, string? english) =>
        new(line, Codes.Of(type), id, original, new Dictionary<string, string?> { ["en"] = english });

    private static Translation? English(TestProject project, TranslationObject type, Guid id) =>
        project.Database.All<Translation>().SingleOrDefault(item => item.ObjectType == type && item.ObjectId == id && item.Language == "en");

    private static Task<IReadOnlyList<TranslatableText>> ListAsync(TestProject project, string? scope) =>
        project.Queries.RunAsync(new GetTranslationsQuery(project.ProjectId, scope, Languages), Token);

    [Fact]
    public async Task The_facilitator_lists_the_texts_of_the_workbook_in_reading_order_without_lock_nor_map_view()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var texts = await ListAsync(project, null);
        Assert.Equal([(TranslationObject.Project, project.ProjectId), (TranslationObject.Lane, project.LaneId), (TranslationObject.Step, step)],
            texts.Select(text => (text.Type, text.Id)));
        Assert.Equal(["access", "load"], project.Database.Calls);
        Assert.Equal([WorkspaceContent.Translations], project.Database.LoadedContents);
        Assert.False(new GetTranslationsQuery(project.ProjectId, null, Languages).ReadsMapView);
    }

    [Fact]
    public async Task The_list_is_for_the_facilitator_and_an_unknown_scope_is_refused_after_the_right()
    {
        var project = new TestProject();
        Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() => ListAsync(project.ActingAs(project.Contributor), "all"))).Code);
        Assert.Equal(ErrorCodes.NotFound, (await Assert.ThrowsAsync<DomainException>(() => ListAsync(project.ActingAs(project.Stranger), "tout"))).Code);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => ListAsync(project.ActingAs(project.Facilitator), "tout"));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"scope"), (refusal.Code, refusal.Details["field"]));
        Assert.Equal([TranslationObject.Project, TranslationObject.Lane], (await ListAsync(project, "all")).Select(text => text.Type));
    }

    [Fact]
    public async Task An_import_writes_automatic_translations_under_the_lock_reports_the_ignored_rows_and_logs_nothing()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var outcome = await project.Runner.RunAsync(new ImportTranslationsCommand(project.ProjectId, "missing",
        [
            Row(2, TranslationObject.Project, project.ProjectId, "Achats fournisseurs", "Supplier purchasing"),
            Row(3, TranslationObject.Step, step, "Ancien libellé", "Old label"),
            Row(4, TranslationObject.Step, project.NextId(), "Absente", "Missing"),
            Row(5, TranslationObject.Lane, project.LaneId, "Acteur 1", null),
        ], Languages), Token);
        Assert.Equal(1, outcome.Result.Written);
        Assert.Equal([new IgnoredRow(3, IgnoredRowReasons.TextChanged), new IgnoredRow(4, IgnoredRowReasons.ObjectNotFound),
            new IgnoredRow(5, IgnoredRowReasons.NoTranslation)], outcome.Result.Ignored);
        Assert.Equal(("Supplier purchasing", TranslationSource.Auto),
            (English(project, TranslationObject.Project, project.ProjectId)!.Text, English(project, TranslationObject.Project, project.ProjectId)!.Source));
        Assert.Equal("Supplier purchasing", English(project, TranslationObject.Map, project.RootMapId)!.Text);
        Assert.Equal((1, 1, 1), (outcome.StructureVersion, project.Project.StructureVersion, project.Database.Commits));
        Assert.Contains("lock", project.Database.Calls);
        Assert.Equal((null, null), (outcome.View, outcome.OperationId));
        Assert.Empty(project.Database.Entries);
        Assert.DoesNotContain(TranslationObject.Project, (await ListAsync(project, "missing")).Select(text => text.Type));
    }

    [Fact]
    public async Task One_invalid_translation_refuses_the_whole_file_and_writes_nothing()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var refusal = await Refused(project, new ImportTranslationsCommand(project.ProjectId, "missing",
        [
            Row(2, TranslationObject.Project, project.ProjectId, "Achats fournisseurs", "Supplier purchasing"),
            Row(3, TranslationObject.Step, step, "Étape 0", "<b>Step</b>"),
        ], Languages));
        Assert.Equal(ErrorCodes.InvalidTranslations, refusal.Code);
        var error = Assert.Single((IReadOnlyList<TranslationRowError>)refusal.Details["errors"]!);
        Assert.Equal((3, "en", ErrorCodes.HtmlForbidden), (error.Line, error.Language, error.Code));
        Assert.Empty(project.Database.All<Translation>());
        Assert.Equal(0, project.Database.Commits);
    }

    [Fact]
    public async Task An_import_is_for_the_facilitator_and_bounded_to_twenty_thousand_rows()
    {
        var project = new TestProject();
        var rows = Enumerable.Range(1, TranslationImport.MaxRows + 1).Select(line => Row(line, TranslationObject.Step, Guid.Empty, "x", "y")).ToList();
        var refusal = await Refused(project, new ImportTranslationsCommand(project.ProjectId, "all", rows, Languages));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"rows"), (refusal.Code, refusal.Details["field"]));
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Contributor),
            new ImportTranslationsCommand(project.ProjectId, "all", [], Languages))).Code);
        var scope = await Refused(project.ActingAs(project.Facilitator), new ImportTranslationsCommand(project.ProjectId, "tout", [], Languages));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"scope"), (scope.Code, scope.Details["field"]));
    }

    [Fact]
    public async Task A_manual_translation_is_kept_by_the_scopes_missing_and_auto_and_overwritten_by_the_scope_all()
    {
        var project = new TestProject();
        var step = project.Step(0);
        await project.Runner.RunAsync(new UpdateStepTranslationsCommand(step, new Dictionary<string, string?> { ["en"] = "Manual" }, Languages), Token);
        var auto = await project.Runner.RunAsync(new ImportTranslationsCommand(project.ProjectId, "auto",
            [Row(2, TranslationObject.Step, step, "Étape 0", "Automatic")], Languages), Token);
        Assert.Equal([new IgnoredRow(2, IgnoredRowReasons.ManualTranslation)], auto.Result.Ignored);
        Assert.Equal("Manual", English(project, TranslationObject.Step, step)!.Text);
        await project.Runner.RunAsync(new ImportTranslationsCommand(project.ProjectId, "all",
            [Row(2, TranslationObject.Step, step, "Étape 0", "Automatic")], Languages), Token);
        Assert.Equal(("Automatic", TranslationSource.Auto), (English(project, TranslationObject.Step, step)!.Text, English(project, TranslationObject.Step, step)!.Source));
    }

    [Fact]
    public async Task The_facilitator_writes_the_manual_translations_of_a_step_and_the_view_shows_them()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var outcome = await project.Runner.RunAsync(new UpdateStepTranslationsCommand(step,
            new Dictionary<string, string?> { ["en"] = "Pay the invoices", ["fr"] = "Ignorée" }, Languages), Token);
        Assert.Equal(1, outcome.Result);
        Assert.Equal(("Pay the invoices", TranslationSource.Manual), (English(project, TranslationObject.Step, step)!.Text, English(project, TranslationObject.Step, step)!.Source));
        Assert.Equal("Pay the invoices", outcome.View!.Steps.Single(view => view.Id == step).Translations["en"]);
        Assert.Null(outcome.OperationId);
        await project.Runner.RunAsync(new UpdateStepTranslationsCommand(step, new Dictionary<string, string?> { ["en"] = "" }, Languages), Token);
        Assert.Null(English(project, TranslationObject.Step, step));
        var language = await Refused(project, new UpdateStepTranslationsCommand(step, new Dictionary<string, string?> { ["de"] = "Zahlen" }, Languages));
        Assert.Equal((ErrorCodes.InvalidLanguage, (object?)TranslationRules.TranslationsField), (language.Code, language.Details["field"]));
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Contributor),
            new UpdateStepTranslationsCommand(step, new Dictionary<string, string?> { ["en"] = "Pay" }, Languages))).Code);
        await AssertNothingToUndo(project);
    }

    [Fact]
    public async Task The_facilitator_writes_the_manual_translations_of_a_flow_within_the_length_of_its_label()
    {
        var project = new TestProject();
        var flow = Flow(project, project.Step(0), project.Step(1), "si urgent");
        var outcome = await project.Runner.RunAsync(new UpdateFlowTranslationsCommand(flow, new Dictionary<string, string?> { ["en"] = "if urgent" }, Languages), Token);
        Assert.Equal("if urgent", outcome.View!.Flows.Single(view => view.Id == flow).Translations["en"]);
        var tooLong = await Refused(project, new UpdateFlowTranslationsCommand(flow, new Dictionary<string, string?> { ["en"] = new string('x', 61) }, Languages));
        Assert.Equal(ErrorCodes.TextTooLong, tooLong.Code);
        Assert.Equal(ErrorCodes.AccessDenied, (await Refused(project.ActingAs(project.Reader),
            new UpdateFlowTranslationsCommand(flow, new Dictionary<string, string?> { ["en"] = "x" }, Languages))).Code);
        await AssertNothingToUndo(project);
    }
}
