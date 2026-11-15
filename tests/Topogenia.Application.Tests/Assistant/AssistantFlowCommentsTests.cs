// AssistantFlowCommentsTests.cs
// The comments of presentation of the flows translated with the model (ADR 099, lot J10): the comments a translation asks after those of the steps within the same bound, each flow named by the codes of its two steps, and the texts of the reply matched to the flows asked.

using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantFlowCommentsTests
{
    private static readonly Guid First = Guid.Parse("00000000-0000-0000-0000-000000000b01");
    private static readonly Guid Second = Guid.Parse("00000000-0000-0000-0000-000000000b02");
    private static readonly Guid Commented = Guid.Parse("00000000-0000-0000-0000-000000000b03");

    private static readonly AssistantFlowCommentTarget[] Targets =
    [
        new(First, "VIS-E001", "VIS-E002", "La demande part chez le responsable."),
        new(Second, "VIS-E002", "VIS-E003", "Le dossier validé part au magasin.\nIl y reste une journée."),
    ];

    private static AssistantReply Read(string text) => AssistantReplyReader.Read(text, out var reason) ?? throw new InvalidOperationException(reason);

    private static FlowPresentationComment Line(TestProject project, Guid flow, string language, string text, CommentSource source = CommentSource.Manual) =>
        new(flow, text, language, source, 1, TestProject.Now, project.Facilitator.Id);

    private static IReadOnlyList<FlowEnds> Tree(TestProject project, Guid mapId) =>
        FlowPresentationCommentRules.TreeFlows(project.Database.Load(project.ProjectId, WorkspaceContent.None)!, mapId);

    [Fact]
    public void A_translation_asks_the_comments_of_the_flows_of_the_tree_that_have_an_original_and_no_line_in_its_language_in_reading_order()
    {
        var project = new TestProject();
        var (a, b, c, d) = (project.Step(0), project.Step(1), project.Step(2), project.Step(3));
        var second = project.Flow(b, c);
        var first = project.Flow(a, b);
        var translated = project.Flow(c, d);
        var orphan = project.Flow(a, c);
        var deleted = project.Flow(a, d);
        project.Database.Add(project.Database.Get<Flow>(Flow.KeyOf(deleted)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });
        var (subMap, subLane) = project.SubMap(a);
        var (below, after) = (project.Step(4, laneId: subLane, mapId: subMap), project.Step(5, laneId: subLane, mapId: subMap));
        var detailed = project.Flow(below, after, subMap);
        var english = project.Step(6);
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(english)) with { SourceLanguage = "en" });
        var fromEnglish = project.Flow(english, a);
        FlowPresentationComment[] lines =
        [
            Line(project, second, "fr", "Deuxième."), Line(project, first, "fr", "Première."), Line(project, detailed, "fr", "Dans le détail."),
            Line(project, translated, "fr", "Déjà traduite."), Line(project, translated, "en", "Already translated.", CommentSource.Assistant),
            Line(project, orphan, "de", "Ohne Original."), Line(project, deleted, "fr", "Supprimée."), Line(project, fromEnglish, "en", "Written in English."),
        ];

        var asked = AssistantCommentTargets.OfFlows(Tree(project, project.RootMapId), lines, "en", 0);

        Assert.Equal([(first, "PRJ-E001", "PRJ-E002", "Première."), (second, "PRJ-E002", "PRJ-E003", "Deuxième."), (detailed, "PRJ-E005", "PRJ-E006", "Dans le détail.")],
            asked.Select(target => (target.FlowId, target.Source, target.Target, target.Original)));
        Assert.Equal("PRJ-E001 > PRJ-E002", asked[0].Reference);
        // From a detailed map, only its own tree; into the language of origin of the source step of every other flow, the one written in English.
        Assert.Equal([detailed], AssistantCommentTargets.OfFlows(Tree(project, subMap), lines, "en", 0).Select(target => target.FlowId));
        Assert.Equal([fromEnglish], AssistantCommentTargets.OfFlows(Tree(project, project.RootMapId), lines, "fr", 0).Select(target => target.FlowId));
    }

    [Fact]
    public void The_bound_of_three_hundred_comments_is_for_the_steps_and_the_flows_together_the_steps_first()
    {
        var project = new TestProject();
        var steps = Enumerable.Range(0, 12).Select(column => project.Step(column)).ToList();
        var flows = steps.Zip(steps.Skip(1)).Select(pair => project.Flow(pair.First, pair.Second)).ToList();
        var lines = flows.Select(flow => Line(project, flow, "fr", "Original.")).ToList();
        var tree = Tree(project, project.RootMapId);

        // With 296 comments of steps asked already, four flows are left; with 300 or more, none; without any, every flow.
        Assert.Equal(flows.Take(4), AssistantCommentTargets.OfFlows(tree, lines, "en", AssistantCommentTargets.CommentsMax - 4).Select(target => target.FlowId));
        Assert.Empty(AssistantCommentTargets.OfFlows(tree, lines, "en", AssistantCommentTargets.CommentsMax));
        Assert.Empty(AssistantCommentTargets.OfFlows(tree, lines, "en", AssistantCommentTargets.CommentsMax + 3));
        Assert.Equal(flows, AssistantCommentTargets.OfFlows(tree, lines, "en", 0).Select(target => target.FlowId));
    }

    [Fact]
    public void The_texts_of_the_reply_are_matched_to_the_flows_asked_by_the_codes_of_their_steps_each_flow_once_an_unknown_one_left_aside()
    {
        var reply = Read("""
            {"form":"proposal","message":"Traduit.","cited":[],"questions":[],"findings":[],"pmap":"{}","report":null,"summaries":null,
             "comments":[{"step":"VIS-E002 > VIS-E003","text":"  The approved file goes to the shop.\r\n\r\n  It stays   there a day.  "},
                         {"step":"VIS-E001","text":"A step, not a flow."},{"step":"VIS-E001>VIS-E002","text":"The request goes to the manager."},
                         {"step":"VIS-E002 > VIS-E003","text":"A second one."},{"step":"VIS-E003 > VIS-E002","text":"The other way: unknown."},
                         {"step":"VIS-E001 > VIS-E002 > VIS-E003","text":"Three codes."},{"step":"VIS-E001 > VIS-E002","text":"   "}]}
            """);

        var views = AssistantCommentsReader.FlowViews(reply.Comments!, Targets);

        Assert.Equal([new AssistantFlowCommentView(Second, "VIS-E002", "VIS-E003", "Le dossier validé part au magasin.\nIl y reste une journée.", "The approved file goes to the shop.\nIt stays there a day."),
            new AssistantFlowCommentView(First, "VIS-E001", "VIS-E002", "La demande part chez le responsable.", "The request goes to the manager.")], views);
        // The same list holds the comments of the steps: each reader takes its own, a flow is never read as a step.
        AssistantCommentTarget[] steps = [new(Commented, "VIS-E001", "Le commercial saisit.")];
        Assert.Equal([new AssistantCommentView(Commented, "VIS-E001", "Le commercial saisit.", "A step, not a flow.")], AssistantCommentsReader.Views(reply.Comments!, steps));
        Assert.Empty(AssistantCommentsReader.FlowViews(reply.Comments!, []));
    }

    [Theory]
    [InlineData("VIS-E001 -> VIS-E002")]
    [InlineData("VIS-E001->VIS-E002")]
    [InlineData("VIS-E001 --> VIS-E002")]
    [InlineData("VIS-E001 => VIS-E002")]
    [InlineData("VIS-E001 \u2192 VIS-E002")]
    [InlineData("VIS-E001\u2192VIS-E002")]
    [InlineData("VIS-E001 \u21D2 VIS-E002")]
    [InlineData("VIS-E001 \u279C VIS-E002")]
    [InlineData("VIS-E001 \\u003E VIS-E002")]
    [InlineData("VIS-E001 \\u003e VIS-E002")]
    [InlineData("VIS-E001 &gt; VIS-E002")]
    public void A_flow_named_with_another_arrow_from_its_source_to_its_target_is_matched_as_well(string reference)
    {
        var view = Assert.Single(AssistantCommentsReader.FlowViews([new AssistantCommentText(reference, "The request goes to the manager.")], Targets));

        Assert.Equal((First, "The request goes to the manager."), (view.FlowId, view.Text));
    }

    [Theory]
    [InlineData("VIS-E002 <- VIS-E001")]
    [InlineData("VIS-E002 \u2190 VIS-E001")]
    [InlineData("VIS-E001<>VIS-E002")]
    [InlineData("VIS-E001 \u2194 VIS-E002")]
    [InlineData("VIS-E001 VIS-E002")]
    [InlineData("VIS-E001 - VIS-E002")]
    [InlineData("VIS-E001 / VIS-E002")]
    [InlineData("VIS-E001 >> VIS-E002")]
    [InlineData("> VIS-E002")]
    [InlineData("VIS-E001 >")]
    public void A_flow_named_without_an_arrow_from_its_source_to_its_target_is_left_aside(string reference)
    {
        // A reference read the wrong way round would write the text on the flow that goes back: nothing is guessed.
        AssistantFlowCommentTarget[] bothWays = [.. Targets, new(Commented, "VIS-E002", "VIS-E001", "Le dossier revient.")];

        Assert.Empty(AssistantCommentsReader.FlowViews([new AssistantCommentText(reference, "The request goes to the manager.")], bothWays));
    }

    [Fact]
    public void A_text_too_long_for_the_comment_of_a_flow_is_cut_as_the_one_of_a_step()
    {
        var words = string.Join(' ', Enumerable.Repeat("processus", 80));
        var reply = Read($$"""{"form":"proposal","message":"m","cited":[],"pmap":"{}","comments":[{"step":"VIS-E001 > VIS-E002","text":"{{words}}"}]}""");

        var view = Assert.Single(AssistantCommentsReader.FlowViews(reply.Comments!, Targets));

        Assert.Equal(AssistantCommentsReader.Bounded(words), view.Text);
        Assert.InRange(view.Text.Length, PresentationCommentRules.TextMax / 2, PresentationCommentRules.TextMax);
    }
}
