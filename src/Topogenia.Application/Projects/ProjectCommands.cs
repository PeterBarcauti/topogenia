// ProjectCommands.cs
// The projects: creation by an administrator, list, detail and settings (annex A 1 `/api/projets`, A 2.1, spec 10).

using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Members;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Queries;
using Topogenia.Application.Translations;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Projects;

/// <summary>A project just created: what the reply 201 and its `Location` name, and the users invited with it.</summary>
public sealed record CreatedProject(Guid Id, string Code, Guid RootMapId, IReadOnlyList<Guid> InvitedUserIds);

/// <summary>
/// A live project of the home page (1.x `ProjetResume`): the role is the effective one, facilitator for an administrator;
/// its classification, shown on its card (spec 4.1).
/// </summary>
public sealed record ProjectListItem(
    Guid Id,
    string Code,
    string Name,
    string SourceLanguage,
    IReadOnlyDictionary<string, string> NameTranslations,
    string? Description,
    Classification Classification,
    Guid RootMapId,
    MemberRole Role,
    int StepCount,
    int MapCount);

/// <summary>The detail of a project (1.x `ProjetDetail`), with its members in display order and its version.</summary>
public sealed record ProjectDetail(
    Guid Id,
    string Code,
    string Name,
    string SourceLanguage,
    IReadOnlyDictionary<string, string> NameTranslations,
    string? Description,
    string? SharedFolder,
    Classification Classification,
    string? SharingGroupLabel,
    string? TranslationGlossary,
    Guid RootMapId,
    MemberRole Role,
    IReadOnlyList<MemberView> Members,
    int Version)
{
    /// <summary>The working day, in hours (spec 14.2, S6).</summary>
    public decimal WorkdayHours { get; init; } = WorkTime.DefaultWorkdayHours;

    /// <summary>The hours of a full-time equivalent in a month (spec 14.2, S6).</summary>
    public decimal FteHoursPerMonth { get; init; } = WorkTime.DefaultFteHoursPerMonth;
}

/// <summary>
/// `POST /api/projects` (1.x `POST /api/projets`, admin): `ProjectCreation.Create` on the codes in use, live or in the
/// trash (A-2.1-02, 03), and on the people of the members, read in the unit on each of its attempts (ADR 033); the
/// language of origin defaults to the effective language of the creator (A-2.1-06). The project, its root map, its first
/// lane, its counters, its members and the invited users are written in one unit of work (A-2.1-22); two concurrent
/// creations of one code meet on its unique index and only one succeeds (A-2.1-04). Its audit event is written with it
/// (spec 5.5).
/// </summary>
[RequiresAdmin]
public sealed class CreateProjectCommand(CreateProjectRequest request) : UserRequest<CreatedProject>
{
    public override bool InUnitOfWork => true;

    public override async Task<CreatedProject> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var codes = await scope.Ports.Projects.CodesAsync(request.Code?.Trim() ?? string.Empty, cancellationToken);
        var people = await PeopleSource.Of(scope.Ports).LookupAsync(request.Members ?? [], cancellationToken);
        var created = ProjectCreation.Create(scope.Operation, request, new ProjectCreationInputs(codes, scope.Languages, scope.User.Language, people));
        var audit = AuditTrail.Event(scope.Operation, AuditKinds.ProjectCreated, scope.User.Id, created.Project.Id);
        var records = created.NewUsers.Cast<IRecord>().Concat(created.Records).Append(audit);
        await scope.Ports.Store.SaveAsync(new ChangeSet([.. records.Select(record => new RecordChange(ChangeKind.Created, null, record))]), cancellationToken);
        return new CreatedProject(created.Project.Id, created.Project.Code, created.RootMap.Id, created.NewUsers.Select(user => user.Id).ToList());
    }
}

/// <summary>
/// `GET /api/projects` (1.x `GET /api/projets`, authenticated): the live projects of which the user is a member, every
/// one for an administrator, sorted by code, with their live steps and maps (A-1.2-07).
/// </summary>
[RequiresAuthenticated]
public sealed class ListProjectsQuery : UserRequest<IReadOnlyList<ProjectListItem>>
{
    public override async Task<IReadOnlyList<ProjectListItem>> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var rows = await scope.Ports.Projects.LiveProjectsAsync(scope.Visibility, cancellationToken);
        return rows.Select(row => Item(row, AccessPolicy.EffectiveRole(scope.User, new ProjectAccess(row.Project.Id, false, row.Role), false)!.Value))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static ProjectListItem Item(ProjectListRow row, MemberRole role) =>
        new(row.Project.Id, row.Project.Code, row.Project.Name, row.Project.SourceLanguage, row.NameTranslations, row.Project.Description,
            row.Project.Classification, row.RootMapId, role, row.StepCount, row.MapCount);
}

