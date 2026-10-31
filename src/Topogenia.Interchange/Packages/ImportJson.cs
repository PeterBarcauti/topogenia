// ImportJson.cs
// Reading of the values of a received entry, already checked against the schema: texts, numbers, dates and codes of the closed lists.

using System.Globalization;
using System.Text.Json.Nodes;
using Topogenia.Domain.Values;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The values of a node of an entry. The schema has checked the types, the patterns and the closed lists before; a value
/// that still does not read (a date out of the calendar) refuses the package with the reason <c>schema</c> on its entry.
/// </summary>
internal sealed class ImportJson(string entry)
{
    public string Entry { get; } = entry;

    public string Text(JsonNode node, string property) => OptionalText(node, property) ?? throw Fault();

    public string? OptionalText(JsonNode node, string property) =>
        node[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public int Integer(JsonNode node, string property) => node[property] is JsonValue value && value.TryGetValue<int>(out var number) ? number : throw Fault();

    public int? OptionalInteger(JsonNode node, string property) => node[property] is null ? null : Integer(node, property);

    public decimal Decimal(JsonNode node, string property) => OptionalDecimal(node, property) ?? throw Fault();

    public decimal? OptionalDecimal(JsonNode node, string property) =>
        node[property] is null ? null : node[property] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : throw Fault();

    public bool Boolean(JsonNode node, string property) => node[property] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : throw Fault();

    public DateTime Date(JsonNode node, string property) => OptionalDate(node, property) ?? throw Fault();

    /// <summary>A UTC date of the canonical form (D08), read back as a UTC date.</summary>
    public DateTime? OptionalDate(JsonNode node, string property)
    {
        if (OptionalText(node, property) is not { } text)
            return node[property] is null ? null : throw Fault();
        return DateTime.TryParseExact(text, "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffff'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : throw Fault();
    }

    public T Code<T>(JsonNode node, string property)
        where T : struct, Enum => OptionalCode<T>(node, property) ?? throw Fault();

    public T? OptionalCode<T>(JsonNode node, string property)
        where T : struct, Enum
    {
        if (OptionalText(node, property) is not { } text)
            return node[property] is null ? null : throw Fault();
        return Codes.TryParse<T>(text, out var value) ? value : throw Fault();
    }

    public IEnumerable<JsonNode> Items(JsonNode node, string property) =>
        node[property] is JsonArray items ? items.Select(item => item ?? throw Fault()) : throw Fault();

    public JsonObject Object(JsonNode node, string property) => node[property] as JsonObject ?? throw Fault();

    public Exception Fault() => PackageReader.Invalid("schema", Entry);

    /// <summary>A reference of the package that names nothing it carries.</summary>
    public Exception Unresolved(string reference) => PackageReader.Invalid("reference", Entry, [reference]);
}
