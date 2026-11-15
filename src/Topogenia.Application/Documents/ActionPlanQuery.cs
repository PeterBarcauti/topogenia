// ActionPlanQuery.cs
// The action plan of a map and of the maps below it (Makigami M7, plan 3.1), for the sheet « Plan d'actions » of the workbook: each pain point with its step, lane, map and plan.

using Topogenia.Application.Authorization;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Documents;

/// <summary>A name as the interface shows it in its language: the text entered, the language it was entered in, its translations.</summary>
public sealed record NamedText(string Text, string SourceLanguage, IReadOnlyDictionary<string, string> Translations);

/// <summary>A line of the action plan: the pain point, its plan and status, the step it is raised on, the lane of the step, the map.</summary>
public sealed record ActionPlanLine(
    Guid PainPointId,
    string Code,
    string Description,
    PainPointStatus Status,
    decimal? LostHoursPerMonth,
    string? Action,
    string? Owner,
    DateOnly? DueDate,
    Guid MapId,
    NamedText Map,
    Guid StepId,
    string StepCode,
    NamedText Step,
    NamedText? Lane);

/// <summary>The action plan of a map (`ActionPlan`), in its order.</summary>
public sealed record ActionPlanView(IReadOnlyList<ActionPlanLine> Lines);

/// <summary>
/// `GET /api/maps/{id}/action-plan` (new in 3.1, Makigami M7, Lecteur): the pain points of the map and of every map below
/// it, as every member reads them on the sheets; INTROUVABLE for a map that is not live. It reads the documents and the
/// translations, never the map view.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class GetActionPlanQuery(Guid mapId) : ProjectQuery<ActionPlanView>(AccessTarget.Map(mapId))
{
    public override WorkspaceContent Content => WorkspaceContent.Documents | WorkspaceContent.Translations;

    public override ActionPlanView Read(ProjectWorkspace workspace, QueryContext context) =>
        new([.. ActionPlan.Of(workspace, Target.Id).Select(entry => LineOf(workspace, entry))]);

    private static ActionPlanLine LineOf(ProjectWorkspace workspace, ActionPlanEntry entry)
    {
        var (point, step, map, lane) = entry;
        return new ActionPlanLine(point.Id, point.Code, point.Description, point.Status, point.LostHoursPerMonth, point.Action, point.Owner,
            point.DueDate, map.Id, new NamedText(map.Name, map.SourceLanguage, Texts(workspace, TranslationObject.Map, map.Id)),
            step.Id, step.Code, new NamedText(step.Label, step.SourceLanguage, Texts(workspace, TranslationObject.Step, step.Id)),
            lane is null ? null : new NamedText(lane.Name, lane.SourceLanguage, Texts(workspace, TranslationObject.Lane, lane.Id)));
    }

    private static Dictionary<string, string> Texts(ProjectWorkspace workspace, TranslationObject type, Guid id) =>
        TranslationRules.Of(workspace, type, id).ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.Ordinal);
}
