// IAccessReader.cs
// Port that finds the project of the object named by a request and the role of a user in it (spec 3.4, annex A 1).

using Topogenia.Domain.Values;

namespace Topogenia.Application.Ports;

/// <summary>The kind of object a request names in its route.</summary>
public enum TargetKind { Project, Map, Lane, Step, Flow, Dissolution }

/// <summary>The object named by a request (the `{id}` of its route).</summary>
public readonly record struct AccessTarget(TargetKind Kind, Guid Id)
{
    public static AccessTarget Project(Guid id) => new(TargetKind.Project, id);

    public static AccessTarget Map(Guid id) => new(TargetKind.Map, id);

    public static AccessTarget Lane(Guid id) => new(TargetKind.Lane, id);

    public static AccessTarget Step(Guid id) => new(TargetKind.Step, id);

    public static AccessTarget Flow(Guid id) => new(TargetKind.Flow, id);

    public static AccessTarget Dissolution(Guid id) => new(TargetKind.Dissolution, id);
}

/// <summary>
/// The project of a target, whether it is in the trash, and the role that the user holds in it as a member (null when the
/// user is not a member). The administrator flag is not applied here: the policy applies it (annex A 1, `Droits.cs:30`).
/// </summary>
public sealed record ProjectAccess(Guid ProjectId, bool InTrash, MemberRole? Role);

/// <summary>
/// Reads, inside the transaction of the current unit of work, the project of the object named by a request and the
/// membership of the user. The object is found even when it is deleted logically (a deleted step still names its
/// project): what a deleted object allows is the rule of the command, not of the access.
/// </summary>
public interface IAccessReader
{
    /// <summary>The access of a user to the project of a target; null when the target does not exist.</summary>
    Task<ProjectAccess?> FindAsync(AccessTarget target, Guid userId, CancellationToken cancellationToken);
}
