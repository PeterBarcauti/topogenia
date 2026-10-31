// FlowCommands.cs
// The commands of the flows: create, relabel, reverse, delete and the command bar (annex A 1 liens, A 2.6, AR-155), each a call of the domain.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Flows;

/// <summary>`POST /api/maps/{id}/flows` (1.x `POST /api/cartes/{id}/liens`, Animateur).</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class CreateFlowCommand(CreateFlowRequest request) : ProjectCommand<Flow>(AccessTarget.Map(request.MapId))
{
    public override string? LogKind => OperationKinds.CreateFlow;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override Flow Execute(ProjectWorkspace workspace, OperationContext context) => FlowRules.Create(workspace, context, request);
}

/// <summary>`PATCH /api/flows/{id}` with If-Match (1.x `PATCH /api/liens/{id}`, Animateur): the label.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class RelabelFlowCommand(RelabelFlowRequest request) : ProjectCommand<RelabelFlowResult>(AccessTarget.Flow(request.FlowId))
{
    /// <summary>A new label drops the automatic translations of the old one (A-2.6-13).</summary>
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override string? LogKind => OperationKinds.UpdateFlow;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveFlow(Target.Id).MapId;

    public override RelabelFlowResult Execute(ProjectWorkspace workspace, OperationContext context) => FlowRules.Relabel(workspace, context, request);

    /// <summary>The If-Match of the request on the flow.</summary>
    public static RelabelFlowCommand WithVersion(RelabelFlowRequest request, int? version) =>
        new(request) { Versions = version is { } read ? [VersionCheck.Of<Flow>(Flow.KeyOf(request.FlowId), read)] : [] };
}

/// <summary>
/// `PUT /api/flows/{id}/share` with If-Match (3.0, ADR 085, Animateur, like the label): the share of the cases of an exit of
/// a decision or an inclusive gateway, or null to clear it; one operation of the log, undone by one Ctrl+Z.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ShareFlowCommand(ShareFlowRequest request) : ProjectCommand<Flow>(AccessTarget.Flow(request.FlowId))
{
    public override string? LogKind => OperationKinds.UpdateFlow;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveFlow(Target.Id).MapId;

    public override Flow Execute(ProjectWorkspace workspace, OperationContext context) => FlowRules.Share(workspace, context, request);

    /// <summary>The If-Match of the request on the flow.</summary>
    public static ShareFlowCommand WithVersion(ShareFlowRequest request, int? version) =>
        new(request) { Versions = version is { } read ? [VersionCheck.Of<Flow>(Flow.KeyOf(request.FlowId), read)] : [] };
}

/// <summary>`POST /api/flows/{id}/reverse` with If-Match (new in 2.0, AR-155, Animateur): the flow in the other direction.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ReverseFlowCommand(Guid flowId) : ProjectCommand<Flow>(AccessTarget.Flow(flowId))
{
    public override string? LogKind => OperationKinds.ReverseFlow;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveFlow(Target.Id).MapId;

    public override Flow Execute(ProjectWorkspace workspace, OperationContext context) => FlowRules.Reverse(workspace, context, Target.Id);

    /// <summary>The If-Match of the request on the flow.</summary>
    public static ReverseFlowCommand WithVersion(Guid flowId, int? version) =>
        new(flowId) { Versions = version is { } read ? [VersionCheck.Of<Flow>(Flow.KeyOf(flowId), read)] : [] };
}

/// <summary>`DELETE /api/flows/{id}` with If-Match (1.x `DELETE /api/liens/{id}`, Animateur): the flow goes to the batch of the operation.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class DeleteFlowCommand(Guid flowId) : ProjectCommand<Guid>(AccessTarget.Flow(flowId))
{
    public override string? LogKind => OperationKinds.Delete;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveFlow(Target.Id).MapId;

    /// <summary>Returns the deletion batch of the flow.</summary>
    public override Guid Execute(ProjectWorkspace workspace, OperationContext context) => FlowRules.Delete(workspace, context, Target.Id);

    /// <summary>The If-Match of the request on the deleted flow (spec 5.3, ADR 045 revised).</summary>
    public static DeleteFlowCommand WithVersion(Guid flowId, int? version) =>
        new(flowId) { Versions = version is { } read ? [VersionCheck.Of<Flow>(Flow.KeyOf(flowId), read)] : [] };
}

/// <summary>`POST /api/maps/{id}/command` (1.x `POST /api/cartes/{id}/commande`, Animateur): the command bar, all or nothing.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ApplyCommandBarCommand(Guid mapId, string? text) : ProjectCommand<CommandResult>(AccessTarget.Map(mapId))
{
    public override string? LogKind => OperationKinds.Command;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override CommandResult Execute(ProjectWorkspace workspace, OperationContext context) => CommandBar.Apply(workspace, context, Target.Id, text);
}
