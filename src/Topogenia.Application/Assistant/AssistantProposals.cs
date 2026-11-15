// AssistantProposals.cs
// The proposal of the assistant on the map open: its preview compared with the scope, levels included, and its application in one operation undone by one Ctrl+Z, refused when the map changed since the preview or when it is for a new project (spec 3.0 sections 3.1-5 to 7, ADR 079, ADR 082).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Exchange;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Proposals;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Assistant;

/// <summary>A change of the preview as the panel lists it: kind, code, text, what becomes of it, the fields changed.</summary>
public sealed record ProposalChangeView(string Kind, string? Code, Guid? Id, string Text, string Action, IReadOnlyList<string> Fields);

/// <summary>A step of the scope that a new project would not carry: its code, label, whether it is detailed and has a sheet.</summary>
public sealed record ProposalLossView(string Code, string Label, bool Detailed, bool HasSheet);

/// <summary>
/// A text a translation by the assistant writes (ADR 083): its kind (`project`, `lane`, `step`, `flow`, and
/// `presentation_comment` for the comment of presentation of a step, whose identifier is the step, ADR 099 revised, or
/// `flow_presentation_comment` for the one of a flow, whose identifier is the flow and whose code is the one of the
/// label of a flow, `A &gt; B`, lot J10), identifier, code, text of origin and translation; `kept` for a translation
/// typed by hand that stays, its own text given.
/// </summary>
public sealed record ProposalTranslatedView(string Kind, Guid Id, string Code, string Original, string Text, bool Kept);

/// <summary>
/// The preview of a proposal on the map open: its changes, their counts by kind and action, its warnings, and the structure
/// version of the project it was computed on, which « Appliquer » gives back (If-Match). Its mode (ADR 082): `in_place`, or
/// `new_project` when it brings steps up a level or moves them (its reasons, `raised:CODE`, `moved:CODE`), with the steps of
/// the scope it would not carry; such a proposal is only opened as a new project. A translation (ADR 083) also gives the
/// texts it writes, which the map shows in place before anything is written.
/// </summary>
public sealed record ProposalPreviewView(IReadOnlyList<ProposalChangeView> Changes, IReadOnlyList<string> Warnings, int StructureVersion, bool Empty,
    string Mode, IReadOnlyList<string> Reasons, IReadOnlyList<ProposalLossView> Missing)
{
    /// <summary>The texts a translation writes, in the order of the maps; empty for any other proposal.</summary>
    public IReadOnlyList<ProposalTranslatedView> Translations { get; init; } = [];

    public static ProposalPreviewView Of(ProposalPlanResult plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new ProposalPreviewView(
            [.. plan.Changes.Where(change => change.Action != PlanAction.Kept)
                .Select(change => new ProposalChangeView(change.Kind, change.Code, change.Id, change.Text, Codes.Of(change.Action), change.Fields))],
            plan.Warnings, plan.StructureVersion, plan.IsEmpty, Codes.Of(plan.Mode), plan.Reasons,
            [.. plan.Missing.Select(loss => new ProposalLossView(loss.Code, loss.Label, loss.Detailed, loss.HasSheet))]);
    }
}

/// <summary>Reads the `.pmap` of a proposal into the state wanted of the map (its root map and its sub-maps), FICHIER_INVALIDE with its faults otherwise.</summary>
public static class DesiredMaps
{
    public static DesiredMap Read(IPmapFormat format, string? pmap, LanguageSettings languages, string raciLetters)
    {
        var text = pmap ?? string.Empty;
        if (text.Length > OpenPmapCommand.ContentMax)
            throw new DomainException(ErrorCodes.FileTooLarge, RefusalKind.Invalid, new Dictionary<string, object?> { ["max"] = OpenPmapCommand.ContentMax / (1024 * 1024) });
        return format.Desired(format.ReadProposal(text, languages, raciLetters));
    }

    /// <summary>
    /// What is taken of a proposal: all of it, or for a translation (ADR 083) only its translations into the language, a
    /// configured one (LANGUE_INVALIDE on `language`), the version of origin locked.
    /// </summary>
    public static DesiredMap Taken(ProjectWorkspace workspace, Guid mapId, DesiredMap desired, string? language, LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        return language is null ? desired : ProposalPlan.TranslationOf(workspace, mapId, desired, languages.Require(language, "language"));
    }
}

