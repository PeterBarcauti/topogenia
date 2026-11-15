// LayoutCommands.cs
// The commands of the layout: the grouped layout of a map and the grouped move of dragged steps (annex A 2.4, AR-44).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Layout;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Layout;

/// <summary>
/// Why the client sends a grouped layout, which names the operation in the log (1.x `magasin.ts`): a drag of a block onto
/// free cells (`deplacer`) or the Align wand (`aligner`).
/// </summary>
public enum LayoutPurpose
{
    /// <summary>A move of steps: kind `deplacer`.</summary>
    Move,

    /// <summary>The Align wand (annex B 2.8): kind `aligner`.</summary>
    Align,
}

/// <summary>`PUT /api/maps/{id}/layout` (1.x `PUT /api/cartes/{id}/disposition`, Animateur): exact cells and lane order.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ApplyLayoutCommand(LayoutRequest request, LayoutPurpose purpose) : ProjectCommand<IReadOnlyList<Guid>>(AccessTarget.Map(request.MapId))
{
    public override string? LogKind => purpose switch
    {
        LayoutPurpose.Move => OperationKinds.Move,
        LayoutPurpose.Align => OperationKinds.Align,
        _ => throw new InvalidOperationException($"Unknown layout purpose {purpose}."),
    };

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override IReadOnlyList<Guid> Execute(ProjectWorkspace workspace, OperationContext context) => StepLayout.Apply(workspace, request);
}

/// <summary>
/// The grouped move of the steps of one drag or one move with the keyboard (AR-44, no 1.x route: the 1.x client sent a
/// layout or one PATCH per step), Animateur: one operation `deplacer`, undone by one undo (B-2.6-21, 22).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class MoveStepsCommand(GroupMoveRequest request) : ProjectCommand<IReadOnlyList<Guid>>(AccessTarget.Map(request.MapId))
{
    public override string? LogKind => OperationKinds.Move;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override IReadOnlyList<Guid> Execute(ProjectWorkspace workspace, OperationContext context) => StepLayout.MoveGroup(workspace, request);
}
