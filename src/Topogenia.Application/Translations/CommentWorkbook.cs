// CommentWorkbook.cs
// The comments of presentation of the steps in the workbook of the translations (ADR 099 revised): their rows listed after the texts of the closed core, their import planned by the rules shared with the comments of the flows, and the import of a whole workbook with the same refusal of the whole file.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Translations;

/// <summary>
/// A comment of presentation and its translations of today, as a row of the workbook: its step and its readable code,
/// its context (`map / lane`, as a step), the language of origin of the step and the original, then the translations
/// and the source of each (`manual`, `assistant`, `workbook`).
/// </summary>
public sealed record CommentText(
    Guid StepId,
    string Code,
    string Context,
    string Language,
    string Original,
    IReadOnlyDictionary<string, string> Translations,
    IReadOnlyDictionary<string, string> Sources);

/// <summary>
/// The comments of presentation of the steps in the workbook of the translations of a project, and the import of a
/// workbook with its rows of comments, those of the steps and those of the flows (<see cref="FlowCommentWorkbook"/>).
/// What a row of comment does is written once, in <see cref="CommentRows"/>.
/// </summary>
public static class CommentWorkbook
{
    /// <summary>The type of a row of the workbook that is the comment of a step: not one of the objects the closed core translates.</summary>
    public const string RowType = PresentationCommentRules.ObjectType;

    /// <summary>
    /// The comments to translate, in the reading order of the steps: those of the live steps of the live maps that have
    /// an original, filtered by the scope on the target languages as the texts of the core are
    /// (<see cref="TranslationCatalog.IsGiven"/>): `missing` when a target language lacks, `auto` when one lacks or was
    /// written by the assistant or a workbook, `all` always.
    /// </summary>
    public static IReadOnlyList<CommentText> List(ProjectWorkspace workspace, IReadOnlyList<PresentationComment> lines, TranslationScope scope,
        IReadOnlyCollection<string> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        return [.. Originals(workspace, lines).Select(Describe).Where(text => IsGiven(text, scope, targets))];
    }

    /// <summary>True when a comment is given for the scope: some target language (not the language of origin) qualifies.</summary>
    public static bool IsGiven(CommentText text, TranslationScope scope, IReadOnlyCollection<string> targets)
    {
        ArgumentNullException.ThrowIfNull(text);
        return CommentRows.IsGiven(text.Language, text.Translations, text.Sources, scope, targets);
    }

    /// <summary>
    /// The import of the rows of comments of steps planned, as the closed core plans its own rows
    /// (<see cref="TranslationImport"/>): a row is ignored when its step has no original any more (OBJET_INTROUVABLE),
    /// when its original differs from the one of today (TEXTE_MODIFIE), when a translation typed by hand exists and the
    /// scope is not `all` (TRADUCTION_MANUELLE), or when it brings no translation (SANS_TRADUCTION). The languages that
    /// are not configured, the language of origin and the empty cells are skipped; an invalid translation is an error
    /// of the file. Nothing is written here (<see cref="CommentRows.Plan"/>).
    /// </summary>
    public static CommentImportPlan Plan(ProjectWorkspace workspace, IReadOnlyList<PresentationComment> lines, TranslationScope scope,
        IReadOnlyList<TranslationRow> rows, LanguageSettings languages) =>
        CommentRows.Plan(Originals(workspace, lines), scope, rows, languages);

    /// <summary>
    /// The lines the plan writes, with the source `workbook`: a translation already written by a workbook with the same
    /// text is left as it is, so that the same file imported again writes nothing more; of two rows that name the same
    /// comment, the last one is written, as the closed core writes its rows one after the other.
    /// </summary>
    public static IReadOnlyList<CommentWrite> Writes(CommentImportPlan plan, OperationContext context) =>
        [.. CommentRows.Writes(plan, context).Select(CommentWrite.Of)];

    /// <summary>True for a row of the workbook that is the comment of presentation of a step.</summary>
    public static bool IsComment(TranslationRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return string.Equals(row.Type, RowType, StringComparison.Ordinal);
    }

    /// <summary>The bound of a workbook, on all its rows, comments included: VALEUR_INVALIDE on `rows` beyond (<see cref="TranslationImport.MaxRows"/>).</summary>
    public static void EnsureBound(IReadOnlyList<TranslationRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > TranslationImport.MaxRows)
            throw DomainException.InvalidValue("rows");
    }

    /// <summary>
    /// The import of a workbook: its rows of the closed core written by the domain, its rows of comments, those of the
    /// steps and those of the flows, as planned (<paramref name="plan"/>, written afterwards in the same unit of work).
    /// One invalid translation, of a text of the core or of a comment, refuses the whole file, TRADUCTIONS_INVALIDES,
    /// with the errors of all by line; the outcome counts the translations of all and lists the rows ignored of all, by
    /// line.
    /// </summary>
    public static TranslationImportResult Import(ProjectWorkspace workspace, OperationContext context, TranslationScope scope,
        IReadOnlyList<TranslationRow> rows, LanguageSettings languages, CommentImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        EnsureBound(rows);
        TranslationImportResult core;
        try
        {
            core = TranslationImport.Import(workspace, context, scope, [.. rows.Where(row => !IsComment(row) && !FlowCommentWorkbook.IsComment(row))], languages);
        }
        catch (DomainException refusal) when (refusal.Code == ErrorCodes.InvalidTranslations && plan.Errors.Count > 0)
        {
            var errors = refusal.Details.TryGetValue(ErrorsKey, out var found) && found is IEnumerable<TranslationRowError> list ? list : [];
            throw Invalid([.. errors, .. plan.Errors]);
        }
        if (plan.Errors.Count > 0)
            throw Invalid(plan.Errors);
        return new TranslationImportResult(core.Written + plan.Rows.Count, [.. core.Ignored.Concat(plan.Ignored).OrderBy(entry => entry.Line)]);
    }

    /// <summary>The key of the errors by line in the details of TRADUCTIONS_INVALIDES, as the closed core writes it.</summary>
    private const string ErrorsKey = "errors";

    private static DomainException Invalid(IReadOnlyList<TranslationRowError> errors) =>
        new(ErrorCodes.InvalidTranslations, RefusalKind.Invalid, new Dictionary<string, object?>
        {
            [ErrorsKey] = errors.OrderBy(error => error.Line).ThenBy(error => error.Language, StringComparer.Ordinal).ToList(),
        });

    /// <summary>The live steps of the live maps that have an original, in the reading order of the maps, with every line of their comment.</summary>
    private static IEnumerable<CommentOriginal> Originals(ProjectWorkspace workspace, IReadOnlyList<PresentationComment> lines)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(lines);
        var byStep = lines.ToLookup(line => line.StepId, line => line.AsLine());
        var maps = workspace.All<ProcessMap>().Where(map => map.DeletedAt is null).OrderBy(map => map.CreatedAt).ThenBy(map => map.Id);
        foreach (var map in maps)
        {
            foreach (var step in workspace.ReadingOrder(map.Id))
            {
                var subject = CommentSubject.Of(step);
                if (CommentLineRules.Original(subject, byStep[step.Id]) is { } original)
                    yield return new CommentOriginal(subject, step.Code, $"{map.Name} / {workspace.Lane(step.LaneId)?.Name ?? string.Empty}", original, [.. byStep[step.Id]]);
            }
        }
    }

    private static CommentText Describe(CommentOriginal comment) =>
        new(comment.Subject.Id, comment.Code, comment.Context, comment.Subject.SourceLanguage, comment.Original.Text, CommentRows.TranslationsOf(comment),
            CommentRows.SourcesOf(comment));
}
