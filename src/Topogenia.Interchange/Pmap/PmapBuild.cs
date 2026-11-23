// PmapBuild.cs
// A new project built from a .pmap document by the rules of the domain: what an opening and a duplication write (A-2.17-38 to 42).

using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Maps;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Summaries;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Interchange.Pmap;

/// <summary>
/// What a new project of a document holds (the reply of the opening, A-2.17-42): maps, lanes, steps, flows and the sheet
/// blocks written (a block of documents or uses alone fills the sheet of its step without a form, so it counts too).
/// </summary>
public sealed record PmapCounts(int Maps, int Lanes, int Steps, int Flows, int Sheets);

/// <summary>
/// A project built from a document: its records as changes to write, its project, its root map and what it holds; the
/// lines of the comments of presentation of its new steps and flows (1.5), which the application writes beside the core.
/// </summary>
public sealed record PmapBuilt(Project Project, Guid RootMapId, ChangeSet Changes, PmapCounts Counts)
{
    public IReadOnlyList<PresentationComment> Comments { get; init; } = [];

    public IReadOnlyList<FlowPresentationComment> FlowComments { get; init; } = [];
}

/// <summary>
/// Builds the records of a new project from a document (the 1.x `ServiceFichiers.Creer` and `CreerContenu`), through
/// the rules of the domain, so that a file writes nothing the interface could not write: the project by
/// <see cref="ProjectCreation"/> (the creator its only facilitator, A-2.17-41), the first lane of each map taken from the
/// file, the other lanes, the steps numbered in reading order (column, lane, row, A-2.17-40) at their column or at the
/// automatic one, the flows, the RACI, the translations, the sub-maps, then the sheets as drafts (AR-93, ADR 054). The
/// built workspace passes <see cref="ImportedProjects.Violations"/>. A refusal of a rule is FICHIER_INVALIDE with its
/// place in the file.
/// </summary>
public static partial class PmapBuild
{
    public static PmapBuilt Run(OperationContext context, PmapDocument document, string code, string name, ProjectCreationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(inputs);
        var file = document.Project;
        // A project renamed at the opening loses the translations of the name of the file (A-2.17-39).
        var translations = name == file.Name ? file.Translations.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal) : null;
        var created = ProjectCreation.Create(context, new CreateProjectRequest(code, name, file.Language, translations, file.Description), inputs);
        var project = WithGlossary(WithWorkTime(created.Project, file), file);
        var workspace = new ProjectWorkspace(project.Id, [project]);
        foreach (var record in created.Records.Skip(1))
            workspace.Put(record);
        var builder = new Builder(workspace, context, file.Language, inputs.Languages);
        builder.Fill(created.RootMap.Id, created.FirstLane, document.Map, "map");
        builder.Sheets();
        var violations = ImportedProjects.Violations(workspace);
        if (violations.Count > 0)
            throw new InvalidOperationException("A checked file built a project that breaks the rules of the structure: " + string.Join(", ", violations));
        var counts = new PmapCounts(builder.Maps, builder.Lanes, builder.Steps, builder.Flows, builder.SheetCount);
        return new PmapBuilt(project, created.RootMap.Id, workspace.Commit(context), counts)
        {
            Comments = builder.StepComments(),
            FlowComments = builder.FlowComments(),
        };
    }

    /// <summary>
    /// The project with the work time of the file (1.2, spec 14.2, S6), checked by the bounds of the domain before the
    /// project is written (a fault of the file at `project` otherwise); the defaults of the creation without it.
    /// </summary>
    private static Project WithWorkTime(Project project, PmapProject file) =>
        file.WorkdayHours is null && file.FteHoursPerMonth is null ? project : Builder.Rule("project", () => project with
        {
            WorkdayHours = file.WorkdayHours is { } workday ? WorkTime.ParseWorkday(workday) : project.WorkdayHours,
            FteHoursPerMonth = file.FteHoursPerMonth is { } fte ? WorkTime.ParseFte(fte) : project.FteHoursPerMonth,
        });

    /// <summary>FICHIER_INVALIDE with one fault: a rule of the domain refused what the file asks at <paramref name="path"/>.</summary>
    internal static DomainException Refused(string path, DomainException refusal) =>
        new(ErrorCodes.InvalidFile, RefusalKind.Invalid, new Dictionary<string, object?>
        {
            ["count"] = 1,
            [FileError.DetailsKey] = new[] { new FileError(path, refusal.Code, refusal.Details) },
        });

    private sealed partial class Builder(ProjectWorkspace workspace, OperationContext context, string language, LanguageSettings languages)
    {
        /// <summary>The steps of the file by their identifier, with their place in the file, for their sheets once every map is built.</summary>
        private readonly List<(PmapStep Step, Guid Id, string Path)> built = [];

        public int Maps { get; private set; }

        public int Lanes { get; private set; }

        public int Steps { get; private set; }

        public int Flows { get; private set; }

        public int SheetCount { get; private set; }

        /// <summary>Fills a map created with its first lane: that lane takes the first lane of the file, then the rest of the map.</summary>
        public void Fill(Guid mapId, Lane firstLane, PmapMap map, string path)
        {
            Maps++;
            var file = map.Lanes[0];
            var first = firstLane with
            {
                Name = file.Name,
                LaneType = Codes.Parse<LaneType>(file.Type, "type"),
                Color = file.Color ?? LaneColors.ForIndex(0),
            };
            workspace.Put(first);
            Headcount(first.Id, file, $"{path}.lanes[0]");
            var lanes = new Dictionary<string, Guid>(StringComparer.Ordinal) { [file.Id] = first.Id };
            for (var i = 1; i < map.Lanes.Count; i++)
            {
                var lane = map.Lanes[i];
                lanes[lane.Id] = StructureEdits.NewLane(workspace, context, mapId, lane.Name, language, Codes.Parse<LaneType>(lane.Type, "type"),
                    lane.Color ?? LaneColors.ForIndex(i), i).Id;
                Headcount(lanes[lane.Id], lane, $"{path}.lanes[{i}]");
            }
            for (var i = 0; i < map.Lanes.Count; i++)
                Rule($"{path}.lanes[{i}]", () => TranslationRules.WriteManual(workspace, context, TranslationObject.Lane, lanes[map.Lanes[i].Id], Optional(map.Lanes[i].Translations), languages));
            Lanes += map.Lanes.Count;
            var steps = PlaceSteps(map, lanes, path);
            Steps += steps.Count;
            FlowsOf(mapId, map, steps, lanes, path);
            foreach (var (step, index) in map.Steps.Select((step, index) => (step, index)))
            {
                var stepPath = $"{path}.steps[{index}]";
                foreach (var (lane, role) in step.Raci)
                    Role(steps[step.Id], lanes[lane], role, $"{stepPath}.raci");
                built.Add((step, steps[step.Id], stepPath));
                if (step.SubMap is { } subMap)
                    SubMap(steps[step.Id], step, subMap, $"{stepPath}.sub_map");
            }
            // The summary of the map (1.4, ADR 094), once its levels are built: it describes the map as opened; the file is the
            // person's, who chose to open it. Written in the language of the file, before its translations: one into the
            // language of the project is a translation, not the text itself (audit of the copy 64, P1-04).
            if (map.Summary is { } summary)
                Rule($"{path}.summary", () =>
                {
                    MapSummaryRules.Write(workspace, context, new WriteMapSummariesRequest([new MapSummaryEntry(mapId, summary.Text, SummaryOrigin.Manual, null)]));
                    if (workspace.SummaryOf(mapId) is { } written && written.SourceLanguage != language)
                        workspace.Put(written with { SourceLanguage = language });
                    return TranslationRules.WriteManual(workspace, context, TranslationObject.Summary, mapId, Optional(summary.Translations), languages);
                });
        }

        /// <summary>
        /// The steps of a map at their column, or at the automatic one when the file gives none (A-2.17-34 to 37), created
        /// in reading order (column, lane, row) so that their codes follow it (A-2.17-40).
        /// </summary>
        private Dictionary<string, Guid> PlaceSteps(PmapMap map, Dictionary<string, Guid> lanes, string path)
        {
            var columns = map.Steps.All(step => step.Column is not null)
                ? map.Steps.ToDictionary(step => step.Id, step => step.Column!.Value, StringComparer.Ordinal)
                : AutomaticColumns.Place([.. map.Steps.Select(step => new PlacementStep(step.Id, step.Lane, step.Row))],
                    [.. map.Flows.Select(flow => new PlacementFlow(flow.Source, flow.Target))]).ToDictionary(StringComparer.Ordinal);
            var laneOrder = map.Lanes.Select((lane, index) => (lane.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);
            var index = map.Steps.Select((step, i) => (step.Id, i)).ToDictionary(pair => pair.Id, pair => pair.i, StringComparer.Ordinal);
            var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var step in map.Steps.OrderBy(step => columns[step.Id]).ThenBy(step => laneOrder[step.Lane]).ThenBy(step => step.Row))
            {
                var stepPath = $"{path}.steps[{index[step.Id]}]";
                var created = Rule(stepPath, () => StructureEdits.NewStep(workspace, context, new Cell(lanes[step.Lane], columns[step.Id], step.Row), step.Label,
                    Codes.Parse<StepType>(step.Type, "type"), language));
                var automation = Codes.Parse<AutomationStatus>(step.Automation, "automation");
                if (automation != AutomationStatus.Manual)
                    workspace.Put(created with { AutomationStatus = automation });
                if (step.Volume is { } volume)
                    Rule($"{stepPath}.volume", () => StepOperations.Update(workspace, context,
                        new UpdateStepRequest(created.Id) { Volume = new(new VolumeEntry(volume.Quantity, volume.Period)) }));
                if (step.ValueClass is { } value)
                    Rule($"{stepPath}.value_class", () => StepOperations.Update(workspace, context, new UpdateStepRequest(created.Id) { ValueClass = new(value) }));
                Rule($"{stepPath}.translations", () => TranslationRules.WriteManual(workspace, context, TranslationObject.Step, created.Id, Optional(step.Translations), languages));
                ids[step.Id] = created.Id;
            }
            return ids;
        }

        private void FlowsOf(Guid mapId, PmapMap map, Dictionary<string, Guid> steps, Dictionary<string, Guid> lanes, string path)
        {
            for (var i = 0; i < map.Flows.Count; i++)
            {
                var flow = map.Flows[i];
                var flowPath = $"{path}.flows[{i}]";
                var created = Rule(flowPath, () => StructureEdits.NewFlow(workspace, context, mapId, steps[flow.Source], steps[flow.Target], flow.Label, flow.Share));
                builtFlows.Add((flow, created.Id));
                if (flow.Route is { } route)
                    Rule($"{flowPath}.route", () => FlowRoutes.Set(workspace, context, RouteRequest(created, route, lanes)));
                Rule($"{flowPath}.translations", () => TranslationRules.WriteManual(workspace, context, TranslationObject.Flow, created.Id, Optional(flow.Translations), languages));
            }
            Flows += map.Flows.Count;
        }

        /// <summary>
        /// The route of a flow of the file (1.3, ADR 091), set by the rules of the domain; the lane of a band by its identifier
        /// in the file. A departure of a gateway on its entry, or on a point an exit before it in the file took, is left to
        /// the drawing, as the drawing does (AR-24): a project whose step became a gateway is copied whole.
        /// </summary>
        private SetFlowRouteRequest RouteRequest(Flow flow, PmapRoute route, Dictionary<string, Guid> lanes)
        {
            var via = route.Via switch
            {
                { Aisle: { } aisle } => FlowPassage.InAisle(aisle),
                { Column: { } column } => FlowPassage.InColumn(column),
                { Lane: { } lane, Row: { } row, Edge: null } when lanes.TryGetValue(lane, out var id) => FlowPassage.InRow(id, row),
                { Lane: { } lane, Row: { } row, Edge: { } edge } when lanes.TryGetValue(lane, out var band) => FlowPassage.InBand(band, row, Codes.Parse<FlowSide>(edge, "edge")),
                _ => null,
            };
            return new SetFlowRouteRequest(flow.Id, StructureEdits.FreeDeparture(workspace, flow.SourceStepId, Side(route.From), flow.Id), Side(route.To), via);
        }

        private static FlowSide? Side(string? code) => code is null ? null : Codes.Parse<FlowSide>(code, "side");

        /// <summary>The sub-map of a step: it takes the label and the language of the step (A-2.7), and its first lane from the file.</summary>
        private void SubMap(Guid stepId, PmapStep step, PmapMap subMap, string path)
        {
            var map = new ProcessMap(context.NewId(), workspace.ProjectId, stepId, step.Label, language, context.Now, context.ActorId, null, null, null, null, 1);
            workspace.Put(map);
            var file = subMap.Lanes[0];
            var lane = StructureEdits.NewLane(workspace, context, map.Id, file.Name, language, Codes.Parse<LaneType>(file.Type, "type"),
                file.Color ?? LaneColors.ForIndex(0), 0);
            Fill(map.Id, lane, subMap, path);
        }

        /// <summary>
        /// A role of the RACI of the file: a letter the installation offers goes through the rule of the domain; a letter of
        /// the RACI it does not offer (S where only A, C and I are offered) is kept as the record that rule writes, as the
        /// package keeps it (decision of the owner of the project, 16/11/2026: a copy opens again as it was). The reader
        /// already checked the lane, the type of the step and the single A, and the built project passes the rules of the
        /// structure (`ImportedProjects.Violations`).
        /// </summary>
        private void Role(Guid stepId, Guid laneId, string role, string path)
        {
            if (RaciRules.Letters(context.Settings).Contains(role, StringComparison.Ordinal))
                Rule(path, () => RaciRules.SetRole(workspace, context, new SetRaciRequest(stepId, laneId, role)));
            else
                workspace.Put(new StepRaci(stepId, laneId, Codes.Parse<RaciRole>(role, "role"), context.Now, context.ActorId));
        }

        /// <summary>The headcount of a lane of the file (1.2), by the rule of the domain: never on a system lane.</summary>
        private void Headcount(Guid laneId, PmapLane lane, string path)
        {
            if (lane.Headcount is { } headcount)
                Rule($"{path}.headcount", () => LaneOperations.Update(workspace, context, new UpdateLaneRequest(laneId) { Headcount = new(headcount) }));
        }

        private static Dictionary<string, string?> Optional(IReadOnlyDictionary<string, string> translations) =>
            translations.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);

        /// <summary>Runs a rule of the domain; its refusal is a fault of the file at <paramref name="path"/>.</summary>
        internal static T Rule<T>(string path, Func<T> rule)
        {
            try
            {
                return rule();
            }
            catch (DomainException refusal)
            {
                throw Refused(path, refusal);
            }
        }
    }
}
