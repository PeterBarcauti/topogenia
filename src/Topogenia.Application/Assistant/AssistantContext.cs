// AssistantContext.cs
// What the model learns of a project for an exchange of the panel: the schema of the project, the map open as a .pmap, the codes selected, the objects it may name (spec 3.0 sections 3.1, 7, decisions of 29/10/2026).

using System.Text;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Application.Queries;
using Topogenia.Application.Snapshots;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.ValueStreams;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Assistant;

/// <summary>
/// The context of an exchange on a map: the project, its language of origin and its classification, the map and whether it is the root (the scope
/// is then the whole project) or empty, the role of the person; with the texts, the schema of the project in its format,
/// the map open as a `.pmap` with the sheets as the person sees them, the codes selected found on the map; the objects of
/// the project by their code, which a reply may name.
/// </summary>
public sealed record AssistantContext(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string ProjectLanguage,
    Classification Classification,
    Guid MapId,
    string MapName,
    bool IsRoot,
    bool EmptyMap,
    MemberRole Role,
    string? Schema,
    AssistantSchemaFormat SchemaFormat,
    string? OpenMap,
    IReadOnlyList<string> Selection,
    IReadOnlyDictionary<string, AssistantObjectView> Objects)
{
    public AssistantScope Scope => IsRoot ? AssistantScope.Project : AssistantScope.Map;

    /// <summary>The pain points of the map as its sheets show them, with the texts only (the report, plan 3.1, L4).</summary>
    public IReadOnlyList<AssistantPainPointView> PainPoints { get; init; } = [];

    /// <summary>Every live map of the project with its summary today, with the texts only (practice `summaries`, plan 3.1, lot R).</summary>
    public IReadOnlyList<AssistantSummaryTarget> SummaryTargets { get; init; } = [];

    /// <summary>The indicators of the project now and its photos compared with now, as the person sees them, with the texts only (F-109).</summary>
    public AssistantIndicators? Indicators { get; init; }

    /// <summary>
    /// The lines of the comments of presentation of the steps of the map open and of the levels below it, read beside
    /// the workspace for a translation only (ADR 099 revised); empty otherwise.
    /// </summary>
    public IReadOnlyList<PresentationComment> TreeComments { get; init; } = [];

    /// <summary>The live steps of the map open and of the levels below it, in reading order, for a translation only; empty otherwise.</summary>
    public IReadOnlyList<Step> TreeSteps { get; init; } = [];

    /// <summary>The lines of the comments of presentation of the flows of the same maps, read beside the workspace for a translation only (ADR 099, lot J10); empty otherwise.</summary>
    public IReadOnlyList<FlowPresentationComment> TreeFlowComments { get; init; } = [];

    /// <summary>The live flows of the map open and of the levels below it, in reading order, for a translation only; empty otherwise.</summary>
    public IReadOnlyList<FlowEnds> TreeFlows { get; init; } = [];

    /// <summary>A proposal changes the structure: the facilitator of the project, the administrator acting as one (spec 3.0 section 3.3, revision 4).</summary>
    public bool CanPropose => Role == MemberRole.Facilitator;
}

