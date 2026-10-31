// UserRequest.cs
// A request of a user that names no existing project, or acts on one outside the command template: what it names and does (spec 3.4).

using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Commands;

/// <summary>
/// One request run by <see cref="UserRunner"/>: the creation of a project, the lists of the projects and of the trash, the
/// profile, the immediate purge of a project in the trash. Like a command, it declares its right with one attribute of
/// <see cref="Authorization.AccessRequirementAttribute"/> (`RequiresAuthenticated`, `RequiresAdmin`, or a role when it
/// names a project) and holds no rule: it reads through the ports of its scope, calls the domain and writes its change set.
/// </summary>
/// <typeparam name="TResult">The reply.</typeparam>
public abstract class UserRequest<TResult>
{
    /// <summary>
    /// The project named by the route, for a request that acts on one project without the lock of the command template
    /// (the purge runs its own short transactions, the last project opened writes the user only); null otherwise.
    /// </summary>
    public virtual AccessTarget? Target => null;

    /// <summary>True when the target must be in the trash (annex A 1, `Droits.cs:20`).</summary>
    public virtual bool TargetsTrash => false;

    /// <summary>
    /// True for a request that writes: it runs whole in one unit of work, run again from the start after a deadlock or a
    /// creation lost on a natural key (ADR 030, 033). False for a read (each port call is one consistent read, spec 3.4)
    /// and for the purge, whose port runs its own short transactions (spec 4.1).
    /// </summary>
    public virtual bool InUnitOfWork => false;

    /// <summary>
    /// True for a request that rewrites its target project outside the command template (the replacement by an import,
    /// ADR 052): the template locks the project (spec 4.7) once the right is granted and before the request reads it, so
    /// that no command of the project interleaves with it. It needs a unit of work.
    /// </summary>
    public virtual bool LocksTarget => false;

    /// <summary>Does the work; inside a unit of work, it runs again whole on a retry.</summary>
    public abstract Task<TResult> RunAsync(UserScope scope, CancellationToken cancellationToken);
}

/// <summary>The ports a user request reads and writes through.</summary>
public sealed record UserPorts(
    IUserReader Users,
    IProjectReader Projects,
    IWorkspaceStore Store,
    IUserDirectory Directory,
    IAdministrators Administrators,
    ITrashPurge Purge,
    IAuditStore Audit,
    AssistantPorts Assistant);

/// <summary>
/// What a user request knows: the user, the project of its target once the right is granted (null without a target),
/// the projects he may see in a list, the context of the operation (new on each attempt of a unit), the ports and the settings.
/// </summary>
public sealed record UserScope(
    CurrentUser User,
    Guid? ProjectId,
    ProjectVisibility Visibility,
    OperationContext Operation,
    UserPorts Ports,
    LanguageSettings Languages,
    AppSettings Settings)
{
    /// <summary>The project of the target; a programming error for a request that names none.</summary>
    public Guid Project => ProjectId ?? throw new InvalidOperationException("The request names no project.");
}

/// <summary>
/// The If-Match of a record that belongs to no project (the profile, AR-07): the version the client read, checked on the
/// record read in the unit, with the author and time of its last change (the details of <see cref="VersionCheck"/>).
/// </summary>
public static class RecordVersion
{
    /// <summary>Nothing to check without a version read; CONFLIT_VERSION when the record moved on.</summary>
    public static void Ensure(IVersionedRecord record, int? expected)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (expected is not { } read || record.Version == read)
            return;
        throw DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
        {
            ["object"] = record.Key.Type,
            ["updated_by"] = record.UpdatedBy ?? (record as ICreationAudited)?.CreatedBy,
            ["updated_at"] = record.UpdatedAt ?? (record as ICreationAudited)?.CreatedAt,
        });
    }
}
