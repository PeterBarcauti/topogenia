// FlowPresentationCommentRules.cs
// The rules of the comment of presentation of a flow (ADR 099, lot J10): its subject, the flow with the language of origin of its source step; the flows of a map in reading order, each named by its two steps; what a person typing a line changes, what an import brings and what a translation by the assistant writes, by the rules shared with the comment of a step.

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Presentation;

/// <summary>
/// A live flow with the two live steps it links on its map: what names it for a reader, as the workbook of the
/// translations names a labelled flow (the codes of its steps, `A &gt; B`, and their labels for its context), and what
/// gives its comment its language of origin, its source step.
/// </summary>
public sealed record FlowEnds(Flow Flow, Step Source, Step Target)
{
    public Guid Id => Flow.Id;

    /// <summary>The subject of the comment of the flow: the flow, with the language of origin of its source step.</summary>
    public CommentSubject Subject => CommentSubject.Of(Flow, Source);

    /// <summary>The readable code of the flow: the codes of its source and of its target.</summary>
    public string Code => FlowPresentationCommentRules.CodeOf(Source.Code, Target.Code);

    /// <summary>The labels of the two steps, for a translator to know which flow it is.</summary>
    public string Context => $"{Source.Label} > {Target.Label}";
}

/// <summary>A translation of the comment of a flow proposed by the assistant: its flow, the original it was translated from, its text.</summary>
public sealed record FlowCommentTranslation(Guid FlowId, string? Original, string? Text);

/// <summary>
/// What becomes of a translation proposed for the comment of a flow: the flow, the original of today, the text shown (the
/// one proposed, or the one typed by hand that stays), and whether that typed one is kept; the line of today in the language.
/// </summary>
public sealed record PlannedFlowCommentTranslation(FlowEnds Flow, string Original, string Text, bool Kept, FlowPresentationComment? Current);

/// <summary>
/// The rules of the comment of presentation of a flow (ADR 099, lot J10): those of the comment of a step, written once in
/// <see cref="CommentLineRules"/>, on another subject. A flow has no language of its own: the original of its comment is
/// the line in the language of origin of its source step; its text and its language follow the rules of every comment
/// (<see cref="PresentationCommentRules.Text"/>, <see cref="PresentationCommentRules.Language"/>).
/// </summary>
public static class FlowPresentationCommentRules
{
    /// <summary>The type of object that the refusal of a version read names for a line of the comment of a flow: its table.</summary>
    public const string ObjectType = "flow_presentation_comment";

