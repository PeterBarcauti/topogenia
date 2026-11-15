// AssistantReports.cs
// The report of a map read from the reply of the model as data (plan 3.1, L4): every text bounded, every list shortened, each score brought within 1 to 5, each wave and each source among those known; the codes checked against the project afterwards.

using System.Text.Json;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Assistant;

/// <summary>
/// Reads the object `report` of a reply: `summary`, `actors`, `chapters`, `journey`, `problems`, `actions`, `conclusion`.
/// Never refused for a length, a number out of its range or an unknown word: the text is cut, the number brought back, the
/// word replaced by the default. Null when the reply holds no object `report`. The pain points noted are given by the
/// application afterwards, never read from the model.
/// </summary>
public static class AssistantReportReader
{
    /// <summary>The sources of a problem and the waves of an action, as the reply writes them.</summary>
    public static readonly IReadOnlyList<string> Sources = ["noted", "spotted"];

    public static readonly IReadOnlyList<string> Waves = ["now", "month", "later"];

    public static AssistantReportView? Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("report", out var report) || report.ValueKind != JsonValueKind.Object)
            return null;
        // The numbers the actions give are those of the list the model wrote: a problem left out does not shift the others.
        var read = Items(report, "problems", AssistantBounds.ProblemsMax).Select(Problem).ToList();
        var problems = read.OfType<AssistantProblemView>().ToList();
        var numbers = read.Select((problem, index) => (problem, index)).Where(pair => pair.problem is not null)
            .Select((pair, kept) => (Written: pair.index + 1, Kept: kept + 1)).ToDictionary(pair => pair.Written, pair => pair.Kept);
        return new AssistantReportView(
            Text(report, "summary", AssistantBounds.SummaryMax) ?? "",
            Text(report, "actors", AssistantBounds.SummaryMax) ?? "",
            Items(report, "chapters", AssistantBounds.ChaptersMax).Select(Chapter).OfType<AssistantChapterView>().ToList(),
            Items(report, "journey", AssistantBounds.JourneyMax).Select(Journey).OfType<AssistantJourneyView>().ToList(),
            problems,
            Items(report, "actions", AssistantBounds.ActionsMax).Select(item => Action(item, numbers)).OfType<AssistantActionView>().ToList(),
            Text(report, "conclusion", AssistantBounds.SummaryMax) ?? "",
            []);
    }

    private static AssistantChapterView? Chapter(JsonElement item)
    {
        var title = Text(item, "title", AssistantBounds.TitleMax);
        return title is null ? null : new AssistantChapterView(title, Short(item, title), CodesOf(item, "steps"), Text(item, "story", AssistantBounds.StoryMax) ?? "");
    }

    private static AssistantJourneyView? Journey(JsonElement item)
    {
        var step = Text(item, "step", 100);
        return step is null ? null : new AssistantJourneyView(step, Short(item, step), Score(item, "score"), Text(item, "why", AssistantBounds.WhyMax) ?? "");
    }

    private static AssistantProblemView? Problem(JsonElement item)
    {
        var title = Text(item, "title", AssistantBounds.TitleMax);
        if (title is null)
            return null;
        var source = Known(Text(item, "source", 20), Sources, "spotted");
        var kind = Codes.TryParse<FindingKind>(Text(item, "kind", 30), out var found) ? Codes.Of(found) : Codes.Of(FindingKind.Other);
        return new AssistantProblemView(source, Text(item, "pain_point", 100), CodesOf(item, "steps"), title, Short(item, title),
            Text(item, "detail", AssistantBounds.DetailMax) ?? "", kind);
    }

    private static AssistantActionView? Action(JsonElement item, IReadOnlyDictionary<int, int> numbers)
    {
        var title = Text(item, "title", AssistantBounds.TitleMax);
        if (title is null)
            return null;
        var treated = Items(item, "problems", AssistantBounds.ProblemsMax * 4).Where(number => number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var value) && numbers.ContainsKey(value))
            .Select(number => numbers[number.GetInt32()]).Distinct().Take(AssistantBounds.ProblemsMax).ToList();
        return new AssistantActionView(title, Short(item, title), Text(item, "detail", AssistantBounds.DetailMax) ?? "", treated, Text(item, "owner", 150) ?? "",
            Score(item, "effort"), Score(item, "impact"), Known(Text(item, "wave", 20), Waves, "later"));
    }

    /// <summary>The short title, cut at its bound; the title itself cut when the reply gives none.</summary>
    private static string Short(JsonElement item, string title) => Text(item, "short", AssistantBounds.ShortMax) ?? Bounded(title, AssistantBounds.ShortMax) ?? title;

    /// <summary>A score from 1 to 5: a number out of the range brought back, none the middle.</summary>
    private static int Score(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)
            ? (int)Math.Clamp(Math.Round(number), 1, 5)
            : 3;

    private static string Known(string? word, IReadOnlyList<string> known, string otherwise) => word is not null && known.Contains(word) ? word : otherwise;

    private static List<string> CodesOf(JsonElement item, string name) =>
        Items(item, name, AssistantBounds.ObjectsMax).Where(code => code.ValueKind == JsonValueKind.String).Select(code => Bounded(code.GetString(), 100))
            .OfType<string>().Distinct(StringComparer.Ordinal).ToList();

    private static IEnumerable<JsonElement> Items(JsonElement element, string name, int max) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Take(max)
            : [];

    private static string? Text(JsonElement element, string name, int max) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? Bounded(value.GetString(), max) : null;

    /// <summary>A text trimmed, without its control characters but the line ends and tabs, cut at its bound; null when empty.</summary>
    private static string? Bounded(string? text, int max)
    {
        var kept = text is null ? null : new string(text.Where(c => !char.IsControl(c) || c is '\n' or '\t').ToArray()).Trim();
        if (string.IsNullOrEmpty(kept))
            return null;
        return kept.Length <= max ? kept : string.Concat(kept.AsSpan(0, max - 1).TrimEnd(), "…");
    }
}

/// <summary>
/// The codes of a report checked against the project (plan 3.1, L4): a step is a live step of the map of the report, or it
/// is left out; a pain point is one the sheets of the map show, or the problem is the model's own (`spotted`); a problem
/// that names one of them is `noted`, whatever the model wrote. The pain points noted are those of the project.
/// </summary>
public static class AssistantReportCodes
{
    public static AssistantReportView Checked(AssistantReportView report, AssistantContext? context)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (context is null)
            return report;
        bool OnMap(string code) => context.Objects.TryGetValue(code, out var found) && found.Kind == "step" && found.MapId == context.MapId;
        var points = context.PainPoints.Select(point => point.Code).ToHashSet(StringComparer.Ordinal);
        string? Noted(string? code) => code is not null && points.Contains(code) ? code : null;
        return report with
        {
            Chapters = [.. report.Chapters.Select(chapter => chapter with { Steps = [.. chapter.Steps.Where(OnMap)] })],
            Journey = [.. report.Journey.Where(step => OnMap(step.Step))],
            Problems = [.. report.Problems.Select(problem => problem with
            {
                Steps = [.. problem.Steps.Where(OnMap)], PainPoint = Noted(problem.PainPoint), Source = Noted(problem.PainPoint) is null ? "spotted" : "noted",
            })],
            Noted = context.PainPoints,
        };
    }

    /// <summary>The codes of steps a report names, for the objects of the reply.</summary>
    public static IEnumerable<string> Named(AssistantReportView? report) =>
        report is null ? [] : report.Chapters.SelectMany(chapter => chapter.Steps).Concat(report.Journey.Select(step => step.Step)).Concat(report.Problems.SelectMany(problem => problem.Steps));
}