/// <summary>`GET /api/projects/{id}` (1.x `GET /api/projets/{id}`, Lecteur): members, version, root map (A-1.2-12).</summary>
[RequiresRole(MemberRole.Reader)]
public sealed class GetProjectQuery(Guid projectId) : ProjectQuery<ProjectDetail>(AccessTarget.Project(projectId))
{
    public override WorkspaceContent Content => WorkspaceContent.Members | WorkspaceContent.Translations;

    public override ProjectDetail Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(context);
        var project = workspace.Project;
        var names = TranslationRules.Of(workspace, TranslationObject.Project, project.Id)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.Ordinal);
        return new ProjectDetail(project.Id, project.Code, project.Name, project.SourceLanguage, names, project.Description, project.SharedFolder,
            project.Classification, project.SharingGroupLabel, project.TranslationGlossary, workspace.RootMap.Id, context.Role, MemberViews.Of(workspace),
            project.Version)
        {
            WorkdayHours = project.WorkdayHours,
            FteHoursPerMonth = project.FteHoursPerMonth,
        };
    }
}

/// <summary>
/// The label of the group with which the documents of the project are shared (spec 10, AR-64: a setting of the project,
/// the 1.x wrote a fixed label in the interface), with If-Match on the project (D06). Animateur, like every change of the
/// project; it touches no map and cannot be undone. The spec names no route: the host gives it one (P4-T04).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ChangeSharingGroupLabelCommand(Guid projectId, string? label) : ProjectCommand<Guid>(AccessTarget.Project(projectId))
{
    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context) => ProjectSettings.ChangeSharingGroupLabel(workspace, label).Id;

    /// <summary>The If-Match of the request on the project.</summary>
    public static ChangeSharingGroupLabelCommand WithVersion(Guid projectId, string? label, int? version) =>
        new(projectId, label) { Versions = version is { } read ? [VersionCheck.Of<Project>(Project.KeyOf(projectId), read)] : [] };
}

/// <summary>
/// The work time of the value stream (3.0, ADR 085, spec 14.2, S6): the working day and the hours of a full-time equivalent
/// in a month, with If-Match on the project (D06). Animateur, like every change of the project; it touches no map and cannot
/// be undone. An absent value keeps the stored one.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ChangeWorkTimeCommand(Guid projectId, decimal? workdayHours, decimal? fteHoursPerMonth) : ProjectCommand<Guid>(AccessTarget.Project(projectId))
{
    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context) =>
        ProjectSettings.ChangeWorkTime(workspace, workdayHours, fteHoursPerMonth).Id;

    /// <summary>The If-Match of the request on the project.</summary>
    public static ChangeWorkTimeCommand WithVersion(Guid projectId, decimal? workdayHours, decimal? fteHoursPerMonth, int? version) =>
        new(projectId, workdayHours, fteHoursPerMonth) { Versions = version is { } read ? [VersionCheck.Of<Project>(Project.KeyOf(projectId), read)] : [] };
}

/// <summary>
/// The classification of the project (spec 4.1, Q5: internal, confidential, strictly confidential), chosen at its creation
/// and changed afterwards by its facilitator, with If-Match on the project (D06), like the label of the sharing group. It
/// touches no map and cannot be undone; the audit keeps the change with both levels (spec 5.1: it governs how the
/// exports are handled). The spec names no route: the host gives it one (gate P6).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ChangeClassificationCommand(Guid projectId, string? classification) : ProjectCommand<Guid>(AccessTarget.Project(projectId))
{
    public override string? AuditKind => AuditKinds.ClassificationChanged;

    public override string? AuditDetails(ChangeSet changes) => AuditTrail.ClassificationDetails(changes);

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context) => ProjectSettings.ChangeClassification(workspace, classification).Id;

    /// <summary>The If-Match of the request on the project.</summary>
    public static ChangeClassificationCommand WithVersion(Guid projectId, string? classification, int? version) =>
        new(projectId, classification) { Versions = version is { } read ? [VersionCheck.Of<Project>(Project.KeyOf(projectId), read)] : [] };
}

/// <summary>
/// The glossary of the translations, written into the instruction sheet of the workbook (spec 10, annex B 5, ADR 065: a
/// setting of the project, the 1.x wrote a finance glossary in the code), with If-Match on the project (D06). Animateur,
/// as the workbook itself; it touches no map and cannot be undone. The spec names no route: the host gives it one (P6-T08).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ChangeTranslationGlossaryCommand(Guid projectId, string? glossary) : ProjectCommand<Guid>(AccessTarget.Project(projectId))
{
    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override Guid Execute(ProjectWorkspace workspace, OperationContext context) => ProjectSettings.ChangeTranslationGlossary(workspace, glossary).Id;

    /// <summary>The If-Match of the request on the project.</summary>
    public static ChangeTranslationGlossaryCommand WithVersion(Guid projectId, string? glossary, int? version) =>
        new(projectId, glossary) { Versions = version is { } read ? [VersionCheck.Of<Project>(Project.KeyOf(projectId), read)] : [] };
}

