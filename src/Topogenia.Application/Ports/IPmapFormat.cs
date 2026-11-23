// IPmapFormat.cs
// Port of the .pmap format (spec 7.5, ADR 054): read a file, outline a project, write a document, build a new project from one.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
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

    /// <summary>The distinct addresses of its documents that reach another machine (web, network path), said before a link is followed (pre-audit 68, P3-02).</summary>
    int RemoteLocations { get; }

    /// <summary>What the document holds, every level of its maps counted (the proposal of the assistant, spec 3.0 section 3.4).</summary>
    PmapCounts Counts { get; }
}

/// <summary>The maps, lanes, steps, flows and sheets of a document.</summary>
public sealed record PmapCounts(int Maps, int Lanes, int Steps, int Flows, int Sheets);

/// <summary>A project outlined as a document, and whether its sub-maps went deeper than the level written (A-2.17-47).</summary>
public sealed record PmapOutlineResult(IPmapDocument Document, bool DepthCut);

/// <summary>
/// The records of a new project built from a document, as changes to write, and what it holds; the lines of the comments
/// of presentation of its new steps and flows (1.5), kept beside the core and written by the application after them.
/// </summary>
public sealed record PmapProjectBuild(Project Project, Guid RootMapId, ChangeSet Changes, int Maps, int Lanes, int Steps, int Flows, int Sheets)
{
    public IReadOnlyList<PresentationComment> Comments { get; init; } = [];

    public IReadOnlyList<FlowPresentationComment> FlowComments { get; init; } = [];
}

/// <summary>
/// What a whole copy carries beside the structure (`.pmap` 1.5, ADR 105): the lines of the comments of presentation of the
/// steps and of the flows, kept beside the core and read by the application. Given, the outline writes them with the
/// glossary of the project and the code and the status of each pain point; not given (the context of the assistant),
/// it writes none of the four.
/// </summary>
public sealed record PmapExtras(IReadOnlyList<PresentationComment> StepComments, IReadOnlyList<FlowPresentationComment> FlowComments);

/// <summary>
/// A file read to be inserted as the detail of a step (3.3, F-208): the document, the language of its texts and the
/// translations of the name of its project (the label proposed for the step and its translations), the levels of its maps,
/// and the languages of the translations left out because they are not configured.
/// </summary>
public sealed record PmapInsertionFile(IPmapDocument Document, string Language, IReadOnlyDictionary<string, string> NameTranslations, int Depth,
    IReadOnlyList<string> DroppedLanguages);

/// <summary>
/// A template collection read (ADR 114): its name and its description, and each of its processes in the order of the file.
/// </summary>
public sealed record PmapCollection(string Name, string? Description, IReadOnlyList<PmapCollectionItem> Processes);

/// <summary>
/// A process of a collection: its rank, its text as the host read it (the one the opening of a file is then sent), and
/// its document when the reader of the `.pmap` accepts it, its faults (30 at most) otherwise; the language of its texts
/// and its description, shown by the preview (ADR 114 point 4), when it has a document.
/// </summary>
public sealed record PmapCollectionItem(int Index, string Content, IPmapDocument? Document, IReadOnlyList<FileError> Faults)
{
    public string? Name { get; init; }

    public string? Language { get; init; }

    public string? Description { get; init; }
}

/// <summary>
/// The detail of a step built from a file (3.3, F-208): its map, what it holds, and the lines of the comments of
/// presentation of its new steps and flows (1.5), kept beside the core and written by the application after them.
/// </summary>
public sealed record PmapDetailBuild(Guid MapId, PmapCounts Counts)
{
    public IReadOnlyList<PresentationComment> Comments { get; init; } = [];

    public IReadOnlyList<FlowPresentationComment> FlowComments { get; init; } = [];
}

/// <summary>
/// The `.pmap` format, implemented by the interchange: the application hands texts and workspaces and gets documents,
/// texts and records back, never the format itself.
/// </summary>
public interface IPmapFormat
{
    /// <summary>Reads a file; FICHIER_INVALIDE with its faults (`FileError`, 30 at most) when it breaks a rule.</summary>
    IPmapDocument Read(string content, LanguageSettings languages, string raciLetters);

    /// <summary>
    /// Reads the proposal of the assistant as <see cref="Read"/> reads a file, without the routes of its flows: a proposal
    /// never sets a route, and a route it copies is neither checked nor kept (ADR 091).
    /// </summary>
    IPmapDocument ReadProposal(string content, LanguageSettings languages, string raciLetters);