/// <summary>
/// The reading of the context of an exchange (any member: a reader asks and analyses, spec 3.0 section 3.3, revision 4).
/// Without the texts it reads the structure only, for the state of the panel. The schema is the package without the people
/// (and without its translations) for the facilitator, who may export it, the `.pmap` of the whole project otherwise, each
/// sheet as the person sees it; both written the same way for the same state, so that the cache of the provider holds.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class AssistantContextQuery(Guid mapId, IPmapFormat format, IPackageExporter packages, IReadOnlyList<string> selection, bool withTexts)
    : ProjectQuery<AssistantContext>(AccessTarget.Map(mapId))
{
    /// <summary>The map open given with every level below it and without its sheets: what a translation returns translated (ADR 083).</summary>
    public bool WholeTree { get; init; }

    private IReadOnlyList<Step> treeSteps = [];
    private IReadOnlyList<PresentationComment> treeComments = [];
    private IReadOnlyList<FlowEnds> treeFlows = [];
    private IReadOnlyList<FlowPresentationComment> treeFlowComments = [];

    public override WorkspaceContent Content => withTexts ? WorkspaceContent.All : WorkspaceContent.None;

    /// <summary>For a translation: the comments of presentation of the steps and of the flows of the tree, which the model translates with the map (ADR 099 revised, lot J10).</summary>
    public override async Task ReadBesideAsync(ProjectWorkspace workspace, BesidePorts beside, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(beside);
        (treeSteps, treeComments, treeFlows, treeFlowComments) = ([], [], [], []);
        if (!WholeTree || !withTexts)
            return;
        treeSteps = PresentationCommentRules.TreeSteps(workspace, mapId);
        treeComments = treeSteps.Count == 0 ? [] : await beside.Comments.OfStepsAsync([.. treeSteps.Select(step => step.Id)], cancellationToken);
        treeFlows = FlowPresentationCommentRules.TreeFlows(workspace, mapId);
        treeFlowComments = treeFlows.Count == 0 ? [] : await beside.FlowComments.OfFlowsAsync([.. treeFlows.Select(flow => flow.Id)], cancellationToken);
    }

    public override AssistantContext Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(context);
        var map = workspace.LiveMap(mapId);
        var project = workspace.Project;
        var objects = Objects(workspace);
        string? schema = null;
        string? openMap = null;
        var schemaFormat = AssistantSchemaFormat.None;
        IReadOnlyList<string> selected = [];
        if (withTexts)
        {
            Func<Step, SheetViewer> viewerOf = step => SheetRights.ViewerOf(workspace, step, context.Role, context.UserId);
            // The stamp of the files written for the model is the creation of the project: the same text for the same
            // state, which the cache of the provider needs (a time of the call would change it at each exchange).
            var stamp = project.CreatedAt;
            if (context.Role == MemberRole.Facilitator)
            {
                schemaFormat = AssistantSchemaFormat.Package;
                schema = Package(packages.Describe(workspace));
            }
            else
            {
                schemaFormat = AssistantSchemaFormat.Pmap;
                schema = format.Write(format.Outline(workspace, viewerOf).Document, ProductIdentity.Name, stamp);
            }
            var outline = WholeTree ? format.OutlineTree(workspace, map.Id) : format.OutlineMap(workspace, map.Id, viewerOf);
            openMap = format.Write(outline.Document, ProductIdentity.Name, stamp);
            var onMap = workspace.StepsOf(map.Id).Select(step => step.Code).ToHashSet(StringComparer.Ordinal);
            selected = selection.Where(onMap.Contains).ToList();
        }
        return new AssistantContext(project.Id, project.Code, project.Name, project.SourceLanguage, project.Classification, map.Id, map.Name, map.IsRoot,
            workspace.IsEmptyMap(map.Id), context.Role, schema, schemaFormat, openMap, selected, objects)
        {
            PainPoints = withTexts ? AssistantPainPoints.Of(workspace, map.Id) : [],
            SummaryTargets = withTexts ? AssistantSummaryTargets.Of(workspace) : [],
            Indicators = withTexts ? AssistantIndicators.Of(workspace, context.Viewer) : null,
            TreeSteps = treeSteps,
            TreeComments = treeComments,
            TreeFlows = treeFlows,
            TreeFlowComments = treeFlowComments,
        };
    }

    /// <summary>The entries of the package, each under a line that names it, in the order of the archive.</summary>
    private static string Package(IReadOnlyList<PackageText> entries)
    {
        var text = new StringBuilder();
        foreach (var entry in entries)
            text.Append("=== ").Append(entry.Path).Append(" ===\n").Append(entry.Content).Append('\n');
        return text.ToString();
    }

    /// <summary>The live steps and lanes of the project by their code, on the live maps.</summary>
    private static Dictionary<string, AssistantObjectView> Objects(ProjectWorkspace workspace)
    {
        var maps = workspace.All<ProcessMap>().Where(map => map.DeletedAt is null).Select(map => map.Id).ToHashSet();
        var objects = new Dictionary<string, AssistantObjectView>(StringComparer.Ordinal);
        foreach (var lane in workspace.All<Lane>().Where(lane => lane.DeletedAt is null && maps.Contains(lane.MapId)))
            objects[lane.Code] = new AssistantObjectView(lane.Code, "lane", lane.MapId, lane.Id, lane.Name);
        foreach (var step in workspace.All<Step>().Where(step => step.DeletedAt is null && maps.Contains(step.MapId)))
            objects[step.Code] = new AssistantObjectView(step.Code, "step", step.MapId, step.Id, step.Label);
        return objects;
    }
}

