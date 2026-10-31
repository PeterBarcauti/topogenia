// PmapExchange.cs
// The .pmap files (spec 7.5, ADR 054): the copy of a project, the opening of a file as a new project, the duplication.

using System.Text.Json;
using System.Text.Json.Serialization;
using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Exchange;

/// <summary>The copy of a project: the name of its file, its text, and whether sub-maps beyond 12 levels were left out.</summary>
public sealed record PmapCopy(string FileName, string Content, bool DepthCut);

/// <summary>
/// A project opened from a file or duplicated (reply 201): what the 1.x returned, the sheets written as drafts besides;
/// whether a duplication left sub-maps out beyond the walks of the domain, said by the header of the route, not the body.
/// </summary>
public sealed record PmapOpened(Guid Id, string Code, Guid RootMapId, int Maps, int Lanes, int Steps, int Flows, int Sheets, IReadOnlyList<string> Uncertainties,
    [property: JsonIgnore] bool DepthCut = false);

/// <summary>
/// `GET /api/projects/{id}/pmap` (reader, A-1.2-15): the structure and the translations and, on option, the sheets, the
/// documents and the uses, read with the role of the user, from which the outline knows which sheet he sees.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class PmapCopyQuery(Guid projectId, bool withSheets) : ProjectQuery<(ProjectWorkspace Workspace, MemberRole Role, Guid UserId)>(AccessTarget.Project(projectId))
{
    public override WorkspaceContent Content => withSheets ? WorkspaceContent.All : WorkspaceContent.Translations;

    public override (ProjectWorkspace Workspace, MemberRole Role, Guid UserId) Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (workspace, context.Role, context.UserId);
    }
}

/// <summary>
/// The copy of a project (the "download a copy" of the interface, A-2.17-43 to 49): the document outlined by the format, each sheet as
/// the user sees it (`SheetRights.ViewerOf`: never a draft for a reader, spec 5.6-1), written, its event of export
/// written before the text is handed out (spec 5.5), like the package.
/// </summary>
public sealed class PmapCopyExport(QueryRunner queries, IPmapFormat format, IAuditStore audit, IClock clock)
{
    public async Task<PmapCopy> RunAsync(Guid projectId, bool withSheets, CancellationToken cancellationToken)
    {
        var (workspace, role, userId) = await queries.RunAsync(new PmapCopyQuery(projectId, withSheets), cancellationToken);
        Func<Step, SheetViewer>? viewerOf = withSheets ? step => SheetRights.ViewerOf(workspace, step, role, userId) : null;
        var outline = format.Outline(workspace, viewerOf);
        var now = clock.UtcNow;
        var content = format.Write(outline.Document, ProductIdentity.Name, now);
        await audit.AppendAsync(AuditTrail.OfPmapCopy(now, userId, workspace.ProjectId, withSheets), cancellationToken);
        return new PmapCopy(format.FileName(workspace.Project, now), content, outline.DepthCut);
    }
}

/// <summary>
/// `POST /api/projects/import-pmap` (administrator, A-1.2-17 to 20): a file read whole before anything is written, its
/// faults listed (FICHIER_INVALIDE), then a new project under the code and the name of the request, those of the file
/// otherwise (A-2.17-38), built by the format and written in one unit with its event of audit (`{"mode":"pmap"}`). A
/// proposal of the assistant names its map of origin (ADR 082): that map is read too, and its steps named by the proposal
/// carry their sheets and translations into the new project (`IPmapFormat.Carry`), read as the facilitator the
/// administrator acts as sees them (spec 3.0 section 3.3); the map of origin is not changed.
/// </summary>
[RequiresAdmin]
public sealed class OpenPmapCommand(IPmapFormat format, string? content, string? code, string? name, Guid? originMapId = null) : UserRequest<PmapOpened>
{
    /// <summary>The longest content opened, in characters (the 1.x `ServiceFichiers.ContenuMax`: 5 000 steps fit in less than 2 MB).</summary>
    public const int ContentMax = 8 * 1024 * 1024;

