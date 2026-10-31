// AccessPolicy.cs
// The single reading of the declared rights: 404 for a non-member, 403 for a weaker role, the administrator as facilitator.

using System.Collections.Concurrent;
using System.Reflection;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Authorization;

/// <summary>
/// Applies the requirement declared on a command (annex A 1, 1.x `Droits.Exiger`): the project of the target must exist
/// in the expected state (live, or in the trash for the commands of the trash), otherwise INTROUVABLE, so that a
/// non-member never learns that the project exists; then the role must reach the minimum, otherwise ACCES_REFUSE. An
/// administrator is treated as facilitator on every project (`Droits.cs:30`).
/// </summary>
public static class AccessPolicy
{
    private static readonly ConcurrentDictionary<Type, AccessRequirementAttribute> Requirements = new();

    /// <summary>The requirement declared on a command type; a command that declares none is a programming error.</summary>
    public static AccessRequirementAttribute RequirementOf(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        return Requirements.GetOrAdd(commandType, type => type.GetCustomAttribute<AccessRequirementAttribute>(inherit: false)
            ?? throw new InvalidOperationException($"The command {type.Name} declares no access requirement (spec 3.4)."));
    }

    /// <summary>
    /// The role the user acts with in the project of the access: null when the target does not exist, when the project is
    /// not in the expected state or when the user is neither a member nor an administrator.
    /// </summary>
    public static MemberRole? EffectiveRole(CurrentUser user, ProjectAccess? access, bool inTrash)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (access is null || access.InTrash != inTrash)
            return null;
        return user.IsAdmin ? MemberRole.Facilitator : access.Role;
    }

    /// <summary>
    /// What the user alone decides, before anything is read: an administrator command is forbidden to anyone else
    /// (ACCES_REFUSE, annex A 1), whether the project exists or not.
    /// </summary>
    public static void DemandUser(AccessRequirementAttribute requirement, CurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (requirement is RequiresAdminAttribute && !user.IsAdmin)
            throw DomainException.Forbidden();
    }

    /// <summary>
    /// Refuses the request that the requirement does not allow on the project of the target and returns the effective
    /// role it acts with; <see cref="DemandUser"/> has run before. The right to fill a sheet needs the step, so it is
    /// finished on the workspace by <see cref="DemandSheetFill"/>.
    /// </summary>
    public static MemberRole Demand(AccessRequirementAttribute requirement, CurrentUser user, ProjectAccess? access, bool inTrash)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        var role = EffectiveRole(user, access, inTrash) ?? throw DomainException.NotFound();
        if (requirement is RequiresRoleAttribute { Minimum: var minimum } && role < minimum)
            throw DomainException.Forbidden();
        return role;
    }

    /// <summary>
    /// The right to fill the sheet of a live step (annex A 1 « Rempl. », AR-01): INTROUVABLE for a step that is not live,
    /// ACCES_REFUSE when the user may not fill it.
    /// </summary>
    public static void DemandSheetFill(ProjectWorkspace workspace, Guid stepId, MemberRole role, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var step = workspace.LiveStep(stepId);
        if (!SheetRights.ViewerOf(workspace, step, role, userId).CanFill)
            throw DomainException.Forbidden();
    }
}
