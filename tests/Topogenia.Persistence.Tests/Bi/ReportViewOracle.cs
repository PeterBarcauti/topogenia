// ReportViewOracle.cs
// The rows that each report view must give for the records of one project, computed in C# from the rules of the 1.x views.

using System.Globalization;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Persistence.Tests.Bi;

/// <summary>
/// An oracle written apart from the SQL of the views: the rules of the 1.x views (`db/004` as changed by `db/006`, with
/// the classification of the project and the latest validated version of a sheet added, AR-100, AR-101) read on the records, and the single computations of the domain where the 1.x had a second copy in SQL (completeness of a
/// sheet, A-2.10-17; visible RACI, A-6-14). Each row is a canonical text (<see cref="Row"/>), so that the rows read from
/// the database and the expected rows compare as sets of texts.
/// </summary>
internal sealed class ReportViewOracle
{
    /// <summary>
    /// The views of the 1.x, each with its columns in their order (`db/004`, `db/006`), plus the columns added at the end
    /// (AR-100, AR-101; the entries of the value stream, ADR 085).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Columns { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["project"] = ["project_id", "code", "name", "source_language", "description", "created_at", "classification", "workday_hours",
            "fte_hours_per_month"],
        ["process_map"] = ["map_id", "project_id", "parent_step_id", "name", "depth", "path"],
        ["lane"] = ["lane_id", "map_id", "project_id", "code", "name", "lane_type", "sort_order", "headcount"],
        ["step"] = ["step_id", "map_id", "project_id", "lane_id", "code", "label", "step_type", "column_index", "row_index", "automation_status",
            "assignee_user_id", "created_at", "updated_at", "volume_quantity", "volume_period"],
        ["flow"] = ["flow_id", "map_id", "project_id", "source_step_id", "target_step_id", "label", "share_percent"],
        ["step_sheet"] = ["step_id", "status", "actor", "purpose", "tools", "frequency", "duration_minutes", "updated_at", "completeness_pct",
            "validated_version", "lead_time", "lead_time_unit"],
        ["step_operation"] = ["step_id", "position", "text", "tool", "nature"],
        ["document"] = ["document_id", "step_id", "direction", "name", "location", "location_type", "wait_hours"],
        ["document_use"] = ["document_id", "step_id", "wait_hours", "percent_complete_accurate", "pain_point_id"],
        ["pain_point"] = ["pain_point_id", "project_id", "step_id", "code", "description", "lost_hours_per_month", "status", "created_at"],
        ["step_raci"] = ["step_id", "lane_id", "role"],
    };

    private readonly ProjectWorkspace workspace;

    public ReportViewOracle(Guid projectId, IEnumerable<object> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        workspace = new ProjectWorkspace(projectId, records.OfType<IRecord>());
    }

    public static IEnumerable<string> Views => Columns.Keys;

    private Project Project => workspace.Project;

    /// <summary>The expected rows of a view, in no order.</summary>
    public IReadOnlyList<string> RowsOf(string view) => view switch
    {
        "project" => Project.DeletedAt is null
            ? [Row(Project.Id, Project.Code, Project.Name, Project.SourceLanguage, Project.Description, Project.CreatedAt, Codes.Of(Project.Classification),
                Project.WorkdayHours, Project.FteHoursPerMonth)]
            : [],
        "process_map" => LiveMaps().Select(item => Row(item.Map.Id, item.Map.ProjectId, item.Map.ParentStepId, item.Map.Name, item.Depth, item.Path)).ToList(),
        "lane" => LiveLanes().Select(lane => Row(lane.Id, lane.MapId, Project.Id, lane.Code, lane.Name, Codes.Of(lane.LaneType), lane.SortOrder,
            lane.Headcount)).ToList(),
        "step" => LiveSteps().Select(StepRow).ToList(),
        "flow" => workspace.All<Flow>().Where(flow => flow.DeletedAt is null && LiveSteps().Any(step => step.Id == flow.SourceStepId))
            .Select(flow => Row(flow.Id, flow.MapId, Project.Id, flow.SourceStepId, flow.TargetStepId, flow.Label, flow.SharePercent)).ToList(),
        "step_sheet" => StepsWithContent().Select(SheetRow).ToList(),
        "step_operation" => StepsWithContent().SelectMany(step => SheetQueries.SheetOf(workspace, step.Id) is { } sheet ? SheetQueries.OperationsOf(workspace, sheet.Id)
            .Select(operation => Row(step.Id, operation.Position, operation.Text, operation.Tool, Code(operation.Nature))) : []).ToList(),
        "document" => workspace.All<Document>().Where(document => HasContent(document.StepId)).Select(document => Row(document.Id, document.StepId,
            Codes.Of(document.Direction), document.Name, document.Location, Code(document.LocationType), document.WaitHours)).ToList(),
        "document_use" => workspace.All<DocumentUse>().Where(UseIsShown)
            .Select(use => Row(use.DocumentId, use.StepId, use.WaitHours, use.PercentCompleteAccurate, use.PainPointId)).ToList(),
        "pain_point" => Project.DeletedAt is null ? workspace.All<PainPoint>().Where(point => point.StepId is null || workspace.Step(point.StepId.Value) is { DeletedAt: null })
            .Select(point => Row(point.Id, point.ProjectId, point.StepId, point.Code, point.Description, point.LostHoursPerMonth, Codes.Of(point.Status), point.CreatedAt))
            .ToList() : [],
        "step_raci" => LiveSteps().SelectMany(step => RaciRules.Visible(workspace, step.Id).Roles)
            .Select(raci => Row(raci.StepId, raci.LaneId, Codes.Of(raci.Role))).ToList(),
        _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unknown report view."),
    };

    /// <summary>The completeness that the domain gives to a live step with content (the draft of its sheet, or no sheet).</summary>
    public int CompletenessOf(Guid stepId) =>
        SheetCompleteness.Of(workspace, stepId, SheetQueries.SheetOf(workspace, stepId) is { } sheet ? SheetQueries.DraftOf(workspace, sheet).FilledRubrics : 0);

    /// <summary>A row as a canonical text: cells joined by `|`, each written by <see cref="Cell"/>.</summary>
    public static string Row(params object?[] cells) => string.Join('|', cells.Select(Cell));

    /// <summary>
    /// A cell as a text that does not depend on the type the database gives: identifiers in the `D` form, dates by their
    /// ticks (the kind of a date read back is not kept by SQL Server), decimals without trailing zeros, integers of any width.
    /// </summary>
    public static string Cell(object? value) => value switch
    {
        null => "null",
        Guid guid => guid.ToString("D"),
        DateTime date => date.Ticks.ToString(CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null",
    };

    private static string? Code<T>(T? value)
        where T : struct, Enum => value is { } code ? Codes.Of(code) : null;

    /// <summary>Live maps of a live project, from the root map down through live steps, with their depth and path.</summary>
    private List<(ProcessMap Map, int Depth, string Path)> LiveMaps()
    {
        var maps = new List<(ProcessMap Map, int Depth, string Path)>();
        if (Project.DeletedAt is not null)
            return maps;
        var pending = new Queue<(ProcessMap Map, int Depth, string Path)>(workspace.All<ProcessMap>()
            .Where(map => map.ParentStepId is null && map.DeletedAt is null).Select(map => (map, 1, map.Name)));
        while (pending.TryDequeue(out var item))
        {
            maps.Add(item);
            foreach (var step in workspace.All<Step>().Where(step => step.MapId == item.Map.Id && step.DeletedAt is null))
            {
                foreach (var child in workspace.All<ProcessMap>().Where(map => map.ParentStepId == step.Id && map.DeletedAt is null))
                    pending.Enqueue((child, item.Depth + 1, item.Path + " > " + child.Name));
            }
        }
        return maps;
    }

    private List<Lane> LiveLanes()
    {
        var maps = LiveMaps().Select(item => item.Map.Id).ToHashSet();
        return workspace.All<Lane>().Where(lane => lane.DeletedAt is null && maps.Contains(lane.MapId)).ToList();
    }

    private List<Step> LiveSteps()
    {
        var maps = LiveMaps().Select(item => item.Map.Id).ToHashSet();
        return workspace.All<Step>().Where(step => step.DeletedAt is null && maps.Contains(step.MapId)).ToList();
    }

    private List<Step> StepsWithContent() => LiveSteps().Where(step => !StepTypes.HasNoContent(step.StepType)).ToList();

    private bool HasContent(Guid stepId) => StepsWithContent().Any(step => step.Id == stepId);

    /// <summary>A use is shown when its step has content and the document comes from a live step with content.</summary>
    private bool UseIsShown(DocumentUse use) =>
        HasContent(use.StepId) && workspace.Document(use.DocumentId) is { } document
        && workspace.Step(document.StepId) is { DeletedAt: null } producer && !StepTypes.HasNoContent(producer.StepType);

    private string StepRow(Step step) => Row(step.Id, step.MapId, Project.Id, step.LaneId, step.Code, step.Label, Codes.Of(step.StepType), step.Column,
        step.Row, Codes.Of(step.AutomationStatus), step.AssigneeUserId, step.CreatedAt, step.UpdatedAt, step.VolumeQuantity, Code(step.VolumePeriod));

    private string SheetRow(Step step)
    {
        var sheet = SheetQueries.SheetOf(workspace, step.Id);
        return Row(step.Id, sheet is null ? "empty" : Codes.Of(sheet.Status), sheet?.Actor, sheet?.Purpose, sheet?.Tools, Code(sheet?.Frequency),
            sheet?.DurationMinutes, sheet?.UpdatedAt, CompletenessOf(step.Id), sheet is null ? null : SheetQueries.LatestVersion(workspace, sheet.Id)?.VersionNumber,
            sheet?.LeadTime, Code(sheet?.LeadTimeUnit));
    }
}
