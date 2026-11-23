// PmapExchange.cs
// The .pmap files (spec 7.5, ADR 054): the copy of a project, the opening of a file as a new project, the duplication.

using System.Text.Json;
using System.Text.Json.Serialization;
using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
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
public sealed record PmapCopy(string? FileName, string Content, bool DepthCut);

/// <summary>
/// A project opened from a file or duplicated (reply 201): what the 1.x returned, the sheets written as drafts besides,
/// and how many addresses of its documents reach another machine (web, network path: pre-audit 68, P3-02), said before a
/// link is followed; whether a duplication left sub-maps out beyond the walks of the domain, said by the header of the route, not the body.
/// </summary>
public sealed record PmapOpened(Guid Id, string Code, Guid RootMapId, int Maps, int Lanes, int Steps, int Flows, int Sheets, IReadOnlyList<string> Uncertainties,
    int RemoteLocations, [property: JsonIgnore] bool DepthCut = false);

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
/// the user sees it (`SheetRights.ViewerOf`: never a draft for a reader, spec 5.6-1), the comments of presentation of its
/// live steps and flows read beside it (1.5, ADR 105), written, its event of export written before the text is handed out
/// (spec 5.5), like the package.
/// </summary>
public sealed class PmapCopyExport(QueryRunner queries, IPmapFormat format, IAuditStore audit, IClock clock, IPresentationCommentStore comments,
    IFlowPresentationCommentStore flowComments)
{
    public async Task<PmapCopy> RunAsync(Guid projectId, bool withSheets, CancellationToken cancellationToken)
    {
        var (workspace, role, userId) = await queries.RunAsync(new PmapCopyQuery(projectId, withSheets), cancellationToken);
        Func<Step, SheetViewer>? viewerOf = withSheets ? step => SheetRights.ViewerOf(workspace, step, role, userId) : null;
        var extras = await PmapExtrasReading.OfProjectAsync(comments, flowComments, workspace, cancellationToken);
        var outline = format.Outline(workspace, viewerOf, extras);
        var now = clock.UtcNow;
        var content = format.Write(outline.Document, ProductIdentity.Name, now);
        await audit.AppendAsync(AuditTrail.OfPmapCopy(now, userId, workspace.ProjectId, withSheets), cancellationToken);
        return new PmapCopy(format.FileName(workspace.Project, now), content, outline.DepthCut);
    }
}

/// <summary>`GET /api/maps/{id}/pmap` (reader, plan 3.1, L7): the project of the map read whole, with the role of the user.</summary>
[RequiresRole(MemberRole.Reader)]
public sealed class MapPmapQuery(Guid mapId) : ProjectQuery<(ProjectWorkspace Workspace, MemberRole Role, Guid UserId)>(AccessTarget.Map(mapId))
{
    public override WorkspaceContent Content => WorkspaceContent.All;

    public override (ProjectWorkspace Workspace, MemberRole Role, Guid UserId) Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (workspace, context.Role, context.UserId);
    }
}

/// <summary>
/// The copy of one map and of the maps detailed below it, which the image of the map carries when the person asks for it
/// (plan 3.1, L7): the branch outlined by the format with the routes of its flows, each sheet as the user sees it
/// (`SheetRights.ViewerOf`), the comments of presentation of its steps and flows (1.5, ADR 105), its event of export
/// written before the text is handed out, as the copy of a project. Read for the project as PDF (ADR 113), the same
/// branch and the same viewer, described for the print: no place of a document, no comment read nor handed, no file
/// named, its event with the format `pdf`. INTROUVABLE for a map deleted.
/// </summary>
public sealed class MapPmapExport(QueryRunner queries, IPmapFormat format, IAuditStore audit, IClock clock, IPresentationCommentStore comments,
    IFlowPresentationCommentStore flowComments)
{
    public async Task<PmapCopy> RunAsync(Guid mapId, MapCopyPurpose purpose, CancellationToken cancellationToken)
    {
        var (workspace, role, userId) = await queries.RunAsync(new MapPmapQuery(mapId), cancellationToken);
        Func<Step, SheetViewer> viewerOf = step => SheetRights.ViewerOf(workspace, step, role, userId);
        var now = clock.UtcNow;
        // The view in BPMN 2.0 is a consultation, as the map itself: no event (ADR 106), so it reads no more than the map
        // shows, the map alone without the places of its documents, and names no file to keep (audit of the copy 64, P2-03).
        if (purpose == MapCopyPurpose.View)
            return new PmapCopy(null, format.Write(format.OutlineView(workspace, mapId, viewerOf).Document, ProductIdentity.Name, now), DepthCut: false);
        var print = purpose == MapCopyPurpose.Pdf;
        var outline = print ? format.OutlinePrint(workspace, mapId, viewerOf)
            : format.OutlineBranch(workspace, mapId, viewerOf, await PmapExtrasReading.OfBranchAsync(comments, flowComments, workspace, mapId, cancellationToken));
        var content = format.Write(outline.Document, ProductIdentity.Name, now);
        // A copy, a .bpmn or the sheets of a PDF leave their trace, before the text leaves the host.
        await audit.AppendAsync(AuditTrail.OfMapPmapCopy(now, userId, workspace.ProjectId, mapId, FormatOf(purpose)), cancellationToken);
        return new PmapCopy(print ? null : format.FileName(workspace.Project, now), content, outline.DepthCut);
    }

    /// <summary>The format the event of an export names: the file the interface writes from the copy, or the copy itself.</summary>
    private static string FormatOf(MapCopyPurpose purpose) => purpose switch
    {
        MapCopyPurpose.Bpmn => "bpmn",
        MapCopyPurpose.Pdf => "pdf",
        _ => "pmap",
    };
}

