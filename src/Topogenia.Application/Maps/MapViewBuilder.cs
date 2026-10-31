// MapViewBuilder.cs
// Builds the view of a map from a workspace with the rules of the domain, without any rule of its own (annex A 2.4).

using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Sheets;
using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Layout;
using Topogenia.Domain.Maps;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.ValueStreams;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Maps;

/// <summary>
/// The view of a map (annex A 2.4): lanes in their order, steps in reading order with their path, sub-map, sheet summary
/// as the viewer sees it and markers, flows with the documents they carry, layout warnings (RG-C08), visible RACI and its
/// letters, rights, breadcrumb from the root map, and the open dissolution frame with its remainder and the sheet of the
/// remainder as the viewer may see it. Every value comes
/// from a rule of the domain (`ReadingPaths`, `SheetVisibility`, `MapMarkers`, `LayoutWarnings`, `RaciRules`, `Frames`).
/// </summary>
public sealed class MapViewBuilder : IMapViewBuilder
{
    private readonly AppSettings settings;

    public MapViewBuilder(AppSettings settings)
    {
        this.settings = settings;
    }

    /// <summary>Sheets for the summaries and the autonomous flows, documents for the markers, translations for the texts.</summary>
    public WorkspaceContent Content => WorkspaceContent.Sheets | WorkspaceContent.Documents | WorkspaceContent.Translations;

    public MapView Build(ProjectWorkspace workspace, Guid mapId, MapViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(viewer);
        var map = workspace.LiveMap(mapId);
        var stream = ValueStream.Compute(workspace, mapId, step => SheetFactsOf(workspace, step, viewer));
        var breadcrumb = SubMapRules.Breadcrumb(workspace, mapId)
            .Select(item => new BreadcrumbView(item.Id, item.Name, item.SourceLanguage, Texts(workspace, TranslationObject.Map, item.Id))).ToList();
        return new MapView(map.Id, workspace.ProjectId, workspace.Project.Code, map.Name, map.SourceLanguage,
            Texts(workspace, TranslationObject.Map, map.Id), map.ParentStepId, workspace.Depth(mapId), settings.EffectiveRecommendedDepth,
            breadcrumb, Lanes(workspace, mapId), Steps(workspace, mapId, viewer), Flows(workspace, mapId),
            LayoutWarnings.Of(workspace, mapId).Select(warning => new WarningView(warning.StepId, warning.Code)).ToList(),
            Raci(workspace, mapId), RaciRules.Letters(settings), new MapRightsView(viewer.Role >= MemberRole.Facilitator, viewer.Role),
            map.Version, workspace.Project.StructureVersion, Frames.OpenFrameOf(workspace, mapId), RemainderSheet(workspace, mapId, viewer),
            Languages(workspace))
        {
            ValueStream = stream,
            StreamSubjects = StreamSubjects(workspace, stream),
        };
    }

    /// <summary>
    /// The objects the completeness check names, each once: a step or a lane by its code and label; a map by the step it
    /// details (the nearest of its parent steps), the root map by the code of the project and its name.
    /// </summary>
    private static Dictionary<Guid, StreamSubjectView> StreamSubjects(ProjectWorkspace workspace, MapStream stream)
    {
        var subjects = new Dictionary<Guid, StreamSubjectView>();
        foreach (var issue in stream.Issues)
        {
            if (issue.StepId is { } stepId && workspace.Step(stepId) is { } step)
                subjects.TryAdd(stepId, new StreamSubjectView(step.Code, step.Label, step.SourceLanguage, step.MapId));
            else if (issue.LaneId is { } laneId && workspace.Lane(laneId) is { } lane)
                subjects.TryAdd(laneId, new StreamSubjectView(lane.Code, lane.Name, lane.SourceLanguage, lane.MapId));
            else if (!subjects.ContainsKey(issue.MapId))
                subjects.Add(issue.MapId, MapSubject(workspace, issue.MapId));
        }
        return subjects;
    }

    private static StreamSubjectView MapSubject(ProjectWorkspace workspace, Guid mapId)
    {
        var map = workspace.LiveMap(mapId);
        return workspace.ParentSteps(mapId) is [var detailed, ..]
            ? new StreamSubjectView(detailed.Code, detailed.Label, detailed.SourceLanguage, mapId)
            : new StreamSubjectView(workspace.Project.Code, map.Name, map.SourceLanguage, mapId);
    }

