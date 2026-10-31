// TranslationCommands.cs
// The translations of a project: the workbook list, its import in one pass, and the manual translations of a step or a flow (annex A 2.14).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Domain.Model;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Translations;

/// <summary>
/// `GET /api/projects/{id}/translations?scope=missing|auto|all&amp;languages=pt,ja` (1.x `GET /api/projets/{id}/traductions?portee=`,
/// Animateur): the texts of the workbook in reading order. A missing scope is `missing`, an unknown one VALEUR_INVALIDE
/// on `scope`, read after the right so that a non-member learns nothing. The target languages are the ones chosen for
/// the workbook (ADR 083), each a configured one (LANGUE_INVALIDE on `languages`); none given: every configured one.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class GetTranslationsQuery(Guid projectId, string? scope, LanguageSettings languages, IReadOnlyList<string>? targets = null)
    : ProjectQuery<IReadOnlyList<TranslatableText>>(AccessTarget.Project(projectId))
{
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override IReadOnlyList<TranslatableText> Read(ProjectWorkspace workspace, QueryContext context) =>
        TranslationCatalog.List(workspace, TranslationCatalog.ParseScope(scope), Targets(targets, languages));

    /// <summary>The chosen target languages, each configured, in the order of the configuration; every configured one when none is chosen.</summary>
    public static IReadOnlyList<string> Targets(IReadOnlyList<string>? targets, LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        if (targets is null || targets.Count == 0)
            return languages.Languages;
        foreach (var target in targets)
            languages.Require(target, "languages");
        return [.. languages.Languages.Where(targets.Contains)];
    }
}

/// <summary>
/// `PUT /api/projects/{id}/translations` `{scope, rows[]}` (1.x `PUT /api/projets/{id}/traductions`, Animateur): the
/// import in one pass, in the unit of work and under the lock of the project (A-2.14-16). Not undoable (annex B 2.6-43,
/// AR-21). It names no map: the translations reach every map of the project, which the client reads again (1.x
/// `projet.recharger()`, annex B 5).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class ImportTranslationsCommand(Guid projectId, string? scope, IReadOnlyList<TranslationRow> rows, LanguageSettings languages)
    : ProjectCommand<TranslationImportResult>(AccessTarget.Project(projectId))
{
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override Guid? MapOf(ProjectWorkspace workspace) => null;

    public override TranslationImportResult Execute(ProjectWorkspace workspace, OperationContext context) =>
        TranslationImport.Import(workspace, context, TranslationCatalog.ParseScope(scope), rows, languages);
}

/// <summary>
/// `PUT /api/steps/{id}/translations` (new in 2.0, spec 3.6 and 10, A-2.14-06; the 1.x had no route, annex A 6-8):
/// the manual translations of the label of a step, by language; an empty value deletes one, the language of origin is
/// ignored. Animateur, as for the translations of a lane. Not undoable, like every translation (annex B 2.6-43, AR-21).
/// The reply carries the view of the map, which shows the translations. If-Match on the step whose label is translated,
/// as the translations of a lane go with the If-Match of the lane (spec 5.3, ADR 045 revised).
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class UpdateStepTranslationsCommand(Guid stepId, IReadOnlyDictionary<string, string?>? translations, LanguageSettings languages)
    : ProjectCommand<int>(AccessTarget.Step(stepId))
{
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override Guid? MapOf(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.LiveStep(Target.Id).MapId;
    }

    public override int Execute(ProjectWorkspace workspace, OperationContext context) =>
        TranslationRules.WriteManual(workspace, context, TranslationObject.Step, Target.Id, translations, languages);

    /// <summary>The If-Match of the request on the step whose label is translated.</summary>
    public static UpdateStepTranslationsCommand WithVersion(Guid stepId, IReadOnlyDictionary<string, string?>? translations, LanguageSettings languages,
        int? version) =>
        new(stepId, translations, languages) { Versions = version is { } read ? [VersionCheck.Of<Step>(Step.KeyOf(stepId), read)] : [] };
}

/// <summary>
/// `PUT /api/flows/{id}/translations` (new in 2.0, spec 3.6 and 10, A-2.14-06): the manual translations of the label of a
/// flow, with the same rules as a step; the language of origin of a flow is the one of its source step. If-Match on the flow.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class UpdateFlowTranslationsCommand(Guid flowId, IReadOnlyDictionary<string, string?>? translations, LanguageSettings languages)
    : ProjectCommand<int>(AccessTarget.Flow(flowId))
{
    public override WorkspaceContent Content => WorkspaceContent.Translations;

    public override Guid? MapOf(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.LiveFlow(Target.Id).MapId;
    }

    public override int Execute(ProjectWorkspace workspace, OperationContext context) =>
        TranslationRules.WriteManual(workspace, context, TranslationObject.Flow, Target.Id, translations, languages);

    /// <summary>The If-Match of the request on the flow whose label is translated.</summary>
    public static UpdateFlowTranslationsCommand WithVersion(Guid flowId, IReadOnlyDictionary<string, string?>? translations, LanguageSettings languages,
        int? version) =>
        new(flowId, translations, languages) { Versions = version is { } read ? [VersionCheck.Of<Flow>(Flow.KeyOf(flowId), read)] : [] };
}
