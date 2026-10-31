// CanonicalJson.cs
// Deterministic JSON of the exchange formats: keys in ordinal order, two-space indentation, LF line ends, fixed formats.

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Topogenia.Interchange.Json;

/// <summary>
/// One writing of a JSON tree, so that two exports of the same state give the same bytes (spec 7.2): the properties of
/// every object in ordinal order of their names, arrays in their given order, two-space indentation, LF line ends and a
/// final LF whatever the system, UTF-8 without byte order mark, and the characters of the texts written as they are
/// (only the quote, the backslash and the control characters are escaped: the file is never embedded in a page).
/// Dates and decimals are written by <see cref="Date"/> and <see cref="Number"/>, the only two formats of the values.
/// </summary>
public static class CanonicalJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The canonical bytes of a tree.</summary>
    public static byte[] Write(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
            WriteNode(writer, node);
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    /// <summary>A UTC date with its seven fractional digits (D08): <c>2026-10-23T08:00:00.0000000Z</c>.</summary>
    public static string Date(DateTime value) =>
        value.Kind is DateTimeKind.Utc or DateTimeKind.Unspecified
            ? value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffff'Z'", CultureInfo.InvariantCulture)
            : throw new ArgumentException("Dates of the model are UTC (D08).", nameof(value));

    /// <summary>
    /// A decimal without its trailing zeros, so that the scale kept by an engine (1.50 on one, 1.5 on the other) never
    /// changes the bytes.
    /// </summary>
    public static JsonNode Number(decimal value) =>
        JsonValue.Create(decimal.Parse(value.ToString("0.############################", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));

    private static void WriteNode(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject value:
                writer.WriteStartObject();
                foreach (var (name, child) in value.OrderBy(property => property.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(name);
                    WriteNode(writer, child);
                }
                writer.WriteEndObject();
                break;
            case JsonArray items:
                writer.WriteStartArray();
                foreach (var item in items)
                    WriteNode(writer, item);
                writer.WriteEndArray();
                break;
            default:
                node.WriteTo(writer);
                break;
        }
    }
}
