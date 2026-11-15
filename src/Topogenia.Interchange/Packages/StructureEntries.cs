// StructureEntries.cs
// project.json, members.json and maps.json of the package (spec 7.2): project, members, maps, lanes, steps, flows, frames.

using System.Text.Json.Nodes;
using Topogenia.Application.Presentation;
using Topogenia.Domain.Model;
using Topogenia.Domain.Summaries;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Json;

namespace Topogenia.Interchange.Packages;

/// <summary>The entries of the structure. Absent values are left out; collections are always written, in their order.</summary>
internal static class StructureEntries
{
    /// <summary>The project, with its photos and the code of its source when it is a target state (2.3, <see cref="SnapshotEntries"/>).</summary>
    public static JsonObject Project(ExportScope scope, People people, string? baselineCode)
    {
        var project = scope.Workspace.Project;
        var node = new JsonObject
        {
            ["code"] = project.Code,
            ["name"] = project.Name,
            ["sourceLanguage"] = project.SourceLanguage,
            ["classification"] = Codes.Of(project.Classification),
            ["counters"] = Counters(scope),
            ["workdayHours"] = CanonicalJson.Number(project.WorkdayHours),
            ["fteHoursPerMonth"] = CanonicalJson.Number(project.FteHoursPerMonth),
        };
        Optional(node, "description", project.Description);
        Optional(node, "sharedFolder", project.SharedFolder);
        Optional(node, "sharingGroupLabel", project.SharingGroupLabel);
        Optional(node, "translationGlossary", project.TranslationGlossary);
        people.WriteAudit(node, project.CreatedAt, project.CreatedBy, project.UpdatedAt, project.UpdatedBy);
        SnapshotEntries.Write(node, scope, people, baselineCode);
        Voices(node, scope);
        return new JsonObject { ["project"] = node };
    }

    /// <summary>The key of the voices of a project, on its project (format 2.4, ADR 103): the field a refusal of its lines names.</summary>
    public const string VoicesKey = PresentationVoiceRules.PackageField;

    /// <summary>
    /// The voices the project keeps for its presentation (ADR 103), by language: the name of each and whether it is a voice
    /// of the computer; neither its version nor its author, which the import writes, so that they go with or without the
    /// people. Left out when the project keeps none: a project without voice keeps the format of the versions before them.
    /// </summary>
    private static void Voices(JsonObject project, ExportScope scope)
    {
        if (scope.Voices.Count == 0)
            return;
        project[VoicesKey] = new JsonArray([.. scope.Voices.Select(voice =>
            (JsonNode)new JsonObject { ["language"] = voice.Language, ["name"] = voice.Name, ["local"] = voice.Local })]);
    }

    /// <summary>Whether the written project.json carries a voice of the project (ADR 103), which only the format 2.4 holds.</summary>
    public static bool CarriesVoices(byte[] project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return JsonNode.Parse(project)?["project"] is JsonObject node && node.ContainsKey(VoicesKey);
    }

    /// <summary>The members and their roles, by display order then login; only when the people are exported.</summary>
    public static JsonObject Members(ExportScope scope, People people)
    {
        var workspace = scope.Workspace;
        var members = workspace.All<ProjectMember>()
            .Select(member => (Member: member, User: workspace.Find<AppUser>(AppUser.KeyOf(member.UserId))
                ?? throw new InvalidOperationException($"The user {member.UserId} of a member is not in the workspace.")))
            .OrderBy(pair => pair.Member.DisplayOrder)
            .ThenBy(pair => pair.User.Login, StringComparer.Ordinal)
            .Select(pair =>
            {
                var node = new JsonObject
                {
                    ["login"] = pair.User.Login,
                    ["displayName"] = pair.User.DisplayName,
                    ["role"] = Codes.Of(pair.Member.Role),
                    ["isSponsor"] = pair.Member.IsSponsor,
                    ["isKeyContributor"] = pair.Member.IsKeyContributor,
                    ["displayOrder"] = pair.Member.DisplayOrder,
                };
                people.WriteAudit(node, pair.Member.CreatedAt, pair.Member.CreatedBy, pair.Member.UpdatedAt, pair.Member.UpdatedBy);
                return (JsonNode)node;
            });
        return new JsonObject { ["members"] = new JsonArray([.. members]) };
    }

