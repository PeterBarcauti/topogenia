// PmapFiles.cs
// Adapter of the port of the .pmap format: the reader, the outline, the writer and the build of the interchange (ADR 054).

using Topogenia.Application.Ports;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Maps;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Proposals;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Files;

namespace Topogenia.Interchange.Pmap;

/// <summary><see cref="IPmapFormat"/> on <see cref="PmapReader"/>, <see cref="PmapOutline"/>, <see cref="PmapWriter"/> and <see cref="PmapBuild"/>.</summary>
public sealed class PmapFiles : IPmapFormat
{
    /// <summary>A file with a fault is FICHIER_INVALIDE, with the number of its faults and each fault (A-1.2-19).</summary>
    public IPmapDocument Read(string content, LanguageSettings languages, string raciLetters)
    {
        var read = PmapReader.Read(content, new PmapReadSettings(languages, raciLetters));
        if (read.Document is null)
            throw new DomainException(ErrorCodes.InvalidFile, RefusalKind.Invalid, new Dictionary<string, object?>
            {
                ["count"] = read.Errors.Count,
                [FileError.DetailsKey] = read.Errors,
            });
        return new Held(read.Document);
    }

    public PmapOutlineResult Outline(ProjectWorkspace workspace, Func<Step, SheetViewer>? viewerOf)
    {
        var outlined = PmapOutline.Of(workspace, new PmapOutlineOptions(viewerOf is not null, viewerOf));
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public PmapOutlineResult OutlineMap(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf)
    {
        var outlined = PmapOutline.OfMap(workspace, mapId, new PmapOutlineOptions(true, viewerOf, DepthMax: 1));
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public PmapOutlineResult OutlineTree(ProjectWorkspace workspace, Guid mapId)
    {
        var outlined = PmapOutline.OfMap(workspace, mapId, new PmapOutlineOptions(false, null, ProjectWorkspace.MaxDepth + 1));
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public DesiredMap Desired(IPmapDocument document)
    {
        var read = Of(document);
        return Desired(read.Map) with { ProjectTranslations = read.Project.Translations };
    }

    public IPmapDocument Carry(IPmapDocument proposal, ProjectWorkspace origin, Guid mapId, Func<Step, SheetViewer> viewerOf)
    {
        var read = PmapOutline.OfMap(origin, mapId, new PmapOutlineOptions(true, viewerOf, ProjectWorkspace.MaxDepth + 1));
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
            })],
            [.. map.Flows.Select(flow => new DesiredFlow(flow.Source, flow.Target, flow.Label) { Translations = flow.Translations, Share = flow.Share })]);
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
            [SheetContentReader.LeadTimeKey] = new(sheet.LeadTime?.Unit, sheet.LeadTime?.Value),
        }, [.. sheet.Procedure.Select(task => (ProcedureLine?)new ProcedureLine(task.Text, task.Tool, task.Nature))], sheet.FreeText),
        [.. sheet.Outputs.Select(output => new DesiredDocument(output.Id,
            new DocumentInput(Codes.Of(DocumentDirection.Output), output.Name, output.Location, null, output.ShareConfirmed)))],
        [.. sheet.Inputs.Select(entry => new DocumentInput(Codes.Of(DocumentDirection.Input), entry.Name, entry.Location, entry.WaitHours, entry.ShareConfirmed))],
        [.. sheet.Uses.Select(use => new DesiredUse(use.Document, new DocumentUseInput(use.WaitHours, use.PercentCompleteAccurate, use.PainPoint, use.LostHoursPerMonth)))]);

    public PmapOutlineResult Model(ProjectWorkspace workspace)
    {
        var outlined = PmapOutline.Of(workspace, new PmapOutlineOptions(false, null, ProjectWorkspace.MaxDepth + 1));
        return new PmapOutlineResult(new Held(outlined.Document), outlined.DepthCut);
    }

    public string Write(IPmapDocument document, string generatedBy, DateTime generatedAt) => PmapWriter.Write(Of(document), generatedBy, generatedAt);

    public PmapProjectBuild Build(OperationContext context, IPmapDocument document, string code, string name, ProjectCreationInputs inputs)
    {
        var built = PmapBuild.Run(context, Of(document), code, name, inputs);
        var counts = built.Counts;
        return new PmapProjectBuild(built.Project, built.RootMapId, built.Changes, counts.Maps, counts.Lanes, counts.Steps, counts.Flows, counts.Sheets);
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
