// TestCommands.cs
// Commands of the template tests: each declares one right and calls a real operation of the domain, or none.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Support;

[RequiresRole(MemberRole.Facilitator)]
public sealed class AddLaneProbe(Guid mapId, string name, string? kind = "lane.add") : ProjectCommand<LaneResult>(AccessTarget.Map(mapId))
{
    public int Runs { get; private set; }

    public List<Guid> OperationIds { get; } = [];

    public override string? LogKind => kind;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override LaneResult Execute(ProjectWorkspace workspace, OperationContext context)
    {
        Runs++;
        OperationIds.Add(context.OperationId);
        return LaneOperations.Add(workspace, context, new AddLaneRequest(Target.Id, name));
    }
}

[RequiresRole(MemberRole.Facilitator)]
public sealed class RenameLaneProbe(Guid laneId, string name) : ProjectCommand<LaneResult>(AccessTarget.Lane(laneId))
{
    public bool Ran { get; private set; }

    public override string? LogKind => "lane.update";

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveLane(Target.Id).MapId;

    public override LaneResult Execute(ProjectWorkspace workspace, OperationContext context)
    {
        Ran = true;
        return LaneOperations.Update(workspace, context, new UpdateLaneRequest(Target.Id) { Name = new FieldChange<string?>(name) });
    }
}

[RequiresRole(MemberRole.Facilitator)]
public sealed class DeleteLaneProbe(Guid laneId) : ProjectCommand<bool>(AccessTarget.Lane(laneId))
{
    public override string? LogKind => "lane.delete";

    public override Guid? MapOf(ProjectWorkspace workspace) => workspace.LiveLane(Target.Id).MapId;

    public override bool Execute(ProjectWorkspace workspace, OperationContext context)
    {
        LaneOperations.Delete(workspace, context, new DeleteLaneRequest(Target.Id, LaneContentChoice.Delete));
        return true;
    }
}

[RequiresRole(MemberRole.Facilitator)]
public sealed class AddStepProbe(Guid mapId, Guid laneId, int column) : ProjectCommand<StepResult>(AccessTarget.Map(mapId))
{
    public override string? LogKind => "step.create";

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override StepResult Execute(ProjectWorkspace workspace, OperationContext context) =>
        StepOperations.Create(workspace, context, new CreateStepRequest(Target.Id, laneId, column, $"Étape {column}"));
}

/// <summary>Adds a lane, then refuses: the template must write nothing.</summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class AddLaneThenRefuseCommand(Guid mapId) : ProjectCommand<bool>(AccessTarget.Map(mapId))
{
    public override string? LogKind => "lane.add";

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override bool Execute(ProjectWorkspace workspace, OperationContext context)
    {
        LaneOperations.Add(workspace, context, new AddLaneRequest(Target.Id, "Refusée"));
        throw DomainException.Conflict(ErrorCodes.LastLane);
    }
}

/// <summary>Changes nothing: no writing, no log entry.</summary>
[RequiresRole(MemberRole.Reader)]
public sealed class ReadOnlyCommand(AccessTarget target, bool inTrash = false) : ProjectCommand<bool>(target)
{
    public bool Ran { get; private set; }

    public override bool TargetsTrash => inTrash;

    public override string? LogKind => "nothing";

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override bool Execute(ProjectWorkspace workspace, OperationContext context) => Ran = true;
}

[RequiresRole(MemberRole.Facilitator)]
public sealed class FacilitatorInTrashCommand(Guid projectId) : ProjectCommand<bool>(AccessTarget.Project(projectId))
{
    public override bool TargetsTrash => true;

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override bool Execute(ProjectWorkspace workspace, OperationContext context) => true;
}

[RequiresSheetFill]
public sealed class FillSheetCommand(Guid stepId) : ProjectCommand<bool>(AccessTarget.Step(stepId))
{
    public bool Ran { get; private set; }

    public override Guid? SheetStepId => Target.Id;

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override bool Execute(ProjectWorkspace workspace, OperationContext context) => Ran = true;
}

/// <summary>A command that requires the right to fill without naming its step: a programming error.</summary>
[RequiresSheetFill]
public sealed class FillWithoutStepCommand(Guid stepId) : ProjectCommand<bool>(AccessTarget.Step(stepId))
{
    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override bool Execute(ProjectWorkspace workspace, OperationContext context) => true;
}

[RequiresAdmin]
public sealed class AdminCommand(Guid projectId) : ProjectCommand<bool>(AccessTarget.Project(projectId))
{
    public bool Ran { get; private set; }

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override bool Execute(ProjectWorkspace workspace, OperationContext context) => Ran = true;
}

/// <summary>Declares no right: the template refuses to run it.</summary>
public sealed class UndeclaredCommand(Guid projectId) : ProjectCommand<bool>(AccessTarget.Project(projectId))
{
    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override bool Execute(ProjectWorkspace workspace, OperationContext context) => true;
}

/// <summary>A lane rename with the version the client read (If-Match).</summary>
public static class Commands
{
    public static RenameLaneProbe Rename(Guid laneId, string name, int version) =>
        new(laneId, name) { Versions = [VersionCheck.Of<Lane>(Lane.KeyOf(laneId), version)] };
}
