// DissolutionCommands.cs
// The commands of the dissolution of a detail: dissolve, assign, remove from and finish the remainder (annex A 1 remontees, A 2.9).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Dissolutions;

/// <summary>
/// `POST /api/steps/{id}/dissolve`: dissolves the detail of a step into its flow (1.x `remonter`, Animateur), logged on the
/// map of the step. The identifier of the result is the dissolution (`DissolutionId`). Undone by `undo` (AR-08: the 1.x
/// route `/remontees/{id}/annuler` is gone).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class DissolveCommand(Guid stepId) : ProjectCommand<DissolveResult>(AccessTarget.Step(stepId))
{
    /// <summary>The frequencies of the sheets are compared, and the labels of the flows keep their translations.</summary>
    public override WorkspaceContent Content => WorkspaceContent.Sheets | WorkspaceContent.Translations;

    public override string? LogKind => OperationKinds.Dissolve;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveStep(Target.Id).MapId;

    public override DissolveResult Execute(ProjectWorkspace workspace, OperationContext context) => Dissolve.Run(workspace, context, Target.Id);

    /// <summary>The If-Match of the request on the dissolved step.</summary>
    public static DissolveCommand WithVersion(Guid stepId, int? version) =>
        new(stepId) { Versions = version is { } read ? [VersionCheck.Of<Step>(Step.KeyOf(stepId), read)] : [] };
}

/// <summary>What the commands of a frame share: the dissolution named by the route, its map and the remainder they read.</summary>
public abstract class FrameCommand<TResult> : ProjectCommand<TResult>
{
    protected FrameCommand(Guid dissolutionId)
        : base(AccessTarget.Dissolution(dissolutionId))
    {
    }

    /// <summary>The documents, usages and pain points of the remainder, and the sheet of the dissolved step of its view.</summary>
    public override WorkspaceContent Content => WorkspaceContent.Documents | WorkspaceContent.Sheets;

    /// <summary>The map of the frame; INTROUVABLE for an unknown dissolution.</summary>
    public override Guid? MapOf(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return (workspace.Find<Dissolution>(Dissolution.KeyOf(Target.Id)) ?? throw DomainException.NotFound()).MapId;
    }
}

/// <summary>
/// `POST /api/dissolutions/{id}/assign`: moves an element of the remainder to a step or to the map, or brings it back
/// (1.x `affecter`, Animateur). Returns the open frame of the map.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class AssignRemainderCommand(Guid dissolutionId, AssignRequest request) : FrameCommand<FrameView?>(dissolutionId)
{
    public override string? LogKind => OperationKinds.Assign;

    public override FrameView? Execute(ProjectWorkspace workspace, OperationContext context) =>
        Remainder.Assign(workspace, context, Target.Id, request);
}

/// <summary>
/// `POST /api/dissolutions/{id}/remove`: removes an element of the remainder for good (1.x `supprimer`, Animateur). It cannot
/// be undone and is not logged (annex B 2.6-39, AR-21).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class RemoveRemainderCommand(Guid dissolutionId, RemoveRequest request) : FrameCommand<FrameView?>(dissolutionId)
{
    public override FrameView? Execute(ProjectWorkspace workspace, OperationContext context) =>
        Remainder.Remove(workspace, context, Target.Id, request);
}

/// <summary>
/// `POST /api/dissolutions/{id}/finish`: stores what is left of the remainder and closes the frame (1.x `terminer`,
/// Animateur); returns the arrangements, none for a closed frame. Logged as `terminer_remontee` when something was stored;
/// the closing of an empty frame stores nothing and is not logged (annex B 2.6-40, AR-21, 1.x `magasin.ts` « Fermer un
/// cadre vide ne se defait pas »).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class FinishDissolutionCommand(Guid dissolutionId) : FrameCommand<IReadOnlyList<Arrangement>>(dissolutionId)
{
    private IReadOnlyList<Arrangement>? stored;

    public override string? LogKind => stored is { Count: 0 } ? null : OperationKinds.FinishDissolution;

    public override IReadOnlyList<Arrangement> Execute(ProjectWorkspace workspace, OperationContext context) =>
        stored = Remainder.Finish(workspace, context, Target.Id);
}
