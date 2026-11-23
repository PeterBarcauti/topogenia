// PmapFiles.cs
// Adapter of the port of the .pmap format: the reader of a file and of a collection, the outline, the writer and the build of the interchange (ADR 054, ADR 114).

using System.Collections.ObjectModel;
using Topogenia.Application.Ports;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Maps;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Proposals;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Files;

namespace Topogenia.Interchange.Pmap;

/// <summary><see cref="IPmapFormat"/> on <see cref="PmapReader"/>, <see cref="PmapCollectionReader"/>, <see cref="PmapOutline"/>, <see cref="PmapWriter"/> and <see cref="PmapBuild"/>.</summary>
public sealed class PmapFiles : IPmapFormat
{
    /// <summary>A file with a fault is FICHIER_INVALIDE, with the number of its faults and each fault (A-1.2-19).</summary>
    public IPmapDocument Read(string content, LanguageSettings languages, string raciLetters) =>
        Read(new PmapReadSettings(languages, raciLetters), content);

    public IPmapDocument ReadProposal(string content, LanguageSettings languages, string raciLetters) =>
        Read(new PmapReadSettings(languages, raciLetters, Proposal: true), content);

    public PmapInsertionFile ReadInsertion(string content, LanguageSettings languages, string raciLetters)
    {
        var held = Read(new PmapReadSettings(languages, raciLetters) { DropUnknownLanguages = true }, content);
        var document = held.Document;
        return new PmapInsertionFile(held, document.Project.Language, document.Project.Translations, document.Depth, document.DroppedLanguages);
    }

    /// <summary>
    /// A fault of the envelope is FICHIER_INVALIDE as a file with a fault; each process keeps its document or its faults
    /// (ADR 114), read with the settings of <see cref="Read(string, LanguageSettings, string)"/>.
    /// </summary>
    public PmapCollection ReadCollection(string content, LanguageSettings languages, string raciLetters)
    {
        var read = PmapCollectionReader.Read(content, new PmapReadSettings(languages, raciLetters));
        if (read.Name is null)
            throw Invalid(read.Errors);
        return new PmapCollection(read.Name, read.Description, [.. read.Processes.Select(process => new PmapCollectionItem(
            process.Index, process.Content, process.Read.Document is { } document ? new Held(document) : null, process.Read.Errors)
        {
            Name = process.Name,
            Language = process.Read.Document?.Project.Language,
            Description = process.Read.Document?.Project.Description,
        })]);
    }

    public PmapDetailBuild Detail(OperationContext context, ProjectWorkspace workspace, IPmapDocument document, Guid stepId, LanguageSettings languages, bool withSheets)
    {
        var read = Of(document);
        var built = PmapBuild.Detail(context, workspace, withSheets ? read : read with { Map = WithoutSheets(read.Map) }, stepId, languages);
        var counts = built.Counts;
        return new PmapDetailBuild(built.MapId, new Application.Ports.PmapCounts(counts.Maps, counts.Lanes, counts.Steps, counts.Flows, counts.Sheets))
        {
            Comments = built.Comments,
            FlowComments = built.FlowComments,
        };
    }

    /// <summary>A map and every map below it without the sheet blocks of their steps (their documents and uses with them).</summary>
    private static PmapMap WithoutSheets(PmapMap map) => map with
    {
        Steps = [.. map.Steps.Select(step => step with { Sheet = null, SubMap = step.SubMap is { } sub ? WithoutSheets(sub) : null })],
    };

    private static Held Read(PmapReadSettings settings, string content)
    {
        var read = PmapReader.Read(content, settings);
        if (read.Document is null)
            throw Invalid(read.Errors);
        return new Held(read.Document);
    }

    private static DomainException Invalid(IReadOnlyList<FileError> errors) =>
        new(ErrorCodes.InvalidFile, RefusalKind.Invalid, new Dictionary<string, object?>
        {
            ["count"] = errors.Count,
            [FileError.DetailsKey] = errors,
        });

