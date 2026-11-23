// CommentDrafts.cs
// The comments of presentation written by the assistant for the map open and its detailed maps and kept after review (3.3, lot 3, F-130): the steps and the flows written at once, with the source of the assistant, a comment typed by a person replaced only when the person accepted it.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Presentation;

/// <summary>
/// One comment kept from the review: its step or its flow (one of the two), its text, the version of the original the
/// client read (0 when there was none, null when it gives none), and whether the person accepted to replace an original
/// typed by hand.
/// </summary>
public sealed record CommentDraftWrite(Guid? StepId, Guid? FlowId, string? Text, int? Version, bool Replace);

/// <summary>What the writing did: the comments written, and those typed by a person that stayed because their replacement was not accepted.</summary>
public sealed record CommentDraftsResult(int Written, int Kept);

/// <summary>
/// `POST /api/maps/{id}/presentation-comments` (3.3, lot 3, F-130, facilitator): the comments written by the assistant for
/// the steps and the flows of a live map and of its detailed maps, kept after review, written at once in the unit of the
/// request (<see cref="CommentLineRules.Drafted"/>): each is the original of its comment, in the language of origin of its
/// step (of the source step for a flow), with the source of the assistant. Each step or flow is a live one of a live map
/// of the project of the map named (INTROUVABLE otherwise): the review may be saved from another level than the one it was
/// asked on, the role of the facilitator is the same on every map of the project. Each comes once, with one text the rule
/// of every comment accepts (VALEUR_INVALIDE otherwise);
/// the version read of its original is checked as the If-Match of a comment typed (CONFLIT_VERSION). The project is
/// locked meanwhile, as for a comment typed. Nothing goes in the history of the map.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class WriteCommentDraftsRequest(Guid mapId, IReadOnlyList<CommentDraftWrite> drafts) : UserRequest<CommentDraftsResult>
{
    /// <summary>The field a refusal of the list names.</summary>
    public const string Field = "comments";

    public override AccessTarget? Target => AccessTarget.Map(mapId);

    public override bool InUnitOfWork => true;

    public override bool LocksTarget => true;

    public override async Task<CommentDraftsResult> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (drafts is not { Count: > 0 } || drafts.Count > CommentLineRules.ProposedMax)
            throw DomainException.InvalidValue(Field);
        var workspace = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.None, cancellationToken) ?? throw DomainException.NotFound();
        workspace.LiveMap(mapId);
        var asked = drafts.Select(draft => Asked(workspace, draft)).ToList();
        if (asked.Select(entry => entry.Subject.Id).Distinct().Count() != asked.Count)
            throw DomainException.InvalidValue(Field);
        var stepIds = asked.Where(entry => !entry.Flow).Select(entry => entry.Subject.Id).ToList();
        var flowIds = asked.Where(entry => entry.Flow).Select(entry => entry.Subject.Id).ToList();
        var stepLines = stepIds.Count == 0 ? [] : (await scope.Ports.Comments.OfStepsAsync(stepIds, cancellationToken)).Select(line => line.AsLine()).ToList();
        var flowLines = flowIds.Count == 0 ? [] : (await scope.Ports.FlowComments.OfFlowsAsync(flowIds, cancellationToken)).Select(line => line.AsLine()).ToList();
        var (stepWrites, flowWrites, kept) = (new List<CommentLineWrite>(), new List<CommentLineWrite>(), 0);
        foreach (var entry in asked)
        {
            var lines = entry.Flow ? flowLines : stepLines;
            var original = CommentLineRules.Original(entry.Subject, lines);
            CommentLineRules.EnsureRead(original, entry.Draft.Version, entry.Flow ? FlowPresentationCommentRules.ObjectType : PresentationCommentRules.ObjectType);
            if (original is { Source: CommentSource.Manual } && !entry.Draft.Replace)
                kept++;
            (entry.Flow ? flowWrites : stepWrites).AddRange(CommentLineRules.Drafted(entry.Subject, lines, entry.Text, entry.Draft.Replace, scope.Operation));
        }
        if (stepWrites.Count > 0)
            await scope.Ports.Comments.WriteAsync([.. stepWrites.Select(CommentWrite.Of)], cancellationToken);
        if (flowWrites.Count > 0)
            await scope.Ports.FlowComments.WriteAsync([.. flowWrites.Select(FlowCommentWrite.Of)], cancellationToken);
        var written = stepWrites.Concat(flowWrites).Where(write => write.Line is not null).Select(write => write.SubjectId).Distinct().Count();
        return new CommentDraftsResult(written, kept);
    }

    /// <summary>The subject of a comment kept: a live step or a live flow on a live map of the project, its text checked by the rule of every comment.</summary>
    private (CommentDraftWrite Draft, CommentSubject Subject, bool Flow, string Text) Asked(ProjectWorkspace workspace, CommentDraftWrite draft)
    {
        var text = PresentationCommentRules.Text(draft.Text) ?? throw DomainException.InvalidValue("text");
        switch (draft)
        {
            case { StepId: { } stepId, FlowId: null }:
                var step = workspace.LiveStep(stepId);
                workspace.LiveMap(step.MapId);
                return (draft, CommentSubject.Of(step), false, text);
            case { FlowId: { } flowId, StepId: null }:
                var flow = workspace.LiveFlow(flowId);
                workspace.LiveMap(flow.MapId);
                return (draft, FlowPresentationCommentRules.Subject(workspace, flow), true, text);
            default:
                throw DomainException.InvalidValue(Field);
        }
    }
}
