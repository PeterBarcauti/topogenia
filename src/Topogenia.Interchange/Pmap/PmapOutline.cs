// PmapOutline.cs
// A project read as a .pmap document: the copy of a reader and the model of a duplication (A-2.17-43 to 49, AR-10, AR-93).

using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Summaries;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Interchange.Pmap;

/// <summary>
/// What the outline asks: the sheets or not (AR-93: on option for the copy, never for a duplication, AR-10) and, with
/// them, what the requester sees of the sheet of a step (`SheetRights.ViewerOf`: the draft for the facilitator and the one
/// who fills, the latest validated version for the others); the deepest level written, the 12 of the format for a file
/// (A-2.17-47), the bound of the walks of the domain for a duplication, which writes no file (AR-118).
/// </summary>
/// <summary>
/// What the document of a project holds: the sheets, as a viewer sees them, the depth of its sub-maps, and the routes chosen
/// by the users (ADR 091), left out of the map the assistant rewrites, which never sets them.
/// </summary>
public sealed record PmapOutlineOptions(bool WithSheets, Func<Step, SheetViewer>? ViewerOf = null, int DepthMax = PmapFormat.DepthMax, bool WithRoutes = true);

/// <summary>A project as a document, and whether its sub-maps went deeper than the level asked (12 for a file, A-2.17-47).</summary>
public sealed record PmapOutlined(PmapDocument Document, bool DepthCut);

