// AssistantContext.cs
// What the model learns of a project for an exchange of the panel: the schema of the project, the map open as a .pmap, the codes selected, the objects it may name (spec 3.0 sections 3.1, 7, decisions of 29/10/2026).

using System.Text;
using Topogenia.Application.Authorization;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
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

    public override WorkspaceContent Content => withTexts ? WorkspaceContent.All : WorkspaceContent.None;

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
            workspace.IsEmptyMap(map.Id), context.Role, schema, schemaFormat, openMap, selected, objects);
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
