// PresentationCommentRules.cs
// The rules of a comment of presentation (ADR 099 revised): its text and its language, whatever carries it; for the comment of a step, its original and its translations, what a person typing a line changes and what an import brings, by the rules shared with the comment of a flow, and what a translation by the assistant writes.

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Presentation;

/// <summary>A translation of a comment proposed by the assistant: its step, the original it was translated from, its text.</summary>
public sealed record CommentTranslation(Guid StepId, string? Original, string? Text);

/// <summary>
/// What becomes of a translation proposed for a comment: the step, the original of today, the text shown (the one
/// proposed, or the one typed by hand that stays), and whether that typed one is kept; the line of today in the language.
/// </summary>
public sealed record PlannedCommentTranslation(Step Step, string Original, string Text, bool Kept, PresentationComment? Current);

/// <summary>
/// The rules of a comment of presentation (ADR 099 revised). Its text and its language are those of every comment; what
/// depends on its original and on its lines is written once in <see cref="CommentLineRules"/>, for the comment of a step
/// here and for the comment of a flow (<see cref="FlowPresentationCommentRules"/>): a step is the subject of its own
/// comment, with its own language of origin.
/// </summary>
public static class PresentationCommentRules
{
    /// <summary>One to three sentences: what a presenter says of a step, read aloud in a few seconds.</summary>
    public const int TextMax = 500;

    /// <summary>
    /// The code of a comment of presentation among the texts translated (a row of the workbook, a text of the preview of
    /// a translation): not one of the objects the closed core translates.
    /// </summary>
    public const string ObjectType = "presentation_comment";

    /// <summary>The field of the translations proposed by the assistant, named by a refusal.</summary>
    public const string TranslationsField = "comments";

    /// <summary>The text given: trimmed, its line ends made `\n`, without markup; null or empty removes the line; TEXTE_TROP_LONG beyond its bound.</summary>
    public static string? Text(string? text) => TextRules.Optional(Lines(text), "text", TextMax);

    /// <summary>A text with the line ends of every system made `\n`, so that the same text reads the same after a workbook or a file.</summary>
    public static string? Lines(string? text) => text?.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>The language of a line: one of the languages of the configuration (LANGUE_INVALIDE otherwise).</summary>
    public static string Language(string? language, LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        return languages.Require(language, "language");
    }

    /// <summary>True for a line that the assistant or a workbook wrote: it falls when its original changes, and may be written again.</summary>
    public static bool IsAutomatic(PresentationComment line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return CommentLineRules.IsAutomatic(line.Source);
    }