/// <summary>
/// `POST /api/maps/{id}/assistant/preview` (facilitator): the changes the proposal makes to the map and the maps below it,
/// nothing written; its translations checked against the languages configured (ADR 081), its mode and what a new project
/// would leave out (ADR 082). A translation (`language`, ADR 083) takes only the translations into that language.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class PreviewProposalQuery(Guid mapId, DesiredMap desired, LanguageSettings languages) : ProjectQuery<ProposalPreviewView>(AccessTarget.Map(mapId))
{
    /// <summary>The language of a translation by the assistant; null for any other proposal.</summary>
    public string? Language { get; init; }

    /// <summary>The translations of comments of presentation the reply brought beside its `.pmap` (ADR 099 revised); none for any other proposal.</summary>
    public IReadOnlyList<CommentTranslation>? Comments { get; init; }

    /// <summary>The translations of the comments of presentation of flows the reply brought with them (ADR 099, lot J10); none for any other proposal.</summary>
    public IReadOnlyList<FlowCommentTranslation>? FlowComments { get; init; }

    private ProposedCommentLines kept = ProposedCommentLines.None;

    public override async Task ReadBesideAsync(ProjectWorkspace workspace, BesidePorts beside, CancellationToken cancellationToken) =>
        kept = await ProposedCommentLines.ReadAsync(beside, Comments, FlowComments, Language, cancellationToken);

    /// <summary>
    /// The translations, the sheets and the documents, which the preview compares with those of the proposal: a step with
    /// documents and no sheet saved has a sheet, as the application of the proposal sees it (audit 3.1 V3 C1).
    /// </summary>
    public override WorkspaceContent Content => WorkspaceContent.Translations | WorkspaceContent.Sheets | WorkspaceContent.Documents;

    public override ProposalPreviewView Read(ProjectWorkspace workspace, QueryContext context)
    {
        var taken = DesiredMaps.Taken(workspace, mapId, desired, Language, languages);
        var preview = ProposalPreviewView.Of(ProposalPlan.Of(workspace, mapId, taken, languages));
        if (Language is not { } language)
            return preview;
        var comments = PresentationCommentRules.Planned(workspace, mapId, kept.Steps, PresentationCommentRules.Proposed(Comments, language), language);
        var onFlows = FlowPresentationCommentRules.Planned(workspace, mapId, kept.Flows, FlowPresentationCommentRules.Proposed(FlowComments, language), language);
        return preview with
        {
            // A translation that only brings comments is not empty: « Appliquer » has something to write.
            Empty = preview.Empty && comments.All(comment => comment.Kept) && onFlows.All(comment => comment.Kept),
            Translations = [.. ProposalPlan.TranslationTexts(workspace, mapId, taken, language)
                .Select(text => new ProposalTranslatedView(Codes.Of(text.Type), text.Id, text.Code, text.Original, text.Text, text.Kept)),
                .. comments.Select(comment => new ProposalTranslatedView(PresentationCommentRules.ObjectType, comment.Step.Id, comment.Step.Code, comment.Original,
                    comment.Text, comment.Kept)),
                .. onFlows.Select(comment => new ProposalTranslatedView(FlowPresentationCommentRules.ObjectType, comment.Flow.Id, comment.Flow.Code, comment.Original,
                    comment.Text, comment.Kept))],
        };
    }
}

