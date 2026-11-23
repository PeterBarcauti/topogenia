// ProjectSearchWalk.cs
// The walk of a search through the project (ADR 110): the maps of the tree, their lanes and steps, the indexes built once and the choice of the candidate sheets.

using Topogenia.Application.Documents;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Search;

/// <summary>A search: the words folded, all found in one text (R9), the language shown (null: the language of origin alone), the map put first (null or unknown: none).</summary>
public sealed record SearchRequest(SearchTerms Terms, string? Language, Guid? MapId);

/// <summary>
/// The walk of a search: the live maps reachable from the root (<see cref="ProjectWorkspace.Subtree"/>), the map asked
/// first then the others in the order of the subtree; in a map its lanes, then its steps in reading order, and for each
/// step the step itself, its documents in entry order, the tools of its sheet as the viewer sees it, its pain points by
/// code. One result per object; <see cref="ProjectSearchQuery.HitsMax"/> results kept, all of them counted.
/// </summary>
public static class ProjectSearchWalk
{
    /// <summary>
    /// The reply of a search, the tools read through <paramref name="viewerOf"/> (the rule of the sheets, called, never
    /// rewritten). Narrowed, the rule is called only for the steps whose sheet may hold the word, on the records it reads
    /// for each of them (<see cref="SheetIndex"/>); otherwise for every step on the whole project: the reference the
    /// narrowing is held to by its tests.
    /// </summary>
    public static ProjectSearchView Run(ProjectWorkspace workspace, SearchRequest request, Func<Step, SheetViewer> viewerOf, bool narrowed = true)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(viewerOf);
        var sheets = new SheetIndex(workspace);
        var walk = new Walk(workspace, request, viewerOf, sheets, narrowed);
        foreach (var mapId in MapsInOrder(workspace, request.MapId))
            walk.Map(mapId);
        return walk.View();
    }

    /// <summary>The tools of a content: the field « Outils », then the tool of each line of the procedure.</summary>
    private static IEnumerable<string?> ToolsOf(SheetContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.Procedure.Select(task => task.Tool).Prepend(content.Tools);
    }

    private static IReadOnlyList<Guid> MapsInOrder(ProjectWorkspace workspace, Guid? asked)
    {
        var tree = workspace.Subtree(workspace.RootMap.Id);
        return asked is { } first && tree.Contains(first) ? [first, .. tree.Where(id => id != first)] : tree;
    }

    /// <summary>
    /// The sheets of the project indexed once, for the rule of the sheets reads every sheet, procedure line and version
    /// of the project for each step (`SheetQueries`): on five thousand steps of ten lines, calling it on the whole
    /// project for every step that holds a common word takes seconds. The index never decides what is shown: it chooses
    /// the candidates, and gives the rule, for each of them, a workspace of the records it reads for that step.
    /// </summary>
    private sealed class SheetIndex(ProjectWorkspace workspace)
    {
        private readonly ILookup<Guid, StepSheet> sheets = workspace.All<StepSheet>().ToLookup(sheet => sheet.StepId);
        private readonly ILookup<Guid, SheetOperation> operations = workspace.All<SheetOperation>().ToLookup(operation => operation.SheetId);
        private readonly ILookup<Guid, StepSheetVersion> versions = workspace.All<StepSheetVersion>().ToLookup(version => version.SheetId);

        /// <summary>The language of the sheet of a step (the first one, as `SheetQueries.SheetOf` reads it), or null.</summary>
        public string? LanguageOf(Guid stepId) => sheets[stepId].FirstOrDefault()?.SourceLanguage;

        /// <summary>
        /// Whether the sheet of a step may show the words to someone: its draft or its latest validated version holds them all in
        /// one tool. The rule shows the draft or the latest version, never an older one (`SheetVisibility.VisibleContent`):
        /// a step that is not a candidate cannot show the words to anyone, whatever the rule gives him, and an older version
        /// is never decoded.
        /// </summary>
        public bool IsCandidate(Guid stepId, SearchTerms terms) => sheets[stepId].Any(sheet =>
            operations[sheet.Id].Select(operation => operation.Tool).Prepend(sheet.Tools)
                .Concat(LatestOf(sheet.Id).SelectMany(version => ToolsOf(SheetJson.Thaw(version.ContentJson).Content)))
                .Any(tool => terms.In(tool) is not null));

        /// <summary>
        /// The validated version of highest number, as `SheetQueries.LatestVersion` chooses it (the versions of a sheet keep
        /// the order of the project, so that an equal number gives the same version), or none; decoded only when the draft
        /// does not hold the word.
        /// </summary>
        private IEnumerable<StepSheetVersion> LatestOf(Guid sheetId) =>
            versions[sheetId].MaxBy(version => version.VersionNumber) is { } latest ? [latest] : [];

        /// <summary>The project, the step with its lane and map, its sheets with their procedure and versions: what the rule reads for it.</summary>
        public ProjectWorkspace RecordsOf(Step step)
        {
            var records = new List<IRecord> { workspace.Project, step };
            records.AddRange(new IRecord?[] { workspace.Lane(step.LaneId), workspace.Map(step.MapId) }.OfType<IRecord>());
            foreach (var sheet in sheets[step.Id])
                records.AddRange([sheet, .. operations[sheet.Id], .. versions[sheet.Id]]);
            return new ProjectWorkspace(workspace.ProjectId, records);
        }
    }

    /// <summary>The state of one walk: the indexes built once, the results kept and the total.</summary>
    private sealed class Walk
    {
        private static readonly IReadOnlyDictionary<string, string> NoTranslation = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly ProjectWorkspace workspace;
        private readonly SearchTerms terms;
        private readonly string? language;
        private readonly Func<Step, SheetViewer> viewerOf;
        private readonly SheetIndex sheets;
        private readonly bool narrowed;
        private readonly Dictionary<(TranslationObject Type, Guid Id), string> translations = [];
        private readonly ILookup<Guid, Document> documents;
        private readonly ILookup<Guid, PainPoint> painPoints;
        private readonly List<SearchMapView> maps = [];
        private int total;
        private int kept;

        public Walk(ProjectWorkspace workspace, SearchRequest request, Func<Step, SheetViewer> viewerOf, SheetIndex index, bool narrowed)
        {
            this.workspace = workspace;
            (terms, language) = (request.Terms, request.Language);
            this.viewerOf = viewerOf;
            (sheets, this.narrowed) = (index, narrowed);
            foreach (var translation in workspace.All<Translation>())
            {
                if (translation.Language == language && translation.Field == TranslationRules.FieldOf(translation.ObjectType))
                    translations.TryAdd((translation.ObjectType, translation.ObjectId), translation.Text);
            }
            documents = workspace.All<Document>().ToLookup(document => document.StepId);
            painPoints = workspace.All<PainPoint>().Where(point => point.StepId is not null).ToLookup(point => point.StepId!.Value);
        }

        public ProjectSearchView View() => new(total, maps);

        public void Map(Guid mapId)
        {
            if (workspace.Map(mapId) is not { DeletedAt: null } map)
                return;
            var hits = new List<SearchHitView>();
            foreach (var lane in workspace.LanesOf(mapId))
            {
                if (NameMatch(TranslationObject.Lane, lane.Id, lane.Name, lane.SourceLanguage) is { } name)
                    Add(hits, SearchHitView.Of(SearchKind.Lane, null, lane.Id, name.Text, name.Language, name.Marks));
            }
            foreach (var step in workspace.ReadingOrder(mapId))
                StepHits(step, hits);
            if (hits.Count > 0)
                maps.Add(new SearchMapView(map.Id, workspace.Depth(map.Id), Named(TranslationObject.Map, map.Id, map.Name, map.SourceLanguage), hits));
        }

        private void StepHits(Step step, List<SearchHitView> hits)
        {
            if (NameMatch(TranslationObject.Step, step.Id, step.Label, step.SourceLanguage) is { } name)
                Add(hits, SearchHitView.Of(SearchKind.Step, step.Id, null, name.Text, name.Language, name.Marks));
            else if (terms.Marks(step.Code) is { } code)
                Add(hits, SearchHitView.Of(SearchKind.Step, step.Id, null, step.Code, null, code));
            foreach (var document in DocumentRules.InEntryOrder(documents[step.Id]))
            {
                if (terms.Marks(document.Name) is { } match)
                    Add(hits, OfStep(SearchKind.Document, step, document.Name, document.SourceLanguage, match));
            }
            ToolHits(step, hits);
            foreach (var point in painPoints[step.Id].OrderBy(point => point.Code, StringComparer.Ordinal))
                PainPointHit(point, step, hits);
        }

        /// <summary>
        /// The tools of the content the rule of the sheets lets the viewer see (its viewer read on the whole project);
        /// narrowed, for the candidates only, on the records the rule reads for the step. A step without content has none.
        /// </summary>
        private void ToolHits(Step step, List<SearchHitView> hits)
        {
            if (StepTypes.HasNoContent(step.StepType) || (narrowed && !sheets.IsCandidate(step.Id, terms)))
                return;
            var viewer = viewerOf(step);
            if (SheetVisibility.VisibleContent(narrowed ? sheets.RecordsOf(step) : workspace, step.Id, viewer) is not { } content)
                return;
            var sheetLanguage = sheets.LanguageOf(step.Id) ?? step.SourceLanguage;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tool in ToolsOf(content))
            {
                if (tool is not null && seen.Add(tool) && terms.Marks(tool) is { } match)
                    Add(hits, OfStep(SearchKind.Tool, step, tool, sheetLanguage, match));
            }
        }

        /// <summary>A pain point by its description, else its code, else its action; its owner, a name of person, is not searched.</summary>
        private void PainPointHit(PainPoint point, Step step, List<SearchHitView> hits)
        {
            var hit = terms.Marks(point.Description) is { } described ? OfStep(SearchKind.PainPoint, step, point.Description, point.SourceLanguage, described)
                : terms.Marks(point.Code) is { } coded ? OfStep(SearchKind.PainPoint, step, point.Code, null, coded)
                : terms.Marks(point.Action) is { } acted ? OfStep(SearchKind.Action, step, point.Action!, point.SourceLanguage, acted)
                : null;
            if (hit is not null)
                Add(hits, hit with { Status = point.Status });
        }

        private SearchHitView OfStep(SearchKind kind, Step step, string text, string? textLanguage, IReadOnlyList<TextMatch> marks) =>
            SearchHitView.Of(kind, step.Id, null, text, textLanguage, marks) with
            {
                StepCode = step.Code,
                Step = Named(TranslationObject.Step, step.Id, step.Label, step.SourceLanguage),
            };

        /// <summary>A name in the language shown when it is translated into it (as the interface shows it), else in its language of origin.</summary>
        private (string Text, string? Language, IReadOnlyList<TextMatch> Marks)? NameMatch(TranslationObject type, Guid id, string original, string sourceLanguage)
        {
            if (language is not null && language != sourceLanguage && translations.TryGetValue((type, id), out var shown) && terms.Marks(shown) is { } inShown)
                return (shown, language, inShown);
            return terms.Marks(original) is { } inOriginal ? (original, sourceLanguage, inOriginal) : null;
        }

        /// <summary>A name with its translations reduced to the language shown.</summary>
        private NamedText Named(TranslationObject type, Guid id, string text, string sourceLanguage) =>
            new(text, sourceLanguage, language is not null && translations.TryGetValue((type, id), out var translated)
                ? new Dictionary<string, string>(StringComparer.Ordinal) { [language] = translated }
                : NoTranslation);

        private void Add(List<SearchHitView> hits, SearchHitView hit)
        {
            total++;
            if (kept >= ProjectSearchQuery.HitsMax)
                return;
            hits.Add(hit);
            kept++;
        }
    }
}