    public PmapOutlineResult Outline(ProjectWorkspace workspace, Func<Step, SheetViewer>? viewerOf, PmapExtras? extras)
    {
        var outlined = PmapOutline.Of(workspace, new PmapOutlineOptions(viewerOf is not null, viewerOf) { Extras = extras });
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public PmapOutlineResult OutlineMap(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf)
    {
        var outlined = PmapOutline.OfMap(workspace, mapId, new PmapOutlineOptions(true, viewerOf, DepthMax: 1, WithRoutes: false));
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public PmapOutlineResult OutlineView(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf)
    {
        var outlined = PmapOutline.OfMap(workspace, mapId, new PmapOutlineOptions(true, viewerOf, DepthMax: 1) { WithLocations = false, WithPainPointStates = true });
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public PmapOutlineResult OutlinePrint(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf)
    {
        var outlined = PmapOutline.OfMap(workspace, mapId, new PmapOutlineOptions(true, viewerOf) { WithLocations = false, WithPainPointStates = true });
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public PmapOutlineResult OutlineBranch(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf, PmapExtras? extras)
    {
        var outlined = PmapOutline.OfMap(workspace, mapId, new PmapOutlineOptions(true, viewerOf) { Extras = extras });
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public PmapOutlineResult OutlineTree(ProjectWorkspace workspace, Guid mapId)
    {
        var outlined = PmapOutline.OfMap(workspace, mapId, new PmapOutlineOptions(false, null, ProjectWorkspace.MaxDepth + 1, WithRoutes: false));
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public DesiredMap Desired(IPmapDocument document)
    {
        var read = Of(document);
        return Desired(read.Map) with { ProjectTranslations = read.Project.Translations };
    }

    public IPmapDocument Carry(IPmapDocument proposal, ProjectWorkspace origin, Guid mapId, Func<Step, SheetViewer> viewerOf)
    {
        // The sheets of the map of origin are read with the status of each pain point (1.5): every pain point keeps its status
        // in the new project, open or dealt with, as a whole copy opened keeps it (ADR 105, ADR 116 point 10; audit of the
        // copy 64, P2-05); the proposal adds none of its own. No comment of presentation is carried: the proposal is a new
        // structure.
        var read = PmapOutline.OfMap(origin, mapId, new PmapOutlineOptions(true, viewerOf, ProjectWorkspace.MaxDepth + 1) { Extras = new PmapExtras([], []) });
        return new Held(PmapCarry.Of(Of(proposal), read.Document));
    }

    /// <summary>A map of the document and, through its steps, its sub-maps and their sheets (ADR 082), each map placed on its own.</summary>
    private static DesiredMap Desired(PmapMap map)
    {
        var placed = map.Steps.All(step => step.Column is not null)
            ? null
            : AutomaticColumns.Place([.. map.Steps.Select(step => new PlacementStep(step.Id, step.Lane, step.Row))],
                [.. map.Flows.Select(flow => new PlacementFlow(flow.Source, flow.Target))]);
        return new DesiredMap(
            [.. map.Lanes.Select(lane => new DesiredLane(lane.Id, lane.Name, Codes.Parse<LaneType>(lane.Type, "type"))
            {
                Translations = lane.Translations, Headcount = lane.Headcount,
            })],
            [.. map.Steps.Select(step => new DesiredStep(step.Id, step.Lane, step.Label, Codes.Parse<StepType>(step.Type, "type"),
                step.Column ?? placed![step.Id], step.Row, Codes.Parse<AutomationStatus>(step.Automation, "automation"))
            {
                Translations = step.Translations,
                SubMap = step.SubMap is { } sub ? Desired(sub) : null,
                Sheet = step.Sheet is { IsEmpty: false } sheet ? Sheet(sheet) : null,
                // The entries of the value stream the proposal gives (ADR 085), checked by the rules of the domain when applied.
                Volume = step.Volume is { } volume ? IncomingVolume.Parse(volume.Quantity, volume.Period) : null,
                // The value class the proposal gives (1.4, F-103), read on a step with content only.
                ValueClass = step.ValueClass is { } value ? Codes.Parse<ValueClass>(value, StepOperations.ValueClassField) : null,
            })],
            [.. map.Flows.Select(flow => new DesiredFlow(flow.Source, flow.Target, flow.Label) { Translations = flow.Translations, Share = flow.Share })])
        {
            // The summary of the map (1.4, plan 3.1, lot R): its translations follow a proposal, its sentence stays the person's.
            Summary = map.Summary?.Text,
            SummaryTranslations = map.Summary?.Translations ?? ReadOnlyDictionary<string, string>.Empty,
        };
    }

    /// <summary>The sheet block of a step as the domain writes it, the same fields as the opening of a file (`PmapBuild.Sheets`).</summary>
    private static DesiredSheet Sheet(PmapSheet sheet) => new(
        new SheetInput(new Dictionary<string, SheetFieldValue?>
        {
            [SheetContentReader.ActorKey] = new(sheet.Actor, null),
            [SheetContentReader.PurposeKey] = new(sheet.Purpose, null),
            [SheetContentReader.ToolsKey] = new(sheet.Tools, null),
            [SheetContentReader.FrequencyKey] = new(sheet.Frequency, null),
            [SheetContentReader.DurationKey] = new(null, sheet.DurationMinutes),
            [SheetContentReader.DurationMinKey] = new(null, sheet.DurationMinMinutes),
            [SheetContentReader.DurationMaxKey] = new(null, sheet.DurationMaxMinutes),
            [SheetContentReader.LeadTimeKey] = new(sheet.LeadTime?.Unit, sheet.LeadTime?.Value),
        }, [.. sheet.Procedure.Select(task => (ProcedureLine?)new ProcedureLine(task.Text, task.Tool, task.Nature))], sheet.FreeText),
        [.. sheet.Outputs.Select(output => new DesiredDocument(output.Id,
            new DocumentInput(Codes.Of(DocumentDirection.Output), output.Name, output.Location, null, output.ShareConfirmed) { Medium = output.Medium }))],
        [.. sheet.Inputs.Select(entry => new DocumentInput(Codes.Of(DocumentDirection.Input), entry.Name, entry.Location, entry.WaitHours, entry.ShareConfirmed) { Medium = entry.Medium })],
        [.. sheet.Uses.Select(use => new DesiredUse(use.Document, use.Input))]);

    public PmapOutlineResult Model(ProjectWorkspace workspace, PmapExtras? extras)
    {
        var outlined = PmapOutline.Of(workspace, new PmapOutlineOptions(false, null, ProjectWorkspace.MaxDepth + 1) { Extras = extras });
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public string Write(IPmapDocument document, string generatedBy, DateTime generatedAt) => PmapWriter.Write(Of(document), generatedBy, generatedAt);

    public PmapProjectBuild Build(OperationContext context, IPmapDocument document, string code, string name, ProjectCreationInputs inputs)
    {
        var built = PmapBuild.Run(context, Of(document), code, name, inputs);
        var counts = built.Counts;
        return new PmapProjectBuild(built.Project, built.RootMapId, built.Changes, counts.Maps, counts.Lanes, counts.Steps, counts.Flows, counts.Sheets)
        {
            Comments = built.Comments,
            FlowComments = built.FlowComments,
        };
    }

    public string FileName(Project project, DateTime exportedAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        return ExportFileName.Of(project.Code, project.Name, exportedAt, PmapFormat.Extension);
    }

    private static PmapDocument Of(IPmapDocument document) =>
        document is Held held ? held.Document : throw new ArgumentException("The document was not made by this format.", nameof(document));

    /// <summary>A document of this format, seen by the application through its project only.</summary>
    private sealed class Held(PmapDocument document) : IPmapDocument
    {
        public PmapDocument Document { get; } = document;

        public string? ProjectCode => Document.Project.Code;

        public string ProjectName => Document.Project.Name;

        public IReadOnlyList<string> Uncertainties => Document.Uncertainties;

        public int RemoteLocations => Document.RemoteLocations;

        public Application.Ports.PmapCounts Counts => Count(Document.Map, new Application.Ports.PmapCounts(0, 0, 0, 0, 0));

        private static Application.Ports.PmapCounts Count(PmapMap map, Application.Ports.PmapCounts counts)
        {
            var here = counts with
            {
                Maps = counts.Maps + 1, Lanes = counts.Lanes + map.Lanes.Count, Steps = counts.Steps + map.Steps.Count, Flows = counts.Flows + map.Flows.Count,
                Sheets = counts.Sheets + map.Steps.Count(step => step.Sheet is { IsEmpty: false }),
            };
            return map.Steps.Where(step => step.SubMap is not null).Aggregate(here, (sum, step) => Count(step.SubMap!, sum));
        }
    }
}