    /// <summary>
    /// The sheet of a step as the viewer sees it, for the value stream (spec 14.4): the draft for the one who may fill it and
    /// the facilitator, the latest validated version for the others; a value of a draft or of a sheet to review is marked.
    /// </summary>
    private static SheetFacts? SheetFactsOf(ProjectWorkspace workspace, Step step, MapViewer viewer)
    {
        var summary = SheetVisibility.FactsFor(workspace, step.Id, SheetRights.ViewerOf(workspace, step, viewer.Role, viewer.UserId));
        return summary is null ? null
            : new SheetFacts(summary.Frequency, summary.DurationMinutes, summary.LeadTime, summary.Status is SheetStatus.Draft or SheetStatus.NeedsReview);
    }

    /// <summary>The language of origin of the project and the coverage of each language (ADR 083).</summary>
    private static ProjectLanguagesView Languages(ProjectWorkspace workspace)
    {
        var coverage = TranslationCatalog.Coverage(workspace);
        return new ProjectLanguagesView(workspace.TextLanguage, coverage.Total, coverage.Written);
    }

    /// <summary>The sheet of the remainder of the open frame of the map, as the viewer may see it; null without an open frame.</summary>
    private static RemainderSheetView? RemainderSheet(ProjectWorkspace workspace, Guid mapId, MapViewer viewer)
    {
        if (DissolutionGuards.OpenOn(workspace, mapId) is not { } open)
            return null;
        var visible = SheetVisibility.ForRemainder(workspace, open, new SheetViewer(false, viewer.Role >= MemberRole.Facilitator));
        var validated = visible.Version is { } version ? new SheetVersionView(version.VersionNumber, version.ValidatedAt, version.ValidatedBy) : null;
        return new RemainderSheetView(visible.Status, visible.Content, visible.IsDraft, validated, visible.ClarificationRequest);
    }

    private static List<LaneView> Lanes(ProjectWorkspace workspace, Guid mapId) =>
        workspace.LanesOf(mapId).Select(lane => new LaneView(lane.Id, lane.Code, lane.Name, lane.SourceLanguage,
            Texts(workspace, TranslationObject.Lane, lane.Id), lane.LaneType, lane.Color, lane.SortOrder, lane.ActorUserId, lane.Version)
        {
            Headcount = lane.Headcount,
        }).ToList();

    private static List<StepView> Steps(ProjectWorkspace workspace, Guid mapId, MapViewer viewer)
    {
        var paths = ReadingPaths.Paths(workspace, mapId);
        var documents = MapMarkers.DocumentsOf(workspace, mapId);
        var painPoints = MapMarkers.PainPointsOf(workspace, mapId);
        return workspace.ReadingOrder(mapId).Select(step =>
        {
            var subMap = workspace.SubMapOf(step.Id);
            var sheet = SheetVisibility.SummaryFor(workspace, step.Id, SheetRights.ViewerOf(workspace, step, viewer.Role, viewer.UserId));
            var markers = new StepMarkersView(documents.GetValueOrDefault(step.Id) ?? [], painPoints.GetValueOrDefault(step.Id) ?? []);
            return new StepView(step.Id, step.Code, step.Label, step.SourceLanguage, step.StepType, step.LaneId, step.Column, step.Row,
                // As the 1.x `sous_carte_vide`: true only for a live sub-map without a live step; a step without sub-map is false.
                paths[step.Id], subMap?.Id, subMap is not null && workspace.IsEmptyMap(subMap.Id), step.AssigneeUserId, step.AutomationStatus,
                step.UpdatedAt ?? step.CreatedAt, sheet, step.Version, Texts(workspace, TranslationObject.Step, step.Id), markers)
            {
                Volume = step.VolumeQuantity is { } quantity && step.VolumePeriod is { } period ? new IncomingVolume(quantity, period) : null,
            };
        }).ToList();
    }

    private static List<FlowView> Flows(ProjectWorkspace workspace, Guid mapId)
    {
        var documents = MapMarkers.FlowDocumentsOf(workspace, mapId);
        return workspace.FlowsOf(mapId).Select(flow => new FlowView(flow.Id, flow.SourceStepId, flow.TargetStepId, flow.Label,
            (documents.GetValueOrDefault(flow.Id) ?? []).Select(document => document.Name).ToList(), flow.Version,
            Texts(workspace, TranslationObject.Flow, flow.Id))
        {
            SharePercent = flow.SharePercent,
        }).ToList();
    }

    /// <summary>The visible roles in reading order of the steps, each step in the order of its lanes.</summary>
    private static List<RaciView> Raci(ProjectWorkspace workspace, Guid mapId)
    {
        var visible = RaciRules.VisibleOnMap(workspace, mapId);
        return workspace.ReadingOrder(mapId)
            .SelectMany(step => visible[step.Id].Roles.Select(role => new RaciView(role.StepId, role.LaneId, role.Role))).ToList();
    }

    private static Dictionary<string, string> Texts(ProjectWorkspace workspace, TranslationObject type, Guid id) =>
        TranslationRules.Of(workspace, type, id).ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.Ordinal);
}
