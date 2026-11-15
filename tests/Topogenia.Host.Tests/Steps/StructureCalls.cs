// StructureCalls.cs
// The calls the tests of the steps and flows repeat: create a lane, a step, a flow, detail a step, and find an object in a map read.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

/// <summary>A step created through the API: its identifier and its readable code.</summary>
public sealed record CreatedStep(Guid Id, string Code);

/// <summary>Builds the content of a project through the routes, as the facilitator, and reads it back from the map.</summary>
public static class StructureCalls
{
    public static async Task<Guid> AddLaneAsync(this ApiProject project, string name, Guid? mapId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var facilitator = project.Facilitator;
        return (await facilitator.PostAsync($"/api/maps/{mapId ?? project.RootMapId}/lanes", new { name })).Ok().GetProperty("id").GetGuid();
    }

    public static async Task<CreatedStep> AddStepAsync(this ApiProject project, int column, string label, Guid? laneId = null, Guid? mapId = null,
        string type = "task", int row = 0)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var facilitator = project.Facilitator;
        var created = (await facilitator.PostAsync($"/api/maps/{mapId ?? project.RootMapId}/steps",
            new { lane_id = laneId ?? project.FirstLaneId, column, label, type, row })).Ok();
        return new CreatedStep(created.GetProperty("id").GetGuid(), created.GetProperty("code").GetString()!);
    }

    public static async Task<Guid> AddFlowAsync(this ApiProject project, Guid source, Guid target, string? label = null, Guid? mapId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var facilitator = project.Facilitator;
        return (await facilitator.PostAsync($"/api/maps/{mapId ?? project.RootMapId}/flows",
            new { source_step_id = source, target_step_id = target, label })).Ok().GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Details a step of the map named (the root map by default) with the version of the step read from that map, and
    /// returns its sub-map and the first lane of the sub-map.
    /// </summary>
    public static async Task<(Guid MapId, Guid LaneId)> DetailAsync(this ApiProject project, Guid stepId, Guid? mapId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var facilitator = project.Facilitator;
        var version = await project.StepVersionAsync(stepId, mapId);
        var subMap = (await facilitator.PostAsync($"/api/steps/{stepId}/detail", null, version)).Ok().GetProperty("id").GetGuid();
        var lanes = (await project.MapAsync(subMap)).GetProperty("lanes");
        return (subMap, lanes[0].GetProperty("id").GetGuid());
    }

    public static JsonElement StepOf(JsonElement map, Guid id) =>
        map.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("id").GetGuid() == id);

    public static JsonElement FlowOf(JsonElement map, Guid id) =>
        map.GetProperty("flows").EnumerateArray().Single(flow => flow.GetProperty("id").GetGuid() == id);

    /// <summary>The flows of a map as pairs of step identifiers.</summary>
    public static IReadOnlyList<(Guid Source, Guid Target)> FlowPairs(JsonElement map) =>
        [.. map.GetProperty("flows").EnumerateArray().Select(flow => (flow.GetProperty("source_step_id").GetGuid(), flow.GetProperty("target_step_id").GetGuid()))];

    public static async Task<int> StepVersionAsync(this ApiProject project, Guid stepId, Guid? mapId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return StepOf(await project.MapAsync(mapId), stepId).GetProperty("version").GetInt32();
    }

    /// <summary>The field named by a refusal on a field (CHAMP_OBLIGATOIRE, VALEUR_INVALIDE...).</summary>
    public static string? Field(this JsonElement details) => details.GetProperty("field").GetString();

    public static IReadOnlyList<Guid> Ids(this JsonElement reply) => [.. reply.GetProperty("ids").EnumerateArray().Select(id => id.GetGuid())];
}
