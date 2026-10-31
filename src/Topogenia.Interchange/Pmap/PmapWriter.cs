// PmapWriter.cs
// Writing of a .pmap 1.1 document: readable JSON in the order of the format, the optional properties only when they say something.

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Topogenia.Domain.Values;

namespace Topogenia.Interchange.Pmap;

/// <summary>
/// Writes a `.pmap` document (ADR 054) as the 1.x did: two-space indentation, LF line ends and a final LF, UTF-8, the
/// accents and apostrophes as they are (only the quote, the backslash and the control characters are escaped: the file is
/// downloaded, never put in a page), the properties in the order of the format (a person or an AI reads it), an optional
/// property only when it carries something (no default value, no empty list).
/// </summary>
public static class PmapWriter
{
    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write(PmapDocument document, string generatedBy, DateTime generatedAt)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Options))
        {
            writer.WriteStartObject();
            writer.WriteString("format", PmapFormat.Name);
            writer.WriteString("version", PmapFormat.Version);
            writer.WriteString("generated_by", generatedBy);
            writer.WriteString("generated_at", DateTime.SpecifyKind(generatedAt, DateTimeKind.Utc).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture));
            Project(writer, document.Project);
            writer.WritePropertyName("map");
            Map(writer, document.Map);
            if (document.Uncertainties.Count > 0)
            {
                writer.WriteStartArray("uncertainties");
                foreach (var text in document.Uncertainties)
                    writer.WriteStringValue(text);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void Project(Utf8JsonWriter writer, PmapProject project)
    {
        writer.WriteStartObject("project");
        Optional(writer, "code", project.Code);
        writer.WriteString("name", project.Name);
        writer.WriteString("language", project.Language);
        Optional(writer, "description", project.Description);
        Optional(writer, "workday_hours", project.WorkdayHours);
        Optional(writer, "fte_hours_per_month", project.FteHoursPerMonth);
        Translations(writer, project.Translations);
        writer.WriteEndObject();
    }

    private static void Map(Utf8JsonWriter writer, PmapMap map)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("lanes");
        foreach (var lane in map.Lanes)
        {
            writer.WriteStartObject();
            writer.WriteString("id", lane.Id);
            writer.WriteString("name", lane.Name);
            writer.WriteString("type", lane.Type);
            Optional(writer, "color", lane.Color);
            if (lane.Headcount is { } headcount)
                writer.WriteNumber("headcount", headcount);
            Translations(writer, lane.Translations);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartArray("steps");
        foreach (var step in map.Steps)
            Step(writer, step);
        writer.WriteEndArray();
        writer.WriteStartArray("flows");
        foreach (var flow in map.Flows)
        {
            writer.WriteStartObject();
            writer.WriteString("source", flow.Source);
            writer.WriteString("target", flow.Target);
            Optional(writer, "label", flow.Label);
            Optional(writer, "share", flow.Share);
            Translations(writer, flow.Translations);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void Step(Utf8JsonWriter writer, PmapStep step)
    {
        writer.WriteStartObject();
        writer.WriteString("id", step.Id);
        writer.WriteString("lane", step.Lane);
        writer.WriteString("label", step.Label);
        writer.WriteString("type", step.Type);
        if (step.Column is { } column)
            writer.WriteNumber("column", column);
        if (step.Row > 0)
            writer.WriteNumber("row", step.Row);
        if (step.Automation != Codes.Of(AutomationStatus.Manual))
            writer.WriteString("automation", step.Automation);
        if (step.Volume is { } volume)
        {
            writer.WriteStartObject("volume");
            writer.WriteNumber("quantity", volume.Quantity);
            writer.WriteString("period", volume.Period);
            writer.WriteEndObject();
        }
        if (step.Raci.Count > 0)
        {
            writer.WriteStartObject("raci");
            foreach (var (lane, role) in step.Raci)
                writer.WriteString(lane, role);
            writer.WriteEndObject();
        }
        if (step.SubMap is not null)
        {
            writer.WritePropertyName("sub_map");
            Map(writer, step.SubMap);
        }
        Translations(writer, step.Translations);
        if (step.Sheet is not null)
            Sheet(writer, step.Sheet);
        writer.WriteEndObject();
    }

    private static void Sheet(Utf8JsonWriter writer, PmapSheet sheet)
    {
        writer.WriteStartObject("sheet");
        Optional(writer, "actor", sheet.Actor);
        Optional(writer, "purpose", sheet.Purpose);
        Optional(writer, "tools", sheet.Tools);
        Optional(writer, "frequency", sheet.Frequency);
        Optional(writer, "duration_minutes", sheet.DurationMinutes);
        if (sheet.LeadTime is { } lead)
        {
            writer.WriteStartObject("lead_time");
            writer.WriteNumber("value", lead.Value);
            writer.WriteString("unit", lead.Unit);
            writer.WriteEndObject();
        }
        if (sheet.Procedure.Count > 0)
        {
            writer.WriteStartArray("procedure");
            foreach (var task in sheet.Procedure)
            {
                writer.WriteStartObject();
                writer.WriteString("text", task.Text);
                Optional(writer, "tool", task.Tool);
                Optional(writer, "nature", task.Nature);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        Optional(writer, "free_text", sheet.FreeText);
        Documents(writer, "outputs", sheet.Outputs);
        Documents(writer, "inputs", sheet.Inputs);
        if (sheet.Uses.Count > 0)
        {
            writer.WriteStartArray("uses");
            foreach (var use in sheet.Uses)
            {
                writer.WriteStartObject();
                writer.WriteString("document", use.Document);
                Optional(writer, "wait_hours", use.WaitHours);
                if (use.PercentCompleteAccurate is { } percent)
                    writer.WriteNumber("percent_complete_accurate", percent);
                Optional(writer, "pain_point", use.PainPoint);
                Optional(writer, "lost_hours_per_month", use.LostHoursPerMonth);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    private static void Documents(Utf8JsonWriter writer, string name, IReadOnlyList<PmapDocumentEntry> documents)
    {
        if (documents.Count == 0)
            return;
        writer.WriteStartArray(name);
        foreach (var document in documents)
        {
            writer.WriteStartObject();
            Optional(writer, "id", document.Id);
            writer.WriteString("name", document.Name);
            Optional(writer, "location", document.Location);
            if (document.ShareConfirmed)
                writer.WriteBoolean("share_confirmed", true);
            Optional(writer, "wait_hours", document.WaitHours);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void Translations(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> translations)
    {
        if (translations.Count == 0)
            return;
        writer.WriteStartObject("translations");
        foreach (var (language, text) in translations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            writer.WriteString(language, text);
        writer.WriteEndObject();
    }

    private static void Optional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
            writer.WriteString(name, value);
    }

    /// <summary>A decimal without its trailing zeros (15 and not 15.00, whatever the scale an engine kept).</summary>
    private static void Optional(Utf8JsonWriter writer, string name, decimal? value)
    {
        if (value is { } number)
            writer.WriteNumber(name, decimal.Parse(number.ToString("0.############################", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
    }
}
