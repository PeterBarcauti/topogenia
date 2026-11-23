// ActionPlanQuery.cs
// The action plan of a map and of the maps below it (Makigami M7, plan 3.1), for the sheet « Plan d'actions » of the workbook and the panel of the action plan (ADR 112): each pain point with its step, lane, map and plan, whether it is open, the use that carries it, the right to fill its plan, its version and the instant of its last status change (ADR 116).

using Topogenia.Application.Authorization;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Documents;

/// <summary>A name as the interface shows it in its language: the text entered, the language it was entered in, its translations.</summary>
public sealed record NamedText(string Text, string SourceLanguage, IReadOnlyDictionary<string, string> Translations);

/// <summary>
/// A line of the action plan: the pain point, its plan and status, the step it is raised on, the lane of the step, the
/// map; whether it is open (neither implemented, nor adopted, nor dropped, as the map shows it); the step that receives
/// the document and the document, from the use that carries the pain point (null when none, a package written by hand);
/// whether the person may fill the sheet where its status is chosen, the receiving step (its plan too, ADR 112) or, when
/// no live use carries it, its own step (R10); the version of the pain point, sent back to choose its status, and the
/// instant of the last record of its history (ADR 116).
/// </summary>
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
    NamedText? Lane)
{
    public required bool Open { get; init; }

    public required Guid? ReceivingStepId { get; init; }

    public required Guid? ReceivingMapId { get; init; }

    public required Guid? DocumentId { get; init; }

    public required bool CanFill { get; init; }

    /// <summary>
    /// The step under which its status is chosen (`PUT /api/steps/{id}/pain-points/{painPointId}/status`): the receiving
    /// step of the live use that carries it, else the step it is raised on (R10).
    /// </summary>
    public required Guid StatusStepId { get; init; }

    /// <summary>The version of the pain point, the If-Match of a change of its status (never the version of the use).</summary>
    public required int Version { get; init; }

    /// <summary>The instant of the last change of its status, in universal time; null for a pain point without history (a package may bring none).</summary>
    public required DateTime? StatusChangedAt { get; init; }
}

/// <summary>The action plan of a map (`ActionPlan`), in its order, and the hours lost per month of its open lines, added exactly.</summary>
public sealed record ActionPlanView(IReadOnlyList<ActionPlanLine> Lines, decimal OpenLostHoursPerMonth);

/// <summary>
/// `GET /api/maps/{id}/action-plan` (new in 3.1, Makigami M7, Lecteur): the pain points of the map and of every map below
/// it, as every member reads them on the sheets; INTROUVABLE for a map that is not live. It reads the documents and the
/// translations, never the map view. Every member reads every line; only the right to fill depends on him (ADR 112).
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class GetActionPlanQuery(Guid mapId) : ProjectQuery<ActionPlanView>(AccessTarget.Map(mapId))
{
    public override WorkspaceContent Content => WorkspaceContent.Documents | WorkspaceContent.Translations;

    public override ActionPlanView Read(ProjectWorkspace workspace, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var entries = ActionPlan.Of(workspace, Target.Id);
        var lines = new ActionPlanLines(workspace, context.Role, context.UserId);
        List<ActionPlanLine> plan = [.. entries.Select(lines.LineOf)];
        return new ActionPlanView(plan, ActionPlanLines.OpenLostHoursOf(plan));
    }
}
