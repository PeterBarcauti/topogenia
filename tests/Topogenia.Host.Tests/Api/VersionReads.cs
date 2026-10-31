// VersionReads.cs
// The version a client reads before a change that sends If-Match: of the project, a lane, a flow, a document, a use (spec 5.3, ADR 045).

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

/// <summary>
/// Reads, as the facilitator, the version of the object of a route that requires `If-Match` in server mode, as the client
/// does before sending it (the version of a step: <c>StructureCalls.StepVersionAsync</c>).
/// </summary>
public static class VersionReads
{
    /// <summary>The version of the project, in its detail (members, sharing group, move to the trash).</summary>
    public static async Task<int> ProjectVersionAsync(this ApiProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var facilitator = project.Facilitator;
        return (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("version").GetInt32();
    }

    public static async Task<int> LaneVersionAsync(this ApiProject project, Guid laneId, Guid? mapId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Item(await project.MapAsync(mapId), "lanes", laneId).GetProperty("version").GetInt32();
    }

    public static async Task<int> FlowVersionAsync(this ApiProject project, Guid flowId, Guid? mapId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Item(await project.MapAsync(mapId), "flows", flowId).GetProperty("version").GetInt32();
    }

    /// <summary>The version of a document of a step, an output or an external input of its sheet.</summary>
    public static async Task<int> DocumentVersionAsync(this ApiProject project, Guid stepId, Guid documentId)
    {
        var sheet = await SheetAsync(project, stepId);
        return sheet.GetProperty("outputs").EnumerateArray().Concat(sheet.GetProperty("inputs").EnumerateArray())
            .Select(entry => entry.GetProperty("document"))
            .First(document => document.GetProperty("id").GetGuid() == documentId).GetProperty("version").GetInt32();
    }

    /// <summary>The version of the use of a document by a step, or 0 when the step does not use it (no use read, ADR 045).</summary>
    public static async Task<int> UseVersionAsync(this ApiProject project, Guid stepId, Guid documentId)
    {
        var use = (await SheetAsync(project, stepId)).GetProperty("inputs").EnumerateArray()
            .Where(input => input.GetProperty("document").GetProperty("id").GetGuid() == documentId)
            .Select(input => input.GetProperty("use")).SingleOrDefault(use => use.ValueKind == JsonValueKind.Object);
        return use.ValueKind == JsonValueKind.Object ? use.GetProperty("version").GetInt32() : 0;
    }

    private static JsonElement Item(JsonElement map, string collection, Guid id) =>
        map.GetProperty(collection).EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);

    private static async Task<JsonElement> SheetAsync(ApiProject project, Guid stepId)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var facilitator = project.Facilitator;
        return (await facilitator.GetAsync($"/api/steps/{stepId}/sheet")).Ok();
    }
}
