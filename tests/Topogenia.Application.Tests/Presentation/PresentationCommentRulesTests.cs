// PresentationCommentRulesTests.cs
// The rules of a comment of presentation that no route shows alone (ADR 099 revised): what becomes of the translations an assistant proposes, the lines it writes, and the lines a package brings.

using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Presentation;

public sealed class PresentationCommentRulesTests
{
    private static readonly LanguageSettings Languages = new(["fr", "en"], "fr", new Dictionary<string, string> { ["fr"] = "Acteur 1" });

    private static PresentationComment Line(TestProject project, Guid step, string language, string text, CommentSource source = CommentSource.Manual, int version = 1) =>
        new(step, text, language, source, version, TestProject.Now, project.Facilitator.Id);

    private static ProjectWorkspace Workspace(TestProject project) => project.Database.Load(project.ProjectId, WorkspaceContent.None)!;

    private static OperationContext Context(TestProject project) => new(project.Contributor.Id, TestProject.Now.AddHours(1), "fr", AppSettings.Default);

    [Fact]
    public void A_translation_proposed_is_planned_on_the_original_of_today_and_one_typed_by_hand_is_kept_with_its_own_text()
    {
        var project = new TestProject();
        var free = project.Step(0);
        var typed = project.Step(1);
        var automatic = project.Step(2);
        PresentationComment[] lines =
        [
            Line(project, free, "fr", "Première."), Line(project, typed, "fr", "Deuxième."), Line(project, typed, "en", "Typed by hand."),
            Line(project, automatic, "fr", "Troisième."), Line(project, automatic, "en", "Older one.", CommentSource.Workbook, 3),
        ];

        var planned = PresentationCommentRules.Planned(Workspace(project), project.RootMapId, lines,
            [new(free, " Première. ", " First. "), new(typed, "Deuxième.", "Second."), new(automatic, "Troisième.", "Third.")], "en");

        Assert.Equal([(free, "Première.", "First.", false), (typed, "Deuxième.", "Typed by hand.", true), (automatic, "Troisième.", "Third.", false)],
            planned.Select(plan => (plan.Step.Id, plan.Original, plan.Text, plan.Kept)));
        var writes = PresentationCommentRules.Translated(planned, "en", Context(project));
        Assert.Equal([(free, 0, 1), (automatic, 3, 4)], writes.Select(write => (write.StepId, write.VersionRead, write.Line!.Version)));
        Assert.All(writes, write => Assert.Equal((CommentSource.Assistant, "en", project.Contributor.Id, TestProject.Now.AddHours(1)),
            (write.Line!.Source, write.Line.Language, write.Line.UpdatedBy, write.Line.UpdatedAt)));
    }

