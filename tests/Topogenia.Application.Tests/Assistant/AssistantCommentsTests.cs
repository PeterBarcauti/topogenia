// AssistantCommentsTests.cs
// The comments of presentation translated with the model (ADR 099 revised): the comments a translation asks, the texts of the reply read, bounded with their lines kept and matched to the comments asked by the code of their step.

using Topogenia.Application.Assistant;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Application.Ports;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantCommentsTests
{
    private static readonly Guid First = Guid.Parse("00000000-0000-0000-0000-000000000a01");
    private static readonly Guid Second = Guid.Parse("00000000-0000-0000-0000-000000000a02");

    private static readonly AssistantCommentTarget[] Targets =
    [
        new(First, "VIS-E001", "Le commercial saisit."),
        new(Second, "VIS-E002", "Le client signe.\nLe magasin prépare."),
    ];

    private static AssistantReply Read(string text) => AssistantReplyReader.Read(text, out var reason) ?? throw new InvalidOperationException(reason);

    private static PresentationComment Line(TestProject project, Guid step, string language, string text, CommentSource source = CommentSource.Manual) =>
        new(step, text, language, source, 1, TestProject.Now, project.Facilitator.Id);

    private static IReadOnlyList<Step> Tree(TestProject project, Guid mapId) =>
        PresentationCommentRules.TreeSteps(project.Database.Load(project.ProjectId, WorkspaceContent.None)!, mapId);

    [Fact]
    public void A_translation_asks_the_comments_of_the_tree_that_have_an_original_and_no_line_in_its_language_in_reading_order()
    {
        var project = new TestProject();
        var second = project.Step(1);
        var first = project.Step(0);
        var translated = project.Step(2);
        var orphan = project.Step(3);
        var (subMap, subLane) = project.SubMap(first);
        var below = project.Step(0, laneId: subLane, mapId: subMap);
        var english = project.Step(4);
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(english)) with { SourceLanguage = "en" });
        PresentationComment[] lines =
        [
            Line(project, second, "fr", "Deuxième."), Line(project, first, "fr", "Première."), Line(project, below, "fr", "Dans le détail."),
            Line(project, translated, "fr", "Déjà traduite."), Line(project, translated, "en", "Already translated.", CommentSource.Assistant),
            Line(project, orphan, "de", "Ohne Original."), Line(project, english, "en", "Written in English."),
        ];

        var asked = AssistantCommentTargets.Of(Tree(project, project.RootMapId), lines, "en");

        Assert.Equal([(first, "PRJ-E001", "Première."), (second, "PRJ-E002", "Deuxième."), (below, "PRJ-E001", "Dans le détail.")],
            asked.Select(target => (target.StepId, target.Reference, target.Original)));
        // From a detailed map, only its own tree; into the language of origin of every step, nothing.
        Assert.Equal([below], AssistantCommentTargets.Of(Tree(project, subMap), lines, "en").Select(target => target.StepId));
        Assert.Equal([english], AssistantCommentTargets.Of(Tree(project, project.RootMapId), lines, "fr").Select(target => target.StepId));
    }

    [Fact]
    public void A_translation_asks_three_hundred_comments_at_most_the_first_ones_in_reading_order()
    {
        var project = new TestProject();
        var steps = Enumerable.Range(0, AssistantCommentTargets.CommentsMax + 5).Select(column => project.Step(column)).ToList();
        var lines = steps.Select(step => Line(project, step, "fr", "Original.")).ToList();
        var asked = AssistantCommentTargets.Of(Tree(project, project.RootMapId), lines, "en");
        Assert.Equal(steps.Take(AssistantCommentTargets.CommentsMax), asked.Select(target => target.StepId));
    }

    [Fact]
    public void The_texts_are_read_with_a_proposal_matched_to_the_comments_asked_each_step_once_an_unknown_one_left_aside()
    {
        var reply = Read("""
            {"form":"proposal","message":"Traduit.","cited":[],"questions":[],"findings":[],"pmap":"{}","report":null,"summaries":null,
             "comments":[{"step":"VIS-E002","text":"  The customer signs.\r\n\r\n  The shop   prepares.  "},{"step":"VIS-E001","text":"The salesperson enters it."},
                         {"step":"VIS-E002","text":"A second one."},{"step":"VIS-E999","text":"Unknown."},{"step":"VIS-E001","text":"   "},{"text":"No step."}]}
            """);
        var views = AssistantCommentsReader.Views(reply.Comments!, Targets);
        Assert.Equal([new AssistantCommentView(Second, "VIS-E002", "Le client signe.\nLe magasin prépare.", "The customer signs.\nThe shop prepares."),
            new AssistantCommentView(First, "VIS-E001", "Le commercial saisit.", "The salesperson enters it.")], views);
    }

    [Theory]
    [InlineData("""{"form":"proposal","message":"m","cited":[],"questions":[],"findings":[],"pmap":"{}","report":null,"summaries":null,"comments":null}""")]
    [InlineData("""{"form":"proposal","message":"m","cited":[],"questions":[],"findings":[],"pmap":"{}","report":null,"summaries":null}""")]
    [InlineData("""{"form":"answer","message":"m","cited":[],"questions":[],"findings":[],"pmap":null,"report":null,"summaries":null,"comments":[{"step":"VIS-E001","text":"x"}]}""")]
    public void A_reply_without_a_list_of_comments_or_that_is_not_a_proposal_brings_none(string text) => Assert.Null(Read(text).Comments);

    [Fact]
    public void A_text_too_long_is_cut_at_a_word_a_tag_is_spaced_and_an_empty_one_is_none()
    {
        var words = string.Join(' ', Enumerable.Repeat("processus", 80));
        var cut = AssistantCommentsReader.Bounded(words)!;
        Assert.InRange(cut.Length, PresentationCommentRules.TextMax / 2, PresentationCommentRules.TextMax);
        Assert.EndsWith("processus…", cut, StringComparison.Ordinal);
        Assert.Equal("Le < b>gras< /b> et 3 <5 jours", AssistantCommentsReader.Bounded("Le <b>gras</b> et 3 <5 jours"));
        Assert.Equal("Une\ttabulation".Replace('\t', ' '), AssistantCommentsReader.Bounded("Une\ttabulation"));
        Assert.Null(AssistantCommentsReader.Bounded(" \t \n "));
        // What the reader gives is always a text the rule of a comment takes as it is.
        Assert.Equal(cut, PresentationCommentRules.Text(cut));
    }
}
