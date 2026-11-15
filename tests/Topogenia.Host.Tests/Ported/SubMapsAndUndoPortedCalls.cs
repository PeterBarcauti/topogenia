// SubMapsAndUndoPortedCalls.cs
// The calls the ported sub-map and undo tests share: a project led by the stranger, the version of a map, the steps of a map by label.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

internal static class SubMapsAndUndoPortedCalls
{
    /// <summary>
    /// A project created by the administrator whose only other member is the stranger, as facilitator: a facilitator who is
    /// no member of the projects of <see cref="ApiProject"/>. Returns its root map.
    /// </summary>
    public static async Task<Guid> ProjectLedByTheStrangerAsync(ApiHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        using var admin = host.As(Logins.Admin);
        var code = ApiProject.NewCode();
        var created = (await admin.PostAsync("/api/projects", new
        {
            code,
            name = $"Projet {code}",
            members = new object[] { new { login = Logins.Stranger, role = "facilitator", is_sponsor = true } },
        })).Ok(201);
        return created.GetProperty("root_map_id").GetGuid();
    }

    /// <summary>The version of a map read by the facilitator, for `If-Match` (« Delete the detail », reattach).</summary>
    public static async Task<int> MapVersionAsync(ApiProject project, Guid mapId)
    {
        ArgumentNullException.ThrowIfNull(project);
        return (await project.MapAsync(mapId)).GetProperty("version").GetInt32();
    }

    /// <summary>The column of each step of a map, by label.</summary>
    public static IReadOnlyDictionary<string, int> ColumnsByLabel(JsonElement map) =>
        map.GetProperty("steps").EnumerateArray().ToDictionary(step => step.GetProperty("label").GetString()!, step => step.GetProperty("column").GetInt32(), StringComparer.Ordinal);

    public static IReadOnlyList<string?> Labels(JsonElement map) =>
        [.. map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("label").GetString())];

    public static IReadOnlyList<Guid> UndoOperations(JsonElement history) =>
        [.. history.GetProperty("undo").EnumerateArray().Select(item => item.GetProperty("operation_id").GetGuid())];

    public static IReadOnlyList<Guid> RedoOperations(JsonElement history) =>
        [.. history.GetProperty("redo").EnumerateArray().Select(item => item.GetProperty("operation_id").GetGuid())];
}