    public override AccessTarget? Target => originMapId is { } map ? AccessTarget.Map(map) : null;

    public override bool InUnitOfWork => true;

    public override async Task<PmapOpened> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var text = content ?? string.Empty;
        if (text.Length > ContentMax)
            throw new DomainException(ErrorCodes.FileTooLarge, RefusalKind.Invalid, new Dictionary<string, object?> { ["max"] = ContentMax / (1024 * 1024) });
        var document = format.Read(text, scope.Languages, RaciRules.Letters(scope.Settings));
        if (originMapId is { } map)
        {
            var origin = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.All, cancellationToken) ?? throw DomainException.NotFound();
            document = format.Carry(document, origin, map, step => SheetRights.ViewerOf(origin, step, MemberRole.Facilitator, scope.User.Id));
        }
        var details = originMapId is { } source ? JsonSerializer.Serialize(new { mode = "pmap", origin = source }) : JsonSerializer.Serialize(new { mode = "pmap" });
        return await PmapCreation.WriteAsync(scope, format, document, code, name, AuditKinds.ProjectImported, details, cancellationToken);
    }
}

/// <summary>
/// `POST /api/projects/{id}/duplicate` (administrator, the source read: A-1.2-21, 22): the document of the source outlined
/// in memory without its sheets (AR-10), every level of its sub-maps kept (AR-118: no file is written, the 12 levels
/// of the format do not apply), its translations of steps and flows kept (spec 7.5), then the path of the opening under
/// the code and the name of the request; its event of audit names the source (S-5.5-03).
/// </summary>
[RequiresAdmin]
public sealed class DuplicateProjectCommand(IPmapFormat format, Guid projectId, string? code, string? name) : UserRequest<PmapOpened>
{
    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override bool InUnitOfWork => true;

    public override async Task<PmapOpened> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var source = await scope.Ports.Store.LoadAsync(scope.Project, WorkspaceContent.Translations, cancellationToken) ?? throw DomainException.NotFound();
        var model = format.Model(source);
        var details = JsonSerializer.Serialize(new { source = source.ProjectId });
        var duplicated = await PmapCreation.WriteAsync(scope, format, model.Document, code, name, AuditKinds.ProjectDuplicated, details, cancellationToken);
        return duplicated with { DepthCut = model.DepthCut };
    }
}

/// <summary>What the opening and the duplication share: the code and the name chosen, the project built and written with its event.</summary>
internal static class PmapCreation
{
    public static async Task<PmapOpened> WriteAsync(UserScope scope, IPmapFormat format, IPmapDocument document, string? code, string? name,
        string auditKind, string details, CancellationToken cancellationToken)
    {
        var chosenCode = string.IsNullOrWhiteSpace(code) ? document.ProjectCode : code.Trim();
        // Trimmed before the format compares it with the name of the file, whose translations it keeps when equal (1.x `ServiceFichiers.cs:172`).
        var chosenName = string.IsNullOrWhiteSpace(name) ? document.ProjectName : name.Trim();
        var codes = await scope.Ports.Projects.CodesAsync(chosenCode ?? string.Empty, cancellationToken);
        var inputs = new ProjectCreationInputs(codes, scope.Languages, scope.User.Language,
            new PeopleLookup([], new Dictionary<string, DirectoryEntry>(), scope.Ports.Administrators.Logins));
        var built = format.Build(scope.Operation, document, chosenCode ?? string.Empty, chosenName, inputs);
        var audit = AuditTrail.Event(scope.Operation, auditKind, scope.User.Id, built.Project.Id, details);
        await scope.Ports.Store.SaveAsync(new ChangeSet([new RecordChange(ChangeKind.Created, null, built.Project), .. built.Changes.Changes,
            new RecordChange(ChangeKind.Created, null, audit)]), cancellationToken);
        return new PmapOpened(built.Project.Id, built.Project.Code, built.RootMapId, built.Maps, built.Lanes, built.Steps, built.Flows, built.Sheets,
            document.Uncertainties);
    }
}