/// <summary>
/// The indicators of the project given to the model (F-109, decision of 07/11/2026): those of the panel « Analysis » now,
/// on the main map and its details as the person sees them, then each photo, the reference of a target state first, with
/// its comparison with now made by the core; the working day, to tell working hours in days. The model cites them, never
/// computes them again.
/// </summary>
public sealed record AssistantIndicators(StreamIndicators Now, IReadOnlyList<AssistantPhoto> Photos, decimal WorkdayHours)
{
    public static AssistantIndicators Of(ProjectWorkspace workspace, MapViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var view = ProjectIndicators.Read(workspace, viewer);
        IEnumerable<SnapshotView> photos = view.Baseline is { } baseline ? [baseline, .. view.Photos] : view.Photos;
        return new AssistantIndicators(view.Current, [.. photos.Select(photo => new AssistantPhoto(photo.Label, photo.Origin, photo.CreatedAt, photo.Changes))],
            workspace.Project.WorkdayHours);
    }
}

/// <summary>A photo given to the model: its label, its origin (`baseline`, the reference of a target state), its date, its comparison with now.</summary>
public sealed record AssistantPhoto(string Label, SnapshotOrigin Origin, DateTime TakenAt, IReadOnlyList<IndicatorChange> Changes);

/// <summary>The pain points of a map, for its report (plan 3.1, L4).</summary>
public static class AssistantPainPoints
{
    /// <summary>
    /// The pain points the sheets of the map show: those of the uses of its steps whose producing step is alive, as a sheet
    /// lists them (any member reads a sheet), each once, in the order of their codes; the step named is the one it was raised on, else the one that uses.
    /// </summary>
    public static IReadOnlyList<AssistantPainPointView> Of(ProjectWorkspace workspace, Guid mapId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var steps = workspace.StepsOf(mapId).ToDictionary(step => step.Id);
        var codes = workspace.All<Step>().Where(step => step.DeletedAt is null).ToDictionary(step => step.Id, step => step.Code);
        // A use shows on a sheet only when the step that produces its document is alive (DocumentFlows): a step in the trash shows nothing.
        var producers = workspace.All<Document>().ToDictionary(document => document.Id, document => document.StepId);
        return workspace.All<DocumentUse>().Where(use => steps.ContainsKey(use.StepId) && use.PainPointId is not null
                && producers.TryGetValue(use.DocumentId, out var producer) && workspace.Step(producer) is { DeletedAt: null })
            .Select(use => (Use: use, Point: workspace.Find<PainPoint>(PainPoint.KeyOf(use.PainPointId!.Value))))
            .Where(pair => pair.Point is not null)
            .GroupBy(pair => pair.Point!.Id).Select(group => group.First())
            .Select(pair => new AssistantPainPointView(pair.Point!.Code, pair.Point.StepId is { } raised && codes.TryGetValue(raised, out var code) ? code : codes.GetValueOrDefault(pair.Use.StepId),
                pair.Point.Description, pair.Point.LostHoursPerMonth, Codes.Of(pair.Point.Status)))
            .OrderBy(point => point.Code, StringComparer.Ordinal)
            .ToList();
    }
}