/// <summary>
/// The document of a project (the 1.x `ServiceFichiers.Construire`): the root map and its live sub-maps that hold a
/// step, the codes of the application as identifiers, the steps in reading order, the visible RACI only, the
/// translations out of the language of origin (of the steps and flows too, new in 1.1) and, on option, the sheets.
/// </summary>
public static class PmapOutline
{
    public static PmapOutlined Of(ProjectWorkspace workspace, PmapOutlineOptions options)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(options);
        var reading = new Reading(workspace, options);
        var project = workspace.Project;
        var document = new PmapDocument(
            new PmapProject(project.Code, project.Name, project.SourceLanguage, project.Description,
                reading.Translations(TranslationObject.Project, project.Id)) { WorkdayHours = project.WorkdayHours, FteHoursPerMonth = project.FteHoursPerMonth },
            reading.Map(workspace.RootMap.Id, 1),
            []);
        return new PmapOutlined(document, reading.DepthCut);
    }

    /// <summary>
    /// The document of one map under its project (the map open in the panel of the assistant, spec 3.0 section 3.1): the
    /// map as the root of the document, its sub-maps down to the depth of the options; INTROUVABLE for a map deleted or of
    /// another project.
    /// </summary>
    public static PmapOutlined OfMap(ProjectWorkspace workspace, Guid mapId, PmapOutlineOptions options)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(options);
        var map = workspace.LiveMap(mapId);
        var reading = new Reading(workspace, options);
        var project = workspace.Project;
        var document = new PmapDocument(
            new PmapProject(project.Code, project.Name, project.SourceLanguage, project.Description,
                reading.Translations(TranslationObject.Project, project.Id)) { WorkdayHours = project.WorkdayHours, FteHoursPerMonth = project.FteHoursPerMonth },
            reading.Map(map.Id, 1),
            []);
        return new PmapOutlined(document, reading.DepthCut);
    }

    private sealed class Reading(ProjectWorkspace workspace, PmapOutlineOptions options)
    {
        public bool DepthCut { get; private set; }

        public PmapMap Map(Guid mapId, int depth)
        {
            var lanes = workspace.LanesOf(mapId);
            var laneCodes = lanes.ToDictionary(lane => lane.Id, lane => lane.Code);
            var order = workspace.ReadingOrder(mapId);
            var rank = order.Select((step, index) => (step.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
            var stepCodes = order.ToDictionary(step => step.Id, step => step.Code);
            var raci = RaciRules.VisibleOnMap(workspace, mapId);
            var outputIds = OutputIds(order);
            var steps = order.Select(step => Step(step, laneCodes, raci, depth, outputIds)).ToList();
            var flows = workspace.FlowsOf(mapId)
                .Where(flow => rank.ContainsKey(flow.SourceStepId) && rank.ContainsKey(flow.TargetStepId))
                .OrderBy(flow => rank[flow.SourceStepId]).ThenBy(flow => rank[flow.TargetStepId])
                .Select(flow => new PmapFlow(stepCodes[flow.SourceStepId], stepCodes[flow.TargetStepId], flow.Label, Translations(TranslationObject.Flow, flow.Id))
                {
                    Share = flow.SharePercent,
                    Route = options.WithRoutes ? RouteOf(flow, laneCodes) : null,
                })
                .ToList();
            return new PmapMap(
                [.. lanes.Select(lane => new PmapLane(lane.Code, lane.Name, Codes.Of(lane.LaneType), lane.Color, Translations(TranslationObject.Lane, lane.Id))
                {
                    Headcount = lane.Headcount,
                })],
                steps, flows)
            {
                // The summary that describes the map as it is (1.4, ADR 094); a stale one would tell another map.
                Summary = workspace.SummaryOf(mapId) is { } summary && summary.Fingerprint == MapFingerprint.Of(workspace, mapId)
                    ? new PmapSummary(summary.Text, Translations(TranslationObject.Summary, mapId)) : null,
            };
        }

        /// <summary>The route chosen by the user (1.3, ADR 091), the lane of a band by its code in the file; null without any part.</summary>
        private static PmapRoute? RouteOf(Flow flow, Dictionary<Guid, string> laneCodes)
        {
            var via = flow.ViaAisle is { } aisle ? new PmapPassage(aisle, null, null, null)
                : flow.ViaColumn is { } column ? new PmapPassage(null, null, null, null) { Column = column }
                : flow.ViaLaneId is { } lane && laneCodes.TryGetValue(lane, out var code) && flow.ViaRow is { } row
                    ? new PmapPassage(null, code, row, flow.ViaEdge is { } edge ? Codes.Of(edge) : null) : null;
            var from = flow.SourceSide is { } source ? Codes.Of(source) : null;
            var to = flow.TargetSide is { } target ? Codes.Of(target) : null;
            return from is null && to is null && via is null ? null : new PmapRoute(from, to, via);
        }

        private PmapStep Step(Step step, Dictionary<Guid, string> laneCodes, IReadOnlyDictionary<Guid, VisibleRaci> raci, int depth,
            Dictionary<Guid, string> outputIds)
        {
            var roles = raci.TryGetValue(step.Id, out var visible)
                ? visible.Roles.Where(role => laneCodes.ContainsKey(role.LaneId)).ToDictionary(role => laneCodes[role.LaneId], role => Codes.Of(role.Role), StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            return new PmapStep(step.Code, laneCodes[step.LaneId], step.Label, Codes.Of(step.StepType), step.Column, step.Row, Codes.Of(step.AutomationStatus),
                roles, SubMap(step, depth), Translations(TranslationObject.Step, step.Id), Sheet(step, outputIds))
            {
                Volume = step.VolumeQuantity is { } quantity && step.VolumePeriod is { } period ? new PmapVolume(quantity, Codes.Of(period)) : null,
                // The class of a step whose detail has steps sleeps: never written, so that a copy or the context of the AI
                // carries none on it (ADR 095 point 9, audit 3.1 V2 4.5.3).
                ValueClass = step.ValueClass is { } value && !workspace.IsMacro(step.Id) ? Codes.Of(value) : null,
            };
        }

        /// <summary>The live sub-map of a step when it holds a step; beyond the deepest level asked it is not written and the cut is reported.</summary>
        private PmapMap? SubMap(Step step, int depth)
        {
            if (workspace.SubMapOf(step.Id) is not { } subMap || workspace.IsEmptyMap(subMap.Id))
                return null;
            if (depth >= options.DepthMax)
            {
                DepthCut = true;
                return null;
            }
            return Map(subMap.Id, depth + 1);
        }

        /// <summary>
        /// The identifier of each output of the steps of a map, the code of the step followed by `-S` and its rank in the entry order of its
        /// documents, by which the uses name it.
        /// </summary>
        private Dictionary<Guid, string> OutputIds(IReadOnlyList<Step> steps)
        {
            var ids = new Dictionary<Guid, string>();
            if (!options.WithSheets)
                return ids;
            foreach (var step in steps)
            {
                var n = 0;
                foreach (var output in Documents(step.Id, DocumentDirection.Output))
                    ids[output.Id] = $"{step.Code}-S{++n}";
            }
            return ids;
        }

        private IEnumerable<Document> Documents(Guid stepId, DocumentDirection direction) =>
            DocumentRules.InEntryOrder(workspace.All<Document>().Where(document => document.StepId == stepId && document.Direction == direction));

        /// <summary>The sheet of a step as the requester sees it, its documents and its uses of outputs of the same map; null without the option.</summary>
        /// <summary>The medium of a document as its code (1.4, Makigami M3), null when not given.</summary>
        private static string? MediumOf(Document document) => document.Medium is { } medium ? Codes.Of(medium) : null;

        private PmapSheet? Sheet(Step step, Dictionary<Guid, string> outputIds)
        {
            if (!options.WithSheets || StepTypes.HasNoContent(step.StepType))
                return null;
            var viewer = options.ViewerOf?.Invoke(step) ?? new SheetViewer(false, false);
            var content = SheetVisibility.VisibleContent(workspace, step.Id, viewer) ?? SheetContent.Empty;
            var outputs = Documents(step.Id, DocumentDirection.Output)
                .Select(document => new PmapDocumentEntry(outputIds[document.Id], document.Name, document.Location, document.ShareConfirmed, null) { Medium = MediumOf(document) }).ToList();
            var inputs = Documents(step.Id, DocumentDirection.Input)
                .Select(document => new PmapDocumentEntry(null, document.Name, document.Location, document.ShareConfirmed, document.WaitHours) { Medium = MediumOf(document) }).ToList();
            var uses = workspace.All<DocumentUse>()
                .Where(use => use.StepId == step.Id && outputIds.ContainsKey(use.DocumentId))
                .OrderBy(use => outputIds[use.DocumentId], StringComparer.Ordinal)
                .Select(use => Use(use, outputIds[use.DocumentId])).ToList();
            var sheet = new PmapSheet(content.Actor, content.Purpose, content.Tools, content.Frequency is { } often ? Codes.Of(often) : null,
                content.DurationMinutes, [.. content.Procedure.Select(task => new PmapTask(task.Text, task.Tool, task.Nature is { } nature ? Codes.Of(nature) : null))],
                content.FreeText, outputs, inputs, uses)
            {
                LeadTime = content.LeadTime is { } lead ? new PmapLeadTime(lead.Value, Codes.Of(lead.Unit)) : null,
                DurationMinMinutes = content.DurationMinMinutes,
                DurationMaxMinutes = content.DurationMaxMinutes,
            };
            return sheet.IsEmpty ? null : sheet;
        }

        private PmapUse Use(DocumentUse use, string document)
        {
            var painPoint = use.PainPointId is { } id ? workspace.Find<PainPoint>(PainPoint.KeyOf(id)) : null;
            return new PmapUse(document, use.WaitHours, use.PercentCompleteAccurate, painPoint?.Description, painPoint?.LostHoursPerMonth)
            {
                Action = painPoint?.Action,
                Owner = painPoint?.Owner,
                DueDate = painPoint?.DueDate,
            };
        }

        /// <summary>The texts of an object in the languages other than its language of origin, by language.</summary>
        public Dictionary<string, string> Translations(TranslationObject type, Guid id)
        {
            var original = TranslationRules.Original(workspace, type, id);
            return TranslationRules.Of(workspace, type, id)
                .Where(pair => pair.Key != original.Language)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.Ordinal);
        }
    }
}