    /// <summary>The subject of the comment of a live flow: the flow, with the language of origin of its source step, a live step (INTROUVABLE otherwise).</summary>
    public static CommentSubject Subject(ProjectWorkspace workspace, Flow flow)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(flow);
        return CommentSubject.Of(flow, workspace.LiveStep(flow.SourceStepId));
    }

    /// <summary>
    /// What a person typing the line of a flow in a language changes, among the lines kept of the flow
    /// (<paramref name="kept"/>): the rule of every comment (<see cref="CommentLineRules.Typed"/>). In the language of
    /// origin of the source step of the flow the line is the original; in another one, a translation typed.
    /// </summary>
    public static IReadOnlyList<FlowCommentWrite> Typed(CommentSubject subject, IReadOnlyList<FlowPresentationComment> kept, string? text, string language,
        OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(kept);
        return [.. CommentLineRules.Typed(subject, LinesOf(kept), text, language, context).Select(FlowCommentWrite.Of)];
    }

    /// <summary>The lines kept once the writes are done.</summary>
    public static IReadOnlyList<FlowPresentationComment> After(IReadOnlyList<FlowPresentationComment> kept, IReadOnlyList<FlowCommentWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(kept);
        ArgumentNullException.ThrowIfNull(writes);
        return [.. CommentLineRules.After(LinesOf(kept), [.. writes.Select(write => write.AsLineWrite())]).Select(FlowPresentationComment.Of)];
    }

    /// <summary>The lines of the comments of the live flows of a project, read for its package (an export, a target state).</summary>
    public static async Task<IReadOnlyList<FlowPresentationComment>> OfLiveFlowsAsync(IFlowPresentationCommentStore store, ProjectWorkspace workspace,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(workspace);
        var flows = workspace.All<Flow>().Where(flow => flow.DeletedAt is null).Select(flow => flow.Id).ToList();
        return flows.Count == 0 ? [] : await store.OfFlowsAsync(flows, cancellationToken);
    }

    /// <summary>
    /// The language of origin of the comment of each flow among the records of an import: the one of its source step
    /// (<paramref name="stepOrigins"/>, the language of origin of each step by its identifier).
    /// </summary>
    public static IReadOnlyDictionary<Guid, string> Origins(IEnumerable<Flow> flows, IReadOnlyDictionary<Guid, string> stepOrigins)
    {
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(stepOrigins);
        return flows.Where(flow => stepOrigins.ContainsKey(flow.SourceStepId)).ToDictionary(flow => flow.Id, flow => stepOrigins[flow.SourceStepId]);
    }

    /// <summary>
    /// The lines a package brings for its flows (format 2.4): the rule of every comment
    /// (<see cref="CommentLineRules.Imported"/>). The line in the language of origin of the source step of its flow
    /// (<paramref name="origins"/>, <see cref="Origins"/>) is the original, always a text typed.
    /// </summary>
    public static IReadOnlyList<FlowPresentationComment> Imported(IReadOnlyList<FlowPresentationComment> comments, IReadOnlyDictionary<Guid, string> origins,
        LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(comments);
        return [.. CommentLineRules.Imported(LinesOf(comments), origins, languages).Select(FlowPresentationComment.Of)];
    }

    /// <summary>The field of the translations of the comments of flows proposed by the assistant, named by a refusal.</summary>
    public const string TranslationsField = "flow_comments";

    /// <summary>The readable code of a flow, the codes of its source and of its target: `A &gt; B`, as the workbook of the translations names a labelled flow.</summary>
    public static string CodeOf(string source, string target) => $"{source} > {target}";

    /// <summary>
    /// The live flows of a map between two of its live steps, in reading order: by their source, then by their target,
    /// as the workbook of the translations lists the labelled flows.
    /// </summary>
    public static IReadOnlyList<FlowEnds> MapFlows(ProjectWorkspace workspace, Guid mapId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var steps = workspace.ReadingOrder(mapId);
        var rank = steps.Select((step, index) => (step.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
        return [.. workspace.FlowsOf(mapId)
            .Where(flow => rank.ContainsKey(flow.SourceStepId) && rank.ContainsKey(flow.TargetStepId))
            .OrderBy(flow => rank[flow.SourceStepId]).ThenBy(flow => rank[flow.TargetStepId]).ThenBy(flow => flow.Id)
            .Select(flow => new FlowEnds(flow, steps[rank[flow.SourceStepId]], steps[rank[flow.TargetStepId]]))];
    }

    /// <summary>The live flows of a live map and of every level below it, map by map (<see cref="PresentationCommentRules.TreeSteps"/>).</summary>
    public static IReadOnlyList<FlowEnds> TreeFlows(ProjectWorkspace workspace, Guid mapId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return [.. workspace.Subtree(workspace.LiveMap(mapId).Id).SelectMany(map => MapFlows(workspace, map))];
    }

    /// <summary>The live flows of the live maps of a project, map by map in the order the workbook of the translations reads them.</summary>
    public static IReadOnlyList<FlowEnds> ProjectFlows(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return [.. workspace.All<ProcessMap>().Where(map => map.DeletedAt is null).OrderBy(map => map.CreatedAt).ThenBy(map => map.Id)
            .SelectMany(map => MapFlows(workspace, map.Id))];
    }

    /// <summary>
    /// The translations of comments of flows a proposal brings, checked before they are planned: none without the
    /// language of a translation (VALEUR_INVALIDE on <see cref="TranslationsField"/>: only a translation carries them).
    /// </summary>
    public static IReadOnlyList<FlowCommentTranslation> Proposed(IReadOnlyList<FlowCommentTranslation>? proposed, string? language) =>
        CommentLineRules.Proposed(proposed, language, TranslationsField);

    /// <summary>
    /// What becomes of the translations of comments an assistant proposed into a language, for the flows of a map and of
    /// the levels below it: the rule of every comment (<see cref="CommentLineRules.Planned"/>), each flow with the
    /// language of origin of its source step. A flow named twice is VALEUR_INVALIDE on <see cref="TranslationsField"/>.
    /// </summary>
    public static IReadOnlyList<PlannedFlowCommentTranslation> Planned(ProjectWorkspace workspace, Guid mapId, IReadOnlyList<FlowPresentationComment> lines,
        IReadOnlyList<FlowCommentTranslation> proposed, string language)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(proposed);
        var flows = TreeFlows(workspace, mapId).ToDictionary(flow => flow.Id);
        var planned = CommentLineRules.Planned(flows.ToDictionary(pair => pair.Key, pair => pair.Value.Subject), LinesOf(lines),
            [.. proposed.Select(entry => new ProposedLine(entry.FlowId, entry.Original, entry.Text))], language, TranslationsField);
        return [.. planned.Select(plan => new PlannedFlowCommentTranslation(flows[plan.Subject.Id], plan.Original, plan.Text, plan.Kept,
            plan.Current is { } current ? FlowPresentationComment.Of(current) : null))];
    }

    /// <summary>The lines a translation by the assistant writes for the flows: those planned that are not kept nor already written as they are.</summary>
    public static IReadOnlyList<FlowCommentWrite> Translated(IReadOnlyList<PlannedFlowCommentTranslation> planned, string language, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(planned);
        var lines = planned.Select(plan => new PlannedLine(plan.Flow.Subject, plan.Original, plan.Text, plan.Kept, plan.Current?.AsLine())).ToList();
        return [.. CommentLineRules.Translated(lines, language, context).Select(FlowCommentWrite.Of)];
    }

    /// <summary>The lines of the comments of flows as the shared rules see them.</summary>
    private static List<CommentLine> LinesOf(IEnumerable<FlowPresentationComment> comments) => [.. comments.Select(comment => comment.AsLine())];
}
