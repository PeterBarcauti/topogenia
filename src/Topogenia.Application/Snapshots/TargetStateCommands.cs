// TargetStateCommands.cs
// The target state of a project (Makigami M6, ADR 095): a new project made by the export then the import of the complete package of its source, linked to it, with the reference photo of its indicators; that reference read again.

using System.Text.Json;
using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Snapshots;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Snapshots;

/// <summary>The ports a target state needs: the package written and read again in memory, by the path every import takes.</summary>
public sealed record TargetStatePorts(IPackageExporter Exporter, IPackageImporter Importer);

/// <summary>
/// `POST /api/projects/{id}/target` (administrator, as « Dupliquer »): the complete package of the source (sheets,
/// documents, pain points, summaries, translations, people) written and imported again in memory, in one unit of work,
/// under a free code and its name; the new project names its source (`baseline_project_id`) and keeps the indicators of
/// the source now as its reference photo, under its label, and none of the photos of the source (P3d). The event of audit
/// is the one of an import, with its source.
/// </summary>
[RequiresAdmin]
public sealed class CreateTargetStateCommand(TargetStatePorts ports, Guid projectId, string? code, string? name, string? label) : UserRequest<ImportedProject>
{
    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override bool InUnitOfWork => true;

    public override async Task<ImportedProject> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var chosenName = TextRules.Required(name, "name", ProjectCreation.NameMax);
        var reference = TextRules.Required(label, SnapshotRules.LabelField, SnapshotRules.LabelMax);
        var source = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.All, cancellationToken) ?? throw DomainException.NotFound();
        var indicators = ProjectIndicators.Of(source, new MapViewer(scope.User.Id, MemberRole.Facilitator));
        var named = ports.Exporter.UsersNamedBy(source);
        var logins = (await scope.Ports.Users.FindManyAsync([.. named], cancellationToken)).ToDictionary(user => user.Id, user => user.Login);
        // The source of the target is the project itself, linked below: the package does not name its own source. The
        // comments of presentation of its steps and of its flows go with them (ADR 099), the voices it keeps too (ADR 103).
        var said = await PresentationCommentRules.OfLiveStepsAsync(scope.Ports.Comments, source, cancellationToken);
        var saidOnFlows = await FlowPresentationCommentRules.OfLiveFlowsAsync(scope.Ports.FlowComments, source, cancellationToken);
        var voices = await scope.Ports.Voices.OfProjectAsync(source.ProjectId, cancellationToken);
        var bytes = ports.Exporter.Write(new PackageRequest(source, logins, false, scope.Operation.Now, scope.User.Login, null)
        {
            Comments = said, FlowComments = saidOnFlows, Voices = voices,
        });
        var package = ports.Importer.Read(bytes);
        var target = ProjectCode.Parse(code);
        ProjectCodeRules.EnsureFree(target, await scope.Ports.Projects.CodesAsync(target.Value, cancellationToken));
        var prepared = await ImportPreparation.PrepareAsync(scope, ports.Importer, package, target.Value, true, null, cancellationToken);
        var project = prepared.Project with { Name = chosenName, BaselineProjectId = source.ProjectId };
        var baseline = indicators.On(new StreamSnapshot(scope.Operation.NewId(), project.Id, reference, SnapshotOrigin.Baseline, scope.Operation.Now,
            scope.User.Id, null, null, 1));
        var details = JsonSerializer.Serialize(new { mode = "target", source = source.ProjectId });
        var audit = AuditTrail.Event(scope.Operation, AuditKinds.ProjectImported, scope.User.Id, project.Id, details);
        // The photos of the source, which its package carries since the format 2.3, stay with the source: the target keeps
        // only its reference (ADR 095). Their authors are users of this installation, never created here.
        var kept = prepared.Changes.Where(change => change.After is not StreamSnapshot);
        var changes = prepared.NewUsers.Select(user => new RecordChange(ChangeKind.Created, null, user))
            .Concat(kept.Select(change => change.After is Project ? change with { After = project } : change))
            .Append(new RecordChange(ChangeKind.Created, null, baseline))
            .Append(new RecordChange(ChangeKind.Created, null, audit));
        await scope.Ports.Store.SaveAsync(new ChangeSet([.. changes]), cancellationToken);
        await scope.Ports.Comments.AddAsync(prepared.Comments, cancellationToken);
        await scope.Ports.FlowComments.AddAsync(prepared.FlowComments, cancellationToken);
        await scope.Ports.Voices.AddAsync(prepared.Voices, cancellationToken);
        return new ImportedProject(project.Id, project.Code, prepared.RootMapId, null, [.. prepared.NewUsers.Select(user => user.Id)]);
    }
}

