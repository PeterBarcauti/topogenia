// SheetView.cs
// The sheet of a step as its reader may see it, with its documents, rights, responsible and suggestions (annex A 2.10, 2.11).

using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Sheets;

/// <summary>The latest validated version of a sheet, shown to every member: its number, when and by whom.</summary>
public sealed record SheetVersionView(int Number, DateTime ValidatedAt, Guid ValidatedBy);

/// <summary>
/// The sheet of a step (1.x `FicheVue`, `GET /api/etapes/{id}/fiche`): state and completeness as the reader sees them
/// (`SheetVisibility`), the content (the draft, or the latest validated version), the inputs and outputs of the step
/// (`DocumentFlows`, visible to every member) with the pain points of their uses, the latest version, the clarification
/// request, the responsible, the rights (`SheetRights`), the suggestions (only for the one who may fill, A-2.10-42) and,
/// for the one who sees the draft, the last change and the version to send back in `If-Match`.
/// </summary>
public sealed record SheetView(
    Guid StepId,
    SheetStatus Status,
    int Completeness,
    SheetContent Content,
    bool IsDraft,
    SheetVersionView? Validated,
    string? ClarificationRequest,
    SheetResponsible? Responsible,
    SheetPermissions Permissions,
    SheetSuggestionList Suggestions,
    IReadOnlyList<ProposedInput> Inputs,
    IReadOnlyList<StepOutput> Outputs,
    IReadOnlyList<PainPoint> PainPoints,
    DateTime? UpdatedAt,
    Guid? UpdatedBy,
    int? Version);

/// <summary>Projects a workspace into the <see cref="SheetView"/> of a reader, with the rules of the domain only.</summary>
public static class SheetProjection
{
    /// <summary>The sheet of a live step on a live map as the user of the role sees it; INTROUVABLE otherwise (A-2.10-44).</summary>
    public static SheetView Of(ProjectWorkspace workspace, Guid stepId, MemberRole role, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var step = SheetQueries.ReadableStep(workspace, stepId);
        var viewer = SheetRights.ViewerOf(workspace, step, role, userId);
        var visible = SheetVisibility.For(workspace, stepId, viewer);
        var summary = SheetVisibility.SummaryFor(workspace, stepId, viewer);
        var suggestions = viewer.CanFill
            ? SheetSuggestions.For(workspace, stepId, other => SheetRights.ViewerOf(workspace, other, role, userId))
            : new SheetSuggestionList(null, []);
        var inputs = DocumentFlows.InputsOf(workspace, stepId);
        var outputs = DocumentFlows.OutputsOf(workspace, stepId);
        var draft = visible.IsDraft ? SheetQueries.SheetOf(workspace, stepId) : null;
        var validated = visible.Version is { } version ? new SheetVersionView(version.VersionNumber, version.ValidatedAt, version.ValidatedBy) : null;
        return new SheetView(stepId, visible.Status, summary.Completeness, visible.Content, visible.IsDraft, validated,
            visible.ClarificationRequest, SheetRights.ResponsibleOf(workspace, step), SheetRights.PermissionsOf(viewer), suggestions,
            inputs, outputs, PainPointsOf(workspace, inputs, outputs),
            draft is null ? validated?.ValidatedAt : draft.UpdatedAt ?? draft.CreatedAt,
            draft is null ? validated?.ValidatedBy : draft.UpdatedBy ?? draft.CreatedBy,
            draft?.Version);
    }

    /// <summary>The pain points of the uses shown on the sheet (the inputs ticked by the step, the uses of its outputs).</summary>
    private static List<PainPoint> PainPointsOf(ProjectWorkspace workspace, IReadOnlyList<ProposedInput> inputs, IReadOnlyList<StepOutput> outputs) =>
        inputs.Select(input => input.Use).OfType<DocumentUse>().Concat(outputs.SelectMany(output => output.UsedBy))
            .Select(use => use.PainPointId).OfType<Guid>().Distinct()
            .Select(id => PainPointRules.Find(workspace, id)).OfType<PainPoint>().ToList();
}