    public static JsonObject Maps(ExportScope scope, People people) =>
        new() { ["maps"] = new JsonArray([.. scope.Maps.Select(map => (JsonNode)Map(scope, people, map))]) };

    private static JsonObject Counters(ExportScope scope)
    {
        var counters = new JsonObject();
        foreach (var kind in Enum.GetValues<CounterKind>())
        {
            var counter = scope.Workspace.Find<ProjectCounter>(ProjectCounter.KeyOf(scope.Workspace.ProjectId, kind));
            counters[Codes.Of(kind)] = counter?.LastValue ?? 0;
        }
        return counters;
    }

    private static JsonObject Map(ExportScope scope, People people, ProcessMap map)
    {
        var node = new JsonObject
        {
            ["name"] = map.Name,
            ["sourceLanguage"] = map.SourceLanguage,
            ["lanes"] = new JsonArray([.. scope.Workspace.LanesOf(map.Id).Select(lane => (JsonNode)Lane(scope, people, lane))]),
            ["steps"] = new JsonArray([.. scope.StepsOf(map).Select(step => (JsonNode)Step(scope, people, step))]),
            ["flows"] = new JsonArray([.. scope.FlowsOf(map).Select(flow => (JsonNode)Flow(scope, people, flow))]),
        };
        Optional(node, "parentStep", scope.ParentCode(map));
        people.WriteAudit(node, map.CreatedAt, map.CreatedBy, map.UpdatedAt, map.UpdatedBy);
        if (scope.Frames.TryGetValue(map.Id, out var frame))
            node["dissolution"] = Frame(scope, people, map, frame);
        if (scope.Workspace.SummaryOf(map.Id) is { } summary)
            node["summary"] = Summary(scope, people, summary);
        return node;
    }

    /// <summary>
    /// The summary of a map (format 2.3, plan 3.1, lot R): its sentence, its language, who wrote it and with which model, and
    /// whether it still described the map when it was exported (<c>current</c>), which the import keeps: its fingerprint
    /// depends on identifiers that the import renews.
    /// </summary>
    private static JsonObject Summary(ExportScope scope, People people, MapSummary summary)
    {
        var node = new JsonObject
        {
            ["text"] = summary.Text,
            ["sourceLanguage"] = summary.SourceLanguage,
            ["origin"] = Codes.Of(summary.Origin),
            ["current"] = string.Equals(summary.Fingerprint, MapFingerprint.Of(scope.Workspace, summary.MapId), StringComparison.Ordinal),
        };
        Optional(node, "model", summary.Model);
        people.WriteAudit(node, summary.CreatedAt, summary.CreatedBy, summary.UpdatedAt, summary.UpdatedBy);
        return node;
    }

    private static JsonObject Lane(ExportScope scope, People people, Lane lane)
    {
        var node = new JsonObject
        {
            ["code"] = lane.Code,
            ["name"] = lane.Name,
            ["sourceLanguage"] = lane.SourceLanguage,
            ["laneType"] = Codes.Of(lane.LaneType),
            ["color"] = lane.Color,
            ["sortOrder"] = lane.SortOrder,
        };
        Optional(node, "sourceLane", scope.LaneCode(lane.SourceLaneId));
        if (lane.Headcount is { } headcount)
            node["headcount"] = headcount;
        people.Write(node, "actor", lane.ActorUserId);
        people.WriteAudit(node, lane.CreatedAt, lane.CreatedBy, lane.UpdatedAt, lane.UpdatedBy);
        return node;
    }

    private static JsonObject Step(ExportScope scope, People people, Step step)
    {
        var node = StepContent(people, step);
        node["lane"] = scope.LaneCode(step.LaneId) ?? throw new InvalidOperationException($"The step {step.Code} is on a deleted lane.");
        node["column"] = step.Column;
        node["row"] = step.Row;
        var raci = scope.Workspace.All<StepRaci>()
            .Where(role => role.StepId == step.Id && scope.LaneCode(role.LaneId) is not null)
            .OrderBy(role => scope.LaneCode(role.LaneId), StringComparer.Ordinal)
            .Select(role =>
            {
                var item = new JsonObject { ["lane"] = scope.LaneCode(role.LaneId), ["role"] = Codes.Of(role.Role) };
                people.WriteAudit(item, role.CreatedAt, role.CreatedBy, role.UpdatedAt, role.UpdatedBy);
                return (JsonNode)item;
            });
        node["raci"] = new JsonArray([.. raci]);
        Comment(node, CommentSubject.Of(step), scope.Comments[step.Id].Select(line => line.AsLine()));
        return node;
    }

