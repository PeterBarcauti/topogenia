// ChangeSetJson.cs
// The JSON of a change set in the operation log: every record before and after, named by the type of its key (spec 4.6, ADR 042).

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.OperationLog;

/// <summary>
/// Writes and reads the change sets kept by the operation log (`forward_json`, `inverse_json`) and the touched objects
/// (`touched`). Each change names its record by the type of its key (`step`, `lane`...: the technical names of
/// `RecordKey`, which never follow a renaming of a class) and holds the whole record before and after, so that the domain
/// replays it exactly (`UndoReplay`). Closed lists are written by their name, dates in UTC with their full precision.
/// </summary>
public static class ChangeSetJson
{
    /// <summary>Every record type that a change set may hold, by the type of its key; the log never holds its own entries.</summary>
    public static IReadOnlyDictionary<string, Type> RecordTypes { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["user"] = typeof(AppUser),
        ["project"] = typeof(Project),
        ["counter"] = typeof(ProjectCounter),
        ["member"] = typeof(ProjectMember),
        ["batch"] = typeof(DeletionBatch),
        ["sheet"] = typeof(StepSheet),
        ["operation"] = typeof(SheetOperation),
        ["sheet_version"] = typeof(StepSheetVersion),
        ["document"] = typeof(Document),
        ["use"] = typeof(DocumentUse),
        ["pain_point"] = typeof(PainPoint),
        ["pain_point_status"] = typeof(PainPointStatusChange),
        ["translation"] = typeof(Translation),
        ["audit"] = typeof(AuditEvent),
        ["map"] = typeof(ProcessMap),
        ["lane"] = typeof(Lane),
        ["step"] = typeof(Step),
        ["flow"] = typeof(Flow),
        ["raci"] = typeof(StepRaci),
        ["dissolution"] = typeof(Dissolution),
        ["dissolution_step"] = typeof(DissolutionStep),
        ["dissolution_object"] = typeof(DissolutionObject),
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
        // Only the fields of a record: its key and its computed properties (IsAlive, IsRoot...) are derived from them.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { DropComputedProperties } },
    };

    public static string Write(ChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var array = new JsonArray();
        foreach (var change in changes.Changes)
        {
            array.Add(new JsonObject
            {
                ["change"] = change.Kind.ToString().ToLowerInvariant(),
                ["type"] = change.Key.Type,
                ["before"] = Node(change.Before),
                ["after"] = Node(change.After),
            });
        }
        return array.ToJsonString(Options);
    }

    public static ChangeSet Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var array = JsonNode.Parse(json)?.AsArray() ?? throw new JsonException("A change set is a JSON array.");
        var changes = new List<RecordChange>();
        foreach (var node in array)
        {
            var item = node?.AsObject() ?? throw new JsonException("A change is a JSON object.");
            var name = item["type"]?.GetValue<string>() ?? throw new JsonException("A change names its type.");
            var type = RecordTypes.TryGetValue(name, out var known) ? known : throw new JsonException($"Unknown record type {name}.");
            var kind = Enum.Parse<ChangeKind>(item["change"]?.GetValue<string>() ?? "", ignoreCase: true);
            changes.Add(new RecordChange(kind, Record(item["before"], type), Record(item["after"], type)));
        }
        return new ChangeSet(changes);
    }

    /// <summary>The touched objects with the version they have after the operation (spec 4.6 `touched`).</summary>
    public static string WriteTouched(ChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var array = new JsonArray();
        foreach (var (key, expected) in changes.Touched)
            array.Add(new JsonObject { ["type"] = key.Type, ["key"] = key.Value, ["version"] = expected.Version });
        return array.ToJsonString(Options);
    }

    private static JsonNode? Node(IRecord? record) =>
        record is null ? null : JsonSerializer.SerializeToNode(record, record.GetType(), Options);

    private static IRecord? Record(JsonNode? node, Type type) =>
        node is null ? null : (IRecord)(node.Deserialize(type, Options) ?? throw new JsonException($"Empty {type.Name}."));

    private static void DropComputedProperties(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || !typeof(IRecord).IsAssignableFrom(info.Type))
            return;
        for (var index = info.Properties.Count - 1; index >= 0; index--)
        {
            if (info.Properties[index].AttributeProvider is PropertyInfo { SetMethod: null })
                info.Properties.RemoveAt(index);
        }
    }
}
