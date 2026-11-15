// TrashCommands.cs
// The trash of the projects: move to the trash, list, restore, immediate purge by an administrator (annex A 1 `/api/corbeille`, A 2.15).

using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Trash;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Trash;

/// <summary>A project of the trash (1.x `ProjetCorbeille`): deleted when and by whom, purged for good when.</summary>
public sealed record TrashItem(Guid Id, string Code, string Name, DateTime DeletedAt, string? DeletedBy, DateTime PurgeAt, int StepCount);

/// <summary>The trash as the home page shows it (1.x `CorbeilleVue`): the retention in days and the projects, last deleted first.</summary>
public sealed record TrashView(int RetentionDays, IReadOnlyList<TrashItem> Projects);

/// <summary>
/// `DELETE /api/projects/{id}?confirmation=CODE` (1.x `DELETE /api/projets/{id}`, Animateur): `TrashRules.MoveToTrash`,
/// confirmed by the exact code (A-2.15-13), with If-Match on the project (spec 5.3, ADR 045 revised). The project is then
/// out of reach of everyone but the trash (A-2.15-14). It touches no map: nothing is logged.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class MoveToTrashCommand(Guid projectId, string? confirmation) : ProjectCommand<Guid>(AccessTarget.Project(projectId))
{
    public override string? AuditKind => AuditKinds.ProjectTrashed;

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context) => TrashRules.MoveToTrash(workspace, context, confirmation).Id;

    /// <summary>The If-Match of the request on the project moved to the trash.</summary>
    public static MoveToTrashCommand WithVersion(Guid projectId, string? confirmation, int? version) =>
        new(projectId, confirmation) { Versions = version is { } read ? [VersionCheck.Of<Project>(Project.KeyOf(projectId), read)] : [] };
}

/// <summary>
/// `POST /api/trash/{id}/restore` (1.x `POST /api/corbeille/{id}/restaurer`, Animateur of the deleted project, an
/// administrator included): `TrashRules.Restore` (A-2.15-17); a live project is not found.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class RestoreProjectCommand(Guid projectId) : ProjectCommand<Guid>(AccessTarget.Project(projectId))
{
    public override bool TargetsTrash => true;

    public override string? AuditKind => AuditKinds.ProjectRestored;

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context) => TrashRules.Restore(workspace).Id;
}

/// <summary>
/// `GET /api/trash` (1.x `GET /api/corbeille`, authenticated): the projects in the trash that the user facilitated, every
/// one for an administrator (A-2.15-15), with the date of their purge (A-2.15-16) and the retention.
/// </summary>
[RequiresAuthenticated]
public sealed class ListTrashQuery : UserRequest<TrashView>
{
    public override async Task<TrashView> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var rows = await scope.Ports.Projects.TrashAsync(scope.Visibility, cancellationToken);
        var items = rows.Select(row => new TrashItem(row.Project.Id, row.Project.Code, row.Project.Name, row.Project.DeletedAt!.Value, row.DeletedByName,
                TrashRules.PurgeDate(row.Project, scope.Settings)!.Value, row.StepCount))
            .OrderByDescending(item => item.DeletedAt)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ToList();
        return new TrashView(TrashRules.RetentionDays(scope.Settings), items);
    }
}

/// <summary>
/// `DELETE /api/trash/{id}` (1.x `DELETE /api/corbeille/{id}`, admin): the immediate purge of a project in the trash
/// (A-2.15-18), `PurgePlanner.WholeProject` on its whole workspace, deleted by <see cref="ITrashPurge"/> in short
/// transactions (spec 4.1); ACCES_REFUSE for anyone else, INTROUVABLE for a live or absent project. A project restored
/// between the plan and the purge stops it (`CONFLIT_VERSION`). The purge done, its audit event is appended (spec 5.5).
/// </summary>
[RequiresAdmin]
public sealed class PurgeProjectCommand(Guid projectId) : UserRequest<PurgeOutcome>
{
    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override bool TargetsTrash => true;

    public override async Task<PurgeOutcome> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var workspace = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.All, cancellationToken) ?? throw DomainException.NotFound();
        var outcome = await scope.Ports.Purge.PurgeAsync(scope.Project, PurgePlanner.WholeProject(workspace), cancellationToken);
        await scope.Ports.Audit.AppendAsync(AuditTrail.Event(scope.Operation, AuditKinds.ProjectPurged, scope.User.Id, scope.Project), cancellationToken);
        return outcome;
    }
}
