// AccessRequirements.cs
// Declarative rights of a command: the minimal role, the right to fill a sheet, the administrator, any authenticated user (spec 3.4, annex A 6-7).

using Topogenia.Domain.Values;

namespace Topogenia.Application.Authorization;

/// <summary>
/// The right a command demands, declared once on its class (spec 3.4 step 1): the template reads it before any work and
/// no handler checks a right itself (annex A 6-7, the 1.x scattered `Droits.Exiger`, `EstAdmin` and `PeutRemplir`).
/// A command declares exactly one requirement; one without any is refused by the template.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public abstract class AccessRequirementAttribute : Attribute
{
}

/// <summary>A minimal role in the project of the target; an administrator acts as facilitator (annex A 1, `Droits.cs:30`).</summary>
public sealed class RequiresRoleAttribute(MemberRole minimum) : AccessRequirementAttribute
{
    public MemberRole Minimum { get; } = minimum;
}

/// <summary>
/// The right to fill the sheet of the step of the command (annex A 1 « Rempl. », AR-01): the facilitator, or the
/// contributor to whom the step is assigned or who holds its lane (`SheetRights.CanFill`).
/// </summary>
public sealed class RequiresSheetFillAttribute : AccessRequirementAttribute
{
}

/// <summary>
/// An administrator of the application (annex A 1: creation, import, duplication, purge of a project). When the command
/// names a project, the project must still exist.
/// </summary>
public sealed class RequiresAdminAttribute : AccessRequirementAttribute
{
}

/// <summary>
/// Any authenticated user (annex A 1: the profile, the list of the projects, the list of the trash). The
/// request names no project, or reads only what the user may see; it is run by <see cref="Commands.UserRunner"/>.
/// </summary>
public sealed class RequiresAuthenticatedAttribute : AccessRequirementAttribute
{
}