    /// <summary>
    /// Reads a file to insert as the detail of a step (3.3, F-208) as <see cref="Read"/> reads it, except that a translation
    /// or a line of a comment in a language that is not configured is left out and its language listed, not a fault.
    /// </summary>
    PmapInsertionFile ReadInsertion(string content, LanguageSettings languages, string raciLetters);

    /// <summary>
    /// Reads a template collection (ADR 114): FICHIER_INVALIDE with its faults when its envelope breaks a rule, otherwise
    /// each process read as <see cref="Read"/> reads a file, a process refused alone with its faults.
    /// </summary>
    PmapCollection ReadCollection(string content, LanguageSettings languages, string raciLetters);

    /// <summary>
    /// Builds the root map of a document read by <see cref="ReadInsertion"/> as the detail of a live step without one, and
    /// every level below it, by the rules of the opening of a file: RACI, routes, colours, summaries, translations, and the
    /// sheets as drafts when <paramref name="withSheets"/> is true (none otherwise). FICHIER_INVALIDE with its place in the
    /// file when a rule refuses it.
    /// </summary>
    PmapDetailBuild Detail(OperationContext context, ProjectWorkspace workspace, IPmapDocument document, Guid stepId, LanguageSettings languages, bool withSheets);

    /// <summary>
    /// The document of a project: its sheets when <paramref name="viewerOf"/> is given, each as that viewer sees it,
    /// none otherwise (a duplication copies no sheet, AR-10); what a whole copy carries besides when
    /// <paramref name="extras"/> is given (1.5).
    /// </summary>
    PmapOutlineResult Outline(ProjectWorkspace workspace, Func<Step, SheetViewer>? viewerOf, PmapExtras? extras);

    /// <summary>
    /// The document of one map of a project, as the map open in the panel of the assistant (spec 3.0 section 3.1): the map
    /// named under the project, without its sub-maps (the schema of the project gives them), its sheets as
    /// <paramref name="viewerOf"/> gives them.
    /// </summary>
    PmapOutlineResult OutlineMap(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf);

    /// <summary>
    /// The document of a map and of the maps detailed below it down to the levels of the format, the routes of its flows
    /// kept, its sheets as <paramref name="viewerOf"/> gives them: the copy an image of the map carries to be opened again
    /// (plan 3.1, L7); what a whole copy carries besides when <paramref name="extras"/> is given (1.5).
    /// </summary>
    PmapOutlineResult OutlineBranch(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf, PmapExtras? extras);

    /// <summary>
    /// What the view in BPMN 2.0 draws of a map (ADR 106), read without any event: the map alone, without its sub-maps (the
    /// view knows its detailed steps from the map), the routes of its flows kept, its sheets as <paramref name="viewerOf"/>
    /// gives them, without the place of any document nor the comments and the glossary a whole copy carries; the code and
    /// the status of each pain point kept, as the map shows them (audit of the copy 65); no more than the map shows, never
    /// a copy of the branch to take away (audit of the copy 64, P2-03).
    /// </summary>
    PmapOutlineResult OutlineView(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf);

    /// <summary>
    /// What the project as PDF prints of a map and of the maps detailed below it (3.4, lot 5, ADR 113): the branch of
    /// <see cref="OutlineBranch"/> down to the levels of the format, its sheets as <paramref name="viewerOf"/> gives them,
    /// without the place of any document nor the comments and the glossary a whole copy carries (arbitration 9 of the
    /// lot: the host serves nothing the PDF does not print); the code and the status of each pain point kept, by which a
    /// sheet cites it.
    /// </summary>
    PmapOutlineResult OutlinePrint(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf);

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
    /// sub-maps kept within the walks of the domain (51 maps, AR-67), since no file is written (AR-118); the comments of
    /// presentation and the glossary when <paramref name="extras"/> is given (1.5).
    /// </summary>
    PmapOutlineResult Model(ProjectWorkspace workspace, PmapExtras? extras);

    /// <summary>The text of a document, written by <paramref name="generatedBy"/> at <paramref name="generatedAt"/>.</summary>
    string Write(IPmapDocument document, string generatedBy, DateTime generatedAt);

    /// <summary>A new project from a document, under a code and a name, by the rules of the domain.</summary>
    PmapProjectBuild Build(OperationContext context, IPmapDocument document, string code, string name, ProjectCreationInputs inputs);

    /// <summary>The name of the file of a copy (A-1.2-16): code, short name, day.</summary>
    string FileName(Project project, DateTime exportedAt);
}
