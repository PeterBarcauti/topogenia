// AssistantComments.cs
// The comments of presentation translated with the model (ADR 099 revised), those of the steps and those of the flows (lot J10): the comments a translation asks, the reading of the texts of the reply and their matching to the comments asked.

using System.Text;
using System.Text.Json;
using Topogenia.Application.Presentation;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Assistant;

/// <summary>A comment whose translation is asked: its step, the code of the step (its reference for the model), its original.</summary>
public sealed record AssistantCommentTarget(Guid StepId, string Reference, string Original);

/// <summary>
/// The comment of a flow whose translation is asked: its flow, the codes of its source and of its target, its original.
/// A flow has no code of its own: its reference for the model is the codes of its two steps, `A &gt; B`, as the workbook
/// of the translations and the preview of a translation already name a flow.
/// </summary>
public sealed record AssistantFlowCommentTarget(Guid FlowId, string Source, string Target, string Original)
{
    public string Reference => FlowPresentationCommentRules.CodeOf(Source, Target);
}

/// <summary>
/// A translation the model wrote for a comment, named by the code of its step, or by the codes of the two steps of its
/// flow (`A &gt; B`), before it is matched to a comment asked.
/// </summary>
public sealed record AssistantCommentText(string Step, string Text);

/// <summary>
/// A translation written by the model for a comment asked, as the panel shows it: the step and its code, the original it
/// translates, which the preview and the application send back, and the text proposed.
/// </summary>
public sealed record AssistantCommentView(Guid StepId, string Code, string Original, string Text);

/// <summary>
/// A translation written by the model for the comment of a flow asked, as the panel shows it: the flow, the codes of its
/// source and of its target, the original it translates, which the preview and the application send back, and the text
/// proposed.
/// </summary>
public sealed record AssistantFlowCommentView(Guid FlowId, string SourceCode, string TargetCode, string Original, string Text);

/// <summary>The comments of presentation a translation by the assistant asks (ADR 099 revised): those of the steps, then those of the flows (lot J10).</summary>
public static class AssistantCommentTargets
{
    /// <summary>
    /// The most comments one exchange translates, those of the steps and those of the flows together: beyond, the first
    /// ones in reading order, the steps before the flows; the rest in a next exchange.
    /// </summary>
    public const int CommentsMax = 300;

    /// <summary>
    /// The comments to translate into a language, for the steps of a map and of every level below it
    /// (<see cref="PresentationCommentRules.TreeSteps"/>), in reading order: those that have an original and no line yet
    /// in that language. A translation typed by hand is kept; one written by the assistant or a workbook is up to date,
    /// since it falls when its original changes.
    /// </summary>
    public static IReadOnlyList<AssistantCommentTarget> Of(IReadOnlyList<Step> steps, IReadOnlyList<PresentationComment> lines, string language)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(lines);
        return [.. Asked(steps, CommentSubject.Of, lines.Select(line => line.AsLine()), language, CommentsMax)
            .Select(asked => new AssistantCommentTarget(asked.Item.Id, asked.Item.Code, asked.Original))];
    }

    /// <summary>
    /// The comments to translate into a language, for the flows of a map and of every level below it
    /// (<see cref="FlowPresentationCommentRules.TreeFlows"/>), in reading order, by the rule of the steps: those that
    /// have an original (the line in the language of origin of the source step) and no line yet in that language. The
    /// bound of an exchange is for the steps and the flows together: with <paramref name="asked"/> comments of steps
    /// asked already, the flows take what is left of <see cref="CommentsMax"/>, none when nothing is left.
    /// </summary>
    public static IReadOnlyList<AssistantFlowCommentTarget> OfFlows(IReadOnlyList<FlowEnds> flows, IReadOnlyList<FlowPresentationComment> lines, string language,
        int asked)
    {
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(lines);
        return [.. Asked(flows, flow => flow.Subject, lines.Select(line => line.AsLine()), language, CommentsMax - asked)
            .Select(asked => new AssistantFlowCommentTarget(asked.Item.Id, asked.Item.Source.Code, asked.Item.Target.Code, asked.Original))];
    }

    /// <summary>What carries a comment to translate into a language, with its original, in the order given: the first ones up to the bound.</summary>
    private static IEnumerable<(T Item, string Original)> Asked<T>(IEnumerable<T> items, Func<T, CommentSubject> subjectOf, IEnumerable<CommentLine> lines,
        string language, int max)
    {
        var bySubject = lines.ToLookup(line => line.SubjectId);
        return items
            .Select(item => (Item: item, Subject: subjectOf(item)))
            .Where(asked => asked.Subject.SourceLanguage != language && !bySubject[asked.Subject.Id].Any(line => line.Language == language))
            .Select(asked => (asked.Item, Original: CommentLineRules.Original(asked.Subject, bySubject[asked.Subject.Id])?.Text))
            .Where(asked => asked.Original is not null)
            .Take(max)
            .Select(asked => (asked.Item, asked.Original!));
    }
}

