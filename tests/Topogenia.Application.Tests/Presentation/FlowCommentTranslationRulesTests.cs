// FlowCommentTranslationRulesTests.cs
// What becomes of the translations an assistant proposes for the comments of the flows (ADR 099, lot J10): planned on the original of today by the rules of the comment of a step, the lines they write, and the flows of a map and of its levels in reading order.

using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Presentation;

public sealed class FlowCommentTranslationRulesTests
{
    private static FlowPresentationComment Line(TestProject project, Guid flow, string language, string text, CommentSource source = CommentSource.Manual, int version = 1) =>
        new(flow, text, language, source, version, TestProject.Now, project.Facilitator.Id);

    private static ProjectWorkspace Workspace(TestProject project) => project.Database.Load(project.ProjectId, WorkspaceContent.None)!;

    private static OperationContext Context(TestProject project) => new(project.Contributor.Id, TestProject.Now.AddHours(1), "fr", AppSettings.Default);

    private static void PutInTrash(TestProject project, Guid flow) =>
        project.Database.Add(project.Database.Get<Flow>(Flow.KeyOf(flow)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });

    [Fact]
    public void The_flows_of_a_map_are_read_by_source_then_by_target_with_their_two_live_steps_and_those_of_its_levels_follow()
    {
        var project = new TestProject();
        var (a, b, c) = (project.Step(0), project.Step(1), project.Step(2));
        var late = project.Flow(b, c);
        var far = project.Flow(a, c);
        var near = project.Flow(a, b);
        var gone = project.Flow(c, a);
        var (subMap, subLane) = project.SubMap(b);
        var (below, after) = (project.Step(3, laneId: subLane, mapId: subMap), project.Step(4, laneId: subLane, mapId: subMap));
        var detailed = project.Flow(below, after, subMap);
        PutInTrash(project, gone);
        var workspace = Workspace(project);

        var flows = FlowPresentationCommentRules.MapFlows(workspace, project.RootMapId);

        Assert.Equal([near, far, late], flows.Select(flow => flow.Id));
        Assert.Equal((a, b, "PRJ-E001 > PRJ-E002", "Étape 0 > Étape 1", new CommentSubject(near, "fr")),
            (flows[0].Source.Id, flows[0].Target.Id, flows[0].Code, flows[0].Context, flows[0].Subject));
        Assert.Equal([near, far, late, detailed], FlowPresentationCommentRules.TreeFlows(workspace, project.RootMapId).Select(flow => flow.Id));
        Assert.Equal([detailed], FlowPresentationCommentRules.TreeFlows(workspace, subMap).Select(flow => flow.Id));
        Assert.Equal([near, far, late, detailed], FlowPresentationCommentRules.ProjectFlows(workspace).Select(flow => flow.Id));
    }

    [Fact]
    public void A_translation_proposed_for_a_flow_is_planned_on_the_original_of_today_and_one_typed_by_hand_is_kept_with_its_own_text()
    {
        var project = new TestProject();
        var (a, b, c, d) = (project.Step(0), project.Step(1), project.Step(2), project.Step(3));
        var free = project.Flow(a, b);
        var typed = project.Flow(b, c);
        var automatic = project.Flow(c, d);
        FlowPresentationComment[] lines =
        [
            Line(project, free, "fr", "Première."), Line(project, typed, "fr", "Deuxième."), Line(project, typed, "en", "Typed by hand."),
            Line(project, automatic, "fr", "Troisième."), Line(project, automatic, "en", "Older one.", CommentSource.Workbook, 3),
        ];

        var planned = FlowPresentationCommentRules.Planned(Workspace(project), project.RootMapId, lines,
            [new(free, " Première. ", " First. "), new(typed, "Deuxième.", "Second."), new(automatic, "Troisième.", "Third.")], "en");

        Assert.Equal([(free, "PRJ-E001 > PRJ-E002", "Première.", "First.", false), (typed, "PRJ-E002 > PRJ-E003", "Deuxième.", "Typed by hand.", true),
            (automatic, "PRJ-E003 > PRJ-E004", "Troisième.", "Third.", false)],
            planned.Select(plan => (plan.Flow.Id, plan.Flow.Code, plan.Original, plan.Text, plan.Kept)));
        var writes = FlowPresentationCommentRules.Translated(planned, "en", Context(project));
        Assert.Equal([(free, 0, 1), (automatic, 3, 4)], writes.Select(write => (write.FlowId, write.VersionRead, write.Line!.Version)));
        Assert.All(writes, write => Assert.Equal((CommentSource.Assistant, "en", project.Contributor.Id, TestProject.Now.AddHours(1)),
            (write.Line!.Source, write.Line.Language, write.Line.UpdatedBy, write.Line.UpdatedAt)));
        // The same text already written by the assistant is not written again.
        FlowPresentationComment[] same = [Line(project, free, "fr", "Première."), Line(project, free, "en", "First.", CommentSource.Assistant, 2)];
        var again = FlowPresentationCommentRules.Planned(Workspace(project), project.RootMapId, same, [new(free, "Première.", "First.")], "en");
        Assert.False(Assert.Single(again).Kept);
        Assert.Empty(FlowPresentationCommentRules.Translated(again, "en", Context(project)));
    }

