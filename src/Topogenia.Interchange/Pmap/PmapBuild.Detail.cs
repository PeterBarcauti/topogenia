// PmapBuild.Detail.cs
// A document built as the detail of a step of a project that exists (3.3, F-208): its maps, lanes, steps, flows, RACI, routes, summaries, translations, sheets and comments, by the same rules as the opening of the file.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Interchange.Pmap;

/// <summary>
/// The detail built from a document under a step: its map, what it holds, and the lines of the comments of presentation of
/// its new steps and flows (1.5), which the application writes beside the core.
/// </summary>
public sealed record PmapDetailBuilt(Guid MapId, PmapCounts Counts)
{
    public IReadOnlyList<PresentationComment> Comments { get; init; } = [];

    public IReadOnlyList<FlowPresentationComment> FlowComments { get; init; } = [];
}

public static partial class PmapBuild
{
    /// <summary>
    /// Builds the root map of a document as the detail of a live step without one (3.3, F-208): the sub-map takes the label,
    /// the language and the translations of its step, as "Detail" makes it (`SubMapRules.Detail`), and its first lane from
    /// the file; then the rest of the file, as an opening builds it (its sheets as drafts when the document carries them).
    /// The texts of the file keep its language, their language of origin. A refusal of a rule is FICHIER_INVALIDE with its
    /// place in the file. The project must not break any rule of the structure it did not break before
    /// (`ImportedProjects.Violations`, compared, so that an older fault of the project never blocks an insertion).
    /// </summary>
    public static PmapDetailBuilt Detail(OperationContext context, ProjectWorkspace workspace, PmapDocument document, Guid stepId, LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(languages);
        var before = ImportedProjects.Violations(workspace).ToHashSet(StringComparer.Ordinal);
        var builder = new Builder(workspace, context, document.Project.Language, languages);
        var mapId = builder.Detail(workspace.LiveStep(stepId), document.Map);
        builder.Sheets();
        var violations = ImportedProjects.Violations(workspace).Where(violation => !before.Contains(violation)).ToList();
        if (violations.Count > 0)
            throw new InvalidOperationException("A checked file built a detail that breaks the rules of the structure: " + string.Join(", ", violations));
        return new PmapDetailBuilt(mapId, new PmapCounts(builder.Maps, builder.Lanes, builder.Steps, builder.Flows, builder.SheetCount))
        {
            Comments = builder.StepComments(),
            FlowComments = builder.FlowComments(),
        };
    }

    private sealed partial class Builder
    {
        /// <summary>The detail of a step made from a map of the file, as "Detail" names it, its first lane from the file.</summary>
        public Guid Detail(Step step, PmapMap map)
        {
            var sub = new ProcessMap(context.NewId(), workspace.ProjectId, step.Id, step.Label, step.SourceLanguage, context.Now, context.ActorId,
                null, null, null, null, 1);
            workspace.Put(sub);
            TranslationRules.Copy(workspace, context, TranslationObject.Step, step.Id, TranslationObject.Map, sub.Id);
            var file = map.Lanes[0];
            var lane = StructureEdits.NewLane(workspace, context, sub.Id, file.Name, language, Codes.Parse<LaneType>(file.Type, "type"),
                file.Color ?? LaneColors.ForIndex(0), 0);
            Fill(sub.Id, lane, map, "map");
            return sub.Id;
        }
    }
}
