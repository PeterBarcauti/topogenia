// FlowCommentWorkbook.cs
// The comments of presentation of the flows in the workbook of the translations (ADR 099, lot J10): their rows listed after those of the steps, each flow named as the workbook names a labelled flow, and their import planned by the rules shared with the comments of the steps.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Translations;

/// <summary>
/// The comment of presentation of a flow and its translations of today, as a row of the workbook: its flow and its
/// readable code (`A &gt; B`, the codes of its two steps), its context (the labels of its two steps), both as the
/// workbook names a labelled flow, the language of origin of its source step and the original, then the translations and
/// the source of each (`manual`, `assistant`, `workbook`).
/// </summary>
public sealed record FlowCommentText(
    Guid FlowId,
    string Code,
    string Context,
    string Language,
    string Original,
    IReadOnlyDictionary<string, string> Translations,
    IReadOnlyDictionary<string, string> Sources);

/// <summary>
/// The comments of presentation of the flows in the workbook of the translations of a project: those of the steps
/// (<see cref="CommentWorkbook"/>) on another subject, by the same rules (<see cref="CommentRows"/>).
/// </summary>
public static class FlowCommentWorkbook
{
    /// <summary>The type of a row of the workbook that is the comment of a flow: not one of the objects the closed core translates.</summary>
    public const string RowType = FlowPresentationCommentRules.ObjectType;

    /// <summary>
    /// The comments to translate, in the reading order of the flows (by their source, then by their target): those of
    /// the live flows of the live maps that have an original, filtered by the scope on the target languages as the
    /// comments of the steps are.
    /// </summary>
    public static IReadOnlyList<FlowCommentText> List(ProjectWorkspace workspace, IReadOnlyList<FlowPresentationComment> lines, TranslationScope scope,
        IReadOnlyCollection<string> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        return [.. Originals(workspace, lines).Select(Describe).Where(text => IsGiven(text, scope, targets))];
    }

    /// <summary>True when the comment of a flow is given for the scope: some target language (not the language of origin of its source step) qualifies.</summary>
    public static bool IsGiven(FlowCommentText text, TranslationScope scope, IReadOnlyCollection<string> targets)
    {
        ArgumentNullException.ThrowIfNull(text);
        return CommentRows.IsGiven(text.Language, text.Translations, text.Sources, scope, targets);
    }

    /// <summary>
    /// The import of the rows of comments of flows planned, with the reasons to ignore a row of the comments of the
    /// steps (<see cref="CommentRows.Plan"/>): a flow that is not a live flow between two live steps, or that has no
    /// original any more, is OBJET_INTROUVABLE. Nothing is written here.
    /// </summary>
    public static CommentImportPlan Plan(ProjectWorkspace workspace, IReadOnlyList<FlowPresentationComment> lines, TranslationScope scope,
        IReadOnlyList<TranslationRow> rows, LanguageSettings languages) =>
        CommentRows.Plan(Originals(workspace, lines), scope, rows, languages);

    /// <summary>The lines the plan writes, with the source `workbook` (<see cref="CommentRows.Writes"/>).</summary>
    public static IReadOnlyList<FlowCommentWrite> Writes(CommentImportPlan plan, OperationContext context) =>
        [.. CommentRows.Writes(plan, context).Select(FlowCommentWrite.Of)];

    /// <summary>True for a row of the workbook that is the comment of presentation of a flow.</summary>
    public static bool IsComment(TranslationRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return string.Equals(row.Type, RowType, StringComparison.Ordinal);
    }

    /// <summary>The live flows of the live maps that have an original, in the reading order of the maps, with every line of their comment.</summary>
    private static IEnumerable<CommentOriginal> Originals(ProjectWorkspace workspace, IReadOnlyList<FlowPresentationComment> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var byFlow = lines.ToLookup(line => line.FlowId, line => line.AsLine());
        foreach (var flow in FlowPresentationCommentRules.ProjectFlows(workspace))
        {
            if (CommentLineRules.Original(flow.Subject, byFlow[flow.Id]) is { } original)
                yield return new CommentOriginal(flow.Subject, flow.Code, flow.Context, original, [.. byFlow[flow.Id]]);
        }
    }

    private static FlowCommentText Describe(CommentOriginal comment) =>
        new(comment.Subject.Id, comment.Code, comment.Context, comment.Subject.SourceLanguage, comment.Original.Text, CommentRows.TranslationsOf(comment),
            CommentRows.SourcesOf(comment));
}