/// <summary>
/// The steps and the flows whose comment of presentation the practice `comments` writes (3.3, lot 3, F-130): those of the
/// map open and of every detailed map below it (essay of the 18/11/2026: a request for « every step and detailed step »
/// is the whole process), map by map in the order of the steps given, each map with its live steps in reading order then
/// its live flows between two of them; each with the name of its map, the original of its comment today (its line in its
/// language of origin), the source and the version of that line; <see cref="AssistantCommentTargets.CommentsMax"/> at most,
/// the last maps left out entire.
/// </summary>
public static class AssistantCommentDraftTargets
{
    public static IReadOnlyList<AssistantDraftTarget> Of(IReadOnlyList<Step> steps, IReadOnlyList<PresentationComment> lines, IReadOnlyList<FlowEnds> flows,
        IReadOnlyList<FlowPresentationComment> flowLines, Func<Guid, string> mapName)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(flowLines);
        ArgumentNullException.ThrowIfNull(mapName);
        var byStep = lines.Select(line => line.AsLine()).ToLookup(line => line.SubjectId);
        var byFlow = flowLines.Select(line => line.AsLine()).ToLookup(line => line.SubjectId);
        var flowsOf = flows.ToLookup(flow => flow.Flow.MapId);
        var maps = steps.GroupBy(step => step.MapId).Select(map =>
        {
            var name = mapName(map.Key);
            return map.Select(step => Target(CommentSubject.Of(step), false, step.Code, step.Label, name, byStep))
                .Concat(flowsOf[map.Key].Select(flow => Target(flow.Subject, true, flow.Code, flow.Context, name, byFlow))).ToList();
        });
        return Bounded(maps);
    }

    /// <summary>
    /// The maps taken whole, in order, while they fit in <see cref="AssistantCommentTargets.CommentsMax"/>: the bound cuts
    /// the last maps entire, never the flows of a map listed (ADR 107, point 7; audit of the copy 65, NEW-P3-01). The first
    /// map, the one open, is always listed, its first entries only when it alone passes the bound (its steps before its flows).
    /// </summary>
    private static List<AssistantDraftTarget> Bounded(IEnumerable<List<AssistantDraftTarget>> maps)
    {
        var listed = new List<AssistantDraftTarget>();
        foreach (var map in maps)
        {
            if (listed.Count > 0 && listed.Count + map.Count > AssistantCommentTargets.CommentsMax)
                break;
            listed.AddRange(map.Take(AssistantCommentTargets.CommentsMax - listed.Count));
        }
        return listed;
    }

    private static AssistantDraftTarget Target(CommentSubject subject, bool flow, string reference, string context, string map, ILookup<Guid, CommentLine> lines)
    {
        var original = CommentLineRules.Original(subject, lines[subject.Id]);
        return new AssistantDraftTarget(subject.Id, flow, reference, context.ReplaceLineEndings(" "), map.ReplaceLineEndings(" "), subject.SourceLanguage,
            original?.Text, original is null ? null : Domain.Values.Codes.Of(original.Source), original?.Version ?? 0);
    }
}

/// <summary>
/// The texts of the field `comments` of a reply (ADR 099 revised): each entry `{ step, text }` read, bounded and matched
/// to a comment asked by the code of its step, each step once, or, for the comment of a flow (lot J10), by the codes of
/// its two steps, `A &gt; B`, each flow once; a text too long is cut at a word with an ellipsis, so that the person
/// corrects it rather than loses it. The reply is data, never trusted.
/// </summary>
public static class AssistantCommentsReader
{
    private const int ReferenceMax = 100;

    private const int RawMax = 4000;

