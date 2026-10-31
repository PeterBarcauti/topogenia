// PackageBuild.Content.cs
// Rebuilds what hangs on the steps (pain points, sheets, documents, translations), and resolves codes and people.

using System.Text.Json.Nodes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Interchange.Packages;

internal sealed partial class PackageBuild
{
    private void PainPoints()
    {
        var json = new ImportJson(PackageFormat.PainPoints);
        foreach (var node in json.Items(entries.Content[PackageFormat.PainPoints], "painPoints"))
        {
            var code = Code(json, node, CounterKind.PainPoint);
            var id = context.NewId();
            painPoints[code] = id;
            var step = json.OptionalText(node, "step") is { } stepCode ? StepOf(json, stepCode).Id : (Guid?)null;
            records.Add(new PainPoint(id, projectId, step, Recode(code), json.Text(node, "description"), json.Text(node, "sourceLanguage"),
                json.OptionalDecimal(node, "lostHoursPerMonth"), json.Code<PainPointStatus>(node, "status"), json.Date(node, "createdAt"),
                Author(node, "createdBy"), json.OptionalDate(node, "updatedAt"), Updater(node, "updatedBy"), 1));
            foreach (var change in json.Items(node, "history"))
                records.Add(new PainPointStatusChange(context.NewId(), id, json.OptionalCode<PainPointStatus>(change, "statusBefore"),
                    json.Code<PainPointStatus>(change, "statusAfter"), json.OptionalText(change, "comment"), Author(change, "changedBy"),
                    json.Date(change, "changedAt")));
        }
    }

    private void Sheets()
    {
        var json = new ImportJson(PackageFormat.Sheets);
        var seen = new HashSet<Guid>();
        foreach (var node in json.Items(entries.Content[PackageFormat.Sheets], "sheets"))
        {
            var step = StepOf(json, json.Text(node, "step"));
            if (!seen.Add(step.Id))
                throw json.Unresolved(json.Text(node, "step"));
            var id = context.NewId();
            records.Add(new StepSheet(id, step.Id, json.Code<SheetStatus>(node, "status"), json.OptionalText(node, "actor"), json.OptionalText(node, "purpose"),
                json.OptionalText(node, "tools"), json.OptionalCode<Frequency>(node, "frequency"), json.OptionalDecimal(node, "durationMinutes"),
                json.OptionalText(node, "freeText"), json.Text(node, "sourceLanguage"), json.OptionalText(node, "clarificationRequest"),
                json.Date(node, "createdAt"), Author(node, "createdBy"), json.OptionalDate(node, "updatedAt"), Updater(node, "updatedBy"), 1)
            {
                LeadTime = node["leadTime"] is { } lead ? json.Decimal(lead, "value") : null,
                LeadTimeUnit = node["leadTime"] is { } unit ? json.Code<LeadTimeUnit>(unit, "unit") : null,
            });
            var positions = new HashSet<int>();
            foreach (var task in json.Items(node, "procedure"))
            {
                var position = json.Integer(task, "position");
                if (!positions.Add(position))
                    throw json.Unresolved(json.Text(node, "step") + "#" + position);
                records.Add(new SheetOperation(id, position, json.Text(task, "text"), json.OptionalText(task, "tool"), json.OptionalCode<OperationNature>(task, "nature")));
            }
            var numbers = new HashSet<int>();
            foreach (var version in json.Items(node, "versions"))
            {
                var number = json.Integer(version, "number");
                if (!numbers.Add(number))
                    throw json.Unresolved(json.Text(node, "step") + "v" + number);
                records.Add(new StepSheetVersion(context.NewId(), id, number, json.Text(version, "content"), json.OptionalCode<Frequency>(version, "frequency"),
                    json.OptionalDecimal(version, "durationMinutes"), Author(version, "validatedBy"), json.Date(version, "validatedAt"))
                {
                    LeadTime = version["leadTime"] is { } versionLead ? json.Decimal(versionLead, "value") : null,
                    LeadTimeUnit = version["leadTime"] is { } versionUnit ? json.Code<LeadTimeUnit>(versionUnit, "unit") : null,
                });
            }
        }
    }

