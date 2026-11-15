// StructurePortedCalls.cs
// What the ported structure tests repeat: lanes and cells read from a map, flows read by label, changes sent with the version read.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// Reads a map as the ported 1.x tests did (names, orders, cells, flows written `Source>Target:label` with the labels of
/// the steps) and sends the changes of a lane, a step or a flow with the version read just before, as a client does
/// (If-Match is required in server mode).
/// </summary>
internal static class StructurePortedCalls
{
    public static string LanesUrl(ApiProject project, Guid? mapId = null) => $"/api/maps/{mapId ?? project.RootMapId}/lanes";

    public static string StepsUrl(ApiProject project, Guid? mapId = null) => $"/api/maps/{mapId ?? project.RootMapId}/steps";

    public static string FlowsUrl(ApiProject project, Guid? mapId = null) => $"/api/maps/{mapId ?? project.RootMapId}/flows";

    public static JsonElement LaneOf(JsonElement map, Guid laneId) =>
        map.GetProperty("lanes").EnumerateArray().Single(lane => lane.GetProperty("id").GetGuid() == laneId);

    public static IReadOnlyList<string?> LaneNames(JsonElement map) =>
        [.. map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("name").GetString())];

    public static IReadOnlyList<int> LaneOrders(JsonElement map) =>
        [.. map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("sort_order").GetInt32())];

    public static IReadOnlyList<Guid> LaneIds(JsonElement map) =>
        [.. map.GetProperty("lanes").EnumerateArray().Select(lane => lane.GetProperty("id").GetGuid())];

    /// <summary>The cell of a step: lane, column, row.</summary>
    public static (Guid Lane, int Column, int Row) CellOf(JsonElement map, Guid stepId)
    {
        var step = StructureCalls.StepOf(map, stepId);
        return (step.GetProperty("lane_id").GetGuid(), step.GetProperty("column").GetInt32(), step.GetProperty("row").GetInt32());
    }

    /// <summary>The labels of the steps in the order of the map (the reading order).</summary>
    public static IReadOnlyList<string?> Labels(JsonElement map) =>
        [.. map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("label").GetString())];

    /// <summary>The labels of the steps of one row of a lane, by column.</summary>
    public static IReadOnlyList<string?> LabelsByColumn(JsonElement map) =>
        [.. map.GetProperty("steps").EnumerateArray().OrderBy(step => step.GetProperty("column").GetInt32()).Select(step => step.GetProperty("label").GetString())];

    public static IReadOnlyList<int> Columns(JsonElement map) =>
        [.. map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("column").GetInt32()).Order()];

    /// <summary>The flows of a map as `Source>Target` or `Source>Target:label`, with the labels of their steps, sorted.</summary>
    public static IReadOnlyList<string> FlowTexts(JsonElement map)
    {
        var labels = map.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetProperty("id").GetGuid(), step => step.GetProperty("label").GetString());
        return [.. map.GetProperty("flows").EnumerateArray().Select(flow =>
        {
            var text = $"{labels[flow.GetProperty("source_step_id").GetGuid()]}>{labels[flow.GetProperty("target_step_id").GetGuid()]}";
            return flow.GetProperty("label").GetString() is { } label ? $"{text}:{label}" : text;
        }).Order(StringComparer.Ordinal)];
    }

    /// <summary>The flow between two steps of a map.</summary>
    public static JsonElement FlowBetween(JsonElement map, Guid source, Guid target) =>
        map.GetProperty("flows").EnumerateArray()
            .Single(flow => flow.GetProperty("source_step_id").GetGuid() == source && flow.GetProperty("target_step_id").GetGuid() == target);

    public static IReadOnlyList<string?> UndoKinds(JsonElement reply) =>
        [.. reply.GetProperty("history").GetProperty("undo").EnumerateArray().Select(item => item.GetProperty("kind").GetString())];

    /// <summary>Adds a lane as the facilitator, at the end or at the order given, and returns the reply.</summary>
    public static async Task<JsonElement> AddLaneAsync(ApiProject project, string name, int? sortOrder = null, Guid? mapId = null)
    {
        using var facilitator = project.Facilitator;
        return (await facilitator.PostAsync(LanesUrl(project, mapId), new { name, sort_order = sortOrder })).Ok();
    }

    /// <summary>Changes a lane with the version read on its map just before.</summary>
    public static async Task<ApiResponse> PatchLaneAsync(ApiProject project, ApiClient client, Guid laneId, object body, Guid? mapId = null)
    {
        var version = LaneOf(await project.MapAsync(mapId), laneId).GetProperty("version").GetInt32();
        return await client.PatchAsync($"/api/lanes/{laneId}", body, version);
    }

    /// <summary>Changes a step with the version read on its map just before.</summary>
    public static async Task<ApiResponse> PatchStepAsync(ApiProject project, ApiClient client, Guid stepId, object body, Guid? mapId = null) =>
        await client.PatchAsync($"/api/steps/{stepId}", body, await project.StepVersionAsync(stepId, mapId));

    /// <summary>Relabels the flow between two steps with the version read on its map just before.</summary>
    public static async Task<ApiResponse> RelabelFlowAsync(ApiProject project, ApiClient client, Guid source, Guid target, string? label, Guid? mapId = null)
    {
        var flow = FlowBetween(await project.MapAsync(mapId), source, target);
        return await client.PatchAsync($"/api/flows/{flow.GetProperty("id").GetGuid()}", new { label }, flow.GetProperty("version").GetInt32());
    }

    /// <summary>The user identifier of a member, as the project names him.</summary>
    public static async Task<Guid> MemberIdAsync(ApiProject project, string login)
    {
        using var facilitator = project.Facilitator;
        var detail = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok();
        return detail.GetProperty("members").EnumerateArray()
            .Single(member => string.Equals(member.GetProperty("login").GetString(), login, StringComparison.OrdinalIgnoreCase)).GetProperty("user_id").GetGuid();
    }

    /// <summary>Steps of the first lane of the root map, one per column from 0, each linked to the next when <paramref name="linked"/>.</summary>
    public static async Task<Guid[]> ChainAsync(ApiProject project, bool linked, params string[] labels)
    {
        var steps = new Guid[labels.Length];
        for (var column = 0; column < labels.Length; column++)
            steps[column] = (await project.AddStepAsync(column, labels[column])).Id;
        if (linked)
        {
            for (var rank = 1; rank < steps.Length; rank++)
                await project.AddFlowAsync(steps[rank - 1], steps[rank]);
        }
        return steps;
    }
}
