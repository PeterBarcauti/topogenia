// ContentEntries.cs
// sheets.json, documents.json and painpoints.json of the package (spec 7.2): what hangs on the exported steps.

using System.Text;
using System.Text.Json.Nodes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Json;
using static Topogenia.Interchange.Packages.StructureEntries;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The sheets, documents and pain points of the exported steps (the dissolved step of an open frame included: its
/// remainder). A list without a natural key is ordered by its fields and, at last, by its canonical text, so that the
/// order never depends on the order in which the database gave the records.
/// </summary>
internal static class ContentEntries
{
    public static JsonObject Sheets(ExportScope scope, People people)
    {
        var workspace = scope.Workspace;
        var sheets = workspace.All<StepSheet>()
            .Where(sheet => scope.HasStep(sheet.StepId))
            .OrderBy(sheet => scope.StepCode(sheet.StepId), StringComparer.Ordinal)
            .Select(sheet => (JsonNode)Sheet(scope, people, sheet));
        return new JsonObject { ["sheets"] = new JsonArray([.. sheets]) };
    }

    public static JsonObject Documents(ExportScope scope, People people)
    {
        var painPoints = PainPointCodes(scope);
        var documents = scope.Workspace.All<Document>()
            .Where(document => scope.HasStep(document.StepId))
            .Select(document => (Step: scope.StepCode(document.StepId)!, document.CreatedAt, Node: Document(scope, people, painPoints, document)));
        return new JsonObject
        {
            ["documents"] = new JsonArray([.. documents
                .OrderBy(item => item.Step, StringComparer.Ordinal)
                .ThenBy(item => item.CreatedAt)
                .ThenBy(item => Text(item.Node), StringComparer.Ordinal)
                .Select(item => (JsonNode)item.Node)]),
        };
    }

    public static JsonObject PainPoints(ExportScope scope, People people)
    {
        var workspace = scope.Workspace;
        var painPoints = workspace.All<PainPoint>()
            .Where(painPoint => painPoint.StepId is null || scope.HasStep(painPoint.StepId))
            .OrderBy(painPoint => painPoint.Code, StringComparer.Ordinal)
            .Select(painPoint => (JsonNode)PainPoint(scope, people, painPoint));
        return new JsonObject { ["painPoints"] = new JsonArray([.. painPoints]) };
    }

    /// <summary>The codes of the exported pain points, named by the uses of the documents.</summary>
    private static IReadOnlyDictionary<Guid, string> PainPointCodes(ExportScope scope) =>
        scope.Workspace.All<PainPoint>()
            .Where(painPoint => painPoint.StepId is null || scope.HasStep(painPoint.StepId))
            .ToDictionary(painPoint => painPoint.Id, painPoint => painPoint.Code);

    private static JsonObject Sheet(ExportScope scope, People people, StepSheet sheet)
    {
        var workspace = scope.Workspace;
        var node = new JsonObject
        {
            ["step"] = scope.StepCode(sheet.StepId),
            ["status"] = Codes.Of(sheet.Status),
            ["sourceLanguage"] = sheet.SourceLanguage,
        };
        Optional(node, "actor", sheet.Actor);
        Optional(node, "purpose", sheet.Purpose);
        Optional(node, "tools", sheet.Tools);
        if (sheet.Frequency is { } frequency)
            node["frequency"] = Codes.Of(frequency);
        Optional(node, "durationMinutes", sheet.DurationMinutes);
        WriteLeadTime(node, sheet.LeadTime, sheet.LeadTimeUnit);
        Optional(node, "freeText", sheet.FreeText);
        Optional(node, "clarificationRequest", sheet.ClarificationRequest);
        people.WriteAudit(node, sheet.CreatedAt, sheet.CreatedBy, sheet.UpdatedAt, sheet.UpdatedBy);
        var procedure = workspace.All<SheetOperation>().Where(task => task.SheetId == sheet.Id).OrderBy(task => task.Position).Select(task =>
        {
            var item = new JsonObject { ["position"] = task.Position, ["text"] = task.Text };
            Optional(item, "tool", task.Tool);
            if (task.Nature is { } nature)
                item["nature"] = Codes.Of(nature);
            return (JsonNode)item;
        });
        node["procedure"] = new JsonArray([.. procedure]);
        var versions = workspace.All<StepSheetVersion>().Where(version => version.SheetId == sheet.Id).OrderBy(version => version.VersionNumber).Select(version =>
        {
            var item = new JsonObject
            {
                ["number"] = version.VersionNumber,
                ["content"] = version.ContentJson,
                ["validatedAt"] = CanonicalJson.Date(version.ValidatedAt),
            };
            if (version.Frequency is { } validated)
                item["frequency"] = Codes.Of(validated);
            Optional(item, "durationMinutes", version.DurationMinutes);
            WriteLeadTime(item, version.LeadTime, version.LeadTimeUnit);
            people.Write(item, "validatedBy", version.ValidatedBy);
            return (JsonNode)item;
        });
        node["versions"] = new JsonArray([.. versions]);
        return node;
    }

    private static JsonObject Document(ExportScope scope, People people, IReadOnlyDictionary<Guid, string> painPoints, Document document)
    {
        var node = new JsonObject
        {
            ["step"] = scope.StepCode(document.StepId),
            ["direction"] = Codes.Of(document.Direction),
            ["name"] = document.Name,
            ["shareConfirmed"] = document.ShareConfirmed,
            ["sourceLanguage"] = document.SourceLanguage,
        };
        Optional(node, "location", document.Location);
        if (document.LocationType is { } type)
            node["locationType"] = Codes.Of(type);
        Optional(node, "waitHours", document.WaitHours);
        people.WriteAudit(node, document.CreatedAt, document.CreatedBy, document.UpdatedAt, document.UpdatedBy);
        var uses = scope.Workspace.All<DocumentUse>()
            .Where(use => use.DocumentId == document.Id && scope.HasStep(use.StepId))
            .Select(use =>
            {
                var item = new JsonObject { ["step"] = scope.StepCode(use.StepId) };
                Optional(item, "waitHours", use.WaitHours);
                if (use.PercentCompleteAccurate is { } percent)
                    item["percentCompleteAccurate"] = percent;
                Optional(item, "painPoint", use.PainPointId is { } id ? painPoints.GetValueOrDefault(id) : null);
                people.WriteAudit(item, use.CreatedAt, use.CreatedBy, use.UpdatedAt, use.UpdatedBy);
                return item;
            })
            .OrderBy(item => (string?)item["step"], StringComparer.Ordinal);
        node["uses"] = new JsonArray([.. uses.Select(item => (JsonNode)item)]);
        return node;
    }

    private static JsonObject PainPoint(ExportScope scope, People people, PainPoint painPoint)
    {
        var node = new JsonObject
        {
            ["code"] = painPoint.Code,
            ["description"] = painPoint.Description,
            ["sourceLanguage"] = painPoint.SourceLanguage,
            ["status"] = Codes.Of(painPoint.Status),
        };
        Optional(node, "step", scope.StepCode(painPoint.StepId));
        Optional(node, "lostHoursPerMonth", painPoint.LostHoursPerMonth);
        people.WriteAudit(node, painPoint.CreatedAt, painPoint.CreatedBy, painPoint.UpdatedAt, painPoint.UpdatedBy);
        var history = scope.Workspace.All<PainPointStatusChange>()
            .Where(change => change.PainPointId == painPoint.Id)
            .Select(change =>
            {
                var item = new JsonObject { ["statusAfter"] = Codes.Of(change.StatusAfter), ["changedAt"] = CanonicalJson.Date(change.ChangedAt) };
                if (change.StatusBefore is { } before)
                    item["statusBefore"] = Codes.Of(before);
                Optional(item, "comment", change.Comment);
                people.Write(item, "changedBy", change.ChangedBy);
                return (change.ChangedAt, Node: item);
            })
            .OrderBy(item => item.ChangedAt)
            .ThenBy(item => Text(item.Node), StringComparer.Ordinal);
        node["history"] = new JsonArray([.. history.Select(item => (JsonNode)item.Node)]);
        return node;
    }

    /// <summary>The lead time of a sheet or of a version (format 2.1, spec 14.7): <c>{ "value", "unit" }</c>, when present.</summary>
    private static void WriteLeadTime(JsonObject node, decimal? value, LeadTimeUnit? unit)
    {
        if (value is { } number && unit is { } code)
            node["leadTime"] = new JsonObject { ["value"] = CanonicalJson.Number(number), ["unit"] = Codes.Of(code) };
    }

    /// <summary>The canonical text of a node, the last key of an order without a natural key.</summary>
    internal static string Text(JsonNode node) => Encoding.UTF8.GetString(CanonicalJson.Write(node));
}