    private void Documents()
    {
        var json = new ImportJson(PackageFormat.Documents);
        foreach (var node in json.Items(entries.Content[PackageFormat.Documents], "documents"))
        {
            var id = context.NewId();
            records.Add(new Document(id, StepOf(json, json.Text(node, "step")).Id, json.Code<DocumentDirection>(node, "direction"), json.Text(node, "name"),
                json.OptionalText(node, "location"), json.OptionalCode<LocationType>(node, "locationType"), json.OptionalDecimal(node, "waitHours"),
                json.Boolean(node, "shareConfirmed"), json.Text(node, "sourceLanguage"), json.Date(node, "createdAt"), Author(node, "createdBy"),
                json.OptionalDate(node, "updatedAt"), Updater(node, "updatedBy"), 1));
            var used = new HashSet<Guid>();
            foreach (var use in json.Items(node, "uses"))
            {
                var step = StepOf(json, json.Text(use, "step"));
                if (!used.Add(step.Id))
                    throw json.Unresolved(json.Text(use, "step"));
                var painPoint = json.OptionalText(use, "painPoint") is { } code
                    ? painPoints.TryGetValue(code, out var found) ? found : throw json.Unresolved(code)
                    : (Guid?)null;
                records.Add(new DocumentUse(id, step.Id, json.OptionalDecimal(use, "waitHours"), json.OptionalInteger(use, "percentCompleteAccurate"), painPoint,
                    json.Date(use, "createdAt"), Author(use, "createdBy"), json.OptionalDate(use, "updatedAt"), Updater(use, "updatedBy")));
            }
        }
    }

    private void Translations()
    {
        var json = new ImportJson(PackageFormat.Translations);
        var keys = new HashSet<RecordKey>();
        foreach (var node in json.Items(entries.Content[PackageFormat.Translations], "translations"))
        {
            var type = json.Code<TranslationObject>(node, "object");
            var translation = new Translation(type, ObjectOf(json, type, node), json.Code<TranslationField>(node, "field"), json.Text(node, "language"),
                json.Text(node, "text"), json.Code<TranslationSource>(node, "source"), json.Date(node, "updatedAt"), Updater(node, "updatedBy"));
            if (!keys.Add(translation.Key))
                throw json.Unresolved(Codes.Of(type) + ":" + translation.Language);
            records.Add(translation);
        }
    }

    /// <summary>The object of a translation, named as everywhere in the package (ADR 051).</summary>
    private Guid ObjectOf(ImportJson json, TranslationObject type, JsonNode node) => type switch
    {
        TranslationObject.Project => projectId,
        TranslationObject.Map => maps.TryGetValue(json.OptionalText(node, "parentStep") ?? "", out var map) ? map : throw json.Unresolved("map"),
        TranslationObject.Lane => LaneOf(json, json.Text(node, "lane")).Id,
        TranslationObject.Step => StepOf(json, json.Text(node, "step")).Id,
        _ => flows.TryGetValue(Ends(json, MapOfFlow(json, json.Object(node, "flow")), json.Object(node, "flow")), out var flow)
            ? flow
            : throw json.Unresolved("flow"),
    };

    private Guid MapOfFlow(ImportJson json, JsonNode ends) => StepOf(json, json.Text(ends, "source")).MapId;

    /// <summary>
    /// The readable code of an object of the package: of its kind, under the code of the package, never beyond its counter,
    /// carried once (D07).
    /// </summary>
    private string Code(ImportJson json, JsonNode node, CounterKind kind)
    {
        var text = json.Text(node, "code");
        if (!ReadableCode.TryParse(text, out var code) || code.Kind != kind || !text.StartsWith(packageCode + "-", StringComparison.Ordinal)
            || code.Number < 1 || code.Number > counters[kind] || !codes.Add(text))
            throw json.Unresolved(text);
        return text;
    }

    /// <summary>A code of the package under the code of the new project (spec 7.3: rewritten with the new prefix).</summary>
    private string Recode(string code) =>
        projectCode == packageCode ? code : ReadableCode.Parse(code, "code").WithProject(projectCode).Value;

    private Lane LaneOf(ImportJson json, string code) => lanes.TryGetValue(code, out var lane) ? lane : throw json.Unresolved(code);

    private Step StepOf(ImportJson json, string code) => steps.TryGetValue(code, out var step) ? step : throw json.Unresolved(code);

    /// <summary>The author of a creation, never empty: the user of the login, or the importer.</summary>
    private Guid Author(JsonNode node, string property) =>
        node[property] is JsonValue value && value.TryGetValue<string>(out var login) ? user(login) ?? context.ActorId : context.ActorId;

    /// <summary>An author that may be empty (the last updater, a translator): the user of the login, or nobody.</summary>
    private Guid? Updater(JsonNode node, string property) =>
        node[property] is JsonValue value && value.TryGetValue<string>(out var login) ? user(login) ?? context.ActorId : null;

    /// <summary>The actor of a lane or the assignee of a step: kept only when he is a member (`ImportedProjects.Settle`).</summary>
    private Guid? Holder(JsonNode node, string property) => Updater(node, property);
}
