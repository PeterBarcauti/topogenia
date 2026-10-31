// RaciCommands.cs
// The command of the RACI: set or remove the role of a lane on a step (annex A 1 etapes, A 2.13), a call of the domain.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Raci;

/// <summary>
/// `PUT /api/steps/{id}/raci` with `{lane_id, role|null}` (1.x `PUT /api/etapes/{id}/raci`, Animateur), logged on the map of
/// the step. Returns the role held by the lane afterwards; a role that does not change logs nothing. The If-Match is the
/// version of the step, which a change of its RACI moves on.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class SetRaciCommand(SetRaciRequest request) : ProjectCommand<StepRaci?>(AccessTarget.Step(request.StepId))
{
    public override string? LogKind => OperationKinds.Raci;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveStep(Target.Id).MapId;

    public override StepRaci? Execute(ProjectWorkspace workspace, OperationContext context) => RaciRules.SetRole(workspace, context, request);

    /// <summary>The If-Match of the request on the step.</summary>
    public static SetRaciCommand WithVersion(SetRaciRequest request, int? version)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request) { Versions = version is { } read ? [VersionCheck.Of<Step>(Step.KeyOf(request.StepId), read)] : [] };
    }
}