/// <summary>
/// What the copy of a map is read for (ADR 106): a copy of the file (an image carries it), the export of a .bpmn that the
/// interface writes from it, the sheets of the project as PDF (ADR 113), each an event of export with its format, or the
/// view in BPMN 2.0, a consultation without event.
/// </summary>
public enum MapCopyPurpose { Copy, Bpmn, View, Pdf }

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

    /// <summary>
    /// The text of a file to read, empty when absent; FICHIER_TROP_GROS beyond <see cref="ContentMax"/> characters, before
    /// any reading: the bound of the opening, of the insertion of a file (F-208) and of the preview of a collection (ADR 114).
    /// </summary>
    public static string Bounded(string? content)
    {
        var text = content ?? string.Empty;
        if (text.Length > ContentMax)
            throw new DomainException(ErrorCodes.FileTooLarge, RefusalKind.Invalid, new Dictionary<string, object?> { ["max"] = ContentMax / (1024 * 1024) });
        return text;
    }

    public override bool InUnitOfWork => true;

    public override async Task<PmapOpened> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var text = Bounded(content);
        // A proposal of the assistant opened as a new project (ADR 082) never sets a route (ADR 091). Every letter of the RACI
        // opens, whatever the setting, which says what a person types (T-FICR-09 revised on 16/11/2026): a copy opens again as it was.
        var document = originMapId is null ? format.Read(text, scope.Languages, RaciRules.KnownLetters)
            : format.ReadProposal(text, scope.Languages, RaciRules.KnownLetters);
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
/// of the format do not apply), its translations of steps and flows kept (spec 7.5), its comments of presentation and
/// its glossary kept (1.5, ADR 105), then the path of the opening under the code and the name of the request; its event
/// of audit names the source (S-5.5-03).
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
        var extras = await PmapExtrasReading.OfProjectAsync(scope.Ports.Comments, scope.Ports.FlowComments, source, cancellationToken);
        var model = format.Model(source, extras);
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
        await WriteCommentsAsync(scope, built, cancellationToken);
        return new PmapOpened(built.Project.Id, built.Project.Code, built.RootMapId, built.Maps, built.Lanes, built.Steps, built.Flows, built.Sheets,
            document.Uncertainties, document.RemoteLocations);
    }

    /// <summary>
    /// The comments of presentation of the new steps and flows (1.5, ADR 105), written beside the core after them in the same
    /// unit, by the rule of every import (`CommentLineRules.Imported`): the line in the language of origin of its step (of
    /// the source step for a flow) is the original.
    /// </summary>
    private static async Task WriteCommentsAsync(UserScope scope, PmapProjectBuild built, CancellationToken cancellationToken)
    {
        var records = built.Changes.Changes.Select(change => change.After).ToList();
        var origins = records.OfType<Step>().ToDictionary(step => step.Id, step => step.SourceLanguage);
        var comments = PresentationCommentRules.Imported(built.Comments, origins, scope.Languages);
        var flowComments = FlowPresentationCommentRules.Imported(built.FlowComments,
            FlowPresentationCommentRules.Origins(records.OfType<Flow>(), origins), scope.Languages);
        if (comments.Count > 0)
            await scope.Ports.Comments.AddAsync(comments, cancellationToken);
        if (flowComments.Count > 0)
            await scope.Ports.FlowComments.AddAsync(flowComments, cancellationToken);
    }
}

/// <summary>
/// The comments of presentation a whole copy carries (1.5, ADR 105), read beside the workspace as the package reads them:
/// those of the live steps and flows of the project, or of a map and of every level below it.
/// </summary>
internal static class PmapExtrasReading
{
    public static async Task<PmapExtras> OfProjectAsync(IPresentationCommentStore comments, IFlowPresentationCommentStore flowComments,
        ProjectWorkspace workspace, CancellationToken cancellationToken) =>
        new(await PresentationCommentRules.OfLiveStepsAsync(comments, workspace, cancellationToken),
            await FlowPresentationCommentRules.OfLiveFlowsAsync(flowComments, workspace, cancellationToken));

    public static async Task<PmapExtras> OfBranchAsync(IPresentationCommentStore comments, IFlowPresentationCommentStore flowComments,
        ProjectWorkspace workspace, Guid mapId, CancellationToken cancellationToken)
    {
        var steps = PresentationCommentRules.TreeSteps(workspace, mapId).Select(step => step.Id).ToList();
        var flows = FlowPresentationCommentRules.TreeFlows(workspace, mapId).Select(flow => flow.Id).ToList();
        return new(steps.Count == 0 ? [] : await comments.OfStepsAsync(steps, cancellationToken),
            flows.Count == 0 ? [] : await flowComments.OfFlowsAsync(flows, cancellationToken));
    }
}
