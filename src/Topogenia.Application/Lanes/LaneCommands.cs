// LaneCommands.cs
// The commands of the lanes: add, update, reorder, delete (annex A 1 couloirs, A 2.3), each a call of the domain.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Lanes;

/// <summary>`POST /api/maps/{id}/lanes` (1.x `POST /api/cartes/{id}/couloirs`, Animateur).</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class AddLaneCommand(AddLaneRequest request) : ProjectCommand<LaneResult>(AccessTarget.Map(request.MapId))
{
    /// <summary>The actor of a lane must be a member of the project (A-2.3).</summary>
    public override WorkspaceContent Content => WorkspaceContent.Members;

    public override string? LogKind => OperationKinds.CreateLane;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override LaneResult Execute(ProjectWorkspace workspace, OperationContext context) => LaneOperations.Add(workspace, context, request);
}

/// <summary>`PATCH /api/lanes/{id}` with If-Match (1.x `PATCH /api/couloirs/{id}`, Animateur).</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class UpdateLaneCommand(UpdateLaneRequest request) : ProjectCommand<LaneResult>(AccessTarget.Lane(request.LaneId))
{
    public override WorkspaceContent Content => WorkspaceContent.Members;

    public override string? LogKind => OperationKinds.UpdateLane;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveLane(Target.Id).MapId;

    public override LaneResult Execute(ProjectWorkspace workspace, OperationContext context) => LaneOperations.Update(workspace, context, request);

    /// <summary>The If-Match of the request on the lane.</summary>
    public static UpdateLaneCommand WithVersion(UpdateLaneRequest request, int? version) =>
        new(request) { Versions = version is { } read ? [VersionCheck.Of<Lane>(Lane.KeyOf(request.LaneId), read)] : [] };
}

/// <summary>`PUT /api/maps/{id}/lane-order`: the complete list of the lanes of the map (1.x `ordre-couloirs`, Animateur).</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ReorderLanesCommand(Guid mapId, IReadOnlyList<Guid>? order) : ProjectCommand<IReadOnlyList<Guid>>(AccessTarget.Map(mapId))
{
    public override string? LogKind => OperationKinds.LaneOrder;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override IReadOnlyList<Guid> Execute(ProjectWorkspace workspace, OperationContext context)
    {
        LaneOperations.Reorder(workspace, Target.Id, order);
        return workspace.LanesOf(Target.Id).Select(lane => lane.Id).ToList();
    }
}

/// <summary>`DELETE /api/lanes/{id}?steps=move|delete&amp;to=` with If-Match (1.x `DELETE /api/couloirs/{id}`, Animateur).</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class DeleteLaneCommand(DeleteLaneRequest request) : ProjectCommand<Guid>(AccessTarget.Lane(request.LaneId))
{
    /// <summary>The steps deleted with the lane take their sheets, documents and sub-maps with them.</summary>
    public override WorkspaceContent Content => WorkspaceContent.All;

    public override string? LogKind => OperationKinds.Delete;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveLane(Target.Id).MapId;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context)
    {
        LaneOperations.Delete(workspace, context, request);
        return request.LaneId;
    }

    /// <summary>The If-Match of the request on the deleted lane (spec 5.3, ADR 045 revised).</summary>
    public static DeleteLaneCommand WithVersion(DeleteLaneRequest request, int? version) =>
        new(request) { Versions = version is { } read ? [VersionCheck.Of<Lane>(Lane.KeyOf(request.LaneId), read)] : [] };
}
