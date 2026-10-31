// ProjectTranslationApi.cs
// The routes of the translations of a project turned into their requests: the texts of the workbook, its import in one pass (annex A 1, A 2.14).

using Topogenia.Application.Commands;
using Topogenia.Application.Projects;
using Topogenia.Application.Queries;
using Topogenia.Application.Translations;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Api.Translations;

/// <summary>A row of the workbook (1.x `LigneTraduite`: `ligne`, `type`, `id`, `original`, `traductions`).</summary>
public sealed record TranslationRowBody(int? Line, string? Type, Guid? Id, string? Original, Dictionary<string, string?>? Translations);

/// <summary>`PUT /api/projects/{id}/translations` (1.x `ImportTraductions`: `portee`, `lignes`).</summary>
public sealed record ImportTranslationsBody(string? Scope, List<TranslationRowBody>? Rows);

/// <summary>
/// The texts of the workbook (1.x `ListeTraductions`: `projet_id`, `projet_code`, `portee`, `langues`, `textes`): the
/// project, the scope read, the languages that are the columns of the workbook (annex B 5: the languages of origin of
/// the texts first, then the chosen targets, each run in the order of the configuration, ADR 083), and the texts to
/// translate in reading order.
/// </summary>
public sealed record TranslationList(Guid ProjectId, string ProjectCode, string Scope, IReadOnlyList<string> Languages, IReadOnlyList<TranslatableText> Texts);

/// <summary>The outcome of an import (1.x `ResultatTraductions`: `enregistrees`, `ignorees[ligne, raison]`).</summary>
public sealed record TranslationImportReply(int Written, IReadOnlyList<IgnoredRow> Ignored);

/// <summary>Reads the query string and the body of the translation routes into the requests of the domain and runs them.</summary>
public sealed class ProjectTranslationApi
{
    private readonly CommandRunner commands;
    private readonly QueryRunner queries;
    private readonly LanguageSettings languages;

    public ProjectTranslationApi(CommandRunner commands, QueryRunner queries, LanguageSettings languages)
    {
        this.commands = commands;
        this.queries = queries;
        this.languages = languages;
    }

    /// <summary>
    /// `GET /api/projects/{id}/translations?scope=&amp;languages=`: the scope and the target languages (separated by commas)
    /// are read by the application after the right.
    /// </summary>
    public async Task<TranslationList> ListAsync(Guid projectId, string? scope, string? targets, CancellationToken cancellationToken)
    {
        var chosen = string.IsNullOrWhiteSpace(targets) ? null : targets.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var texts = await queries.RunAsync(new GetTranslationsQuery(projectId, scope, languages, chosen), cancellationToken);
        var project = await queries.RunAsync(new GetProjectQuery(projectId), cancellationToken);
        var columns = GetTranslationsQuery.Targets(chosen, languages);
        // The languages of origin of the texts first, the column a translator reads from, then the targets.
        var origins = languages.Languages.Where(language => texts.Any(text => text.Language == language)).ToList();
        var shown = origins.Concat(languages.Languages.Where(language => columns.Contains(language) && !origins.Contains(language))).ToList();
        return new TranslationList(project.Id, project.Code, Codes.Of(TranslationCatalog.ParseScope(scope)), shown, texts);
    }

    public async Task<TranslationImportReply> ImportAsync(Guid projectId, ImportTranslationsBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var rows = ApiInput.Required(body.Rows, "rows").Select(Row).ToList();
        var outcome = await commands.RunAsync(new ImportTranslationsCommand(projectId, body.Scope, rows, languages), cancellationToken);
        return new TranslationImportReply(outcome.Result.Written, outcome.Result.Ignored);
    }

    private static TranslationRow Row(TranslationRowBody row) =>
        new(ApiInput.Required(row.Line, "line"), row.Type, ApiInput.Required(row.Id, "id"), row.Original, row.Translations);
}
