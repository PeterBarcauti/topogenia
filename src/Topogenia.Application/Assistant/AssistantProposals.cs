// AssistantProposals.cs
// The proposal of the assistant on the map open: its preview compared with the scope, levels included, and its application in one operation undone by one Ctrl+Z, refused when the map changed since the preview or when it is for a new project (spec 3.0 sections 3.1-5 to 7, ADR 079, ADR 082).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Exchange;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Proposals;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Assistant;

/// <summary>A change of the preview as the panel lists it: kind, code, text, what becomes of it, the fields changed.</summary>
public sealed record ProposalChangeView(string Kind, string? Code, Guid? Id, string Text, string Action, IReadOnlyList<string> Fields);

/// <summary>A step of the scope that a new project would not carry: its code, label, whether it is detailed and has a sheet.</summary>
public sealed record ProposalLossView(string Code, string Label, bool Detailed, bool HasSheet);

/// <summary>
/// A text a translation by the assistant writes (ADR 083): its kind (`project`, `lane`, `step`, `flow`), identifier, code,
/// text of origin and translation; `kept` for a translation typed by hand that stays, its own text given.
/// </summary>
public sealed record ProposalTranslatedView(string Kind, Guid Id, string Code, string Original, string Text, bool Kept);

/// <summary>
/// The preview of a proposal on the map open: its changes, their counts by kind and action, its warnings, and the structure
/// version of the project it was computed on, which « Appliquer » gives back (If-Match). Its mode (ADR 082): `in_place`, or
/// `new_project` when it brings steps up a level or moves them (its reasons, `raised:CODE`, `moved:CODE`), with the steps of
/// the scope it would not carry; such a proposal is only opened as a new project. A translation (ADR 083) also gives the
/// texts it writes, which the map shows in place before anything is written.
/// </summary>
public sealed record ProposalPreviewView(IReadOnlyList<ProposalChangeView> Changes, IReadOnlyList<string> Warnings, int StructureVersion, bool Empty,
    string Mode, IReadOnlyList<string> Reasons, IReadOnlyList<ProposalLossView> Missing)
{
    /// <summary>The texts a translation writes, in the order of the maps; empty for any other proposal.</summary>
    public IReadOnlyList<ProposalTranslatedView> Translations { get; init; } = [];

    public static ProposalPreviewView Of(ProposalPlanResult plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new ProposalPreviewView(
            [.. plan.Changes.Where(change => change.Action != PlanAction.Kept)
                .Select(change => new ProposalChangeView(change.Kind, change.Code, change.Id, change.Text, Codes.Of(change.Action), change.Fields))],
            plan.Warnings, plan.StructureVersion, plan.IsEmpty, Codes.Of(plan.Mode), plan.Reasons,
            [.. plan.Missing.Select(loss => new ProposalLossView(loss.Code, loss.Label, loss.Detailed, loss.HasSheet))]);
    }
}

/// <summary>Reads the `.pmap` of a proposal into the state wanted of the map (its root map and its sub-maps), FICHIER_INVALIDE with its faults otherwise.</summary>
public static class DesiredMaps
{
    public static DesiredMap Read(IPmapFormat format, string? pmap, LanguageSettings languages, string raciLetters)
    {
        var text = pmap ?? string.Empty;
        if (text.Length > OpenPmapCommand.ContentMax)
            throw new DomainException(ErrorCodes.FileTooLarge, RefusalKind.Invalid, new Dictionary<string, object?> { ["max"] = OpenPmapCommand.ContentMax / (1024 * 1024) });
        return format.Desired(format.Read(text, languages, raciLetters));
    }

    /// <summary>
    /// What is taken of a proposal: all of it, or for a translation (ADR 083) only its translations into the language, a
    /// configured one (LANGUE_INVALIDE on `language`), the version of origin locked.
    /// </summary>
    public static DesiredMap Taken(ProjectWorkspace workspace, Guid mapId, DesiredMap desired, string? language, LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        return language is null ? desired : ProposalPlan.TranslationOf(workspace, mapId, desired, languages.Require(language, "language"));
    }
}

/// <summary>
/// `POST /api/maps/{id}/assistant/preview` (facilitator): the changes the proposal makes to the map and the maps below it,
/// nothing written; its translations checked against the languages configured (ADR 081), its mode and what a new project
/// would leave out (ADR 082). A translation (`language`, ADR 083) takes only the translations into that language.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class PreviewProposalQuery(Guid mapId, DesiredMap desired, LanguageSettings languages) : ProjectQuery<ProposalPreviewView>(AccessTarget.Map(mapId))
{
    /// <summary>The language of a translation by the assistant; null for any other proposal.</summary>
    public string? Language { get; init; }

    /// <summary>The translations and the sheets, which the preview compares with those of the proposal.</summary>
    public override WorkspaceContent Content => WorkspaceContent.Translations | WorkspaceContent.Sheets;

    public override ProposalPreviewView Read(ProjectWorkspace workspace, QueryContext context)
    {
        var taken = DesiredMaps.Taken(workspace, mapId, desired, Language, languages);
        var preview = ProposalPreviewView.Of(ProposalPlan.Of(workspace, mapId, taken, languages));
        return Language is not { } language ? preview : preview with
        {
            Translations = [.. ProposalPlan.TranslationTexts(workspace, mapId, taken, language)
                .Select(text => new ProposalTranslatedView(Codes.Of(text.Type), text.Id, text.Code, text.Original, text.Text, text.Kept))],
        };
    }
}

/// <summary>
/// `POST /api/maps/{id}/assistant/apply` with If-Match (facilitator, spec 3.0 section 3.1-6, 7): the proposal applied to the
/// map by the rules of the domain, one operation of the log, undone by one Ctrl+Z; the structure version of the project
/// read with the preview checked first: a map changed since refuses the stale preview (CONFLIT_VERSION). Its translations
/// are written as automatic ones, never over a manual one (ADR 081); the translations of the project are read with the
/// map the command gives back (`MapViewBuilder.Content`).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ApplyProposalCommand(Guid mapId, DesiredMap desired, int? structureVersion, LanguageSettings languages)
    : ProjectCommand<ProposalPlanResult>(AccessTarget.Map(mapId))
{
    /// <summary>The language of a translation by the assistant (ADR 083): only its translations into it are written; null for any other proposal.</summary>
    public string? Language { get; init; }

    public override string? LogKind => OperationKinds.Proposal;

    public override Guid? MapOf(ProjectWorkspace workspace) => Target.Id;

    public override ProposalPlanResult Execute(ProjectWorkspace workspace, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        // The lock of the command moved the structure version on by one before the load (AR-111): the preview read the one before.
        if (structureVersion is { } read && read != workspace.Project.StructureVersion - 1)
            throw DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
            {
                ["object"] = "map", ["updated_by"] = workspace.Project.UpdatedBy, ["updated_at"] = workspace.Project.UpdatedAt,
            });
        return ProposalPlan.Apply(workspace, context, mapId, DesiredMaps.Taken(workspace, mapId, desired, Language, languages), languages);
    }
}
