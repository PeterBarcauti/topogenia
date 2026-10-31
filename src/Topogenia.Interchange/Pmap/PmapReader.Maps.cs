// PmapReader.Maps.cs
// The maps of a .pmap file: lanes, steps, columns and cells, sub-maps, RACI of the other lanes, flows (annex A 2.17, spec 7.5).

using System.Text.Json;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Interchange.Pmap;

internal sealed partial class PmapReading
{
    private const string LaneKind = "lane";
    private const string StepKind = "step";

    private static readonly string[] MapProperties = ["lanes", "steps", "flows"];
    private static readonly string[] LaneProperties = ["id", "name", "type", "color", "translations"];
    private static readonly string[] StepProperties = ["id", "lane", "label", "type", "column", "row", "automation", "raci", "sub_map", "translations", "sheet"];
    private static readonly string[] FlowProperties = ["source", "target", "label", "translations"];

    // The version 1.2 adds the entries of the value stream (ADR 085); in a file of the version 1.1 they are unknown.
    private static readonly string[] LaneProperties12 = [.. LaneProperties, "headcount"];
    private static readonly string[] StepProperties12 = [.. StepProperties, "volume"];
    private static readonly string[] FlowProperties12 = [.. FlowProperties, "share"];
    private static readonly string[] VolumeProperties = ["quantity", "period"];
    private static readonly IReadOnlyList<string> Periods = Codes.All<VolumePeriod>();

    private static readonly IReadOnlyList<string> LaneTypes = Codes.All<LaneType>();
    private static readonly IReadOnlyList<string> StepTypeCodes = Codes.All<StepType>();
    private static readonly IReadOnlyList<string> Automations = Codes.All<AutomationStatus>();

    /// <summary>
    /// A map and its sub-maps, <paramref name="depth"/> 1 for the root. The valid identifiers are kept even when their
    /// element has another fault: a fault on a lane or a step does not raise others on what refers to it.
    /// </summary>
    private PmapMap? Map(JsonElement map, string path, int depth)
    {
        Properties(map, path, MapProperties);
        var laneIds = new HashSet<string>(StringComparer.Ordinal);
        var lanes = new List<PmapLane>();
        if (List(map, "lanes", path, required: true) is { } laneList)
        {
            if (laneList.GetArrayLength() == 0)
                Fault($"{path}.lanes", FileErrorCodes.MapWithoutLane);
            if (laneList.GetArrayLength() > PmapFormat.LanesMax)
                Fault($"{path}.lanes", FileErrorCodes.TooManyLanes, ("max", PmapFormat.LanesMax));
            var i = 0;
            foreach (var element in laneList.EnumerateArray())
            {
                if (Lane(element, $"{path}.lanes[{i++}]", laneIds) is { } lane)
                    lanes.Add(lane);
            }
        }
        var stepIds = new HashSet<string>(StringComparer.Ordinal);
        var steps = new List<PmapStep>();
        var stepPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        if (List(map, "steps", path, required: false) is { } stepList)
        {
            var i = 0;
            foreach (var element in stepList.EnumerateArray())
            {
                var stepPath = $"{path}.steps[{i++}]";
                if (Step(element, stepPath, laneIds, stepIds, depth) is { } step)
                {
                    steps.Add(step);
                    stepPaths[step.Id] = stepPath;
                }
            }
        }
        stepCount += steps.Count;
        Cells(steps, stepPaths, path);
        Uses(steps, stepPaths);
        var flows = Flows(map, path, stepIds);
        return lanes.Count == 0 ? null : new PmapMap(lanes, steps, flows);
    }

    /// <summary>Columns for all the steps of a map or for none (automatic placement); two steps never share a cell.</summary>
    private void Cells(List<PmapStep> steps, Dictionary<string, string> stepPaths, string path)
    {
        var placed = steps.Count(step => step.Column is not null);
        if (placed > 0 && placed < steps.Count)
        {
            Fault($"{path}.steps", FileErrorCodes.PartialColumns);
            return;
        }
        if (placed == 0)
            return;
        foreach (var same in steps.GroupBy(step => (step.Lane, step.Column, step.Row)).Where(group => group.Count() > 1))
            Fault(stepPaths[same.First().Id], FileErrorCodes.SameCell, ("lane", same.Key.Lane), ("column", same.Key.Column),
                ("steps", string.Join(", ", same.Select(step => step.Id))));
    }

