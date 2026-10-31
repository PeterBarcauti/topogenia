// IPmapFormat.cs
// Port of the .pmap format (spec 7.5, ADR 054): read a file, outline a project, write a document, build a new project from one.

using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Proposals;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Ports;

/// <summary>A `.pmap` document, read from a file or outlined from a project: the application sees its project only.</summary>
public interface IPmapDocument
{
    /// <summary>The code of the project of the file, null when absent or malformed (the opening asks for it).</summary>
    string? ProjectCode { get; }

    string ProjectName { get; }

    /// <summary>The points the author of the file asks a person to check, shown before the map opens.</summary>
    IReadOnlyList<string> Uncertainties { get; }

    /// <summary>What the document holds, every level of its maps counted (the proposal of the assistant, spec 3.0 section 3.4).</summary>
    PmapCounts Counts { get; }
}

/// <summary>The maps, lanes, steps, flows and sheets of a document.</summary>
public sealed record PmapCounts(int Maps, int Lanes, int Steps, int Flows, int Sheets);

/// <summary>A project outlined as a document, and whether its sub-maps went deeper than the level written (A-2.17-47).</summary>
public sealed record PmapOutlineResult(IPmapDocument Document, bool DepthCut);

/// <summary>The records of a new project built from a document, as changes to write, and what it holds.</summary>
public sealed record PmapProjectBuild(Project Project, Guid RootMapId, ChangeSet Changes, int Maps, int Lanes, int Steps, int Flows, int Sheets);

/// <summary>
/// The `.pmap` format, implemented by the interchange: the application hands texts and workspaces and gets documents,
/// texts and records back, never the format itself.
/// </summary>
public interface IPmapFormat
{
    /// <summary>Reads a file; FICHIER_INVALIDE with its faults (`FileError`, 30 at most) when it breaks a rule.</summary>
    IPmapDocument Read(string content, LanguageSettings languages, string raciLetters);

    /// <summary>
    /// The document of a project: its sheets when <paramref name="viewerOf"/> is given, each as that viewer sees it,
    /// none otherwise (a duplication copies no sheet, AR-10).
    /// </summary>
    PmapOutlineResult Outline(ProjectWorkspace workspace, Func<Step, SheetViewer>? viewerOf);

    /// <summary>
    /// The document of one map of a project, as the map open in the panel of the assistant (spec 3.0 section 3.1): the map
    /// named under the project, without its sub-maps (the schema of the project gives them), its sheets as
    /// <paramref name="viewerOf"/> gives them.
    /// </summary>
    PmapOutlineResult OutlineMap(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf);

    /// <summary>
    /// The state wanted of a map from a document read (the proposal of the assistant, spec 3.0 section 7): its root map,
    /// its lanes, steps and flows with their translations (ADR 081), its sub-maps and the sheets of its steps (ADR 082),
    /// the columns placed as at the opening of a file when the document gives none.
    /// </summary>
    DesiredMap Desired(IPmapDocument document);

    /// <summary>
    /// The `.pmap` of a map and of every level below it, without the sheets, with the translations: what a translation
    /// by the assistant returns translated (ADR 083).
    /// </summary>
    PmapOutlineResult OutlineTree(ProjectWorkspace workspace, Guid mapId);

    /// <summary>
    /// A proposal opened as a new project with what its map of origin holds (ADR 082): the map and every map below it read
    /// with their sheets as <paramref name="viewerOf"/> gives them; each step of the proposal named by one of their codes
    /// takes its sheet and its translations when the proposal gives none.
    /// </summary>
    IPmapDocument Carry(IPmapDocument proposal, ProjectWorkspace origin, Guid mapId, Func<Step, SheetViewer> viewerOf);

    /// <summary>
    /// The model of a duplication (A-1.2-21): the document of a project without its sheets (AR-10), every level of its
    /// sub-maps kept within the walks of the domain (51 maps, AR-67), since no file is written (AR-118).
    /// </summary>
    PmapOutlineResult Model(ProjectWorkspace workspace);

    /// <summary>The text of a document, written by <paramref name="generatedBy"/> at <paramref name="generatedAt"/>.</summary>
    string Write(IPmapDocument document, string generatedBy, DateTime generatedAt);

    /// <summary>A new project from a document, under a code and a name, by the rules of the domain.</summary>
    PmapProjectBuild Build(OperationContext context, IPmapDocument document, string code, string name, ProjectCreationInputs inputs);

    /// <summary>The name of the file of a copy (A-1.2-16): code, short name, day.</summary>
    string FileName(Project project, DateTime exportedAt);
}
