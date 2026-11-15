// PackageExport.cs
// `POST /api/projects/{id}/export`: the exchange package of a project, for its facilitator, traced in the audit (spec 7, 5.5).

using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Exchange;

/// <summary>The package of a project and the name of its file.</summary>
public sealed record ExportedPackage(string FileName, byte[] Content);

/// <summary>
/// The read of a project for its package: every group of records, in one consistent read (ADR 031). Reserved to the
/// facilitator (AR-113): the package carries the whole project, the drafts of the sheets that only the facilitator and
/// the one who fills may read (spec 5.6-1), and the members; a reader takes a `.pmap` copy.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ExportPackageQuery(Guid projectId) : ProjectQuery<ProjectWorkspace>(AccessTarget.Project(projectId))
{
    public override WorkspaceContent Content => WorkspaceContent.All;

    public override ProjectWorkspace Read(ProjectWorkspace workspace, QueryContext context) => workspace;
}

/// <summary>
/// Exports a project: the workspace read by <see cref="ExportPackageQuery"/>, the logins of the users it names read
/// afterwards (an author who changed his login since gives his login of now) as the comments of presentation of its steps
/// and of its flows (ADR 099) and the voices it keeps (ADR 103), the package written by the interchange
/// and the event <see cref="AuditKinds.ProjectExported"/> written before the bytes are handed out (spec 5.5): no
/// package leaves without its trace.
/// </summary>
public sealed class PackageExport(QueryRunner queries, IPackageExporter exporter, IUserReader users, IProjectReader projects, IAuditStore audit,
    IClock clock, ICurrentUser currentUser, IPresentationCommentStore comments, IFlowPresentationCommentStore flowComments, IPresentationVoiceStore voices)
{
    public async Task<ExportedPackage> RunAsync(Guid projectId, bool withoutPeople, CancellationToken cancellationToken)
    {
        var workspace = await queries.RunAsync(new ExportPackageQuery(projectId), cancellationToken);
        var user = currentUser.User ?? throw new DomainException(ErrorCodes.NotAuthenticated, RefusalKind.Forbidden);
        IReadOnlyDictionary<Guid, string> logins = new Dictionary<Guid, string>();
        if (!withoutPeople)
        {
            var named = exporter.UsersNamedBy(workspace);
            logins = (await users.FindManyAsync([.. named], cancellationToken)).ToDictionary(found => found.Id, found => found.Login);
        }
        // A target state names its source by code (ADR 095): only a live source that the exporter may see.
        var source = workspace.Project.BaselineProjectId is { } sourceId
            ? await projects.LiveCodeAsync(sourceId, new ProjectVisibility(user.Id, user.IsAdmin), cancellationToken)
            : null;
        // The comments of presentation of its steps and of its flows (ADR 099), kept beside the workspace, read afterwards as the logins.
        var said = await PresentationCommentRules.OfLiveStepsAsync(comments, workspace, cancellationToken);
        var saidOnFlows = await FlowPresentationCommentRules.OfLiveFlowsAsync(flowComments, workspace, cancellationToken);
        // The voices it keeps (ADR 103) go with it, with or without the people: a voice is no person.
        var kept = await voices.OfProjectAsync(workspace.ProjectId, cancellationToken);
        var now = clock.UtcNow;
        var content = exporter.Write(new PackageRequest(workspace, logins, withoutPeople, now, user.Login, source)
        {
            Comments = said, FlowComments = saidOnFlows, Voices = kept,
        });
        await audit.AppendAsync(AuditTrail.OfExport(now, user.Id, workspace.ProjectId, withoutPeople), cancellationToken);
        return new ExportedPackage(exporter.FileName(workspace.Project, now), content);
    }
}