    private PmapLane? Lane(JsonElement element, string path, HashSet<string> laneIds)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            Fault(path, FileErrorCodes.LaneShape);
            return null;
        }
        Properties(element, path, valueStream ? LaneProperties12 : LaneProperties);
        var id = Identifier(element, path, LaneKind);
        if (id is not null)
            laneIds.Add(id);
        var name = Entered(element, "name", path, PmapFormat.LaneNameMax, required: true);
        var type = Enumeration(element, "type", path, LaneTypes) ?? Codes.Of(LaneType.Person);
        var color = Text(element, "color", path, required: false);
        string? parsed = null;
        if (color is not null)
        {
            if (LaneColor.TryParse(color, out var valid))
                parsed = valid.Value;
            else
                Fault($"{path}.color", FileErrorCodes.InvalidColor, ("color", color));
        }
        var translations = Translations(element, path, PmapFormat.LaneNameMax);
        var headcount = Whole(element, "headcount", path);
        return id is null || name is null ? null : new PmapLane(id, name, type, parsed, translations) { Headcount = headcount };
    }

    private PmapStep? Step(JsonElement element, string path, HashSet<string> laneIds, HashSet<string> stepIds, int depth)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            Fault(path, FileErrorCodes.StepShape);
            return null;
        }
        Properties(element, path, valueStream ? StepProperties12 : StepProperties);
        var id = Identifier(element, path, StepKind);
        if (id is not null)
            stepIds.Add(id);
        var lane = Text(element, "lane", path, required: true);
        if (lane is not null && !laneIds.Contains(lane))
        {
            Fault($"{path}.lane", FileErrorCodes.UnknownLane, ("lane", lane));
            lane = null;
        }
        var label = Entered(element, "label", path, PmapFormat.StepLabelMax, required: true);
        var type = Enumeration(element, "type", path, StepTypeCodes) ?? Codes.Of(StepType.Task);
        var noContent = StepTypes.HasNoContent(Codes.Parse<StepType>(type, "type"));
        var automation = Enumeration(element, "automation", path, Automations) ?? Codes.Of(AutomationStatus.Manual);
        var column = Place(element, "column", path, Cell.MaxColumn, FileErrorCodes.InvalidColumn);
        var row = Place(element, "row", path, Cell.MaxRow, FileErrorCodes.InvalidRow) ?? 0;
        var raci = Raci(element, path, laneIds, lane, noContent);
        var subMap = SubMap(element, path, noContent, depth);
        var translations = Translations(element, path, PmapFormat.StepLabelMax);
        currentStep = id ?? string.Empty;
        var sheet = Sheet(element, path, noContent);
        var volume = Volume(element, path);
        if (id is null || lane is null || label is null)
            return null;
        // A sub-map without a step details nothing: it is left out (A-2.17-25).
        if (subMap is { Steps.Count: 0 })
            subMap = null;
        return new PmapStep(id, lane, label, type, column, row, automation, raci, subMap, translations, sheet) { Volume = volume };
    }

    /// <summary>The incoming volume of a step (1.2): an object with a quantity and a period code; its rules are those of the domain.</summary>
    private PmapVolume? Volume(JsonElement element, string path)
    {
        if (Object(element, "volume", path, required: false) is not { } volume)
            return null;
        var place = Join(path, "volume");
        Properties(volume, place, VolumeProperties);
        var quantity = Number(volume, "quantity", place);
        var period = Enumeration(volume, "period", place, Periods);
        if (quantity is null || period is null)
        {
            Fault(place, FileErrorCodes.InvalidValue, ("value", volume.ToString()), ("possible", "{ \"quantity\", \"period\": " + string.Join(", ", Periods) + " }"));
            return null;
        }
        return new PmapVolume(quantity.Value, period);
    }

    /// <summary>A column (0 to 10 000) or a row (0 to 50), null when absent or out of bounds (a fault that gives the bounds).</summary>
    private int? Place(JsonElement element, string name, string path, int max, string code)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 0 && number <= max)
            return number;
        Fault($"{path}.{name}", code, ("max", max));
        return null;
    }

    private PmapMap? SubMap(JsonElement element, string path, bool noContent, int depth)
    {
        if (!element.TryGetProperty("sub_map", out var subMap) || subMap.ValueKind == JsonValueKind.Null)
            return null;
        if (subMap.ValueKind != JsonValueKind.Object)
            Fault($"{path}.sub_map", FileErrorCodes.SubMapShape);
        else if (noContent)
            Fault($"{path}.sub_map", FileErrorCodes.SubMapOnEmptyStep);
        else if (depth >= PmapFormat.DepthMax)
            Fault($"{path}.sub_map", FileErrorCodes.TooDeep, ("max", PmapFormat.DepthMax));
        else
            return Map(subMap, $"{path}.sub_map", depth + 1);
        return null;
    }

    /// <summary>
    /// The roles of the other lanes of the same map, `{lane: letter}` (A-2.17-26 to 29): the R is the lane of the step,
    /// written again it is ignored, on another lane it is refused; another letter on the lane of the step is refused; the
    /// letters are those of the setting, one A at most; no RACI on a start, an end or a parallel or inclusive gateway
    /// (spec 7.5).
    /// </summary>
    private Dictionary<string, string> Raci(JsonElement element, string path, HashSet<string> laneIds, string? stepLane, bool noContent)
    {
        var roles = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!element.TryGetProperty("raci", out var raci) || raci.ValueKind == JsonValueKind.Null)
            return roles;
        var place = $"{path}.raci";
        if (raci.ValueKind != JsonValueKind.Object)
        {
            Fault(place, FileErrorCodes.RaciShape);
            return roles;
        }
        if (noContent && raci.EnumerateObject().Any())
        {
            Fault(place, FileErrorCodes.RaciOnStepType);
            return roles;
        }
        var letters = string.Join(", ", settings.RaciLetters.Select(letter => letter.ToString()));
        foreach (var property in raci.EnumerateObject())
        {
            var lane = property.Name;
            var role = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : string.Empty;
            if (!laneIds.Contains(lane))
                Fault(place, FileErrorCodes.UnknownLane, ("lane", lane));
            else if (role == "R")
            {
                // The R is always the lane of the step: writing it again changes nothing.
                if (lane != stepLane)
                    Fault(place, FileErrorCodes.RaciRElsewhere, ("lane", lane), ("own", stepLane), ("letters", letters));
            }
            else if (role.Length != 1 || !settings.RaciLetters.Contains(role[0], StringComparison.Ordinal))
                Fault(place, FileErrorCodes.RaciInvalidRole, ("role", role), ("lane", lane), ("letters", letters));
            else if (lane == stepLane)
                Fault(place, FileErrorCodes.RaciOwnLane, ("lane", lane));
            else
                roles[lane] = role;
        }
        if (roles.Values.Count(role => role == "A") > 1)
            Fault(place, FileErrorCodes.RaciSingleAccountable);
        return roles;
    }

    private List<PmapFlow> Flows(JsonElement map, string path, HashSet<string> stepIds)
    {
        var flows = new List<PmapFlow>();
        if (List(map, "flows", path, required: false) is not { } list)
            return flows;
        var pairs = new HashSet<(string, string)>();
        var i = 0;
        foreach (var element in list.EnumerateArray())
        {
            var place = $"{path}.flows[{i++}]";
            if (Flow(element, place, stepIds) is not { } flow)
                continue;
            if (!pairs.Add((flow.Source, flow.Target)))
            {
                Fault(place, FileErrorCodes.DuplicateFlow, ("source", flow.Source), ("target", flow.Target));
                continue;
            }
            flows.Add(flow);
        }
        return flows;
    }

    private PmapFlow? Flow(JsonElement element, string path, HashSet<string> stepIds)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            Fault(path, FileErrorCodes.FlowShape);
            return null;
        }
        Properties(element, path, valueStream ? FlowProperties12 : FlowProperties);
        var source = End(element, "source", path, stepIds);
        var target = End(element, "target", path, stepIds);
        var label = Entered(element, "label", path, PmapFormat.FlowLabelMax, required: false);
        var translations = Translations(element, path, PmapFormat.FlowLabelMax);
        var share = Number(element, "share", path);
        if (source is null || target is null)
            return null;
        if (source == target)
        {
            Fault(path, FileErrorCodes.SelfFlow, ("step", source));
            return null;
        }
        return new PmapFlow(source, target, label, translations) { Share = share };
    }

    /// <summary>An end of a flow: a step of the same map, with a fault of its own when the step is in another map.</summary>
    private string? End(JsonElement element, string name, string path, HashSet<string> stepIds)
    {
        var id = Text(element, name, path, required: true);
        if (id is null || stepIds.Contains(id))
            return id;
        Fault($"{path}.{name}", ids.TryGetValue(id, out var kind) && kind == StepKind ? FileErrorCodes.StepInOtherMap : FileErrorCodes.UnknownStep,
            ("step", id));
        return null;
    }
}
