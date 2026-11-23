// ActionPlanLines.cs
// The lines of the action plan built in one pass (ADR 112): the translations of the maps, steps and lanes and the uses that carry the pain points indexed once, open and the right to fill read through the rules of the map and of the sheets.

using Topogenia.Domain.Documents;
using Topogenia.Domain.Maps;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Documents;

/// <summary>
/// The lines of the plan of one request, for one viewer. The rule of the translations reads every translation of the
/// project for each object (`TranslationRules.Of`): read for each line of the whole project, it would read them three
/// times a line. The index gives the same names, built once; the use that carries a pain point is found the same way.
/// </summary>
internal sealed class ActionPlanLines
{
    private static readonly IReadOnlyDictionary<string, string> NoTranslation = new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly ProjectWorkspace workspace;
    private readonly MemberRole role;
    private readonly Guid userId;
    private readonly Dictionary<(TranslationObject Type, Guid Id), Dictionary<string, string>> translations = [];
    private readonly Dictionary<Guid, DocumentUse> uses = [];

    public ActionPlanLines(ProjectWorkspace workspace, MemberRole role, Guid userId)
    {
        (this.workspace, this.role, this.userId) = (workspace, role, userId);
        foreach (var translation in workspace.All<Translation>())
        {
            if (translation.ObjectType is not (TranslationObject.Map or TranslationObject.Step or TranslationObject.Lane)
                || translation.Field != TranslationRules.FieldOf(translation.ObjectType))
                continue;
            var key = (translation.ObjectType, translation.ObjectId);
            if (!translations.TryGetValue(key, out var texts))
                translations[key] = texts = new Dictionary<string, string>(StringComparer.Ordinal);
            texts.Add(translation.Language, translation.Text);
        }
        foreach (var use in workspace.All<DocumentUse>())
        {
            if (use.PainPointId is { } painPointId && LiveStep(workspace, use.StepId) is not null)
                uses.TryAdd(painPointId, use);
        }
    }

    /// <summary>The hours lost per month of the open lines, added exactly; a line without hours counts for none.</summary>
    public static decimal OpenLostHoursOf(IEnumerable<ActionPlanLine> lines) =>
        lines.Where(line => line.Open).Sum(line => line.LostHoursPerMonth ?? 0m);

    /// <summary>
    /// A line of the plan: open as the map shows it (<see cref="MapMarkers.IsOpen"/>); the step that receives the document (and its map, which may not be the map of the producer)
    /// and the document, from the live use that carries the pain point (none for a pain point without it); the right to
    /// fill the sheet where its status is chosen: the receiving step, the one the use command demands, or, when no live use
    /// carries it, the step it is raised on (R10), as the rule of the core says; the version of the pain point and the
    /// instant of the latest record of its history, read by the rule of the core (ADR 116).
    /// </summary>
    public ActionPlanLine LineOf(ActionPlanEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var (point, step, map, lane) = entry;
        var use = uses.GetValueOrDefault(point.Id);
        var receiving = use is null ? null : LiveStep(workspace, use.StepId);
        var place = receiving ?? step;
        return new ActionPlanLine(point.Id, point.Code, point.Description, point.Status, point.LostHoursPerMonth, point.Action, point.Owner,
            point.DueDate, map.Id, Named(TranslationObject.Map, map.Id, map.Name, map.SourceLanguage),
            step.Id, step.Code, Named(TranslationObject.Step, step.Id, step.Label, step.SourceLanguage),
            lane is null ? null : Named(TranslationObject.Lane, lane.Id, lane.Name, lane.SourceLanguage))
        {
            Open = MapMarkers.IsOpen(point),
            ReceivingStepId = receiving?.Id,
            ReceivingMapId = receiving?.MapId,
            DocumentId = receiving is null ? null : use!.DocumentId,
            CanFill = !StepTypes.HasNoContent(place.StepType) && SheetRights.ViewerOf(workspace, place, role, userId).CanFill,
            StatusStepId = place.Id,
            Version = point.Version,
            StatusChangedAt = PainPointRules.LatestChange(workspace, point.Id)?.ChangedAt,
        };
    }

    /// <summary>
    /// A live step on a live map, whose sheet may be read and, with content, written; null otherwise. A use of such a step
    /// is a live use: the one that carries a pain point (the sheet of the step reads it the same way, R10).
    /// </summary>
    internal static Step? LiveStep(ProjectWorkspace workspace, Guid stepId) =>
        workspace.Step(stepId) is { DeletedAt: null } step && workspace.Map(step.MapId) is { DeletedAt: null } ? step : null;

    private NamedText Named(TranslationObject type, Guid id, string text, string sourceLanguage) =>
        new(text, sourceLanguage, translations.TryGetValue((type, id), out var texts) ? texts : NoTranslation);
}