    [Fact]
    public void A_translation_proposed_is_left_aside_without_its_original_out_of_the_tree_or_into_the_language_of_origin()
    {
        var project = new TestProject();
        var changed = project.Step(0);
        var none = project.Step(1);
        var empty = project.Step(2);
        var parent = project.Step(3);
        var deleted = project.Step(4);
        var (subMap, subLane) = project.SubMap(parent);
        var below = project.Step(0, laneId: subLane, mapId: subMap);
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(deleted)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });
        PresentationComment[] lines =
        [
            Line(project, changed, "fr", "Nouvel original."), Line(project, empty, "fr", "Original."), Line(project, below, "fr", "Dans le détail."),
            Line(project, parent, "fr", "Sur la carte."), Line(project, deleted, "fr", "Supprimée."),
        ];
        CommentTranslation[] proposed =
        [
            new(changed, "Ancien original.", "Old."), new(none, "Sans commentaire.", "None."), new(empty, "Original.", "   "),
            new(below, "Dans le détail.", "In the detail."), new(parent, "Sur la carte.", "On the map."), new(deleted, "Supprimée.", "Deleted."),
            new(project.NextId(), "Inconnue.", "Unknown."),
        ];
        var workspace = Workspace(project);

        Assert.Equal([parent, below], PresentationCommentRules.Planned(workspace, project.RootMapId, lines, proposed, "en").Select(plan => plan.Step.Id).Order());
        // From the detailed map, only its own steps; into the language of origin of the steps, nothing.
        Assert.Equal([below], PresentationCommentRules.Planned(workspace, subMap, lines, proposed, "en").Select(plan => plan.Step.Id));
        Assert.Empty(PresentationCommentRules.Planned(workspace, project.RootMapId, lines, proposed, "fr"));
    }

    [Fact]
    public void A_step_named_twice_too_many_translations_or_an_invalid_text_refuse_the_proposal()
    {
        var project = new TestProject();
        var step = project.Step(0);
        PresentationComment[] lines = [Line(project, step, "fr", "Original.")];
        var workspace = Workspace(project);
        DomainException Refused(params CommentTranslation[] proposed) =>
            Assert.Throws<DomainException>(() => PresentationCommentRules.Planned(workspace, project.RootMapId, lines, proposed, "en"));

        var twice = Refused(new(step, "Original.", "One."), new(step, "Original.", "Two."));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"comments"), (twice.Code, twice.Details["field"]));
        var many = Refused([.. Enumerable.Range(0, PresentationCommentRules.ProposedMax + 1).Select(_ => new CommentTranslation(Guid.NewGuid(), "x", "y"))]);
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"comments"), (many.Code, many.Details["field"]));
        Assert.Equal(ErrorCodes.HtmlForbidden, Refused(new CommentTranslation(step, "Original.", "<b>Bold</b>")).Code);
        Assert.Equal(ErrorCodes.TextTooLong, Refused(new CommentTranslation(step, "Original.", new string('x', 501))).Code);
        Assert.Single(PresentationCommentRules.Planned(workspace, project.RootMapId, lines, [new(step, "Original.", new string('x', 500))], "en"));
    }

    [Fact]
    public void The_translations_of_a_proposal_come_only_with_the_language_of_a_translation()
    {
        var step = Guid.NewGuid();
        Assert.Empty(PresentationCommentRules.Proposed(null, null));
        Assert.Empty(PresentationCommentRules.Proposed([], null));
        Assert.Single(PresentationCommentRules.Proposed([new(step, "o", "t")], "en"));
        var refusal = Assert.Throws<DomainException>(() => PresentationCommentRules.Proposed([new(step, "o", "t")], null));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"comments"), (refusal.Code, refusal.Details["field"]));
    }

    [Fact]
    public void A_translation_already_written_by_the_assistant_with_the_same_text_is_not_written_again()
    {
        var project = new TestProject();
        var step = project.Step(0);
        PresentationComment[] lines = [Line(project, step, "fr", "Original."), Line(project, step, "en", "Same.", CommentSource.Assistant, 2)];
        var planned = PresentationCommentRules.Planned(Workspace(project), project.RootMapId, lines, [new(step, "Original.", "Same.")], "en");
        Assert.False(Assert.Single(planned).Kept);
        Assert.Empty(PresentationCommentRules.Translated(planned, "en", Context(project)));
    }

    [Fact]
    public void A_package_brings_the_lines_of_the_languages_configured_the_one_of_the_language_of_origin_of_its_step_as_a_text_typed()
    {
        var project = new TestProject();
        var step = project.Step(0);
        var other = project.Step(1);
        var origins = new Dictionary<Guid, string> { [step] = "fr", [other] = "en" };
        PresentationComment[] brought =
        [
            Line(project, step, "fr", "Original.", CommentSource.Workbook), Line(project, step, "en", "Translation.", CommentSource.Assistant),
            Line(project, step, "de", "Nicht konfiguriert."), Line(project, other, "fr", "Traduction.", CommentSource.Workbook),
        ];

        var kept = PresentationCommentRules.Imported(brought, origins, Languages);

        Assert.Equal([(step, "fr", CommentSource.Manual), (step, "en", CommentSource.Assistant), (other, "fr", CommentSource.Workbook)],
            kept.Select(line => (line.StepId, line.Language, line.Source)));
    }

    [Fact]
    public void An_empty_text_in_the_language_of_origin_removes_the_whole_comment_and_nothing_when_the_step_has_no_original()
    {
        var project = new TestProject();
        var alone = project.Step(0);
        var both = project.Step(1);
        PresentationComment[] lines =
        [
            Line(project, alone, "en", "Typed while presenting in English."), Line(project, both, "fr", "Original."),
            Line(project, both, "en", "Translated.", CommentSource.Assistant, 2),
        ];
        Step StepOf(Guid id) => project.Database.Get<Step>(Step.KeyOf(id));

        // No original: there is nothing an empty text replaces, the translation typed in another language stays (ADR 099, point 3).
        Assert.Empty(PresentationCommentRules.Typed(StepOf(alone), lines, null, "fr", Context(project)));
        // An original: the comment goes, every line of it.
        Assert.Equal([("fr", 1), ("en", 2)],
            PresentationCommentRules.Typed(StepOf(both), lines, null, "fr", Context(project)).Select(write => (write.Language, write.VersionRead)));
        Assert.All(PresentationCommentRules.Typed(StepOf(both), lines, null, "fr", Context(project)), write => Assert.Null(write.Line));
        // In another language, the line of that language alone, as before.
        Assert.Equal([("en", 1)], PresentationCommentRules.Typed(StepOf(alone), lines, null, "en", Context(project)).Select(write => (write.Language, write.VersionRead)));
    }

    [Theory]
    [InlineData("  not trimmed  ", "en")]
    [InlineData("<b>markup</b>", "en")]
    [InlineData("line\r\nends", "en")]
    [InlineData("Twice in one language.", "fr")]
    public void A_package_with_a_text_its_rule_refuses_or_two_lines_of_a_step_in_one_language_is_refused(string text, string language)
    {
        var project = new TestProject();
        var step = project.Step(0);
        var refusal = Assert.Throws<DomainException>(() => PresentationCommentRules.Imported(
            [Line(project, step, "fr", "Original."), Line(project, step, language, text)], new Dictionary<Guid, string> { [step] = "fr" }, Languages));
        Assert.Equal((ErrorCodes.InvalidPackage, "values"), (refusal.Code, refusal.Details["reason"]));
    }
}
