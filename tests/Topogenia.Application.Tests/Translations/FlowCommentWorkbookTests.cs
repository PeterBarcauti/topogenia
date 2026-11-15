// FlowCommentWorkbookTests.cs
// The comments of presentation of the flows in the workbook of the translations (ADR 099, lot J10): listed beside those of the steps by scope and named as a labelled flow, imported in the same unit of work with the same reasons to ignore a row, the whole file refused on one invalid translation.

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Translations;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Translations;

public sealed class FlowCommentWorkbookTests
{
    private static readonly LanguageSettings Languages = new(["fr", "en", "de"], "fr", new Dictionary<string, string> { ["fr"] = "Acteur 1" });

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FlowPresentationComment Line(TestProject project, Guid flow, string language, string text, CommentSource source = CommentSource.Manual, int version = 1) =>
        new(flow, text, language, source, version, TestProject.Now, project.Facilitator.Id);

    private static PresentationComment StepLine(TestProject project, Guid step, string language, string text) =>
        new(step, text, language, CommentSource.Manual, 1, TestProject.Now, project.Facilitator.Id);

    private static TranslationRow Comment(int line, Guid flow, string? original, params (string Language, string? Text)[] cells) =>
        new(line, FlowCommentWorkbook.RowType, flow, original, cells.ToDictionary(cell => cell.Language, cell => cell.Text));

    private static Task<WorkbookTexts> WorkbookAsync(TestProject project, string? scope, params string[] targets) =>
        project.Queries.RunAsync(new GetTranslationsQuery(project.ProjectId, scope, Languages, targets.Length == 0 ? null : targets), Token);

    private static async Task<IReadOnlyList<Guid>> ListAsync(TestProject project, string? scope, params string[] targets) =>
        [.. (await WorkbookAsync(project, scope, targets)).FlowComments.Select(comment => comment.FlowId)];

    private static Task<CommandOutcome<TranslationImportResult>> ImportAsync(TestProject project, string scope, params TranslationRow[] rows) =>
        project.Runner.RunAsync(new ImportTranslationsCommand(project.ProjectId, scope, rows, Languages), Token);

    private static async Task<DomainException> RefusedAsync(TestProject project, string scope, params TranslationRow[] rows) =>
        await Assert.ThrowsAsync<DomainException>(() => ImportAsync(project, scope, rows));

    private static void PutInTrash(TestProject project, Guid flow) =>
        project.Database.Add(project.Database.Get<Flow>(Flow.KeyOf(flow)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });

    [Fact]
    public async Task The_workbook_lists_the_comments_of_the_flows_that_have_an_original_in_reading_order_named_as_a_labelled_flow()
    {
        var project = new TestProject();
        var (a, b, c) = (project.Step(0), project.Step(1), project.Step(2));
        var last = project.Flow(b, c);
        var first = project.Flow(a, b);
        var second = project.Flow(a, c);
        var orphan = project.Flow(c, a);
        var deleted = project.Flow(c, b);
        project.Database.AddFlowComment(Line(project, last, "fr", "Le dossier part au paiement."));
        project.Database.AddFlowComment(Line(project, first, "fr", "La demande part chez le responsable."));
        project.Database.AddFlowComment(Line(project, first, "en", "The request goes to the manager.", CommentSource.Assistant));
        project.Database.AddFlowComment(Line(project, first, "de", "Der Antrag geht an die Leitung."));
        project.Database.AddFlowComment(Line(project, second, "fr", "Sans validation, le dossier avance."));
        project.Database.AddFlowComment(Line(project, orphan, "en", "Without an original."));
        project.Database.AddFlowComment(Line(project, deleted, "fr", "Flèche supprimée."));
        PutInTrash(project, deleted);
        project.Database.AddComment(StepLine(project, a, "fr", "Le salarié saisit."));

        var workbook = await WorkbookAsync(project, "all");

        // The comments of the steps keep their own list; those of the flows come by source, then by target.
        Assert.Equal([a], workbook.Comments.Select(comment => comment.StepId));
        Assert.Equal([first, second, last], workbook.FlowComments.Select(comment => comment.FlowId));
        var listed = workbook.FlowComments[0];
        Assert.Equal(("PRJ-E001 > PRJ-E002", "Étape 0 > Étape 1", "fr", "La demande part chez le responsable."), (listed.Code, listed.Context, listed.Language, listed.Original));
        Assert.Equal(new Dictionary<string, string> { ["en"] = "The request goes to the manager.", ["de"] = "Der Antrag geht an die Leitung." }, listed.Translations);
        Assert.Equal(new Dictionary<string, string> { ["en"] = "assistant", ["de"] = "manual" }, listed.Sources);
        Assert.Empty(workbook.FlowComments[1].Translations);
    }

