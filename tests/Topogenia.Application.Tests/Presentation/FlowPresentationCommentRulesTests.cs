// FlowPresentationCommentRulesTests.cs
// The rules of the comment of presentation of a flow that no route shows alone (ADR 099, lot J10): its subject, the flow with the language of origin of its source step, the lines a package brings, and the rules it shares with the comment of a step.

using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Presentation;

public sealed class FlowPresentationCommentRulesTests
{
    private static readonly LanguageSettings Languages = new(["fr", "en"], "fr", new Dictionary<string, string> { ["fr"] = "Acteur 1" });

    private static FlowPresentationComment Line(TestProject project, Guid flow, string language, string text, CommentSource source = CommentSource.Manual, int version = 1) =>
        new(flow, text, language, source, version, TestProject.Now, project.Facilitator.Id);

    private static ProjectWorkspace Workspace(TestProject project) => project.Database.Load(project.ProjectId, WorkspaceContent.None)!;

    private static OperationContext Context(TestProject project) => new(project.Contributor.Id, TestProject.Now.AddHours(1), "fr", AppSettings.Default);

    private static void SetLanguage(TestProject project, Guid step, string language) =>
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(step)) with { SourceLanguage = language });

    [Fact]
    public void The_subject_of_the_comment_of_a_flow_is_the_flow_with_the_language_of_origin_of_its_source_step()
    {
        var project = new TestProject();
        var (source, target) = (project.Step(0), project.Step(1));
        SetLanguage(project, source, "en");
        var flow = project.Flow(source, target);
        var workspace = Workspace(project);

        var subject = FlowPresentationCommentRules.Subject(workspace, workspace.LiveFlow(flow));

        Assert.Equal(new CommentSubject(flow, "en"), subject);
        Assert.Equal((true, false), (subject.WritesOriginalIn("en"), subject.WritesOriginalIn("fr")));
        // The reverse flow takes the language of its own source step, and no step but the source gives the language.
        var back = project.Flow(target, source);
        workspace = Workspace(project);
        Assert.Equal(new CommentSubject(back, "fr"), FlowPresentationCommentRules.Subject(workspace, workspace.LiveFlow(back)));
        Assert.Throws<ArgumentException>(() => CommentSubject.Of(workspace.LiveFlow(flow), workspace.LiveStep(target)));
    }

    [Fact]
    public void A_flow_whose_source_step_is_not_live_has_no_subject()
    {
        var project = new TestProject();
        var (source, target) = (project.Step(0), project.Step(1));
        var flow = project.Flow(source, target);
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(source)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });
        var workspace = Workspace(project);

        var refusal = Assert.Throws<DomainException>(() => FlowPresentationCommentRules.Subject(workspace, workspace.LiveFlow(flow)));

        Assert.Equal(ErrorCodes.NotFound, refusal.Code);
    }

    [Fact]
    public void An_empty_text_in_the_language_of_origin_removes_the_whole_comment_and_nothing_when_the_flow_has_no_original()
    {
        var project = new TestProject();
        var alone = project.Flow(project.Step(0), project.Step(1));
        var both = project.Flow(project.Step(2), project.Step(3));
        FlowPresentationComment[] lines =
        [
            Line(project, alone, "en", "Typed while presenting in English."), Line(project, both, "fr", "Original."),
            Line(project, both, "en", "Translated.", CommentSource.Assistant, 2),
        ];
        var workspace = Workspace(project);
        CommentSubject SubjectOf(Guid id) => FlowPresentationCommentRules.Subject(workspace, workspace.LiveFlow(id));

        // No original: there is nothing an empty text replaces, the translation typed in another language stays (ADR 099, point 3).
        Assert.Empty(FlowPresentationCommentRules.Typed(SubjectOf(alone), lines, null, "fr", Context(project)));
        // An original: the comment goes, every line of it.
        Assert.Equal([(both, "fr", 1), (both, "en", 2)],
            FlowPresentationCommentRules.Typed(SubjectOf(both), lines, null, "fr", Context(project)).Select(write => (write.FlowId, write.Language, write.VersionRead)));
        Assert.All(FlowPresentationCommentRules.Typed(SubjectOf(both), lines, null, "fr", Context(project)), write => Assert.Null(write.Line));
        // In another language, the line of that language alone.
        Assert.Equal([("en", 1)], FlowPresentationCommentRules.Typed(SubjectOf(alone), lines, null, "en", Context(project)).Select(write => (write.Language, write.VersionRead)));
    }

    [Fact]
    public void A_line_typed_is_written_by_the_actor_at_its_time_one_version_on_and_the_lines_after_are_those_kept_with_the_writes_done()
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1));
        var other = project.Flow(project.Step(2), project.Step(3));
        FlowPresentationComment[] kept =
        [
            Line(project, flow, "fr", "Original.", CommentSource.Manual, 4), Line(project, flow, "en", "Automatic.", CommentSource.Workbook, 2),
            Line(project, other, "fr", "Autre flèche."),
        ];
        var workspace = Workspace(project);
        var subject = FlowPresentationCommentRules.Subject(workspace, workspace.LiveFlow(flow));

        var writes = FlowPresentationCommentRules.Typed(subject, kept, "Original changé.", "fr", Context(project));

        var written = Assert.Single(writes, write => write.Line is not null);
        Assert.Equal(new FlowCommentWrite(flow, "fr", new FlowPresentationComment(flow, "Original changé.", "fr", CommentSource.Manual, 5, TestProject.Now.AddHours(1),
            project.Contributor.Id), 4), written);
        Assert.Equal(new FlowCommentWrite(flow, "en", null, 2), Assert.Single(writes, write => write.Line is null));
        Assert.Equal([(flow, "fr", "Original changé."), (other, "fr", "Autre flèche.")],
            FlowPresentationCommentRules.After(kept, writes).Select(line => (line.FlowId, line.Language, line.Text)).Order());
    }

    /// <summary>One implementation for both (<see cref="CommentLineRules"/>): the same lines on a step and on a flow give the same writes, their subject aside.</summary>
    [Theory]
    [InlineData("Nouveau texte.", "fr")]
    [InlineData("Original.", "fr")]
    [InlineData(" Original. ", "fr")]
    [InlineData(null, "fr")]
    [InlineData("New text.", "en")]
    [InlineData(null, "en")]
    [InlineData("Neuer Text.", "de")]
    [InlineData(null, "es")]
    public void The_same_lines_follow_the_same_rules_on_a_step_and_on_a_flow(string? text, string language)
    {
        var project = new TestProject();
        var step = project.Step(0);
        var flow = project.Flow(step, project.Step(1));
        (string Language, string Text, CommentSource Source, int Version)[] lines =
        [
            ("fr", "Original.", CommentSource.Manual, 3), ("en", "Automatic.", CommentSource.Assistant, 2), ("de", "Von Hand.", CommentSource.Manual, 1),
            ("es", "Del libro.", CommentSource.Workbook, 5),
        ];
        var workspace = Workspace(project);
        var typed = text?.Trim();

        var onStep = PresentationCommentRules.Typed(workspace.LiveStep(step),
            [.. lines.Select(line => new PresentationComment(step, line.Text, line.Language, line.Source, line.Version, TestProject.Now, project.Facilitator.Id))],
            typed, language, Context(project));
        var onFlow = FlowPresentationCommentRules.Typed(FlowPresentationCommentRules.Subject(workspace, workspace.LiveFlow(flow)),
            [.. lines.Select(line => Line(project, flow, line.Language, line.Text, line.Source, line.Version))], typed, language, Context(project));

        Assert.Equal(onStep.Select(write => (write.Language, write.VersionRead, write.Line?.Text, write.Line?.Source, write.Line?.Version, write.Line?.UpdatedBy)),
            onFlow.Select(write => (write.Language, write.VersionRead, write.Line?.Text, write.Line?.Source, write.Line?.Version, write.Line?.UpdatedBy)));
        Assert.All(onStep, write => Assert.Equal(step, write.StepId));
        Assert.All(onFlow, write => Assert.Equal((flow, flow), (write.FlowId, write.Line?.FlowId ?? flow)));
    }

    [Fact]
    public void A_package_brings_the_lines_of_the_languages_configured_the_one_of_the_language_of_origin_of_the_source_step_of_its_flow_as_a_text_typed()
    {
        var project = new TestProject();
        var (first, second, third) = (project.Step(0), project.Step(1), project.Step(2));
        SetLanguage(project, second, "en");
        var flow = project.Flow(first, second);
        var other = project.Flow(second, third);
        var stepOrigins = project.Database.All<Step>().ToDictionary(step => step.Id, step => step.SourceLanguage);

        var origins = FlowPresentationCommentRules.Origins(project.Database.All<Flow>(), stepOrigins);

        Assert.Equal([(flow, "fr"), (other, "en")], origins.Select(origin => (origin.Key, origin.Value)).Order());
        FlowPresentationComment[] brought =
        [
            Line(project, flow, "fr", "Original.", CommentSource.Workbook), Line(project, flow, "en", "Translation.", CommentSource.Assistant),
            Line(project, flow, "de", "Nicht konfiguriert."), Line(project, other, "fr", "Traduction.", CommentSource.Workbook),
            Line(project, other, "en", "Original of the other.", CommentSource.Assistant),
        ];

        var kept = FlowPresentationCommentRules.Imported(brought, origins, Languages);

        Assert.Equal([(flow, "fr", CommentSource.Manual), (flow, "en", CommentSource.Assistant), (other, "fr", CommentSource.Workbook), (other, "en", CommentSource.Manual)],
            kept.Select(line => (line.FlowId, line.Language, line.Source)));
    }

    [Fact]
    public void A_flow_whose_source_step_the_records_do_not_hold_has_no_language_of_origin()
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1));
        Assert.Empty(FlowPresentationCommentRules.Origins(project.Database.All<Flow>(), new Dictionary<Guid, string>()));
        // Its lines are then translations, kept with their source.
        var kept = FlowPresentationCommentRules.Imported([Line(project, flow, "fr", "Sans origine connue.", CommentSource.Workbook)], new Dictionary<Guid, string>(), Languages);
        Assert.Equal(CommentSource.Workbook, Assert.Single(kept).Source);
    }

    [Theory]
    [InlineData("  not trimmed  ", "en")]
    [InlineData("<b>markup</b>", "en")]
    [InlineData("line\r\nends", "en")]
    [InlineData("Twice in one language.", "fr")]
    public void A_package_with_a_text_its_rule_refuses_or_two_lines_of_a_flow_in_one_language_is_refused(string text, string language)
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1));
        var refusal = Assert.Throws<DomainException>(() => FlowPresentationCommentRules.Imported(
            [Line(project, flow, "fr", "Original."), Line(project, flow, language, text)], new Dictionary<Guid, string> { [flow] = "fr" }, Languages));
        Assert.Equal((ErrorCodes.InvalidPackage, "values"), (refusal.Code, refusal.Details["reason"]));
        Assert.Equal(["presentationComment"], (IEnumerable<string>)refusal.Details["fields"]!);
    }

    [Fact]
    public void The_version_read_of_a_line_is_checked_against_the_line_kept_and_nothing_is_checked_without_one()
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1));
        var line = Line(project, flow, "fr", "Original.", CommentSource.Manual, 3).AsLine();
        CommentLineRules.EnsureRead(line, null, FlowPresentationCommentRules.ObjectType);
        CommentLineRules.EnsureRead(line, 3, FlowPresentationCommentRules.ObjectType);
        CommentLineRules.EnsureRead(null, 0, FlowPresentationCommentRules.ObjectType);
        var stale = Assert.Throws<DomainException>(() => CommentLineRules.EnsureRead(line, 2, FlowPresentationCommentRules.ObjectType));
        Assert.Equal((ErrorCodes.VersionConflict, RefusalKind.Conflict, "flow_presentation_comment", (object?)project.Facilitator.Id),
            (stale.Code, stale.Kind, stale.Details["object"], stale.Details["updated_by"]));
        var gone = Assert.Throws<DomainException>(() => CommentLineRules.EnsureRead(null, 3, FlowPresentationCommentRules.ObjectType));
        Assert.Equal((ErrorCodes.VersionConflict, (object?)null, (object?)null), (gone.Code, gone.Details["updated_by"], gone.Details["updated_at"]));
    }
}
