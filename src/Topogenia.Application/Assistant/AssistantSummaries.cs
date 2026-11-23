// AssistantSummaries.cs
// The summaries of the maps written with the model (plan 3.1, lot R): the maps that may be asked, the ones asked, the reading of the sentences of the reply and their matching to the maps.

using System.Text.Json;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Summaries;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Assistant;

/// <summary>The maps of a project whose summary may be asked, and the ones a request asks (plan 3.1, lot R).</summary>
public static class AssistantSummaryTargets
{
    /// <summary>The most maps one exchange summarises: beyond, the first ones in the order of the project; the rest in a next exchange.</summary>
    public const int MapsMax = 100;

    /// <summary>
    /// Every live map of the project, in the order of their creation (the root map first): its reference for the model
    /// (the code of the project for the root, the code of the step a detailed map details), its name, its summary today.
    /// </summary>
    public static IReadOnlyList<AssistantSummaryTarget> Of(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.All<ProcessMap>().Where(map => map.DeletedAt is null).OrderBy(map => map.CreatedAt).ThenBy(map => map.Id)
            .Select(map =>
            {
                var reference = workspace.ParentSteps(map.Id) is [var parent, ..] ? parent.Code : workspace.Project.Code;
                var summary = workspace.SummaryOf(map.Id);
                var stale = summary is not null && !string.Equals(summary.Fingerprint, MapFingerprint.Of(workspace, map.Id), StringComparison.Ordinal);
                return new AssistantSummaryTarget(map.Id, reference, map.Name, summary?.Text, summary?.Version ?? 0, stale);
            })
            .ToList();
    }

    /// <summary>
    /// The maps a request asks: the maps it names (INTROUVABLE for one that is not a live map of the project), else every map
    /// without a summary or whose summary is stale; RESUMES_A_JOUR when none is left; <see cref="MapsMax"/> at most.
    /// </summary>
    public static IReadOnlyList<AssistantSummaryTarget> Asked(IReadOnlyList<AssistantSummaryTarget> targets, IReadOnlyList<Guid>? maps)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var byId = targets.ToDictionary(target => target.MapId);
        var asked = maps is { Count: > 0 }
            ? maps.Distinct().Select(id => byId.TryGetValue(id, out var target) ? target : throw DomainException.NotFound()).ToList()
            : targets.Where(target => target.Current is null || target.Stale).ToList();
        if (asked.Count == 0)
            throw DomainException.Conflict(ErrorCodes.SummariesUpToDate);
        return asked.Take(MapsMax).ToList();
    }
}

/// <summary>
/// The sentences of a reply of the form `summaries` (plan 3.1, lot R): each entry `{ map, text }` read, bounded and
/// matched to a map of the project by its reference, each map once; a sentence too long is cut at a word with an ellipsis,
/// on one line, so that the person corrects it rather than loses it. The reply is data, never trusted.
/// </summary>
public static class AssistantSummariesReader
{
    private const int ReferenceMax = 100;

    private const int RawMax = 2000;

    public static IReadOnlyList<AssistantSummaryText>? Read(JsonElement root)
    {
        if (!root.TryGetProperty("summaries", out var list) || list.ValueKind != JsonValueKind.Array)
            return null;
        var read = new List<AssistantSummaryText>();
        foreach (var item in list.EnumerateArray().Take(AssistantSummaryTargets.MapsMax * 2))
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var map = TextOf(item, "map", ReferenceMax);
            var text = TextOf(item, "text", RawMax);
            if (map is not null && text is not null)
                read.Add(new AssistantSummaryText(map, text));
        }
        return read;
    }

    /// <summary>The views of the sentences matched to the maps of the project, in the order of the reply; a reference unknown or seen already is left aside.</summary>
    public static IReadOnlyList<AssistantSummaryView> Views(IReadOnlyList<AssistantSummaryText> texts, IReadOnlyList<AssistantSummaryTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(targets);
        var byReference = targets.GroupBy(target => target.Reference, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var seen = new HashSet<Guid>();
        var views = new List<AssistantSummaryView>();
        foreach (var text in texts)
        {
            if (!byReference.TryGetValue(text.Map, out var target) || !seen.Add(target.MapId) || Bounded(text.Text) is not { } sentence)
                continue;
            views.Add(new AssistantSummaryView(target.MapId, target.Reference, target.Name, sentence, target.Current, target.Version));
        }
        return views;
    }

    /// <summary>
    /// A sentence on one line: its control characters made spaces, a `&lt;` before a letter spaced so that it is never read as a
    /// tag (the rules of entered texts refuse one), cut at a word past <see cref="StructureEdits.MapSummaryMax"/>.
    /// </summary>
    public static string? Bounded(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clean = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            clean.Append(char.IsControl(character) ? ' ' : character);
            if (character == '<' && index + 1 < text.Length && (char.IsAsciiLetter(text[index + 1]) || text[index + 1] is '!' or '/' or '?'))
                clean.Append(' ');
        }
        var line = string.Join(' ', clean.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (line.Length == 0)
            return null;
        if (line.Length <= StructureEdits.MapSummaryMax)
            return line;
        // Never between the two halves of a character outside the basic plane, as the comments (audit of the copy 64, P2-06).
        var end = StructureEdits.MapSummaryMax - 1;
        var cut = line[..(char.IsHighSurrogate(line[end - 1]) ? end - 1 : end)];
        var space = cut.LastIndexOf(' ');
        return (space > StructureEdits.MapSummaryMax / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }

    private static string? TextOf(JsonElement item, string name, int max) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text && text.Trim().Length > 0
            ? text.Trim()[..Math.Min(text.Trim().Length, max)]
            : null;
}