    /// <summary>
    /// The comment of presentation of a step or of a flow (ADR 099 revised), written the same on both: its original, the
    /// line in the language of origin of its subject (the step; for a flow, its source step), as `presentationComment`;
    /// its other lines as `presentationCommentTranslations`, by language, each with its source. A translation without an
    /// original is carried too: it is said in its language.
    /// </summary>
    private static void Comment(JsonObject node, CommentSubject subject, IEnumerable<CommentLine> lines)
    {
        var translations = new JsonArray();
        foreach (var line in lines.OrderBy(line => line.Language, StringComparer.Ordinal))
        {
            if (subject.WritesOriginalIn(line.Language))
                node[CommentKey] = new JsonObject { ["text"] = line.Text, ["language"] = line.Language };
            else
                translations.Add(new JsonObject { ["language"] = line.Language, ["text"] = line.Text, ["source"] = Codes.Of(line.Source) });
        }
        if (translations.Count > 0)
            node[CommentTranslationsKey] = translations;
    }

    /// <summary>
    /// The key of the original of the comment of presentation of a step or of a flow, the field a refusal of its lines
    /// names, and the key of its translations (format 2.4).
    /// </summary>
    public const string CommentKey = CommentLineRules.PackageField;

    public const string CommentTranslationsKey = "presentationCommentTranslations";

    /// <summary>What a step carries besides its cell: also the whole of a dissolved step, whose cell means nothing any more.</summary>
    private static JsonObject StepContent(People people, Step step)
    {
        var node = new JsonObject
        {
            ["code"] = step.Code,
            ["label"] = step.Label,
            ["sourceLanguage"] = step.SourceLanguage,
            ["stepType"] = Codes.Of(step.StepType),
            ["automationStatus"] = Codes.Of(step.AutomationStatus),
        };
        if (step.VolumeQuantity is { } quantity && step.VolumePeriod is { } period)
            node["volume"] = new JsonObject { ["quantity"] = CanonicalJson.Number(quantity), ["period"] = Codes.Of(period) };
        if (step.ValueClass is { } value)
            node["valueClass"] = Codes.Of(value);
        people.Write(node, "assignee", step.AssigneeUserId);
        people.WriteAudit(node, step.CreatedAt, step.CreatedBy, step.UpdatedAt, step.UpdatedBy);
        return node;
    }

    private static JsonObject Flow(ExportScope scope, People people, Flow flow)
    {
        var node = Ends(scope, flow);
        Optional(node, "label", flow.Label);
        Optional(node, "share", flow.SharePercent);
        if (Route(scope, flow) is { } route)
            node["route"] = route;
        people.WriteAudit(node, flow.CreatedAt, flow.CreatedBy, flow.UpdatedAt, flow.UpdatedBy);
        // The comment of presentation of the flow (ADR 099, lot J10): its original is in the language of origin of its source step.
        var source = scope.Workspace.Step(flow.SourceStepId) ?? throw new InvalidOperationException("An exported flow has its source step in the workspace.");
        Comment(node, CommentSubject.Of(flow, source), scope.FlowComments[flow.Id].Select(line => line.AsLine()));
        return node;
    }

    /// <summary>
    /// The route chosen by the user (2.2, ADR 091): only its parts that are set, the lane of a row by its code, a row
    /// without an edge being its middle; null without any.
    /// </summary>
    private static JsonObject? Route(ExportScope scope, Flow flow)
    {
        var route = new JsonObject();
        if (flow.SourceSide is { } from)
            route["from"] = Codes.Of(from);
        if (flow.TargetSide is { } to)
            route["to"] = Codes.Of(to);
        if (flow.ViaAisle is { } aisle)
            route["via"] = new JsonObject { ["aisle"] = aisle };
        else if (flow.ViaColumn is { } column)
            route["via"] = new JsonObject { ["column"] = column };
        else if (flow.ViaLaneId is { } lane && flow.ViaRow is { } row && scope.LaneCode(lane) is { } code)
        {
            var band = new JsonObject { ["lane"] = code, ["row"] = row };
            if (flow.ViaEdge is { } edge)
                band["edge"] = Codes.Of(edge);
            route["via"] = band;
        }
        return route.Count == 0 ? null : route;
    }