    [Fact]
    public async Task The_original_of_the_comment_of_a_flow_is_its_line_in_the_language_of_origin_of_its_source_step()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(a)) with { SourceLanguage = "en" });
        var flow = project.Flow(a, b);
        var back = project.Flow(b, a);
        project.Database.AddFlowComment(Line(project, flow, "en", "The request goes on."));
        project.Database.AddFlowComment(Line(project, flow, "fr", "La demande continue.", CommentSource.Workbook));
        project.Database.AddFlowComment(Line(project, back, "en", "Only a translation: the source of this flow is written in French."));

        var listed = Assert.Single((await WorkbookAsync(project, "all")).FlowComments);

        Assert.Equal((flow, "en", "The request goes on."), (listed.FlowId, listed.Language, listed.Original));
        Assert.Equal(new Dictionary<string, string> { ["fr"] = "La demande continue." }, listed.Translations);
        Assert.Equal(new Dictionary<string, string> { ["fr"] = "workbook" }, listed.Sources);
    }

    [Fact]
    public async Task The_scope_filters_the_comments_of_the_flows_as_the_texts_missing_automatic_or_all_on_the_languages_chosen()
    {
        var project = new TestProject();
        var (a, b, c, d) = (project.Step(0), project.Step(1), project.Step(2), project.Step(3));
        var none = project.Flow(a, b);
        var automatic = project.Flow(b, c);
        var typed = project.Flow(c, d);
        foreach (var flow in new[] { none, automatic, typed })
            project.Database.AddFlowComment(Line(project, flow, "fr", "Original."));
        project.Database.AddFlowComment(Line(project, automatic, "en", "By the workbook.", CommentSource.Workbook));
        project.Database.AddFlowComment(Line(project, typed, "en", "By hand."));

        Assert.Equal([none], await ListAsync(project, "missing", "en"));
        Assert.Equal([none, automatic], await ListAsync(project, "auto", "en"));
        Assert.Equal([none, automatic, typed], await ListAsync(project, "all", "en"));
        // German lacks everywhere; the language of origin alone asks nothing.
        Assert.Equal(3, (await ListAsync(project, null)).Count);
        Assert.Empty(await ListAsync(project, "missing", "fr"));
    }

    [Fact]
    public async Task An_import_writes_the_comments_of_the_flows_with_those_of_the_steps_and_the_translations_of_the_core_in_one_unit_and_counts_them_all()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var flow = project.Flow(a, b);
        project.Database.AddComment(StepLine(project, a, "fr", "Le salarié saisit sa demande."));
        project.Database.AddFlowComment(Line(project, flow, "fr", "La demande part chez le responsable.\nIl la reçoit le lendemain."));

        var outcome = await ImportAsync(project, "missing",
            new TranslationRow(2, Codes.Of(TranslationObject.Step), a, "Étape 0", new Dictionary<string, string?> { ["en"] = "Step 0" }),
            new TranslationRow(3, CommentWorkbook.RowType, a, "Le salarié saisit sa demande.", new Dictionary<string, string?> { ["en"] = "The employee enters the request." }),
            Comment(4, flow, "La demande part chez le responsable.\r\nIl la reçoit le lendemain.", ("en", " The request goes to the manager.\r\nHe receives it the next day. "),
                ("de", "  "), ("fr", "ignored"), ("eo", "Ne agordita.")));

        Assert.Equal((3, 0), (outcome.Result.Written, outcome.Result.Ignored.Count));
        var written = project.Database.FlowComments[(flow, "en")];
        Assert.Equal(("The request goes to the manager.\nHe receives it the next day.", CommentSource.Workbook, 1, project.Facilitator.Id),
            (written.Text, written.Source, written.Version, written.UpdatedBy));
        Assert.Equal(2, project.Database.FlowCommentsOf(flow).Count);
        Assert.Equal(CommentSource.Workbook, project.Database.Comments[(a, "en")].Source);
        Assert.Single(project.Database.All<Translation>(), translation => translation.ObjectId == a);
        Assert.Equal((1, 1, 1), (project.Database.Commits, project.Comments.Writes, project.FlowComments.Writes));
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_row_of_comment_of_a_flow_is_ignored_for_the_same_reasons_as_a_text_of_the_core()
    {
        var project = new TestProject();
        var steps = Enumerable.Range(0, 6).Select(column => project.Step(column)).ToList();
        var changed = project.Flow(steps[0], steps[1]);
        var typed = project.Flow(steps[1], steps[2]);
        var empty = project.Flow(steps[2], steps[3]);
        var orphan = project.Flow(steps[3], steps[4]);
        var deleted = project.Flow(steps[4], steps[5]);
        foreach (var flow in new[] { changed, typed, empty, deleted })
            project.Database.AddFlowComment(Line(project, flow, "fr", "Original."));
        project.Database.AddFlowComment(Line(project, typed, "en", "Typed by hand."));
        project.Database.AddFlowComment(Line(project, orphan, "en", "No original."));
        PutInTrash(project, deleted);
        // A step is no flow: a row of comment of a flow that names one finds nothing, whatever the comment of the step.
        project.Database.AddComment(StepLine(project, steps[0], "fr", "Original."));

        var outcome = await ImportAsync(project, "auto",
            Comment(2, changed, "Ancien original.", ("en", "Old.")),
            Comment(3, typed, "Original.", ("en", "From the workbook."), ("de", "Aus der Arbeitsmappe.")),
            Comment(4, empty, null, ("fr", "Original."), ("en", null)),
            Comment(5, orphan, "No original.", ("de", "Kein Original.")),
            Comment(6, deleted, "Original.", ("en", "In the trash.")),
            Comment(7, steps[0], "Original.", ("en", "A step.")),
            Comment(8, project.NextId(), "Original.", ("en", "Unknown flow.")));

        Assert.Equal([new IgnoredRow(2, IgnoredRowReasons.TextChanged), new IgnoredRow(3, IgnoredRowReasons.ManualTranslation),
            new IgnoredRow(4, IgnoredRowReasons.NoTranslation), new IgnoredRow(5, IgnoredRowReasons.ObjectNotFound),
            new IgnoredRow(6, IgnoredRowReasons.ObjectNotFound), new IgnoredRow(7, IgnoredRowReasons.ObjectNotFound),
            new IgnoredRow(8, IgnoredRowReasons.ObjectNotFound)], outcome.Result.Ignored);
        // The German of the row whose English is typed is still written.
        Assert.Equal(1, outcome.Result.Written);
        Assert.Equal(("Typed by hand.", CommentSource.Manual), (project.Database.FlowComments[(typed, "en")].Text, project.Database.FlowComments[(typed, "en")].Source));
        Assert.Equal(CommentSource.Workbook, project.Database.FlowComments[(typed, "de")].Source);
        Assert.Equal([(steps[0], "fr")], project.Database.Comments.Keys);
    }

    [Fact]
    public async Task The_scope_all_replaces_a_translation_typed_the_same_file_again_writes_nothing_more_and_of_two_rows_the_last_one_is_written()
    {
        var project = new TestProject();
        var (a, b, c) = (project.Step(0), project.Step(1), project.Step(2));
        var flow = project.Flow(a, b);
        var twice = project.Flow(b, c);
        project.Database.AddFlowComment(Line(project, flow, "fr", "Original."));
        project.Database.AddFlowComment(Line(project, flow, "en", "Typed by hand.", CommentSource.Manual, 4));
        project.Database.AddFlowComment(Line(project, twice, "fr", "Original."));
        var row = Comment(2, flow, "Original.", ("en", "From the workbook."));

        Assert.Equal(1, (await ImportAsync(project, "all", row)).Result.Written);
        var replaced = project.Database.FlowComments[(flow, "en")];
        Assert.Equal(("From the workbook.", CommentSource.Workbook, 5), (replaced.Text, replaced.Source, replaced.Version));
        var lines = project.FlowComments.Lines;
        Assert.Equal(1, (await ImportAsync(project, "all", row)).Result.Written);
        Assert.Equal((lines, 5), (project.FlowComments.Lines, project.Database.FlowComments[(flow, "en")].Version));

        var outcome = await ImportAsync(project, "all", Comment(2, twice, "Original.", ("en", "First.")), Comment(3, twice, "Original.", ("en", "Second.")));
        Assert.Equal(2, outcome.Result.Written);
        Assert.Equal(("Second.", 1), (project.Database.FlowComments[(twice, "en")].Text, project.Database.FlowComments[(twice, "en")].Version));
    }

    [Fact]
    public async Task One_invalid_translation_of_the_comment_of_a_flow_refuses_the_whole_file_with_the_errors_of_the_core_and_of_every_comment()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var flow = project.Flow(a, b);
        project.Database.AddComment(StepLine(project, a, "fr", "Original."));
        project.Database.AddFlowComment(Line(project, flow, "fr", "Original."));
        var valid = new TranslationRow(2, Codes.Of(TranslationObject.Project), project.ProjectId, "Achats fournisseurs", new Dictionary<string, string?> { ["en"] = "Purchasing" });
        var comment = Comment(5, flow, "Original.", ("en", new string('x', PresentationCommentRules.TextMax + 1)), ("de", "<b>fett</b>"));

        var alone = await RefusedAsync(project, "missing", valid, comment);
        Assert.Equal(ErrorCodes.InvalidTranslations, alone.Code);
        Assert.Equal([(5, "de", ErrorCodes.HtmlForbidden, 500), (5, "en", ErrorCodes.TextTooLong, 500)],
            ((IReadOnlyList<TranslationRowError>)alone.Details["errors"]!).Select(error => (error.Line, error.Language, error.Code, error.Max)));

        var core = new TranslationRow(3, Codes.Of(TranslationObject.Step), a, "Étape 0", new Dictionary<string, string?> { ["en"] = "<i>Step</i>" });
        var step = new TranslationRow(4, CommentWorkbook.RowType, a, "Original.", new Dictionary<string, string?> { ["en"] = "<u>Step</u>" });
        var all = await RefusedAsync(project, "missing", valid, core, step, comment);
        Assert.Equal([3, 4, 5, 5], ((IReadOnlyList<TranslationRowError>)all.Details["errors"]!).Select(error => error.Line));

        Assert.Empty(project.Database.All<Translation>());
        Assert.Equal([(flow, "fr")], project.Database.FlowComments.Keys);
        Assert.Equal((0, 0, 0), (project.Database.Commits, project.Comments.Writes, project.FlowComments.Writes));
    }

    [Fact]
    public async Task The_bound_of_twenty_thousand_rows_counts_the_comments_of_the_flows_with_the_texts()
    {
        var project = new TestProject();
        var half = TranslationImport.MaxRows / 2;
        var rows = Enumerable.Range(1, half).Select(line => new TranslationRow(line, Codes.Of(TranslationObject.Step), Guid.Empty, "x", null))
            .Concat(Enumerable.Range(half + 1, half + 1).Select(line => Comment(line, Guid.Empty, "x"))).ToArray();
        var refusal = await RefusedAsync(project, "all", rows);
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"rows"), (refusal.Code, refusal.Details["field"]));
        Assert.Equal(TranslationImport.MaxRows, (await ImportAsync(project, "all", rows[1..])).Result.Ignored.Count);
    }

    [Fact]
    public async Task A_comment_of_a_flow_that_cannot_be_written_takes_the_comments_of_the_steps_and_the_translations_of_the_core_back_with_it()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var flow = project.Flow(a, b);
        project.Database.AddComment(StepLine(project, a, "fr", "Original."));
        project.Database.AddFlowComment(Line(project, flow, "fr", "Original."));
        var failing = new CommandRunner(project.Unit, new InMemoryAccessReader(project.Database), new InMemoryWorkspaceStore(project.Database),
            new InMemoryOperationLog(project.Database), new MapViewBuilder(AppSettings.Default), project.CurrentUser, project.Clock, AppSettings.Default,
            project.Versions, new BesidePorts(project.Comments, new FailingFlowComments(project.FlowComments)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.RunAsync(new ImportTranslationsCommand(project.ProjectId, "all",
        [
            new TranslationRow(2, Codes.Of(TranslationObject.Step), a, "Étape 0", new Dictionary<string, string?> { ["en"] = "Step 0" }),
            new TranslationRow(3, CommentWorkbook.RowType, a, "Original.", new Dictionary<string, string?> { ["en"] = "Translated." }),
            Comment(4, flow, "Original.", ("en", "Translated.")),
        ], Languages), Token));

        Assert.Empty(project.Database.All<Translation>());
        Assert.Equal([(a, "fr")], project.Database.Comments.Keys);
        Assert.Equal(0, project.Database.Commits);
    }

    [Fact]
    public async Task An_import_run_again_after_a_deadlock_plans_and_writes_the_comments_of_its_flows_once()
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var flow = project.Flow(a, b);
        project.Database.AddFlowComment(Line(project, flow, "fr", "Original."));
        project.Database.CommitFailures.Enqueue(new TimeoutException("deadlock"));
        var outcome = await ImportAsync(project, "all", Comment(2, flow, "Original.", ("en", "Translated.")));
        Assert.Equal(1, outcome.Result.Written);
        Assert.Equal(("Translated.", 1), (project.Database.FlowComments[(flow, "en")].Text, project.Database.FlowComments[(flow, "en")].Version));
    }

    /// <summary>A store of the comments of the flows that reads as the real one and fails at its first write, as a refused statement would.</summary>
    private sealed class FailingFlowComments(IFlowPresentationCommentStore inner) : IFlowPresentationCommentStore
    {
        public Task<IReadOnlyList<FlowPresentationComment>> OfFlowsAsync(IReadOnlyCollection<Guid> flowIds, CancellationToken cancellationToken) =>
            inner.OfFlowsAsync(flowIds, cancellationToken);

        public Task WriteAsync(IReadOnlyList<FlowCommentWrite> writes, CancellationToken cancellationToken) => throw new InvalidOperationException("refused");

        public Task AddAsync(IReadOnlyList<FlowPresentationComment> comments, CancellationToken cancellationToken) => throw new InvalidOperationException("refused");
    }
}