    /// <summary>The original of the comment of a step among its lines: the line in the language of origin of the step, or none.</summary>
    public static PresentationComment? Original(Step step, IEnumerable<PresentationComment> lines)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(lines);
        var subject = CommentSubject.Of(step);
        return lines.FirstOrDefault(line => line.StepId == subject.Id && subject.WritesOriginalIn(line.Language));
    }

    /// <summary>
    /// What a person typing the line of a step in a language changes, among the lines kept of the step
    /// (<paramref name="kept"/>). In the language of origin of the step the line is the original: a text really changed
    /// drops its automatic translations and keeps the typed ones, as the texts of the closed core do; an empty text
    /// removes the comment, every line of it, and nothing when the step has no original: an empty text removes what it
    /// replaces, and a translation typed in another language stays. In another language the line is a translation
    /// typed; an empty text removes that line only. The same text already typed writes nothing. The rule is the one of
    /// every comment (<see cref="CommentLineRules.Typed"/>), the step being its subject.
    /// </summary>
    public static IReadOnlyList<CommentWrite> Typed(Step step, IReadOnlyList<PresentationComment> kept, string? text, string language, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(kept);
        return [.. CommentLineRules.Typed(CommentSubject.Of(step), LinesOf(kept), text, language, context).Select(CommentWrite.Of)];
    }

    /// <summary>
    /// What becomes of the translations of comments an assistant proposed into a language, for the steps of a map and of
    /// the levels below it: a step named twice is VALEUR_INVALIDE on <see cref="TranslationsField"/> and an invalid text
    /// is refused by its rule; an entry is left aside when its step is not a live step of that tree, when the language is
    /// the language of origin of the step, when the step has no original or another one than the one translated, or when
    /// its text is empty. A translation typed by hand is kept: it is given with its own text. The rule is the one of
    /// every comment (<see cref="CommentLineRules.Planned"/>), each step being its own subject.
    /// </summary>
    public static IReadOnlyList<PlannedCommentTranslation> Planned(ProjectWorkspace workspace, Guid mapId, IReadOnlyList<PresentationComment> lines,
        IReadOnlyList<CommentTranslation> proposed, string language)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(proposed);
        var steps = TreeSteps(workspace, mapId).ToDictionary(step => step.Id);
        var planned = CommentLineRules.Planned(steps.ToDictionary(pair => pair.Key, pair => CommentSubject.Of(pair.Value)), LinesOf(lines),
            [.. proposed.Select(entry => new ProposedLine(entry.StepId, entry.Original, entry.Text))], language, TranslationsField);
        return [.. planned.Select(plan => new PlannedCommentTranslation(steps[plan.Subject.Id], plan.Original, plan.Text, plan.Kept,
            plan.Current is { } current ? PresentationComment.Of(current) : null))];
    }

    /// <summary>
    /// The translations of comments a proposal brings, checked before they are planned: none without the language of a
    /// translation (VALEUR_INVALIDE on <see cref="TranslationsField"/>: only a translation carries them).
    /// </summary>
    public static IReadOnlyList<CommentTranslation> Proposed(IReadOnlyList<CommentTranslation>? proposed, string? language) =>
        CommentLineRules.Proposed(proposed, language, TranslationsField);

    /// <summary>The most translations of comments one proposal brings: beyond, VALEUR_INVALIDE (a reply never lists more than it was asked).</summary>
    public const int ProposedMax = CommentLineRules.ProposedMax;

    /// <summary>The lines a translation by the assistant writes: those planned that are not kept nor already written as they are.</summary>
    public static IReadOnlyList<CommentWrite> Translated(IReadOnlyList<PlannedCommentTranslation> planned, string language, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(planned);
        var lines = planned.Select(plan => new PlannedLine(CommentSubject.Of(plan.Step), plan.Original, plan.Text, plan.Kept, plan.Current?.AsLine())).ToList();
        return [.. CommentLineRules.Translated(lines, language, context).Select(CommentWrite.Of)];
    }

    /// <summary>The live steps of a live map and of every level below it.</summary>
    public static IReadOnlyList<Step> TreeSteps(ProjectWorkspace workspace, Guid mapId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return [.. workspace.Subtree(workspace.LiveMap(mapId).Id).SelectMany(workspace.ReadingOrder)];
    }

    /// <summary>True when two texts are the same once trimmed, whatever their line ends.</summary>
    public static bool SameText(string? first, string? second) =>
        string.Equals((Lines(first) ?? string.Empty).Trim(), (Lines(second) ?? string.Empty).Trim(), StringComparison.Ordinal);

    /// <summary>The line of a step in a language written over the one kept (none: created), its version moved on by one.</summary>
    public static CommentWrite Write(Guid stepId, string language, string text, CommentSource source, PresentationComment? kept, OperationContext context) =>
        CommentWrite.Of(CommentLineRules.Write(stepId, language, text, source, kept?.AsLine(), context));

    /// <summary>The removal of a line kept, its version read.</summary>
    public static CommentWrite Removal(PresentationComment kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        return CommentWrite.Of(CommentLineRules.Removal(kept.AsLine()));
    }

    /// <summary>The lines kept once the writes are done.</summary>
    public static IReadOnlyList<PresentationComment> After(IReadOnlyList<PresentationComment> kept, IReadOnlyList<CommentWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(kept);
        ArgumentNullException.ThrowIfNull(writes);
        return [.. CommentLineRules.After(LinesOf(kept), [.. writes.Select(write => write.AsLineWrite())]).Select(PresentationComment.Of)];
    }

    /// <summary>The lines of the comments of steps as the shared rules see them.</summary>
    private static List<CommentLine> LinesOf(IEnumerable<PresentationComment> comments) => [.. comments.Select(comment => comment.AsLine())];

    /// <summary>The lines of the comments of the live steps of a project, read for its package (an export, a target state).</summary>
    public static async Task<IReadOnlyList<PresentationComment>> OfLiveStepsAsync(IPresentationCommentStore store, ProjectWorkspace workspace,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(workspace);
        var steps = workspace.All<Step>().Where(step => step.DeletedAt is null).Select(step => step.Id).ToList();
        return steps.Count == 0 ? [] : await store.OfStepsAsync(steps, cancellationToken);
    }

    /// <summary>
    /// The lines a package brings (format 2.4): those in a language this installation does not configure are left out,
    /// as the translations of the core (AR-119); a text that the rule of the entry refuses, or two lines of one step in
    /// one language, refuse the package, PAQUET_INVALIDE, reason <c>values</c>, field <c>presentationComment</c> (the
    /// schema already refuses a text: this is the last guard). The line in the language of origin of its step is the
    /// original, always a text typed. The rule is the one of every comment (<see cref="CommentLineRules.Imported"/>).
    /// </summary>
    public static IReadOnlyList<PresentationComment> Imported(IReadOnlyList<PresentationComment> comments, IReadOnlyDictionary<Guid, string> origins,
        LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(comments);
        return [.. CommentLineRules.Imported(LinesOf(comments), origins, languages).Select(PresentationComment.Of)];
    }
}
