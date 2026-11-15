// CommentWorkbookTests.cs
// The comments of presentation in the workbook of the translations (ADR 099 revised): listed after the texts of the core by scope, imported in the same unit of work as them with the same reasons to ignore a row, the whole file refused on one invalid translation.

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Application.Translations;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Selection.SelectionFixture;

namespace Topogenia.Application.Tests.Translations;

public sealed class CommentWorkbookTests
{
    private static readonly LanguageSettings Languages = new(["fr", "en", "de"], "fr", new Dictionary<string, string> { ["fr"] = "Acteur 1" });

    private static PresentationComment Line(TestProject project, Guid step, string language, string text, CommentSource source = CommentSource.Manual, int version = 1) =>
        new(step, text, language, source, version, TestProject.Now, project.Facilitator.Id);

    private static TranslationRow Comment(int line, Guid step, string? original, params (string Language, string? Text)[] cells) =>
        new(line, CommentWorkbook.RowType, step, original, cells.ToDictionary(cell => cell.Language, cell => cell.Text));

    private static async Task<IReadOnlyList<CommentText>> ListAsync(TestProject project, string? scope, params string[] targets) =>
        (await project.Queries.RunAsync(new GetTranslationsQuery(project.ProjectId, scope, Languages, targets.Length == 0 ? null : targets), Token)).Comments;

    private static Task<CommandOutcome<TranslationImportResult>> ImportAsync(TestProject project, string scope, params TranslationRow[] rows) =>
        project.Runner.RunAsync(new ImportTranslationsCommand(project.ProjectId, scope, rows, Languages), Token);

    private static async Task<DomainException> RefusedAsync(TestProject project, string scope, params TranslationRow[] rows) =>
        await Assert.ThrowsAsync<DomainException>(() => ImportAsync(project, scope, rows));

