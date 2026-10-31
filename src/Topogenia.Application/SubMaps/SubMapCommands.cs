// SubMapCommands.cs
// The commands of the sub-maps: detail a step, delete the detail, reattach a sub-map (annex A 1, A 2.2), each a call of the domain.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.SubMaps;

/// <summary>
/// `POST /api/steps/{id}/detail`: creates or reopens the sub-map of a step (1.x `detailler`, Animateur), logged on the map
/// of the step; warnings SOUS_CARTE_EXISTANTE and PROFONDEUR_DEPASSEE. A reopening changes nothing and logs nothing
/// (annex B 2.6-41).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class DetailStepCommand(Guid stepId) : ProjectCommand<DetailResult>(AccessTarget.Step(stepId))
{
    /// <summary>The first lane of the sub-map takes the translations of the name of the lane of the step.</summary>
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override string? LogKind => OperationKinds.Detail;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveStep(Target.Id).MapId;

    public override DetailResult Execute(ProjectWorkspace workspace, OperationContext context) => SubMapRules.Detail(workspace, context, Target.Id);

    /// <summary>The If-Match of the request on the step, whose version a new detail moves on.</summary>
    public static DetailStepCommand WithVersion(Guid stepId, int? version) =>
        new(stepId) { Versions = version is { } read ? [VersionCheck.Of<Step>(Step.KeyOf(stepId), read)] : [] };

    public override IReadOnlyList<string> WarningsOf(DetailResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Warnings;
    }
}

/// <summary>
/// `DELETE /api/maps/{id}`: « Delete the detail », the sub-map and its whole descent in one batch (1.x `DELETE /api/cartes/{id}`
/// without `si_vide`, AR-08; Animateur). Logged on the map of the step that held the detail, where the user acts.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class DeleteSubMapCommand(Guid mapId) : ProjectCommand<IReadOnlyList<Guid>>(AccessTarget.Map(mapId))
{
    /// <summary>The steps of the descent take their sheets, documents and sub-maps with them.</summary>
    public override WorkspaceContent Content => WorkspaceContent.All;

    public override string? LogKind => OperationKinds.DeleteDetail;

    /// <summary>The map of the parent step; the root map has none and is refused by the domain (CARTE_RACINE).</summary>
    public override Guid? MapOf(ProjectWorkspace workspace) =>
        workspace.LiveMap(Target.Id).ParentStepId is { } parent ? workspace.LiveStep(parent).MapId : Target.Id;

    public override IReadOnlyList<Guid> Execute(ProjectWorkspace workspace, OperationContext context) =>
        SubMapRules.DeleteSubMap(workspace, context, Target.Id);

    /// <summary>The If-Match of the request on the sub-map.</summary>
    public static DeleteSubMapCommand WithVersion(Guid mapId, int? version) =>
        new(mapId) { Versions = version is { } read ? [VersionCheck.Of<ProcessMap>(ProcessMap.KeyOf(mapId), read)] : [] };
}

/// <summary>
/// `POST /api/maps/{id}/reattach`: gives the sub-map to another step (1.x `rattacher`, Animateur). The 1.x never logged it
/// (the client called it only to undo the deletion of a macro step); in 2.0 it is logged with the kind `detailler`, the
/// nearest one: the target step receives a detail. It is logged on the map of the target step; an unknown target step is
/// refused by the domain (RATTACHEMENT_INVALIDE).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ReattachSubMapCommand(Guid mapId, Guid targetStepId) : ProjectCommand<Guid>(AccessTarget.Map(mapId))
{
    public override string? LogKind => OperationKinds.Detail;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.Step(targetStepId)?.MapId ?? Target.Id;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context)
    {
        SubMapRules.Reattach(workspace, context, Target.Id, targetStepId);
        return Target.Id;
    }

    /// <summary>The If-Match of the request on the sub-map, whose parent step changes.</summary>
    public static ReattachSubMapCommand WithVersion(Guid mapId, Guid targetStepId, int? version) =>
        new(mapId, targetStepId) { Versions = version is { } read ? [VersionCheck.Of<ProcessMap>(ProcessMap.KeyOf(mapId), read)] : [] };
}
