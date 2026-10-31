// StepSheets.cs
// What the tests of the sheets and documents set up through the API: a step, the contributor made actor of its lane, a sheet to save and the If-Match read.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Sheets;

/// <summary>
/// Steps created by `POST /api/maps/{id}/steps` and the right to fill of the contributor given by `PATCH /api/lanes/{id}`
/// with his identifier read in `GET /api/projects/{id}` and the version of the lane read on its map (the actor of the
/// lane fills its sheets, A-1.1-15).
/// </summary>
public static class StepSheets
{
    /// <summary>A new step of the root map, in the first lane unless another is named; returns its identifier.</summary>
    public static async Task<Guid> StepAsync(ApiProject project, int column, string label = "Commander", string? type = null, Guid? laneId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var facilitator = project.Facilitator;
        var created = await facilitator.PostAsync($"/api/maps/{project.RootMapId}/steps", new { lane_id = laneId ?? project.FirstLaneId, column, label, type });
        return created.Ok().GetProperty("id").GetGuid();
    }

    /// <summary>Makes the contributor the actor of the lane (the first one by default), which gives him the right to fill its sheets.</summary>
    public static async Task MakeContributorActorAsync(ApiProject project, Guid? laneId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var contributor = await ContributorIdAsync(project);
        var lane = laneId ?? project.FirstLaneId;
        var read = (await project.MapAsync()).GetProperty("lanes").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetGuid() == lane).GetProperty("version").GetInt32();
        using var facilitator = project.Facilitator;
        (await facilitator.PatchAsync($"/api/lanes/{lane}", new { actor_user_id = contributor }, read)).Ok();
    }

    /// <summary>The identifier of the contributor, as the project names him.</summary>
    public static async Task<Guid> ContributorIdAsync(ApiProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var facilitator = project.Facilitator;
        var members = (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("members").EnumerateArray();
        return members.Single(member => string.Equals(member.GetProperty("login").GetString(), Logins.Contributor, StringComparison.OrdinalIgnoreCase))
            .GetProperty("user_id").GetGuid();
    }

    /// <summary>A complete sheet with only its purpose filled.</summary>
    public static object Purpose(string purpose) => new { fields = new { purpose = new { value = purpose } } };

    /// <summary>The purpose shown by a sheet, or null.</summary>
    public static string? PurposeOf(JsonElement sheet) => sheet.GetProperty("content").GetProperty("purpose").GetString();

    /// <summary>The version of the draft to send back in `If-Match`.</summary>
    public static int VersionOf(JsonElement sheet) => sheet.GetProperty("version").GetInt32();

    /// <summary>The version the client read no record with: `If-Match` 0 (required in server mode, ADR 045).</summary>
    public const int NoRecord = 0;

    /// <summary>
    /// The `If-Match` a client sends after reading the sheet: the version of the draft, or <see cref="NoRecord"/> when the
    /// reply carries none (`version` null, no draft read).
    /// </summary>
    public static int ReadVersionOf(JsonElement sheet) =>
        sheet.GetProperty("version") is { ValueKind: JsonValueKind.Number } version ? version.GetInt32() : NoRecord;

    /// <summary>Reads the sheet of the step as the client does before a write, and returns the `If-Match` to send.</summary>
    public static async Task<int> ReadVersionAsync(ApiClient client, Guid stepId)
    {
        ArgumentNullException.ThrowIfNull(client);
        return ReadVersionOf((await client.GetAsync($"/api/steps/{stepId}/sheet")).Ok());
    }
}
