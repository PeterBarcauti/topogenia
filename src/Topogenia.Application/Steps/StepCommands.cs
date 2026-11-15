// StepCommands.cs
// The commands of the steps: create, update, insert and delete (annex A 1, A 2.4, A 2.2), each a call of the domain.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Selection;
using Topogenia.Domain.Steps;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Steps;

/// <summary>`POST /api/maps/{id}/steps` (1.x `POST /api/cartes/{id}/etapes`, Animateur).</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class CreateStepCommand(CreateStepRequest request) : ProjectCommand<StepResult>(AccessTarget.Map(request.MapId))
{
    public override string? LogKind => OperationKinds.CreateStep;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override StepResult Execute(ProjectWorkspace workspace, OperationContext context) => StepOperations.Create(workspace, context, request);
}

/// <summary>
/// `PATCH /api/steps/{id}` with If-Match (1.x `PATCH /api/etapes/{id}`, Animateur): label, type, automation, assignee and
/// cell. A drag or a move with the keyboard goes through <see cref="Layout.MoveStepsCommand"/> (kind `deplacer`, AR-44).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class UpdateStepCommand(UpdateStepRequest request) : ProjectCommand<StepResult>(AccessTarget.Step(request.StepId))
{
    /// <summary>The assignee must be a member (A-2.4-25); a new label drops the automatic translations.</summary>
    public override WorkspaceContent Content => WorkspaceContent.Members | WorkspaceContent.Translations;

    public override string? LogKind => OperationKinds.UpdateStep;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveStep(Target.Id).MapId;

    public override StepResult Execute(ProjectWorkspace workspace, OperationContext context) => StepOperations.Update(workspace, context, request);

    /// <summary>The If-Match of the request on the step.</summary>
    public static UpdateStepCommand WithVersion(UpdateStepRequest request, int? version) =>
        new(request) { Versions = version is { } read ? [VersionCheck.Of<Step>(Step.KeyOf(request.StepId), read)] : [] };
}

/// <summary>`POST /api/steps/{id}/insert` (1.x `POST /api/etapes/{id}/inserer`, Animateur).</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class InsertStepCommand(InsertStepRequest request) : ProjectCommand<StepResult>(AccessTarget.Step(request.StepId))
{
    public override string? LogKind => OperationKinds.InsertStep;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveStep(Target.Id).MapId;

    public override StepResult Execute(ProjectWorkspace workspace, OperationContext context) => StepInsertion.Insert(workspace, context, request);
}

/// <summary>
/// `DELETE /api/steps/{id}?sub_map=delete|attach&amp;to=&amp;stitch=` (1.x `DELETE /api/etapes/{id}?sous_carte=&amp;vers=&amp;recoudre=`,
/// Animateur), with If-Match on the step. The sub-map is settled by RG-C09 (<see cref="MacroStepDeletion"/>); with `stitch`, the predecessors of the
/// step are linked to its successors by the rules of the selection (<see cref="GroupDeletion"/>, A-2.8).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class DeleteStepCommand(Guid stepId, SubMapChoice? subMap = null, Guid? heirStepId = null, bool stitch = false)
    : ProjectCommand<Guid>(AccessTarget.Step(stepId))
{
    /// <summary>The step and its sub-map take their sheets, documents and translations with them.</summary>
    public override WorkspaceContent Content => WorkspaceContent.All;

    public override string? LogKind => OperationKinds.Delete;

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveStep(Target.Id).MapId;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context)
    {
        if (!stitch)
        {
            MacroStepDeletion.DeleteStep(workspace, context, Target.Id, subMap, heirStepId);
            return Target.Id;
        }
        var mapId = workspace.LiveStep(Target.Id).MapId;
        MacroStepDeletion.SettleSubMap(workspace, context, Target.Id, subMap, heirStepId);
        GroupDeletion.Delete(workspace, context, mapId, new GroupDeletionRequest([Target.Id], null, Stitch: true));
        return Target.Id;
    }

    /// <summary>The If-Match of the request on the deleted step (spec 5.3, ADR 045 revised).</summary>
    public static DeleteStepCommand WithVersion(Guid stepId, SubMapChoice? subMap, Guid? heirStepId, bool stitch, int? version) =>
        new(stepId, subMap, heirStepId, stitch) { Versions = version is { } read ? [VersionCheck.Of<Step>(Step.KeyOf(stepId), read)] : [] };
}
