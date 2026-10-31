// AssistantReplies.cs
// The reply of the model read as data (spec 3.0 section 6: validated like an imported file): its form, message, questions, findings and proposal, every text bounded; a bare .pmap pasted is a proposal.

using System.Text.Json;
using System.Text.RegularExpressions;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Assistant;

/// <summary>
/// A reply read: its form, its message, the codes it cites, its questions and findings, the text of the `.pmap` it
/// proposes (null but for a proposal), and the JSON as written, given back to the model with the next turns.
/// </summary>
public sealed record AssistantReply(
    AssistantForm Form,
    string Message,
    IReadOnlyList<string> Cited,
    IReadOnlyList<AssistantQuestionView> Questions,
    IReadOnlyList<AssistantFindingView> Findings,
    string? Pmap,
    string Raw);

/// <summary>
/// Reads the JSON object of a reply (spec 3.0 section 7: `form`, `message`, `cited`, `questions`, `findings`, `pmap`). The
/// reply of a model constrained to its schema reads as is; a reply pasted by the person may come with words or a fence
/// around it, which are left aside, and a `.pmap` alone (its `format` is the marker of the format) is a proposal. A text too long is cut,
/// a list too long shortened: the reply is data, never trusted, never refused for its length alone. Null when no object
/// of a reply can be read: the reason is then given.
/// </summary>
public static partial class AssistantReplyReader
{
    /// <summary>The `format` of a `.pmap` file: the root identifier of the solution, as the format writes it (ADR 054).</summary>
    private static readonly string PmapMarker = nameof(Topogenia).ToLowerInvariant();

    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 256, AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>The codes a text may hold: the code of a project, a dash, a letter and digits (`CONGES-E004`, `FAC-C02`).</summary>
    [GeneratedRegex(@"\b[A-Z][A-Z0-9]{1,15}-[A-Z][0-9]{2,6}\b")]
    public static partial Regex CodePattern();

    public static AssistantReply? Read(string? text, out string? reason)
    {
        reason = null;
        var json = ObjectOf(text);
        if (json is null)
        {
            reason = "no JSON object";
            return null;
        }
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, Options);
        }
        catch (JsonException failure)
        {
            reason = failure.Message;
            return null;
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                reason = "not an object";
                return null;
            }
            if (Text(root, "format") == PmapMarker)
                return new AssistantReply(AssistantForm.Proposal, "", [], [], [], json, json);
            if (!Codes.TryParse<AssistantForm>(Text(root, "form"), out var form))
            {
                reason = "form missing or unknown";
                return null;
            }
            var message = Bounded(Text(root, "message"), AssistantBounds.MessageMax) ?? "";
            var cited = Strings(root, "cited", AssistantBounds.ObjectsMax, 100);
            var questions = Items(root, "questions", AssistantBounds.QuestionsMax).Select(Question).OfType<AssistantQuestionView>().ToList();
            var findings = Items(root, "findings", AssistantBounds.FindingsMax).Select(Finding).OfType<AssistantFindingView>().ToList();
            var pmap = root.TryGetProperty("pmap", out var value) ? PmapOf(value) : null;
            switch (form)
            {
                case AssistantForm.Answer when message.Length == 0:
                    reason = "answer without message";
                    return null;
                case AssistantForm.Questions when questions.Count == 0:
                    reason = "questions without question";
                    return null;
                case AssistantForm.Proposal when string.IsNullOrWhiteSpace(pmap):
                    reason = "proposal without pmap";
                    return null;
            }
            return new AssistantReply(form, message, cited, questions, findings, form == AssistantForm.Proposal ? pmap : null, json);
        }
    }

    /// <summary>
    /// The `.pmap` of a proposal: the text the schema asks, or an object written in its place by a reply pasted, taken as
    /// its JSON text.
    /// </summary>
    private static string? PmapOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Object => value.GetRawText(),
        _ => null,
    };

    /// <summary>
    /// The JSON object of a text: from its first brace to its last, a fence of Markdown or words around it being left aside
    /// (the reply of an AI pasted, AR-154); null when the text has none.
    /// </summary>
    public static string? ObjectOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var start = text.IndexOf('{', StringComparison.Ordinal);
        var end = text.LastIndexOf('}');
        return start < 0 || end <= start ? null : text[start..(end + 1)];
    }

    private static AssistantQuestionView? Question(JsonElement item)
    {
        var question = item.ValueKind == JsonValueKind.String ? item.GetString() : Text(item, "question");
        var bounded = Bounded(question, AssistantBounds.QuestionMax);
        return bounded is null ? null : new AssistantQuestionView(bounded, Strings(item, "choices", AssistantBounds.ChoicesMax, AssistantBounds.ChoiceMax));
    }

    private static AssistantFindingView? Finding(JsonElement item)
    {
        var title = Bounded(Text(item, "title"), AssistantBounds.TitleMax);
        var detail = Bounded(Text(item, "detail"), AssistantBounds.DetailMax) ?? "";
        if (title is null && detail.Length == 0)
            return null;
        var kind = Codes.TryParse<FindingKind>(Text(item, "kind"), out var known) ? known : FindingKind.Other;
        return new AssistantFindingView(Codes.Of(kind), title ?? "", detail, Strings(item, "objects", AssistantBounds.ObjectsMax, 100),
            Bounded(Text(item, "suggestion"), AssistantBounds.DetailMax));
    }

    private static IEnumerable<JsonElement> Items(JsonElement element, string name, int max) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Take(max)
            : [];

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string> Strings(JsonElement element, string name, int count, int length) =>
        Items(element, name, count).Where(item => item.ValueKind == JsonValueKind.String).Select(item => Bounded(item.GetString(), length))
            .OfType<string>().Distinct(StringComparer.Ordinal).ToList();

    /// <summary>A text trimmed, without its control characters but the line ends and tabs, cut at its bound; null when empty.</summary>
    private static string? Bounded(string? text, int max)
    {
        var kept = text is null ? null : new string(text.Where(c => !char.IsControl(c) || c is '\n' or '\t').ToArray()).Trim();
        if (string.IsNullOrEmpty(kept))
            return null;
        return kept.Length <= max ? kept : string.Concat(kept.AsSpan(0, max - 1), "…");
    }
}
