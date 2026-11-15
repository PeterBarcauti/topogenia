// CommentRows.cs
// The rows of the comments of presentation in the workbook of the translations, written once for the comments of the steps and for those of the flows (ADR 099, lot J10): which comment a scope gives, the import of their rows planned with the reasons to ignore a row, the lines it writes.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Translations;

/// <summary>
/// A comment of presentation that has an original, as the workbook names it, whatever carries it: its subject (a step,
/// or a flow with the language of origin of its source step), its readable code and its context, its original and every
/// line of the comment.
/// </summary>
public sealed record CommentOriginal(CommentSubject Subject, string Code, string Context, CommentLine Original, IReadOnlyList<CommentLine> Lines);

/// <summary>
/// What the rows of comments of a workbook do, planned before anything is written: the rows ignored with their reason,
/// the invalid translations, and the translations to write, each over the line of today.
/// </summary>
public sealed record CommentImportPlan(IReadOnlyList<IgnoredRow> Ignored, IReadOnlyList<TranslationRowError> Errors, IReadOnlyList<PlannedCommentRow> Rows)
{
    public static CommentImportPlan Empty { get; } = new([], [], []);

    /// <summary>This plan and another one together: the rows of the comments of the steps and those of the flows, for the outcome of one import.</summary>
    public CommentImportPlan With(CommentImportPlan other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new([.. Ignored, .. other.Ignored], [.. Errors, .. other.Errors], [.. Rows, .. other.Rows]);
    }
}

/// <summary>A translation of a comment a workbook brings: its subject (the step, or the flow), its language, its text, the line of today in that language.</summary>
public sealed record PlannedCommentRow(Guid SubjectId, string Language, string Text, CommentLine? Current);

/// <summary>The rules of the rows of comments of presentation in the workbook, the same for the comment of a step and for the comment of a flow.</summary>
public static class CommentRows
{
    /// <summary>The translations of a comment by language: every line but its original.</summary>
    public static IReadOnlyDictionary<string, string> TranslationsOf(CommentOriginal comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return Translated(comment).ToDictionary(line => line.Language, line => line.Text, StringComparer.Ordinal);
    }

    /// <summary>The source of each translation of a comment by language (`manual`, `assistant`, `workbook`).</summary>
    public static IReadOnlyDictionary<string, string> SourcesOf(CommentOriginal comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return Translated(comment).ToDictionary(line => line.Language, line => Codes.Of(line.Source), StringComparer.Ordinal);
    }

    /// <summary>
    /// True when a comment written in a language of origin is given for the scope, as the texts of the core are
    /// (<see cref="TranslationCatalog.IsGiven"/>): some target language (not the language of origin) qualifies, `missing`
    /// when it lacks, `auto` when it lacks or was written by the assistant or a workbook, `all` always.
    /// </summary>
    public static bool IsGiven(string origin, IReadOnlyDictionary<string, string> translations, IReadOnlyDictionary<string, string> sources,
        TranslationScope scope, IReadOnlyCollection<string> targets)
    {
        ArgumentNullException.ThrowIfNull(translations);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(targets);
        var wanted = targets.Where(language => LocalizedText.TakesTranslationIn(origin, language));
        var typed = Codes.Of(CommentSource.Manual);
        return scope switch
        {
            TranslationScope.Missing => wanted.Any(language => !translations.ContainsKey(language)),
            TranslationScope.Auto => wanted.Any(language => !sources.TryGetValue(language, out var source) || source != typed),
            _ => true,
        };
    }

    /// <summary>
    /// The import of the rows of comments planned, as the closed core plans its own rows
    /// (<see cref="TranslationImport"/>), among the comments that have an original (<paramref name="originals"/>): a row
    /// is ignored when its subject has no original any more (OBJET_INTROUVABLE), when its original differs from the one
    /// of today (TEXTE_MODIFIE), when a translation typed by hand exists and the scope is not `all`
    /// (TRADUCTION_MANUELLE), or when it brings no translation (SANS_TRADUCTION). The languages that are not configured,
    /// the language of origin and the empty cells are skipped; an invalid translation is an error of the file. Nothing
    /// is written here.
    /// </summary>
    public static CommentImportPlan Plan(IEnumerable<CommentOriginal> originals, TranslationScope scope, IReadOnlyList<TranslationRow> rows, LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(originals);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(languages);
        var known = originals.ToDictionary(comment => comment.Subject.Id);
        var ignored = new List<IgnoredRow>();
        var errors = new List<TranslationRowError>();
        var planned = new List<PlannedCommentRow>();
        foreach (var row in rows)
        {
            if (!known.TryGetValue(row.Id, out var comment))
            {
                ignored.Add(new IgnoredRow(row.Line, IgnoredRowReasons.ObjectNotFound));
                continue;
            }
            var origin = comment.Subject.SourceLanguage;
            var original = string.IsNullOrWhiteSpace(row.Original) ? row.Translations?.GetValueOrDefault(origin) : row.Original;
            if (!PresentationCommentRules.SameText(original, comment.Original.Text))
            {
                ignored.Add(new IgnoredRow(row.Line, IgnoredRowReasons.TextChanged));
                continue;
            }
            var before = planned.Count;
            var manual = false;
            foreach (var (language, raw) in row.Translations ?? new Dictionary<string, string?>())
            {
                if (!LocalizedText.TakesTranslationIn(origin, language) || !languages.IsSupported(language) || string.IsNullOrWhiteSpace(raw))
                    continue;
                string text;
                try
                {
                    text = TextRules.Required(PresentationCommentRules.Lines(raw), language, PresentationCommentRules.TextMax);
                }
                catch (DomainException refusal)
                {
                    errors.Add(new TranslationRowError(row.Line, language, refusal.Code, PresentationCommentRules.TextMax));
                    continue;
                }
                var current = comment.Lines.FirstOrDefault(line => line.Language == language);
                if (scope != TranslationScope.All && current is { Source: CommentSource.Manual })
                {
                    manual = true;
                    continue;
                }
                planned.Add(new PlannedCommentRow(row.Id, language, text, current));
            }
            if (manual)
                ignored.Add(new IgnoredRow(row.Line, IgnoredRowReasons.ManualTranslation));
            else if (planned.Count == before)
                ignored.Add(new IgnoredRow(row.Line, IgnoredRowReasons.NoTranslation));
        }
        return new CommentImportPlan(ignored, errors, planned);
    }

    /// <summary>
    /// The lines the plan writes, with the source `workbook`: a translation already written by a workbook with the same
    /// text is left as it is, so that the same file imported again writes nothing more; of two rows that name the same
    /// comment, the last one is written, as the closed core writes its rows one after the other.
    /// </summary>
    public static IReadOnlyList<CommentLineWrite> Writes(CommentImportPlan plan, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return [.. plan.Rows
            .GroupBy(row => (row.SubjectId, row.Language)).Select(group => group.Last())
            .Where(row => !(row.Current is { Source: CommentSource.Workbook } current && string.Equals(current.Text, row.Text, StringComparison.Ordinal)))
            .Select(row => CommentLineRules.Write(row.SubjectId, row.Language, row.Text, CommentSource.Workbook, row.Current, context))];
    }

    private static IEnumerable<CommentLine> Translated(CommentOriginal comment) => comment.Lines.Where(line => !comment.Subject.WritesOriginalIn(line.Language));
}
