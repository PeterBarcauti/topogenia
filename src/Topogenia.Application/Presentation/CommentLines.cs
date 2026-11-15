// CommentLines.cs
// The rules of the lines of a comment of presentation, written once for whatever carries the comment, a step or a flow (ADR 099, lot J10): its original and its translations, the version read of a line, what a person typing a line changes, what a package brings, what becomes of the translations an assistant proposes.

using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Presentation;

/// <summary>
/// What a comment of presentation is written on, as its rules see it: its identifier, which keys the lines of the comment
/// with their language, and its language of origin, in which the original of the comment is written. A step is its own
/// subject; a flow has no language of its own and takes the language of origin of its source step.
/// </summary>
public readonly record struct CommentSubject(Guid Id, string SourceLanguage)
{
    /// <summary>True for the language of the original of the comment: a line in any other language is a translation.</summary>
    public bool WritesOriginalIn(string language) => language == SourceLanguage;

    public static CommentSubject Of(Step step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(step.Id, step.SourceLanguage);
    }

    /// <summary>A flow and its source step, which gives the language of origin of the comment of the flow.</summary>
    public static CommentSubject Of(Flow flow, Step source)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(source);
        if (source.Id != flow.SourceStepId)
            throw new ArgumentException("The language of origin of the comment of a flow is the one of its source step.", nameof(source));
        return new(flow.Id, source.SourceLanguage);
    }
}

/// <summary>
/// A line of a comment as its rules see it, whatever it is kept as (<see cref="PresentationComment"/>,
/// <see cref="FlowPresentationComment"/>): the same values, its step or its flow as its subject.
/// </summary>
public sealed record CommentLine(Guid SubjectId, string Text, string Language, CommentSource Source, int Version, DateTime UpdatedAt, Guid UpdatedBy);

/// <summary>
/// One line to write, as the rules decide it: created or replaced by <see cref="Line"/>, or removed when it is null;
/// <see cref="VersionRead"/> is the version of the line the writer read (0 for none).
/// </summary>
public sealed record CommentLineWrite(Guid SubjectId, string Language, CommentLine? Line, int VersionRead);

/// <summary>A translation of a comment proposed by the assistant, whatever carries the comment: its subject, the original it was translated from, its text.</summary>
public sealed record ProposedLine(Guid SubjectId, string? Original, string? Text);

/// <summary>
/// What becomes of a translation proposed for a comment: its subject, the original of today, the text shown (the one
/// proposed, or the one typed by hand that stays), whether that typed one is kept, and the line of today in the language.
/// </summary>
public sealed record PlannedLine(CommentSubject Subject, string Original, string Text, bool Kept, CommentLine? Current);

/// <summary>The rules of the lines of a comment of presentation, the same for the comment of a step and for the comment of a flow (ADR 099, lot J10).</summary>
public static class CommentLineRules
{
    /// <summary>True for a source that is not a person typing: the assistant or a workbook, whose line falls when its original changes and may be written again.</summary>
    public static bool IsAutomatic(CommentSource source) => source != CommentSource.Manual;

    /// <summary>The original of the comment of a subject among some lines: its line in its language of origin, or none.</summary>
    public static CommentLine? Original(CommentSubject subject, IEnumerable<CommentLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.FirstOrDefault(line => line.SubjectId == subject.Id && subject.WritesOriginalIn(line.Language));
    }

    /// <summary>
    /// The If-Match of a request on the line it writes: nothing to check without a version read; CONFLIT_VERSION when the
    /// line kept (<paramref name="line"/>, none: version 0) is no longer the one read, with the author and the time of the
    /// line kept, on the type of object given.
    /// </summary>
    public static void EnsureRead(CommentLine? line, int? versionRead, string objectType) =>
        Commands.RecordVersion.EnsureLine(versionRead, line?.Version ?? 0, objectType, line?.UpdatedBy, line?.UpdatedAt);

