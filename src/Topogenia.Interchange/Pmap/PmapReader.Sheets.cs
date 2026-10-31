// PmapReader.Sheets.cs
// The sheet block of a step (AR-93): checked by the rules of the API, its documents, and the uses of the outputs of other steps.

using System.Text.Json;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;

namespace Topogenia.Interchange.Pmap;

internal sealed partial class PmapReading
{
    private const string DocumentKind = "document";

    private static readonly string[] SheetProperties =
    [
        SheetContentReader.ActorKey, SheetContentReader.PurposeKey, SheetContentReader.ToolsKey, SheetContentReader.FrequencyKey,
        SheetContentReader.DurationKey, SheetContentReader.ProcedureKey, SheetContentReader.FreeTextKey, "outputs", "inputs", "uses",
    ];

    private static readonly string[] SheetProperties12 = [.. SheetProperties, SheetContentReader.LeadTimeKey];
    private static readonly string[] LeadTimeProperties = ["value", "unit"];
    private static readonly string[] TaskProperties = ["text", "tool", "nature"];
    private static readonly string[] OutputProperties = ["id", "name", "location", "share_confirmed"];
    private static readonly string[] InputProperties = ["id", "name", "location", "share_confirmed", "wait_hours"];

    private static readonly string[] UseProperties =
        ["document", DocumentRules.WaitHoursField, DocumentRules.PercentField, DocumentRules.PainPointField, DocumentRules.LostHoursField];

    /// <summary>The step whose sheet is being read: the owner of the outputs it declares.</summary>
    private string currentStep = string.Empty;

    /// <summary>The outputs of each step read so far, by their identifier in the file: what a use may name.</summary>
    private readonly Dictionary<string, string> outputOwners = new(StringComparer.Ordinal);

    /// <summary>The path of each use, to report a fault of its reference at its place once the map is read.</summary>
    private readonly Dictionary<PmapUse, string> usePaths = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The sheet block of a step, every part optional: refused on a start, an end and a gateway, which have no sheet
    /// (annex A 2.7). The form, the procedure and the free text are read by <see cref="SheetContentReader"/>, the rules
    /// the API applies to a draft; a refusal is a fault of the block with its code. A block of which nothing is left gives
    /// no sheet.
    /// </summary>
    private PmapSheet? Sheet(JsonElement step, string path, bool noContent)
    {
        if (Object(step, "sheet", path, required: false) is not { } sheet)
            return null;
        var place = $"{path}.sheet";
        if (noContent)
        {
            Fault(place, FileErrorCodes.SheetOnStepType);
            return null;
        }
        Properties(sheet, place, valueStream ? SheetProperties12 : SheetProperties);
        var actor = Text(sheet, SheetContentReader.ActorKey, place, required: false);
        var purpose = Text(sheet, SheetContentReader.PurposeKey, place, required: false);
        var tools = Text(sheet, SheetContentReader.ToolsKey, place, required: false);
        var frequency = Text(sheet, SheetContentReader.FrequencyKey, place, required: false);
        if (!valueStream && frequency == Codes.Of(Frequency.PerCase))
        {
            // Per case belongs to the version 1.2 (ADR 085): in a file of the version 1.1 it is not one of the frequencies.
            Fault(Join(place, SheetContentReader.FrequencyKey), FileErrorCodes.InvalidValue, ("value", frequency),
                ("possible", string.Join(", ", Codes.All<Frequency>().Where(code => code != Codes.Of(Frequency.PerCase)))));
            frequency = null;
        }
        var (leadValue, leadUnit) = LeadTimeOf(sheet, place);
        var duration = Number(sheet, SheetContentReader.DurationKey, place);
        var procedure = Procedure(sheet, place);
        var freeText = Text(sheet, SheetContentReader.FreeTextKey, place, required: false);
        var outputs = Documents(sheet, "outputs", place, OutputProperties, withWait: false);
        var inputs = Documents(sheet, "inputs", place, InputProperties, withWait: true);
        var uses = Uses(sheet, place);
        SheetContent content;
        try
        {
            content = SheetContentReader.Read(new SheetInput(new Dictionary<string, SheetFieldValue?>
            {
                [SheetContentReader.ActorKey] = new(actor, null),
                [SheetContentReader.PurposeKey] = new(purpose, null),
                [SheetContentReader.ToolsKey] = new(tools, null),
                [SheetContentReader.FrequencyKey] = new(frequency, null),
                [SheetContentReader.DurationKey] = new(null, duration),
                [SheetContentReader.LeadTimeKey] = new(leadUnit, leadValue),
            }, [.. procedure], freeText));
        }
        catch (DomainException refusal)
        {
            Fault(place, refusal);
            return null;
        }
        var read = new PmapSheet(content.Actor, content.Purpose, content.Tools, content.Frequency is { } often ? Codes.Of(often) : null,
            content.DurationMinutes, [.. content.Procedure.Select(task => new PmapTask(task.Text, task.Tool, task.Nature is { } nature ? Codes.Of(nature) : null))],
            content.FreeText, outputs, inputs, uses)
        {
            LeadTime = content.LeadTime is { } lead ? new PmapLeadTime(lead.Value, Codes.Of(lead.Unit)) : null,
        };
        return read.IsEmpty ? null : read;
    }