    [Fact]
    public void A_translation_proposed_for_a_flow_is_left_aside_without_its_original_out_of_the_tree_or_into_the_language_of_origin_of_its_source_step()
    {
        var project = new TestProject();
        var steps = Enumerable.Range(0, 6).Select(column => project.Step(column)).ToList();
        var changed = project.Flow(steps[0], steps[1]);
        var none = project.Flow(steps[1], steps[2]);
        var empty = project.Flow(steps[2], steps[3]);
        var onMap = project.Flow(steps[3], steps[4]);
        var deleted = project.Flow(steps[4], steps[5]);
        var (subMap, subLane) = project.SubMap(steps[5]);
        var (below, after) = (project.Step(6, laneId: subLane, mapId: subMap), project.Step(7, laneId: subLane, mapId: subMap));
        var detailed = project.Flow(below, after, subMap);
        PutInTrash(project, deleted);
        FlowPresentationComment[] lines =
        [
            Line(project, changed, "fr", "Nouvel original."), Line(project, empty, "fr", "Original."), Line(project, detailed, "fr", "Dans le détail."),
            Line(project, onMap, "fr", "Sur la carte."), Line(project, deleted, "fr", "Supprimée."),
        ];
        FlowCommentTranslation[] proposed =
        [
            new(changed, "Ancien original.", "Old."), new(none, "Sans commentaire.", "None."), new(empty, "Original.", "   "),
            new(detailed, "Dans le détail.", "In the detail."), new(onMap, "Sur la carte.", "On the map."), new(deleted, "Supprimée.", "Deleted."),
            new(steps[0], "Une étape.", "A step."), new(project.NextId(), "Inconnue.", "Unknown."),
        ];
        var workspace = Workspace(project);

        Assert.Equal([onMap, detailed], FlowPresentationCommentRules.Planned(workspace, project.RootMapId, lines, proposed, "en").Select(plan => plan.Flow.Id).Order());
        // From the detailed map, only its own flows; into the language of origin of the source steps, nothing.
        Assert.Equal([detailed], FlowPresentationCommentRules.Planned(workspace, subMap, lines, proposed, "en").Select(plan => plan.Flow.Id));
        Assert.Empty(FlowPresentationCommentRules.Planned(workspace, project.RootMapId, lines, proposed, "fr"));
    }

    [Fact]
    public void A_flow_named_twice_too_many_translations_or_an_invalid_text_refuse_the_proposal_on_the_field_of_the_flows()
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1));
        FlowPresentationComment[] lines = [Line(project, flow, "fr", "Original.")];
        var workspace = Workspace(project);
        DomainException Refused(params FlowCommentTranslation[] proposed) =>
            Assert.Throws<DomainException>(() => FlowPresentationCommentRules.Planned(workspace, project.RootMapId, lines, proposed, "en"));

        var twice = Refused(new(flow, "Original.", "One."), new(flow, "Original.", "Two."));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"flow_comments"), (twice.Code, twice.Details["field"]));
        var many = Refused([.. Enumerable.Range(0, PresentationCommentRules.ProposedMax + 1).Select(_ => new FlowCommentTranslation(Guid.NewGuid(), "x", "y"))]);
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"flow_comments"), (many.Code, many.Details["field"]));
        Assert.Equal(ErrorCodes.HtmlForbidden, Refused(new FlowCommentTranslation(flow, "Original.", "<b>Bold</b>")).Code);
        Assert.Equal(ErrorCodes.TextTooLong, Refused(new FlowCommentTranslation(flow, "Original.", new string('x', 501))).Code);
        Assert.Single(FlowPresentationCommentRules.Planned(workspace, project.RootMapId, lines, [new(flow, "Original.", new string('x', 500))], "en"));
        // Only a translation carries them.
        Assert.Empty(FlowPresentationCommentRules.Proposed(null, null));
        Assert.Empty(FlowPresentationCommentRules.Proposed([], null));
        Assert.Single(FlowPresentationCommentRules.Proposed([new(flow, "o", "t")], "en"));
        var refusal = Assert.Throws<DomainException>(() => FlowPresentationCommentRules.Proposed([new(flow, "o", "t")], null));
        Assert.Equal((ErrorCodes.InvalidValue, (object?)"flow_comments"), (refusal.Code, refusal.Details["field"]));
    }
}