    /// <summary>
    /// What a person typing the line of a subject in a language changes, among the lines kept of the subject
    /// (<paramref name="kept"/>). In the language of origin of the subject the line is the original: a text really
    /// changed drops its automatic translations and keeps the typed ones, as the texts of the closed core do; an empty
    /// text removes the comment, every line of it, and nothing when the subject has no original: an empty text removes
    /// what it replaces, and a translation typed in another language stays. In another language the line is a
    /// translation typed; an empty text removes that line only. The same text already typed writes nothing.
    /// </summary>
    public static IReadOnlyList<CommentLineWrite> Typed(CommentSubject subject, IReadOnlyList<CommentLine> kept, string? text, string language,
        OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(kept);
        ArgumentNullException.ThrowIfNull(context);
        var lines = kept.Where(comment => comment.SubjectId == subject.Id).ToList();
        var line = lines.FirstOrDefault(comment => comment.Language == language);
        var original = subject.WritesOriginalIn(language);
        if (text is null)
        {
            var removed = original && line is not null ? lines : lines.Where(comment => comment.Language == language);
            return [.. removed.Select(Removal)];
        }
        if (line is not null && line.Source == CommentSource.Manual && string.Equals(line.Text, text, StringComparison.Ordinal))
            return [];
        var writes = new List<CommentLineWrite> { Write(subject.Id, language, text, CommentSource.Manual, line, context) };
        if (original && !string.Equals(line?.Text.Trim(), text.Trim(), StringComparison.Ordinal))
            writes.AddRange(lines.Where(comment => comment.Language != language && IsAutomatic(comment.Source)).Select(Removal));
        return writes;
    }

    /// <summary>The line of a subject in a language written over the one kept (none: created), its version moved on by one, by the actor of the context at its time.</summary>
    public static CommentLineWrite Write(Guid subjectId, string language, string text, CommentSource source, CommentLine? kept, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var version = kept?.Version ?? 0;
        return new CommentLineWrite(subjectId, language, new CommentLine(subjectId, text, language, source, version + 1, context.Now, context.ActorId), version);
    }

    /// <summary>The removal of a line kept, its version read.</summary>
    public static CommentLineWrite Removal(CommentLine kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        return new CommentLineWrite(kept.SubjectId, kept.Language, null, kept.Version);
    }

    /// <summary>The lines kept once the writes are done.</summary>
    public static IReadOnlyList<CommentLine> After(IReadOnlyList<CommentLine> kept, IReadOnlyList<CommentLineWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(kept);
        ArgumentNullException.ThrowIfNull(writes);
        var lines = kept.ToDictionary(line => (line.SubjectId, line.Language));
        foreach (var write in writes)
        {
            if (write.Line is { } line)
                lines[(write.SubjectId, write.Language)] = line;
            else
                lines.Remove((write.SubjectId, write.Language));
        }
        return [.. lines.Values];
    }

    /// <summary>The most translations of comments one proposal brings for the steps, and as many for the flows: beyond, VALEUR_INVALIDE (a reply never lists more than it was asked).</summary>
    public const int ProposedMax = 1000;

    /// <summary>
    /// The translations of comments a proposal brings, checked before they are planned: none without the language of a
    /// translation (VALEUR_INVALIDE on the field given: only a translation carries them).
    /// </summary>
    public static IReadOnlyList<T> Proposed<T>(IReadOnlyList<T>? proposed, string? language, string field)
    {
        if (proposed is not { Count: > 0 })
            return [];
        return language is null ? throw DomainException.InvalidValue(field) : proposed;
    }