    /// <summary>The lead time of a sheet (1.2): an object with a value and a unit code, read by the rules of the sheet.</summary>
    private (decimal? Value, string? Unit) LeadTimeOf(JsonElement sheet, string place)
    {
        if (Object(sheet, SheetContentReader.LeadTimeKey, place, required: false) is not { } lead)
            return (null, null);
        var path = Join(place, SheetContentReader.LeadTimeKey);
        Properties(lead, path, LeadTimeProperties);
        return (Number(lead, "value", path), Text(lead, "unit", path, required: false));
    }

    /// <summary>The lines of the procedure as written: a line is an object with a text, a tool and a nature code.</summary>
    private List<ProcedureLine?> Procedure(JsonElement sheet, string path)
    {
        var tasks = new List<ProcedureLine?>();
        if (List(sheet, SheetContentReader.ProcedureKey, path, required: false) is not { } list)
            return tasks;
        var i = 0;
        foreach (var element in list.EnumerateArray())
        {
            var place = $"{path}.{SheetContentReader.ProcedureKey}[{i++}]";
            if (element.ValueKind != JsonValueKind.Object)
            {
                Fault(place, FileErrorCodes.SheetShape);
                continue;
            }
            Properties(element, place, TaskProperties);
            tasks.Add(new ProcedureLine(Text(element, "text", place, required: false), Text(element, "tool", place, required: false),
                Text(element, "nature", place, required: false)));
        }
        return tasks;
    }

    /// <summary>
    /// The documents of a step, outputs or external inputs, under the rules of the API (`DocumentRules`): name required
    /// (200), link an http(s) address or a path, waiting time of an external input. An output that another step uses
    /// carries an identifier, unique in the file.
    /// </summary>
    private List<PmapDocumentEntry> Documents(JsonElement sheet, string name, string path, string[] properties, bool withWait)
    {
        var documents = new List<PmapDocumentEntry>();
        if (List(sheet, name, path, required: false) is not { } list)
            return documents;
        var i = 0;
        foreach (var element in list.EnumerateArray())
        {
            var place = $"{path}.{name}[{i++}]";
            if (element.ValueKind != JsonValueKind.Object)
            {
                Fault(place, FileErrorCodes.SheetShape);
                continue;
            }
            Properties(element, place, properties);
            var id = element.TryGetProperty("id", out _) ? Identifier(element, place, DocumentKind) : null;
            try
            {
                var documentName = DocumentRules.Name(Text(element, DocumentRules.NameField, place, required: false));
                var location = DocumentRules.Address(Text(element, DocumentRules.LocationField, place, required: false)).Location;
                var wait = withWait ? DocumentRules.WaitHours(Number(element, DocumentRules.WaitHoursField, place)) : null;
                var shared = Flag(element, "share_confirmed", place);
                documents.Add(new PmapDocumentEntry(id, documentName, location, shared, wait));
                if (id is not null && !withWait)
                    outputOwners[id] = currentStep;
            }
            catch (DomainException refusal)
            {
                Fault(place, refusal);
            }
        }
        return documents;
    }

    /// <summary>The uses of the outputs of other steps, under the rules of the API (`DocumentRules`); their references are checked with the map.</summary>
    private List<PmapUse> Uses(JsonElement sheet, string path)
    {
        var uses = new List<PmapUse>();
        if (List(sheet, "uses", path, required: false) is not { } list)
            return uses;
        var i = 0;
        foreach (var element in list.EnumerateArray())
        {
            var place = $"{path}.uses[{i++}]";
            if (element.ValueKind != JsonValueKind.Object)
            {
                Fault(place, FileErrorCodes.SheetShape);
                continue;
            }
            Properties(element, place, UseProperties);
            var document = Text(element, "document", place, required: true);
            try
            {
                var wait = DocumentRules.WaitHours(Number(element, DocumentRules.WaitHoursField, place));
                var percent = DocumentRules.Percent(Percent(Number(element, DocumentRules.PercentField, place)));
                var painPoint = DocumentRules.PainPointDescription(Text(element, DocumentRules.PainPointField, place, required: false));
                var lost = DocumentRules.LostHours(Number(element, DocumentRules.LostHoursField, place));
                if (document is null)
                    continue;
                var use = new PmapUse(document, wait, percent, painPoint, lost);
                uses.Add(use);
                usePaths[use] = place;
            }
            catch (DomainException refusal)
            {
                Fault(place, refusal);
            }
        }
        return uses;
    }

    /// <summary>A part usable as is is a whole number; its bounds are those of <c>DocumentRules.Percent</c>, which reads it next.</summary>
    private static int? Percent(decimal? value) => value switch
    {
        null => null,
        { } whole when decimal.IsInteger(whole) && whole is >= int.MinValue and <= int.MaxValue => (int)whole,
        _ => throw DomainException.InvalidValue(DocumentRules.PercentField),
    };

    /// <summary>
    /// The references of the uses of a map, once all its steps are read: an output of another step of the same map
    /// (the rule of `DocumentUses.Use`), used once at most by a step.
    /// </summary>
    private void Uses(List<PmapStep> steps, Dictionary<string, string> stepPaths)
    {
        var mapSteps = steps.Select(step => step.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var step in steps.Where(step => step.Sheet is not null))
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var use in step.Sheet!.Uses)
            {
                var place = usePaths.GetValueOrDefault(use, stepPaths[step.Id]);
                if (!outputOwners.TryGetValue(use.Document, out var owner) || owner == step.Id || !mapSteps.Contains(owner))
                    Fault($"{place}.document", FileErrorCodes.UnknownOutput, ("document", use.Document));
                else if (!used.Add(use.Document))
                    Fault(place, FileErrorCodes.DuplicateUse, ("document", use.Document));
            }
        }
    }
}
