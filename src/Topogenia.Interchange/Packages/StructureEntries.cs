// StructureEntries.cs
// project.json, members.json and maps.json of the package (spec 7.2): project, members, maps, lanes, steps, flows, frames.

using System.Text.Json.Nodes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Json;

namespace Topogenia.Interchange.Packages;

/// <summary>The entries of the structure. Absent values are left out; collections are always written, in their order.</summary>
internal static class StructureEntries
{
    public static JsonObject Project(ExportScope scope, People people)
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
        return new JsonObject { ["project"] = node };
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
        return node;
    }

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
        people.Write(node, "assignee", step.AssigneeUserId);
        people.WriteAudit(node, step.CreatedAt, step.CreatedBy, step.UpdatedAt, step.UpdatedBy);
        return node;
    }

    private static JsonObject Flow(ExportScope scope, People people, Flow flow)
    {
        var node = Ends(scope, flow);
        Optional(node, "label", flow.Label);
        Optional(node, "share", flow.SharePercent);
        people.WriteAudit(node, flow.CreatedAt, flow.CreatedBy, flow.UpdatedAt, flow.UpdatedBy);
        return node;
    }

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
