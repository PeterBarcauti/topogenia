// MapDrawing.cs
// Draws the steps and flows of a test through the existing routes and reads them back from a map view.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Selection;

/// <summary>The steps and flows of a test, created by `POST /api/maps/{id}/steps` and `/flows`, and the readings of a map.</summary>
internal static class MapDrawing
{
    public static async Task<Guid> StepAsync(ApiClient client, Guid mapId, Guid laneId, int column, string label) =>
        (await client.PostAsync($"/api/maps/{mapId}/steps", new { lane_id = laneId, column, label })).Ok().GetProperty("id").GetGuid();

    public static async Task<Guid> FlowAsync(ApiClient client, Guid mapId, Guid source, Guid target) =>
        (await client.PostAsync($"/api/maps/{mapId}/flows", new { source_step_id = source, target_step_id = target })).Ok().GetProperty("id").GetGuid();

    /// <summary>Steps in a row on the first lane of the root map, each linked to the next.</summary>
    public static async Task<IReadOnlyList<Guid>> RowAsync(ApiProject project, params string[] labels)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(labels);
        using var facilitator = project.Facilitator;
        var steps = new List<Guid>();
        for (var column = 0; column < labels.Length; column++)
            steps.Add(await StepAsync(facilitator, project.RootMapId, project.FirstLaneId, column, labels[column]));
        for (var rank = 1; rank < steps.Count; rank++)
            await FlowAsync(facilitator, project.RootMapId, steps[rank - 1], steps[rank]);
        return steps;
    }

    public static IReadOnlyList<Guid> StepIds(JsonElement map) =>
        map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetGuid()).ToList();

    public static JsonElement Step(JsonElement map, Guid stepId) =>
        map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("id").GetGuid() == stepId);

    public static IReadOnlySet<(Guid Source, Guid Target)> Flows(JsonElement map) =>
        map.GetProperty("flows").EnumerateArray()
            .Select(flow => (flow.GetProperty("source_step_id").GetGuid(), flow.GetProperty("target_step_id").GetGuid()))
            .ToHashSet();

    public static Guid FirstLane(JsonElement map) => map.GetProperty("lanes")[0].GetProperty("id").GetGuid();

    public static IReadOnlyList<Guid> Ids(JsonElement reply) => reply.GetProperty("ids").EnumerateArray().Select(id => id.GetGuid()).ToList();

    public static IReadOnlyList<string?> Warnings(JsonElement reply) => reply.GetProperty("warnings").EnumerateArray().Select(code => code.GetString()).ToList();
}