/// <summary>
/// A text that lacks its translation into a language asked as the language of origin, as a refusal names it: the code of
/// its type (one of the objects the closed core translates, or a comment of presentation, of a step or of a flow), its
/// identifier (the step or the flow of a comment) and its readable code.
/// </summary>
public sealed record MissingText(string Type, Guid Id, string Code);

/// <summary>
/// `PUT /api/projects/{id}/origin-language` (3.2, ADR 100): a language into which every live text of the project is
/// translated becomes its language of origin, each text exchanged with its translation
/// (<see cref="OriginLanguage.Change"/>); TRADUCTION_INCOMPLETE while a text lacks. Animateur, with If-Match on the
/// project (D06). It names no map and is no operation of a map: it cannot be undone by Ctrl+Z, the same change towards
/// the former language gives the texts back; the audit keeps it with both languages. The client reads the maps again.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ChangeOriginLanguageCommand(Guid projectId, string? language, LanguageSettings languages)
    : ProjectCommand<OriginLanguageChange>(AccessTarget.Project(projectId))
{
    /// <summary>
    /// The translations of every text, declared here since it renders no view of a map (audit 3.2.0): the structure and
    /// the summaries are always loaded, the comments of presentation are read beside.
    /// </summary>
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override string? AuditKind => AuditKinds.OriginLanguageChanged;

    public override string? AuditDetails(ChangeSet changes) => AuditTrail.OriginLanguageDetails(changes);

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    /// <summary>
    /// The operations of before the change are forgotten: most would be refused (the labels they knew changed), and the
    /// others (a labelled flow taken back from the trash) would bring back a text of the former language as an original.
    /// </summary>
    public override bool ForgetsOperations => true;

    /// <summary>
    /// The comments of presentation are texts of the project like the others (ADR 099 revised), those of the steps and
    /// those of the flows (lot J10): one that has an original and no line in the language asked lacks its translation,
    /// and the change would leave it without an original. They are kept beside the closed core, so they are refused
    /// here, with the texts of the core that lack and after them, the comments of the steps before those of the flows,
    /// before the core is asked: TRADUCTION_INCOMPLETE, as the core says it. An invalid language, or the language of
    /// origin itself, is left to the core and its own refusals.
    /// </summary>
    public override async Task ReadBesideAsync(ProjectWorkspace workspace, BesidePorts beside, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(beside);
        var to = languages.Require(language, OriginLanguage.LanguageField);
        if (!LocalizedText.TakesTranslationIn(workspace.Project.SourceLanguage, to))
            return;
        var lines = await PresentationCommentRules.OfLiveStepsAsync(beside.Comments, workspace, cancellationToken);
        var onFlows = await FlowPresentationCommentRules.OfLiveFlowsAsync(beside.FlowComments, workspace, cancellationToken);
        var comments = CommentWorkbook.List(workspace, lines, TranslationScope.Missing, [to])
            .Select(comment => new MissingText(CommentWorkbook.RowType, comment.StepId, comment.Code))
            .Concat(FlowCommentWorkbook.List(workspace, onFlows, TranslationScope.Missing, [to])
                .Select(comment => new MissingText(FlowCommentWorkbook.RowType, comment.FlowId, comment.Code)))
            .ToList();
        if (comments.Count == 0)
            return;
        var texts = OriginLanguage.Missing(workspace, to);
        var missing = texts.Select(text => new MissingText(Codes.Of(text.Type), text.Id, text.Code)).Concat(comments);
        throw DomainException.Conflict(ErrorCodes.IncompleteTranslation, new Dictionary<string, object?>
        {
            [OriginLanguage.LanguageField] = to, ["count"] = texts.Count + comments.Count, ["missing"] = missing.Take(OriginLanguage.MissingMax).ToList(),
        });
    }

    public override OriginLanguageChange Execute(ProjectWorkspace workspace, OperationContext context) =>
        OriginLanguage.Change(workspace, context, language, languages);

    /// <summary>The If-Match of the request on the project.</summary>
    public static ChangeOriginLanguageCommand WithVersion(Guid projectId, string? language, LanguageSettings languages, int? version) =>
        new(projectId, language, languages) { Versions = version is { } read ? [VersionCheck.Of<Project>(Project.KeyOf(projectId), read)] : [] };
}