/// <summary>
/// `POST /api/projects/{id}/baseline` (facilitator of the target state, choice of the client of 07/11/2026: the one who
/// leads the workshop): the reference read again on its source, the indicators of the source as the person sees them
/// there, by their role on it. A target without source, or whose source is gone, is VALEUR_INVALIDE on
/// `baseline_project_id`; a source out of the projects of the person is ACCES_REFUSE (an administrator sees every one).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class RefreshBaselineCommand(Guid projectId, string? label) : UserRequest<Guid>
{
    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override bool InUnitOfWork => true;

    public override async Task<Guid> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var target = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.All, cancellationToken) ?? throw DomainException.NotFound();
        var source = target.Project.BaselineProjectId is { } sourceId ? await scope.Ports.Store.LoadAsync(sourceId, WorkspaceContent.All, cancellationToken) : null;
        if (source is null || source.Project.IsInTrash)
            throw DomainException.InvalidValue("baseline_project_id");
        var role = RoleOn(source, scope.User) ?? throw DomainException.Forbidden();
        var indicators = ProjectIndicators.Of(source, new MapViewer(scope.User.Id, role));
        var kept = SnapshotRules.KeepBaseline(target, scope.Operation, label, indicators);
        await scope.Ports.Store.SaveAsync(target.Commit(scope.Operation), cancellationToken);
        return kept;
    }

    /// <summary>The role of the person on the source: an administrator as facilitator (as everywhere), a member by their role, else none.</summary>
    private static MemberRole? RoleOn(ProjectWorkspace source, CurrentUser user) =>
        user.IsAdmin ? MemberRole.Facilitator : source.All<ProjectMember>().FirstOrDefault(member => member.UserId == user.Id)?.Role;
}

/// <summary>Where the source of a target state stands for the person who reads it (P3e).</summary>
public enum BaselineSourceState
{
    /// <summary>Live and among the projects of the person: its code is given.</summary>
    Visible,

    /// <summary>Live, out of the projects of the person: one of its facilitators or an administrator refreshes the reference.</summary>
    Hidden,

    /// <summary>Deleted, in the trash or purged: the reference stays readable and can no longer be refreshed.</summary>
    Missing,
}

/// <summary>The source of a target state as the person sees it, its code when it is visible.</summary>
public sealed record BaselineSource(BaselineSourceState State, string? Code);

/// <summary>
/// `GET /api/projects/{id}/snapshots`, its part on the source of a target state (every member of the target), so that the
/// screen says whether the reference can be refreshed and why not; null for a project that is no target state.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class BaselineSourceQuery(Guid projectId) : UserRequest<BaselineSource?>
{
    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override async Task<BaselineSource?> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var target = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.None, cancellationToken) ?? throw DomainException.NotFound();
        if (target.Project.BaselineProjectId is not { } sourceId)
            return null;
        if (await scope.Ports.Projects.LiveCodeAsync(sourceId, scope.Visibility, cancellationToken) is { } code)
            return new BaselineSource(BaselineSourceState.Visible, code);
        var live = await scope.Ports.Projects.LiveCodeAsync(sourceId, new ProjectVisibility(scope.User.Id, Everything: true), cancellationToken) is not null;
        return new BaselineSource(live ? BaselineSourceState.Hidden : BaselineSourceState.Missing, null);
    }
}
