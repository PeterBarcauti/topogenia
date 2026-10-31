// PmapCarry.cs
// A proposal opened as a new project with what the steps of its map of origin hold (ADR 082): their sheets and their translations carried by their codes, the uses left without their output dropped.

namespace Topogenia.Interchange.Pmap;

/// <summary>
/// The content of a map of origin carried into a proposal of the assistant opened as a new project (ADR 082, decision of
/// the owner of the product: a process brought back to one level always opens as a new project). The model writes the
/// structure with the codes of the steps of origin; the application carries, for each step whose identifier is one of
/// them: its sheet, when the proposal gives none, and its translations of the languages the proposal does not give, while
/// its label is the one of origin. Then, on each map, a use whose output is not on the same map is dropped, as a use asks.
/// </summary>
public static class PmapCarry
{
    public static PmapDocument Of(PmapDocument proposal, PmapDocument origin)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(origin);
        var steps = new Dictionary<string, PmapStep>(StringComparer.Ordinal);
        Index(origin.Map, steps);
        return proposal with { Map = Settled(Carried(proposal.Map, steps)) };
    }

    private static void Index(PmapMap map, Dictionary<string, PmapStep> steps)
    {
        foreach (var step in map.Steps)
        {
            steps[step.Id] = step;
            if (step.SubMap is { } sub)
                Index(sub, steps);
        }
    }

    private static PmapMap Carried(PmapMap map, Dictionary<string, PmapStep> steps) => map with
    {
        Steps = [.. map.Steps.Select(step =>
        {
            var sub = step.SubMap is { } detail ? Carried(detail, steps) : null;
            if (!steps.TryGetValue(step.Id, out var origin))
                return step with { SubMap = sub };
            var translations = step.Label == origin.Label
                ? origin.Translations.Where(pair => !step.Translations.ContainsKey(pair.Key)).Concat(step.Translations)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : step.Translations;
            return step with { SubMap = sub, Sheet = step.Sheet ?? origin.Sheet, Translations = translations };
        })],
    };

    /// <summary>The uses of each map kept when their output is one of another step of the same map.</summary>
    private static PmapMap Settled(PmapMap map)
    {
        var outputs = map.Steps.Where(step => step.Sheet is not null)
            .SelectMany(step => step.Sheet!.Outputs.Where(output => output.Id is not null).Select(output => (Output: output.Id!, Step: step.Id)))
            .GroupBy(pair => pair.Output, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Step, StringComparer.Ordinal);
        return map with
        {
            Steps = [.. map.Steps.Select(step => step with
            {
                SubMap = step.SubMap is { } sub ? Settled(sub) : null,
                Sheet = step.Sheet is { } sheet
                    ? sheet with { Uses = [.. sheet.Uses.Where(use => outputs.TryGetValue(use.Document, out var producer) && producer != step.Id)] }
                    : null,
            })],
        };
    }
}