    [Fact]
    public async Task The_workbook_lists_the_comments_that_have_an_original_in_reading_order_with_their_translations_and_sources()
    {
        var project = new TestProject();
        var second = project.Step(1);
        var first = project.Step(0);
        var orphan = project.Step(2);
        var deleted = project.Step(3);
        project.Database.AddComment(Line(project, second, "fr", "Le responsable valide."));
        project.Database.AddComment(Line(project, first, "fr", "Le salarié saisit."));
        project.Database.AddComment(Line(project, first, "en", "The employee enters it.", CommentSource.Assistant));
        project.Database.AddComment(Line(project, first, "de", "Der Mitarbeiter erfasst.", CommentSource.Manual));
        project.Database.AddComment(Line(project, orphan, "en", "Without an original."));
        project.Database.AddComment(Line(project, deleted, "fr", "Étape supprimée."));
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(deleted)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });

        var comments = await ListAsync(project, "all");

        Assert.Equal([first, second], comments.Select(comment => comment.StepId));
        var listed = comments[0];
        Assert.Equal(("PRJ-E001", "fr", "Le salarié saisit."), (listed.Code, listed.Language, listed.Original));
        Assert.EndsWith(" / Acteur 1", listed.Context, StringComparison.Ordinal);
        Assert.Equal(new Dictionary<string, string> { ["en"] = "The employee enters it.", ["de"] = "Der Mitarbeiter erfasst." }, listed.Translations);
        Assert.Equal(new Dictionary<string, string> { ["en"] = "assistant", ["de"] = "manual" }, listed.Sources);
        Assert.Empty(comments[1].Translations);
    }

    [Fact]
    public async Task The_scope_filters_the_comments_as_the_texts_missing_automatic_or_all_on_the_languages_chosen()
    {
        var project = new TestProject();
        var none = project.Step(0);
        var automatic = project.Step(1);
        var typed = project.Step(2);
        foreach (var step in new[] { none, automatic, typed })
            project.Database.AddComment(Line(project, step, "fr", "Original."));
        project.Database.AddComment(Line(project, automatic, "en", "By the workbook.", CommentSource.Workbook));
        project.Database.AddComment(Line(project, typed, "en", "By hand."));

        Assert.Equal([none], (await ListAsync(project, "missing", "en")).Select(comment => comment.StepId));
        Assert.Equal([none, automatic], (await ListAsync(project, "auto", "en")).Select(comment => comment.StepId));
        Assert.Equal([none, automatic, typed], (await ListAsync(project, "all", "en")).Select(comment => comment.StepId));
        // German lacks everywhere; the language of origin alone asks nothing.
        Assert.Equal(3, (await ListAsync(project, null)).Count);
        Assert.Empty(await ListAsync(project, "missing", "fr"));
    }

    [Fact]
    public async Task An_import_writes_the_comments_with_the_translations_of_the_core_in_one_unit_and_counts_both()
    {
        var project = new TestProject();
        var step = project.Step(0);
        project.Database.AddComment(Line(project, step, "fr", "Le salarié saisit sa demande.\nLe responsable la reçoit."));

        var outcome = await ImportAsync(project, "missing",
            new TranslationRow(2, Codes.Of(TranslationObject.Step), step, "Étape 0", new Dictionary<string, string?> { ["en"] = "Step 0" }),
            Comment(3, step, "Le salarié saisit sa demande.\r\nLe responsable la reçoit.", ("en", " The employee enters the request.\r\nThe manager receives it. "), ("de", "  "),
                ("fr", "ignored"), ("eo", "Ne agordita.")));

        Assert.Equal((2, 0), (outcome.Result.Written, outcome.Result.Ignored.Count));
        var written = project.Database.Comments[(step, "en")];
        Assert.Equal(("The employee enters the request.\nThe manager receives it.", CommentSource.Workbook, 1, project.Facilitator.Id),
            (written.Text, written.Source, written.Version, written.UpdatedBy));
        Assert.Equal(2, project.Database.CommentsOf(step).Count);
        Assert.Single(project.Database.All<Translation>(), translation => translation.ObjectId == step);
        Assert.Equal((1, 1), (project.Database.Commits, project.Comments.Writes));
        Assert.Empty(project.Database.Entries);
    }

    [Fact]
    public async Task A_row_of_comment_is_ignored_for_the_same_reasons_as_a_text_of_the_core()
    {
        var project = new TestProject();
        var changed = project.Step(0);
        var typed = project.Step(1);
        var empty = project.Step(2);
        var orphan = project.Step(3);
        foreach (var step in new[] { changed, typed, empty })
            project.Database.AddComment(Line(project, step, "fr", "Original."));
        project.Database.AddComment(Line(project, typed, "en", "Typed by hand."));
        project.Database.AddComment(Line(project, orphan, "en", "No original."));

        var outcome = await ImportAsync(project, "auto",
            Comment(2, changed, "Ancien original.", ("en", "Old.")),
            Comment(3, typed, "Original.", ("en", "From the workbook."), ("de", "Aus der Arbeitsmappe.")),
            Comment(4, empty, null, ("fr", "Original."), ("en", null)),
            Comment(5, orphan, "No original.", ("de", "Kein Original.")),
            Comment(6, project.NextId(), "Original.", ("en", "Unknown step.")));

        Assert.Equal([new IgnoredRow(2, IgnoredRowReasons.TextChanged), new IgnoredRow(3, IgnoredRowReasons.ManualTranslation),
            new IgnoredRow(4, IgnoredRowReasons.NoTranslation), new IgnoredRow(5, IgnoredRowReasons.ObjectNotFound),
            new IgnoredRow(6, IgnoredRowReasons.ObjectNotFound)], outcome.Result.Ignored);
        // The German of the row whose English is typed is still written.
        Assert.Equal(1, outcome.Result.Written);
        Assert.Equal(("Typed by hand.", CommentSource.Manual), (project.Database.Comments[(typed, "en")].Text, project.Database.Comments[(typed, "en")].Source));
        Assert.Equal(CommentSource.Workbook, project.Database.Comments[(typed, "de")].Source);
    }

    [Fact]
    public async Task The_scope_all_replaces_a_translation_typed_and_the_same_file_again_writes_nothing_more()
    {
        var project = new TestProject();
        var step = project.Step(0);
        project.Database.AddComment(Line(project, step, "fr", "Original."));
        project.Database.AddComment(Line(project, step, "en", "Typed by hand.", CommentSource.Manual, 4));
        var row = Comment(2, step, "Original.", ("en", "From the workbook."));

        Assert.Equal(1, (await ImportAsync(project, "all", row)).Result.Written);
        var replaced = project.Database.Comments[(step, "en")];
        Assert.Equal(("From the workbook.", CommentSource.Workbook, 5), (replaced.Text, replaced.Source, replaced.Version));
        var lines = project.Comments.Lines;
        Assert.Equal(1, (await ImportAsync(project, "all", row)).Result.Written);
        Assert.Equal((lines, 5), (project.Comments.Lines, project.Database.Comments[(step, "en")].Version));
    }

    [Fact]
    public async Task Of_two_rows_on_the_same_comment_the_last_one_is_written()
    {
        var project = new TestProject();
        var step = project.Step(0);
        project.Database.AddComment(Line(project, step, "fr", "Original."));
        var outcome = await ImportAsync(project, "all", Comment(2, step, "Original.", ("en", "First.")), Comment(3, step, "Original.", ("en", "Second.")));
        Assert.Equal(2, outcome.Result.Written);
        Assert.Equal(("Second.", 1), (project.Database.Comments[(step, "en")].Text, project.Database.Comments[(step, "en")].Version));
    }

    [Fact]
    public async Task One_invalid_translation_of_a_comment_refuses_the_whole_file_with_the_errors_of_the_core_and_of_the_comments()
    {
        var project = new TestProject();
        var step = project.Step(0);
        project.Database.AddComment(Line(project, step, "fr", "Original."));
        var valid = new TranslationRow(2, Codes.Of(TranslationObject.Project), project.ProjectId, "Achats fournisseurs", new Dictionary<string, string?> { ["en"] = "Purchasing" });
        var comment = Comment(4, step, "Original.", ("en", new string('x', PresentationCommentRules.TextMax + 1)), ("de", "<b>fett</b>"));

        var alone = await RefusedAsync(project, "missing", valid, comment);
        Assert.Equal(ErrorCodes.InvalidTranslations, alone.Code);
        Assert.Equal([(4, "de", ErrorCodes.HtmlForbidden, 500), (4, "en", ErrorCodes.TextTooLong, 500)],
            ((IReadOnlyList<TranslationRowError>)alone.Details["errors"]!).Select(error => (error.Line, error.Language, error.Code, error.Max)));

        var core = new TranslationRow(3, Codes.Of(TranslationObject.Step), step, "Étape 0", new Dictionary<string, string?> { ["en"] = "<i>Step</i>" });
        var both = await RefusedAsync(project, "missing", valid, core, comment);
        Assert.Equal([3, 4, 4], ((IReadOnlyList<TranslationRowError>)both.Details["errors"]!).Select(error => error.Line));

        Assert.Empty(project.Database.All<Translation>());
        Assert.Equal([(step, "fr")], project.Database.Comments.Keys);
        Assert.Equal((0, 0), (project.Database.Commits, project.Comments.Writes));
    }

    [Fact]
    public async Task The_bound_of_twenty_thousand_rows_counts_the_comments_with_the_texts()
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
    public async Task A_comment_that_cannot_be_written_takes_the_translations_of_the_core_back_with_it()
    {
        var project = new TestProject();
        var step = project.Step(0);
        project.Database.AddComment(Line(project, step, "fr", "Original."));
        var failing = new CommandRunnerWith(project, new FailingComments(project.Comments));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.Runner.RunAsync(new ImportTranslationsCommand(project.ProjectId, "all",
        [
            new TranslationRow(2, Codes.Of(TranslationObject.Step), step, "Étape 0", new Dictionary<string, string?> { ["en"] = "Step 0" }),
            Comment(3, step, "Original.", ("en", "Translated.")),
        ], Languages), Token));

        Assert.Empty(project.Database.All<Translation>());
        Assert.Equal(0, project.Database.Commits);
    }

    [Fact]
    public async Task An_import_run_again_after_a_deadlock_plans_and_writes_its_comments_once()
    {
        var project = new TestProject();
        var step = project.Step(0);
        project.Database.AddComment(Line(project, step, "fr", "Original."));
        project.Database.CommitFailures.Enqueue(new TimeoutException("deadlock"));
        var outcome = await ImportAsync(project, "all", Comment(2, step, "Original.", ("en", "Translated.")));
        Assert.Equal(1, outcome.Result.Written);
        Assert.Equal(("Translated.", 1), (project.Database.Comments[(step, "en")].Text, project.Database.Comments[(step, "en")].Version));
    }

    /// <summary>A store of comments that reads as the real one and fails at its first write, as a refused statement would.</summary>
    private sealed class FailingComments(IPresentationCommentStore inner) : IPresentationCommentStore
    {
        public Task<IReadOnlyList<PresentationComment>> OfStepsAsync(IReadOnlyCollection<Guid> stepIds, CancellationToken cancellationToken) =>
            inner.OfStepsAsync(stepIds, cancellationToken);

        public Task WriteAsync(IReadOnlyList<CommentWrite> writes, CancellationToken cancellationToken) => throw new InvalidOperationException("refused");

        public Task AddAsync(IReadOnlyList<PresentationComment> comments, CancellationToken cancellationToken) => throw new InvalidOperationException("refused");
    }

    /// <summary>The command template of a project on another store of comments.</summary>
    private sealed class CommandRunnerWith(TestProject project, IPresentationCommentStore comments)
    {
        public CommandRunner Runner { get; } = new(project.Unit, new InMemoryAccessReader(project.Database), new InMemoryWorkspaceStore(project.Database),
            new InMemoryOperationLog(project.Database), new MapViewBuilder(AppSettings.Default), project.CurrentUser, project.Clock, AppSettings.Default,
            project.Versions, new BesidePorts(comments, project.FlowComments));
    }
}
