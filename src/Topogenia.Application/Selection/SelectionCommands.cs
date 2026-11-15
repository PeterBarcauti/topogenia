// SelectionCommands.cs
// The commands of a selection: delete, paste, transfer a block, detail the selection (annex A 1 cartes, A 2.8), each a call of the domain.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Selection;

/// <summary>`POST /api/maps/{id}/deletion`: steps and flows in one batch, `stitch`, `sub_maps` (1.x `suppression`, Animateur).</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class DeleteSelectionCommand(Guid mapId, GroupDeletionRequest request) : ProjectCommand<SelectionResult>(AccessTarget.Map(mapId))
{
    /// <summary>The steps deleted take their sheets, documents and sub-maps with them.</summary>
    public override WorkspaceContent Content => WorkspaceContent.All;

    public override string? LogKind => OperationKinds.Delete;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override SelectionResult Execute(ProjectWorkspace workspace, OperationContext context) =>
        GroupDeletion.Delete(workspace, context, Target.Id, request);
}

/// <summary>`POST /api/maps/{id}/paste`: a copy, with an optional anchor in the flow (1.x `coller`, Animateur).</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class PasteCommand(Guid mapId, PasteRequest request) : ProjectCommand<SelectionResult>(AccessTarget.Map(mapId))
{
    public override string? LogKind => OperationKinds.Paste;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override SelectionResult Execute(ProjectWorkspace workspace, OperationContext context) =>
        PasteCopy.Paste(workspace, context, Target.Id, request);
}

/// <summary>
/// `POST /api/maps/{id}/transfer`: cut and paste or move of a block to the map of the route (1.x `transfert`, Animateur on
/// the map of origin and on the target map, which are of the same project). The operation is logged on the target map,
/// where the user pastes (annex B 2.6-30, AR-06): the stack of the map of origin is kept.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class TransferBlockCommand(Guid targetMapId, TransferRequest request) : ProjectCommand<SelectionResult>(AccessTarget.Map(targetMapId))
{
    public override string? LogKind => OperationKinds.MoveBlock;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override SelectionResult Execute(ProjectWorkspace workspace, OperationContext context) =>
        BlockTransfer.Transfer(workspace, context, Target.Id, request);

    /// <summary>
    /// AR-71, A-2.8-26: a step not found in the workspace of the target map that belongs to another live project is
    /// TRANSFERT_HORS_PROJET, after the checks of the domain that come first (count, duplicates, maps of the steps). The
    /// code is given only to a user who may read that other project; anyone else keeps INTROUVABLE, which reveals nothing
    /// (spec 5.6-2).
    /// </summary>
    public override async Task<DomainException> TranslateRefusalAsync(DomainException refusal, RefusalContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        ArgumentNullException.ThrowIfNull(context);
        if (refusal.Code != ErrorCodes.NotFound)
            return refusal;
        foreach (var step in request.Steps ?? [])
        {
            var found = await context.Access.FindAsync(AccessTarget.Step(step.StepId), context.User.Id, cancellationToken);
            if (found is { InTrash: false } && found.ProjectId != context.ProjectId && (found.Role is not null || context.User.IsAdmin))
                return DomainException.Invalid(ErrorCodes.TransferOutsideProject);
        }
        return refusal;
    }
}

/// <summary>`POST /api/maps/{id}/group`: « Detail the selection » (1.x `regrouper`, Animateur); warning PROFONDEUR_DEPASSEE.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class GroupSelectionCommand(Guid mapId, GroupRequest request) : ProjectCommand<SelectionResult>(AccessTarget.Map(mapId))
{
    /// <summary>The copies of the lanes take the translations of their names.</summary>
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override string? LogKind => OperationKinds.Group;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override SelectionResult Execute(ProjectWorkspace workspace, OperationContext context) =>
        GroupIntoSubMap.Group(workspace, context, Target.Id, request);

    public override IReadOnlyList<string> WarningsOf(SelectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Warnings;
    }
}