    public static IReadOnlyList<AssistantCommentText>? Read(JsonElement root)
    {
        if (!root.TryGetProperty("comments", out var list) || list.ValueKind != JsonValueKind.Array)
            return null;
        var read = new List<AssistantCommentText>();
        foreach (var item in list.EnumerateArray().Take(AssistantCommentTargets.CommentsMax * 2))
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var step = TextOf(item, "step", ReferenceMax);
            var text = TextOf(item, "text", RawMax);
            if (step is not null && text is not null)
                read.Add(new AssistantCommentText(step, text));
        }
        return read;
    }

    /// <summary>The views of the texts matched to the comments asked, in the order of the reply; a code unknown or seen already is left aside.</summary>
    public static IReadOnlyList<AssistantCommentView> Views(IReadOnlyList<AssistantCommentText> texts, IReadOnlyList<AssistantCommentTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(targets);
        return [.. Matched(texts, targets.ToLookup(target => target.Reference, StringComparer.Ordinal), target => target.StepId, reference => reference)
            .Select(match => new AssistantCommentView(match.Target.StepId, match.Target.Reference, match.Target.Original, match.Text))];
    }

    /// <summary>
    /// The views of the texts matched to the comments of flows asked, in the order of the reply: a flow is named by the
    /// codes of its two steps, whatever the spaces around their sign and whatever arrow from the source to the target
    /// stands for it; a flow unknown or seen already is left aside.
    /// </summary>
    public static IReadOnlyList<AssistantFlowCommentView> FlowViews(IReadOnlyList<AssistantCommentText> texts, IReadOnlyList<AssistantFlowCommentTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(targets);
        return [.. Matched(texts, targets.ToLookup(target => target.Reference, StringComparer.Ordinal), target => target.FlowId, FlowReference)
            .Select(match => new AssistantFlowCommentView(match.Target.FlowId, match.Target.Source, match.Target.Target, match.Target.Original, match.Text))];
    }

    /// <summary>
    /// The views of the comments written by the practice `comments` (F-130), matched to the steps and the flows listed (the
    /// map open and its detailed maps), in the order of the list, so that the review reads the maps one after the other
    /// whatever order the reply wrote them in: a step by its code, a flow by the codes of its two steps as for a
    /// translation; a reference unknown or seen already is left aside.
    /// </summary>
    public static IReadOnlyList<AssistantCommentDraftView> DraftViews(IReadOnlyList<AssistantCommentText> texts, IReadOnlyList<AssistantDraftTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(targets);
        var rank = targets.Select((target, index) => (target.SubjectId, index)).ToDictionary(pair => pair.SubjectId, pair => pair.index);
        return [.. Matched(texts, targets.ToLookup(target => target.Reference, StringComparer.Ordinal), target => target.SubjectId, FlowReference)
            .OrderBy(match => rank[match.Target.SubjectId])
            .Select(match => new AssistantCommentDraftView(match.Target.Flow ? null : match.Target.SubjectId, match.Target.Flow ? match.Target.SubjectId : null,
                match.Target.Reference, match.Target.Context, match.Target.Map, match.Text, match.Target.Current, match.Target.CurrentSource, match.Target.Version))];
    }

    /// <summary>The texts of a reply matched to what was asked by its reference, each one once, its text bounded.</summary>
    private static IEnumerable<(T Target, string Text)> Matched<T>(IReadOnlyList<AssistantCommentText> texts, ILookup<string, T> asked, Func<T, Guid> idOf,
        Func<string, string> referenceOf)
    {
        var seen = new HashSet<Guid>();
        foreach (var text in texts)
        {
            var named = asked[referenceOf(text.Step)];
            if (named.Any() && seen.Add(idOf(named.First())) && Bounded(text.Text) is { } said)
                yield return (named.First(), said);
        }
    }

    /// <summary>The signs a reply may write for the `&gt;` of a flow: the sign written as an escape, and the arrows that go from the source to the target.</summary>
    private static readonly string[] ForwardSigns = ["\\u003E", "\\u003e", "&gt;", "\u2192", "\u21D2", "\u27F6", "\u2794", "\u279C", "\u27A1"];

    /// <summary>
    /// The reference of a flow as the comments asked write it, `A &gt; B`, whatever the spaces a reply put around the
    /// sign and whatever arrow from the source to the target it wrote for it (`-&gt;`, `=&gt;`, an arrow character, the
    /// sign written as an escape); any other text as it is. An arrow that goes back, or no arrow, names no flow: a text
    /// is never written on a flow that was guessed.
    /// </summary>
    private static string FlowReference(string reference)
    {
        var codes = ForwardSigns.Aggregate(reference, (text, sign) => text.Replace(sign, ">", StringComparison.Ordinal)).Split('>');
        if (codes.Length != 2)
            return reference;
        var (source, target) = (codes[0].Trim().TrimEnd('-', '=').TrimEnd(), codes[1].Trim());
        return source.Length > 0 && target.Length > 0 ? FlowPresentationCommentRules.CodeOf(source, target) : reference;
    }

    /// <summary>
    /// A text as a comment keeps it: its lines kept (each one is a sentence said), each trimmed, the empty ones dropped;
    /// its other control characters made spaces, a `&lt;` before a letter spaced so that it is never read as a tag (the
    /// rules of entered texts refuse one); cut at a word past <see cref="PresentationCommentRules.TextMax"/>.
    /// </summary>
    public static string? Bounded(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clean = new StringBuilder(text.Length);
        var lines = PresentationCommentRules.Lines(text)!;
        for (var index = 0; index < lines.Length; index++)
        {
            var character = lines[index];
            clean.Append(char.IsControl(character) && character != '\n' ? ' ' : character);
            if (character == '<' && index + 1 < lines.Length && (char.IsAsciiLetter(lines[index + 1]) || lines[index + 1] is '!' or '/' or '?'))
                clean.Append(' ');
        }
        var kept = string.Join('\n', clean.ToString().Split('\n').Select(line => string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0));
        if (kept.Length == 0)
            return null;
        if (kept.Length <= PresentationCommentRules.TextMax)
            return kept;
        // Never between the two halves of a character outside the basic plane (an emoji): the stores would keep a broken one (audit of the copy 64, P2-06).
        var end = PresentationCommentRules.TextMax - 1;
        var cut = kept[..(char.IsHighSurrogate(kept[end - 1]) ? end - 1 : end)];
        var space = cut.LastIndexOfAny([' ', '\n']);
        return (space > PresentationCommentRules.TextMax / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }

    private static string? TextOf(JsonElement item, string name, int max) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text && text.Trim().Length > 0
            ? text.Trim()[..Math.Min(text.Trim().Length, max)]
            : null;
}
