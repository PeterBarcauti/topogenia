// SnapshotEntries.cs
// The photos of the indicators of a project and the link of a target state to its source in project.json (format 2.3, Makigami M6, M8, ADR 095).

using System.Text;
using System.Text.Json.Nodes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Json;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// What the package keeps of the photos (ADR 095, P3d): each photo and the reference of a target state, with their
/// indicators, label, origin and authors by login, in the object <c>project</c> of project.json; and the source of a target
/// state by the code of its project (<c>baselineProject</c>): an identifier means nothing in another installation, so the
/// import links it again only to a live project of that code. Both are left out when there is none, so that a project
/// without photo nor source keeps the format of the versions before them.
/// </summary>
internal static class SnapshotEntries
{
    public const string Snapshots = "snapshots";
    public const string BaselineProject = "baselineProject";

    /// <summary>
    /// Adds the photos of the project, in the order they were taken (then by their content, so that two exports of the same
    /// state, or of its import, give the same bytes whatever the identifiers), and the code of the source when it is given.
    /// </summary>
    public static void Write(JsonObject project, ExportScope scope, People people, string? baselineCode)
    {
        StructureEntries.Optional(project, BaselineProject, baselineCode);
        var photos = scope.Workspace.All<StreamSnapshot>()
            .Select(snapshot => (snapshot.CreatedAt, Node: Snapshot(people, snapshot)))
            .Select(item => (item.CreatedAt, item.Node, Text: Encoding.UTF8.GetString(CanonicalJson.Write(item.Node))))
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Text, StringComparer.Ordinal)
            .Select(item => (JsonNode)item.Node)
            .ToList();
        if (photos.Count > 0)
            project[Snapshots] = new JsonArray([.. photos]);
    }

    /// <summary>Whether the written project.json carries a photo or the source of a target state: the package is then of the format 2.3.</summary>
    public static bool Carried(byte[] project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return JsonNode.Parse(project)?["project"] is JsonObject node && (node.ContainsKey(Snapshots) || node.ContainsKey(BaselineProject));
    }

    /// <summary>
    /// project.json without the photos nor the source: what the model of the assistant is given (ADR 095, P3d). It reads the
    /// map, not frozen indicators beside which it would not have those of now.
    /// </summary>
    public static byte[] Without(byte[] project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var root = JsonNode.Parse(project) as JsonObject ?? throw new ArgumentException("project.json is an object.", nameof(project));
        if (root["project"] is not JsonObject node || !(node.ContainsKey(Snapshots) || node.ContainsKey(BaselineProject)))
            return project;
        node.Remove(Snapshots);
        node.Remove(BaselineProject);
        return CanonicalJson.Write(root);
    }

    private static JsonObject Snapshot(People people, StreamSnapshot snapshot)
    {
        var node = new JsonObject
        {
            ["label"] = snapshot.Label,
            ["origin"] = Codes.Of(snapshot.Origin),
            ["handoffs"] = snapshot.Handoffs,
            ["mediaBreaks"] = snapshot.MediaBreaks,
            ["lostHoursPerMonth"] = CanonicalJson.Real(snapshot.LostHoursPerMonth),
            ["openPainPoints"] = snapshot.OpenPainPoints,
        };
        Real(node, "leadHours", snapshot.LeadHours);
        Real(node, "handoffWaitHours", snapshot.HandoffWaitHours);
        Real(node, "otherWaitHours", snapshot.OtherWaitHours);
        Real(node, "workMinutes", snapshot.WorkMinutes);
        Real(node, "valueAddedShare", snapshot.ValueAddedShare);
        Real(node, "coverage", snapshot.Coverage);
        Real(node, "handoffsPerCase", snapshot.HandoffsPerCase);
        Real(node, "rolledCompleteAccurate", snapshot.RolledCompleteAccurate);
        people.WriteAudit(node, snapshot.CreatedAt, snapshot.CreatedBy, snapshot.UpdatedAt, snapshot.UpdatedBy);
        return node;
    }

    /// <summary>An indicator that may be unknown: left out when it is.</summary>
    private static void Real(JsonObject node, string property, double? value)
    {
        if (value is { } number)
            node[property] = CanonicalJson.Real(number);
    }
}