    /// <summary>Whether the written maps.json holds a flow with a route chosen by the user: the format it needs (ADR 091).</summary>
    public static bool CarriesRoutes(byte[] maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        return JsonNode.Parse(maps) is { } root && HasRoute(root);
    }

    /// <summary>True when a map of the entry <c>maps</c> carries a summary (plan 3.1, lot R): the package is then of the format 2.3.</summary>
    public static bool CarriesSummaries(byte[] maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        return JsonNode.Parse(maps)?["maps"] is JsonArray list && list.Any(map => map is JsonObject item && item.ContainsKey("summary"));
    }

    /// <summary>Whether a step or a flow of the entry of the maps carries a comment of presentation (ADR 099), which only the format 2.4 holds.</summary>
    public static bool CarriesComments(byte[] maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        return JsonNode.Parse(maps) is { } root && (HasKey(root, CommentKey) || HasKey(root, CommentTranslationsKey));
    }

    /// <summary>Whether a step of the entry of the maps carries a value class (Makigami M1), which only the format 2.3 holds.</summary>
    public static bool CarriesValueClasses(byte[] maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        return JsonNode.Parse(maps) is { } root && HasKey(root, "valueClass");
    }

    /// <summary>Whether a node or one of its children holds a key.</summary>
    public static bool HasKey(JsonNode node, string key) => node switch
    {
        JsonObject item => item.ContainsKey(key) || item.Any(pair => pair.Value is not null && HasKey(pair.Value, key)),
        JsonArray list => list.Any(child => child is not null && HasKey(child, key)),
        _ => false,
    };

    private static bool HasRoute(JsonNode node) => node switch
    {
        JsonObject item => item.ContainsKey("route") || item.Any(pair => pair.Value is not null && HasRoute(pair.Value)),
        JsonArray list => list.Any(element => element is not null && HasRoute(element)),
        _ => false,
    };

    private static JsonObject Ends(ExportScope scope, Flow flow) =>
        new() { ["source"] = scope.StepCode(flow.SourceStepId), ["target"] = scope.StepCode(flow.TargetStepId) };

    /// <summary>
    /// The open frame: the dissolved step, the steps of the frame still live on the map, and the flows and lanes created
    /// by the dissolution that are still live. The original cells of the frame steps are not exported: they name lanes of
    /// the dissolved sub-map, which is in the trash (ADR 051).
    /// </summary>
    private static JsonObject Frame(ExportScope scope, People people, ProcessMap map, Dissolution frame)
    {
        var workspace = scope.Workspace;
        var members = workspace.All<DissolutionStep>().Where(item => item.DissolutionId == frame.Id).Select(item => item.StepId).ToHashSet();
        var objects = workspace.All<DissolutionObject>().Where(item => item.DissolutionId == frame.Id).ToList();
        var flowIds = objects.Where(item => item.ObjectType == Domain.Dissolutions.DissolutionObjectTypes.Flow).Select(item => item.ObjectId).ToHashSet();
        var laneIds = objects.Where(item => item.ObjectType == Domain.Dissolutions.DissolutionObjectTypes.Lane).Select(item => item.ObjectId).ToHashSet();
        var node = new JsonObject
        {
            ["step"] = StepContent(people, workspace.Step(frame.StepId)!),
            ["frameSteps"] = new JsonArray([.. scope.StepsOf(map).Where(step => members.Contains(step.Id)).Select(step => (JsonNode)step.Code)]),
            ["createdFlows"] = new JsonArray([.. scope.FlowsOf(map).Where(flow => flowIds.Contains(flow.Id)).Select(flow => (JsonNode)Ends(scope, flow))]),
            ["createdLanes"] = new JsonArray([.. workspace.LanesOf(map.Id).Where(lane => laneIds.Contains(lane.Id)).Select(lane => (JsonNode)lane.Code)]),
        };
        people.WriteAudit(node, frame.CreatedAt, frame.CreatedBy, frame.UpdatedAt, frame.UpdatedBy);
        return node;
    }

    internal static void Optional(JsonObject node, string property, string? value)
    {
        if (value is not null)
            node[property] = value;
    }

    internal static void Optional(JsonObject node, string property, decimal? value)
    {
        if (value is { } number)
            node[property] = CanonicalJson.Number(number);
    }
}
