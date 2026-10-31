// PackageBuild.cs
// Rebuilds the records of a read package under new identifiers: project, members, maps, lanes, steps, flows, RACI, open frames.

using System.Text.Json.Nodes;
using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The records of the project of a package (spec 7.3, ADR 052): new identifiers for everything, the readable codes and
/// the counters of the package, rewritten under <c>projectCode</c> when it is another code. Every reference by code is
/// resolved here (the step of a sheet, the ends of a flow...) and refuses the package with the reason <c>reference</c>
/// when it names nothing it carries, or names it twice. An author is found by <c>user</c> (a login that gives nothing
/// takes the actor of the context, the importer); a holder (actor of a lane, assignee of a step) or an updater without
/// login stays empty. The members are taken only when <c>withMembers</c> is true.
/// </summary>
internal sealed partial class PackageBuild
{
    private readonly PackageEntries entries;
    private readonly OperationContext context;
    private readonly string packageCode;
    private readonly string projectCode;
    private readonly Func<string, Guid?> user;
    private readonly bool withMembers;
    private readonly List<IRecord> records = [];
    private readonly Dictionary<CounterKind, int> counters = [];
    private readonly Dictionary<string, Guid> maps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Lane> lanes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Step> steps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> painPoints = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid Source, Guid Target), Guid> flows = [];
    private readonly HashSet<string> codes = new(StringComparer.Ordinal);
    private Guid projectId;

    public PackageBuild(PackageEntries entries, OperationContext context, string projectCode, Func<string, Guid?> user, bool withMembers)
    {
        this.entries = entries;
        this.context = context;
        this.projectCode = projectCode;
        this.user = user;
        this.withMembers = withMembers;
        packageCode = entries.Manifest["projectCode"]!.GetValue<string>();
    }

    /// <summary>The records, the project first, each one after what it names.</summary>
    public IReadOnlyList<IRecord> Run()
    {
        Project();
        if (withMembers && entries.Content.TryGetValue(PackageFormat.Members, out var members))
            Members(members);
        var mapNodes = new ImportJson(PackageFormat.Maps).Items(entries.Content[PackageFormat.Maps], "maps").ToList();
        IndexMaps(mapNodes);
        foreach (var map in mapNodes)
            MapContent(map);
        PainPoints();
        Sheets();
        Documents();
        Translations();
        return records;
    }

    /// <summary>The live steps rebuilt, the dissolved steps of the open frames left out (for the preview).</summary>
    public int LiveSteps => steps.Values.Count(step => step.DeletedAt is null);

    public int Count<T>() => records.OfType<T>().Count();

    private void Project()
    {
        var json = new ImportJson(PackageFormat.Project);
        var node = json.Object(entries.Content[PackageFormat.Project], "project");
        if (json.Text(node, "code") != packageCode)
            throw json.Unresolved("code");
        var counterNode = json.Object(node, "counters");
        foreach (var kind in Enum.GetValues<CounterKind>())
            counters[kind] = json.Integer(counterNode, Codes.Of(kind));
        projectId = context.NewId();
        records.Add(new Project(projectId, projectCode, json.Text(node, "name"), json.Text(node, "sourceLanguage"), json.OptionalText(node, "description"),
            json.OptionalText(node, "sharedFolder"), json.Code<Classification>(node, "classification"), json.OptionalText(node, "sharingGroupLabel"),
            json.OptionalText(node, "translationGlossary"), 0, json.Date(node, "createdAt"), Author(node, "createdBy"), json.OptionalDate(node, "updatedAt"),
            Updater(node, "updatedBy"), null, null, 1)
        {
            WorkdayHours = json.Decimal(node, "workdayHours"),
            FteHoursPerMonth = json.Decimal(node, "fteHoursPerMonth"),
        });
        foreach (var kind in Enum.GetValues<CounterKind>())
            records.Add(new ProjectCounter(projectId, kind, counters[kind]));
    }

    private void Members(JsonObject content)
    {
        var json = new ImportJson(PackageFormat.Members);
        var seen = new HashSet<Guid>();
        foreach (var node in json.Items(content, "members"))
        {
            var login = json.Text(node, "login");
            var member = user(login) ?? context.ActorId;
            if (!seen.Add(member))
                throw json.Unresolved(login);
            records.Add(new ProjectMember(projectId, member, json.Code<MemberRole>(node, "role"), json.Boolean(node, "isSponsor"),
                json.Boolean(node, "isKeyContributor"), json.Integer(node, "displayOrder"), json.Date(node, "createdAt"), Author(node, "createdBy"),
                json.OptionalDate(node, "updatedAt"), Updater(node, "updatedBy")));
        }
    }

    /// <summary>
    /// First pass: an identifier for every map, lane and step of the package, so that a sub-map names its parent step and
    /// a lane its source lane wherever they are. The maps are named by the code of their parent step (none for the root).
    /// </summary>
    private void IndexMaps(IReadOnlyList<JsonNode> mapNodes)
    {
        var json = new ImportJson(PackageFormat.Maps);
        foreach (var map in mapNodes)
        {
            var mapId = context.NewId();
            if (!maps.TryAdd(json.OptionalText(map, "parentStep") ?? "", mapId))
                throw json.Unresolved(json.OptionalText(map, "parentStep") ?? "root");
            foreach (var lane in json.Items(map, "lanes"))
                lanes[Code(json, lane, CounterKind.Lane)] = new Lane(context.NewId(), mapId, "", "", "", LaneType.Person, null, "", 0, null,
                    default, default, null, null, null, null, 1);
            foreach (var step in json.Items(map, "steps"))
                steps[Code(json, step, CounterKind.Step)] = Placeholder(mapId);
            if (map["dissolution"] is JsonObject frame)
                steps[Code(json, json.Object(frame, "step"), CounterKind.Step)] = Placeholder(mapId);
        }
        if (!maps.ContainsKey(""))
            throw json.Unresolved("root");
    }

    private Step Placeholder(Guid mapId) =>
        new(context.NewId(), mapId, Guid.Empty, "", "", "", StepType.Task, 0, 0, null, AutomationStatus.Manual, null, default, default, null, null, null, null, 1);

    /// <summary>Second pass on one map: its record, its lanes, its steps and their RACI, its flows and its open frame.</summary>
    private void MapContent(JsonNode node)
    {
        var json = new ImportJson(PackageFormat.Maps);
        var parent = json.OptionalText(node, "parentStep");
        var mapId = maps[parent ?? ""];
        records.Add(new ProcessMap(mapId, projectId, parent is null ? null : StepOf(json, parent).Id, json.Text(node, "name"),
            json.Text(node, "sourceLanguage"), json.Date(node, "createdAt"), Author(node, "createdBy"), json.OptionalDate(node, "updatedAt"),
            Updater(node, "updatedBy"), null, null, 1));
        var mapLanes = json.Items(node, "lanes").Select(lane => LaneOf(json, mapId, lane)).ToList();
        records.AddRange(mapLanes);
        var mapSteps = json.Items(node, "steps").Select(step => (Node: step, Step: StepOf(json, mapId, step))).ToList();
        records.AddRange(mapSteps.Select(item => item.Step));
        foreach (var (stepNode, step) in mapSteps)
        {
            var roles = new HashSet<Guid>();
            foreach (var raci in json.Items(stepNode, "raci"))
                records.Add(roles.Add(LaneOf(json, json.Text(raci, "lane")).Id) ? new StepRaci(step.Id, LaneOf(json, json.Text(raci, "lane")).Id, json.Code<RaciRole>(raci, "role"), json.Date(raci, "createdAt"),
                    Author(raci, "createdBy")) { UpdatedAt = json.OptionalDate(raci, "updatedAt"), UpdatedBy = Updater(raci, "updatedBy") }
                    : throw json.Unresolved(json.Text(stepNode, "code") + "/" + json.Text(raci, "lane")));
        }
        foreach (var flow in json.Items(node, "flows"))
            records.Add(FlowOf(json, mapId, flow));
        if (node["dissolution"] is JsonObject frame)
            Frame(json, mapId, mapLanes, frame);
    }

    private Lane LaneOf(ImportJson json, Guid mapId, JsonNode node)
    {
        var code = json.Text(node, "code");
        var source = json.OptionalText(node, "sourceLane") is { } sourceCode ? LaneOf(json, sourceCode).Id : (Guid?)null;
        var lane = lanes[code] with
        {
            Code = Recode(code),
            Name = json.Text(node, "name"),
            SourceLanguage = json.Text(node, "sourceLanguage"),
            LaneType = json.Code<LaneType>(node, "laneType"),
            ActorUserId = Holder(node, "actor"),
            Color = json.Text(node, "color"),
            SortOrder = json.Integer(node, "sortOrder"),
            SourceLaneId = source,
            Headcount = json.OptionalInteger(node, "headcount"),
            CreatedAt = json.Date(node, "createdAt"),
            CreatedBy = Author(node, "createdBy"),
            UpdatedAt = json.OptionalDate(node, "updatedAt"),
            UpdatedBy = Updater(node, "updatedBy"),
        };
        lanes[code] = lane;
        return lane.MapId == mapId ? lane : throw json.Unresolved(code);
    }

    /// <summary>A live step on its cell: its lane must be a lane of its map.</summary>
    private Step StepOf(ImportJson json, Guid mapId, JsonNode node)
    {
        var lane = LaneOf(json, json.Text(node, "lane"));
        if (lane.MapId != mapId)
            throw json.Unresolved(json.Text(node, "lane"));
        return Content(json, node, lane.Id, json.Integer(node, "column"), json.Integer(node, "row"), null, null);
    }

    /// <summary>What a step carries besides its cell, on the cell and in the deletion it is given.</summary>
    private Step Content(ImportJson json, JsonNode node, Guid laneId, int column, int row, DateTime? deletedAt, Guid? batch)
    {
        var code = json.Text(node, "code");
        var step = steps[code] with
        {
            LaneId = laneId,
            Code = Recode(code),
            Label = json.Text(node, "label"),
            SourceLanguage = json.Text(node, "sourceLanguage"),
            StepType = json.Code<StepType>(node, "stepType"),
            Column = column,
            Row = row,
            AssigneeUserId = Holder(node, "assignee"),
            AutomationStatus = json.Code<AutomationStatus>(node, "automationStatus"),
            VolumeQuantity = node["volume"] is { } volume ? json.Decimal(volume, "quantity") : null,
            VolumePeriod = node["volume"] is { } period ? json.Code<VolumePeriod>(period, "period") : null,
            CreatedAt = json.Date(node, "createdAt"),
            CreatedBy = Author(node, "createdBy"),
            UpdatedAt = json.OptionalDate(node, "updatedAt"),
            UpdatedBy = Updater(node, "updatedBy"),
            DeletedAt = deletedAt,
            DeletionBatchId = batch,
        };
        steps[code] = step;
        return step;
    }

    private Flow FlowOf(ImportJson json, Guid mapId, JsonNode node)
    {
        var (source, target) = Ends(json, mapId, node);
        var flow = new Flow(context.NewId(), mapId, source, target, json.OptionalText(node, "label"), json.Date(node, "createdAt"), Author(node, "createdBy"),
            json.OptionalDate(node, "updatedAt"), Updater(node, "updatedBy"), null, null, 1)
        {
            SharePercent = json.OptionalDecimal(node, "share"),
        };
        return flows.TryAdd((source, target), flow.Id) ? flow : throw json.Unresolved(json.Text(node, "source") + ">" + json.Text(node, "target"));
    }

    /// <summary>The two ends of a flow: live steps of its map.</summary>
    private (Guid Source, Guid Target) Ends(ImportJson json, Guid mapId, JsonNode node)
    {
        var source = StepOf(json, json.Text(node, "source"));
        var target = StepOf(json, json.Text(node, "target"));
        if (source.MapId != mapId || target.MapId != mapId || source.DeletedAt is not null || target.DeletedAt is not null)
            throw json.Unresolved(json.Text(node, "source") + ">" + json.Text(node, "target"));
        return (source.Id, target.Id);
    }

    /// <summary>
    /// The open frame (ADR 051, 052): its dissolved step comes back deleted in a new deletion batch, dated from the frame
    /// and by its author, on the first lane of the map (a deleted step holds no cell); the original cell of each frame step
    /// is its cell of now; the flows and lanes created by the dissolution are linked to it.
    /// </summary>
    private void Frame(ImportJson json, Guid mapId, IReadOnlyList<Lane> mapLanes, JsonObject frame)
    {
        var createdAt = json.Date(frame, "createdAt");
        var author = Author(frame, "createdBy");
        var batch = new DeletionBatch(context.NewId(), projectId, null, createdAt, author, null, null);
        records.Add(batch);
        var first = mapLanes.OrderBy(lane => lane.SortOrder).FirstOrDefault() ?? throw json.Unresolved("dissolution");
        var dissolved = Content(json, json.Object(frame, "step"), first.Id, 0, 0, createdAt, batch.Id);
        records.Add(dissolved);
        var dissolution = new Dissolution(context.NewId(), mapId, dissolved.Id, batch.Id, createdAt, author, null, null)
        {
            UpdatedAt = json.OptionalDate(frame, "updatedAt"),
            UpdatedBy = Updater(frame, "updatedBy"),
        };
        records.Add(dissolution);
        var framed = new HashSet<Guid>();
        foreach (var code in json.Items(frame, "frameSteps").Select(item => item.GetValue<string>()))
        {
            var step = StepOf(json, code);
            if (step.MapId != mapId || step.DeletedAt is not null || !framed.Add(step.Id))
                throw json.Unresolved(code);
            records.Add(new DissolutionStep(dissolution.Id, step.Id, step.LaneId, step.Column, step.Row));
        }
        var created = new HashSet<Guid>();
        foreach (var ends in json.Items(frame, "createdFlows"))
        {
            var pair = Ends(json, mapId, ends);
            records.Add(new DissolutionObject(dissolution.Id, DissolutionObjectTypes.Flow, flows.TryGetValue(pair, out var flow) && created.Add(flow)
                ? flow
                : throw json.Unresolved(json.Text(ends, "source") + ">" + json.Text(ends, "target"))));
        }
        foreach (var code in json.Items(frame, "createdLanes").Select(item => item.GetValue<string>()))
        {
            var lane = LaneOf(json, code);
            records.Add(new DissolutionObject(dissolution.Id, DissolutionObjectTypes.Lane,
                lane.MapId == mapId && created.Add(lane.Id) ? lane.Id : throw json.Unresolved(code)));
        }
    }
}