    /// <summary>
    /// What becomes of the translations of comments an assistant proposed into a language, among the subjects that may
    /// take one (<paramref name="subjects"/>: the live steps, or the live flows, of a map and of the levels below it): a
    /// subject named twice is VALEUR_INVALIDE on the field given and an invalid text is refused by its rule; an entry is
    /// left aside when its subject is not one of them, when the language is its language of origin, when it has no
    /// original or another one than the one translated, or when its text is empty. A translation typed by hand is kept:
    /// it is given with its own text.
    /// </summary>
    public static IReadOnlyList<PlannedLine> Planned(IReadOnlyDictionary<Guid, CommentSubject> subjects, IReadOnlyList<CommentLine> lines,
        IReadOnlyList<ProposedLine> proposed, string language, string field)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(proposed);
        if (proposed.Count > ProposedMax || proposed.Select(entry => entry.SubjectId).Distinct().Count() != proposed.Count)
            throw DomainException.InvalidValue(field);
        var bySubject = lines.ToLookup(line => line.SubjectId);
        var planned = new List<PlannedLine>();
        foreach (var entry in proposed)
        {
            var text = PresentationCommentRules.Text(entry.Text);
            if (text is null || !subjects.TryGetValue(entry.SubjectId, out var subject) || !LocalizedText.TakesTranslationIn(subject.SourceLanguage, language))
                continue;
            if (Original(subject, bySubject[subject.Id]) is not { } original || !PresentationCommentRules.SameText(original.Text, entry.Original))
                continue;
            var current = bySubject[subject.Id].FirstOrDefault(line => line.Language == language);
            var kept = current is { Source: CommentSource.Manual };
            planned.Add(new PlannedLine(subject, original.Text, kept ? current!.Text : text, kept, current));
        }
        return planned;
    }

    /// <summary>The lines a translation by the assistant writes: those planned that are not kept nor already written as they are.</summary>
    public static IReadOnlyList<CommentLineWrite> Translated(IReadOnlyList<PlannedLine> planned, string language, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(planned);
        ArgumentNullException.ThrowIfNull(context);
        return [.. planned
            .Where(plan => !plan.Kept && !(plan.Current is { Source: CommentSource.Assistant } current && string.Equals(current.Text, plan.Text, StringComparison.Ordinal)))
            .Select(plan => Write(plan.Subject.Id, language, plan.Text, CommentSource.Assistant, plan.Current, context))];
    }

    /// <summary>The key of a comment in a package, on its step or on its flow: the field a refusal of its lines names.</summary>
    public const string PackageField = "presentationComment";

    /// <summary>
    /// The lines a package brings (format 2.4): those in a language this installation does not configure are left out,
    /// as the translations of the core (AR-119); a text that the rule of the entry refuses, or two lines of one subject
    /// in one language, refuse the package, PAQUET_INVALIDE, reason <c>values</c>, field <c>presentationComment</c> (the
    /// schema already refuses a text: this is the last guard). The line in the language of origin of its subject
    /// (<paramref name="origins"/>) is the original, always a text typed.
    /// </summary>
    public static IReadOnlyList<CommentLine> Imported(IReadOnlyList<CommentLine> comments, IReadOnlyDictionary<Guid, string> origins, LanguageSettings languages)
    {
        ArgumentNullException.ThrowIfNull(comments);
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(languages);
        if (comments.Select(comment => (comment.SubjectId, comment.Language)).Distinct().Count() != comments.Count)
            throw Domain.Projects.ImportedRecords.Refusal([PackageField]);
        var kept = new List<CommentLine>();
        foreach (var comment in comments.Where(comment => languages.IsSupported(comment.Language)))
        {
            string? text;
            try
            {
                text = PresentationCommentRules.Text(comment.Text);
            }
            catch (DomainException)
            {
                throw Domain.Projects.ImportedRecords.Refusal([PackageField]);
            }
            if (text != comment.Text)
                throw Domain.Projects.ImportedRecords.Refusal([PackageField]);
            var original = origins.TryGetValue(comment.SubjectId, out var origin) && origin == comment.Language;
            kept.Add(original ? comment with { Source = CommentSource.Manual } : comment);
        }
        return kept;
    }
}