/// <summary>
/// `POST /api/maps/{id}/assistant/apply` with If-Match (facilitator, spec 3.0 section 3.1-6, 7): the proposal applied to the
/// map by the rules of the domain, one operation of the log, undone by one Ctrl+Z; the structure version of the project
/// read with the preview checked first: a map changed since refuses the stale preview (CONFLIT_VERSION). Its translations
/// are written as automatic ones, never over a manual one (ADR 081); the translations of the project are read with the
/// map the command gives back (`MapViewBuilder.Content`). The translations of comments of presentation a translation
/// brings (ADR 099 revised), those of the steps and those of the flows (lot J10), are written beside the core in the
/// same unit of work, never over one typed by hand; they are outside the operation log, as the comment itself: Ctrl+Z
/// undoes the translations of the map, not theirs.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ApplyProposalCommand(Guid mapId, DesiredMap desired, int? structureVersion, LanguageSettings languages)
    : ProjectCommand<ProposalPlanResult>(AccessTarget.Map(mapId))
{
    /// <summary>The language of a translation by the assistant (ADR 083): only its translations into it are written; null for any other proposal.</summary>
    public string? Language { get; init; }

    /// <summary>The translations of comments of presentation the reply brought beside its `.pmap` (ADR 099 revised); none for any other proposal.</summary>
    public IReadOnlyList<CommentTranslation>? Comments { get; init; }

    /// <summary>The translations of the comments of presentation of flows the reply brought with them (ADR 099, lot J10); none for any other proposal.</summary>
    public IReadOnlyList<FlowCommentTranslation>? FlowComments { get; init; }

    private ProposedCommentLines kept = ProposedCommentLines.None;
    private IReadOnlyList<CommentWrite> writes = [];
    private IReadOnlyList<FlowCommentWrite> flowWrites = [];

    public override string? LogKind => OperationKinds.Proposal;

    public override async Task ReadBesideAsync(ProjectWorkspace workspace, BesidePorts beside, CancellationToken cancellationToken)
    {
        (writes, flowWrites) = ([], []);
        kept = await ProposedCommentLines.ReadAsync(beside, Comments, FlowComments, Language, cancellationToken);
    }

    public override async Task WriteBesideAsync(BesidePorts beside, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(beside);
        if (writes.Count > 0)
            await beside.Comments.WriteAsync(writes, cancellationToken);
        if (flowWrites.Count > 0)
            await beside.FlowComments.WriteAsync(flowWrites, cancellationToken);
    }

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override ProposalPlanResult Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        // The lock of the command moved the structure version on by one before the load (AR-111): the preview read the one before.
        if (structureVersion is { } read && read != workspace.Project.StructureVersion - 1)
            throw DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
            {
                ["object"] = "map", ["updated_by"] = workspace.Project.UpdatedBy, ["updated_at"] = workspace.Project.UpdatedAt,
            });
        var taken = DesiredMaps.Taken(workspace, mapId, desired, Language, languages);
        if (Language is { } language)
        {
            writes = PresentationCommentRules.Translated(
                PresentationCommentRules.Planned(workspace, mapId, kept.Steps, PresentationCommentRules.Proposed(Comments, language), language), language, context);
            flowWrites = FlowPresentationCommentRules.Translated(
                FlowPresentationCommentRules.Planned(workspace, mapId, kept.Flows, FlowPresentationCommentRules.Proposed(FlowComments, language), language), language, context);
        }
        return ProposalPlan.Apply(workspace, context, mapId, taken, languages);
    }
}

/// <summary>
/// The lines kept of the comments a translation proposes to translate, read beside the workspace for its preview and for
/// its application: those of the steps and those of the flows it names (ADR 099 revised, lot J10).
/// </summary>
public sealed record ProposedCommentLines(IReadOnlyList<PresentationComment> Steps, IReadOnlyList<FlowPresentationComment> Flows)
{
    public static ProposedCommentLines None { get; } = new([], []);

    /// <summary>
    /// Every line of the comments of the steps and of the flows a proposal names; the translations it brings are checked
    /// first: none without the language of a translation (VALEUR_INVALIDE on their field).
    /// </summary>
    public static async Task<ProposedCommentLines> ReadAsync(BesidePorts beside, IReadOnlyList<CommentTranslation>? comments,
        IReadOnlyList<FlowCommentTranslation>? flowComments, string? language, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(beside);
        var steps = PresentationCommentRules.Proposed(comments, language);
        var flows = FlowPresentationCommentRules.Proposed(flowComments, language);
        return new ProposedCommentLines(
            steps.Count == 0 ? [] : await beside.Comments.OfStepsAsync([.. steps.Select(entry => entry.StepId).Distinct()], cancellationToken),
            flows.Count == 0 ? [] : await beside.FlowComments.OfFlowsAsync([.. flows.Select(entry => entry.FlowId).Distinct()], cancellationToken));
    }
}
