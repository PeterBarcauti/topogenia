// PmapReader.cs
// Reading of a .pmap 1.1 file written by the application, a person or an AI: cleaning, every rule of annex A 2.17, faults by code.

using System.Text.Json;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Json;

namespace Topogenia.Interchange.Pmap;

/// <summary>What the reading needs of the installation: the configured languages and the RACI letters besides R.</summary>
public sealed record PmapReadSettings(LanguageSettings Languages, string RaciLetters);

/// <summary>A read file: its document when it has no fault, and its faults (30 at most), in the order found.</summary>
public sealed record PmapRead(PmapDocument? Document, IReadOnlyList<FileError> Errors);

/// <summary>
/// Reads a `.pmap` file (annex A 2.17, ADR 054). What a copy-paste from a conversation with an AI adds around the JSON is
/// removed first (byte order mark, Markdown code block) and the JSON may carry trailing commas and comments. Every rule is
/// checked before anything is built, and every fault is reported with its place (`map.steps[3].lane`), its code and the
/// values of its message, 30 at most; a file with a fault gives no document.
/// </summary>
public static class PmapReader
{
    private const char ByteOrderMark = (char)0xFEFF;

    public static PmapRead Read(string? text, PmapReadSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new PmapReading(settings).Read(text ?? string.Empty);
    }

    /// <summary>
    /// Removes what a copy-paste from a conversation with an AI adds around the JSON: the byte order mark, the spaces,
    /// the Markdown code block (<c>```json ... ```</c>).
    /// </summary>
    public static string Clean(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clean = text.TrimStart(ByteOrderMark).Trim();
        if (!clean.StartsWith("```", StringComparison.Ordinal))
            return clean;
        var firstLineEnd = clean.IndexOf('\n', StringComparison.Ordinal);
        clean = firstLineEnd < 0 ? string.Empty : clean[(firstLineEnd + 1)..];
        var end = clean.LastIndexOf("```", StringComparison.Ordinal);
        if (end >= 0)
            clean = clean[..end];
        return clean.Trim();
    }
}

/// <summary>One reading of a file: its faults and the identifiers seen so far, shared by every map of the file.</summary>
internal sealed partial class PmapReading(PmapReadSettings settings)
{
    private static readonly string[] DocumentProperties = ["$schema", "format", "version", "generated_by", "generated_at", "project", "map", "uncertainties"];
    private static readonly string[] ProjectProperties = ["code", "name", "language", "description", "translations"];
    private static readonly string[] ProjectProperties12 = [.. ProjectProperties, "workday_hours", "fte_hours_per_month"];

    /// <summary>The file is of the version 1.2: the properties of the value stream are known (ADR 085).</summary>
    private bool valueStream;

    private readonly List<FileError> errors = [];

    /// <summary>The kind of each identifier of the file (lane, step or document): one namespace for the whole file.</summary>
    private readonly Dictionary<string, string> ids = new(StringComparer.Ordinal);

    private int stepCount;

    public PmapRead Read(string text)
    {
        var clean = PmapReader.Clean(text);
        if (clean.Length == 0)
            return Refused("", FileErrorCodes.Empty);
        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(clean, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = PmapFormat.JsonDepthMax,
            });
        }
        catch (JsonException exception)
        {
            return Refused("", FileErrorCodes.NotJson, ("line", (exception.LineNumber ?? 0) + 1), ("column", (exception.BytePositionInLine ?? 0) + 1));
        }
        using (json)
        {
            var root = json.RootElement;
            if (!JsonText.Reads(root))
                return Refused("", FileErrorCodes.NotJson);
            if (root.ValueKind != JsonValueKind.Object)
                return Refused("", FileErrorCodes.NotObject);
            Properties(root, "", DocumentProperties);
            var format = Text(root, "format", "", required: true);
            if (format is not null && format != PmapFormat.Name)
                return Refused("format", FileErrorCodes.WrongFormat, ("expected", PmapFormat.Name));
            var version = Text(root, "version", "", required: true);
            if (version is not null && !PmapFormat.Versions.Contains(version, StringComparer.Ordinal))
                return Refused("version", FileErrorCodes.WrongVersion, ("version", version), ("supported", string.Join(", ", PmapFormat.Versions)));
            valueStream = version == PmapFormat.Version;
            Text(root, "generated_by", "", required: false);
            Text(root, "generated_at", "", required: false);
            var project = Project(root);
            PmapMap? map = null;
            if (Object(root, "map", "", required: true) is { } element)
                map = Map(element, "map", 1);
            var uncertainties = Uncertainties(root);
            if (stepCount > PmapFormat.StepsMax)
                Fault("map", FileErrorCodes.TooManySteps, ("count", stepCount), ("max", PmapFormat.StepsMax));
            if (errors.Count > 0 || project is null || map is null)
                return new PmapRead(null, errors);
            return new PmapRead(new PmapDocument(project, map, uncertainties), errors);
        }
    }

    private PmapRead Refused(string path, string code, params (string Key, object? Value)[] values)
    {
        Fault(path, code, values);
        return new PmapRead(null, errors);
    }

    private void Fault(string path, string code, params (string Key, object? Value)[] values)
    {
        if (errors.Count < PmapFormat.ErrorsMax)
            errors.Add(new FileError(path, code, values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)));
    }

    /// <summary>A refusal of a rule of the domain, kept as a fault of the file with its code and its values.</summary>
    private void Fault(string path, DomainException refusal) =>
        Fault(path, refusal.Code, [.. refusal.Details.Select(pair => (pair.Key, pair.Value))]);

    private PmapProject? Project(JsonElement root)
    {
        if (Object(root, "project", "", required: true) is not { } project)
            return null;
        Properties(project, "project", valueStream ? ProjectProperties12 : ProjectProperties);
        // A code absent or malformed does not prevent the opening: the application asks for it (A-2.17-33).
        var code = Text(project, "code", "project", required: false);
        if (code is not null && !ProjectCode.TryParse(code, out _))
            code = null;
        var name = Entered(project, "name", "project", PmapFormat.ProjectNameMax, required: true);
        var language = Text(project, "language", "project", required: false) ?? settings.Languages.DefaultLanguage;
        if (!settings.Languages.IsSupported(language))
        {
            Fault("project.language", FileErrorCodes.UnknownLanguage, ("language", language), ("possible", string.Join(", ", settings.Languages.Languages)));
            language = settings.Languages.DefaultLanguage;
        }
        var description = Entered(project, "description", "project", PmapFormat.DescriptionMax, required: false);
        var translations = Translations(project, "project", PmapFormat.ProjectNameMax);
        var workday = Number(project, "workday_hours", "project");
        var fte = Number(project, "fte_hours_per_month", "project");
        return name is null ? null : new PmapProject(code, name, language, description, translations) { WorkdayHours = workday, FteHoursPerMonth = fte };
    }

    private List<string> Uncertainties(JsonElement root)
    {
        var kept = new List<string>();
        if (!root.TryGetProperty("uncertainties", out var list) || list.ValueKind == JsonValueKind.Null)
            return kept;
        if (list.ValueKind != JsonValueKind.Array)
        {
            Fault("uncertainties", FileErrorCodes.NotAList, ("name", "uncertainties"));
            return kept;
        }
        var i = 0;
        foreach (var element in list.EnumerateArray())
        {
            var path = $"uncertainties[{i++}]";
            if (element.ValueKind != JsonValueKind.String)
                Fault(path, FileErrorCodes.UncertaintyNotText);
            else if (Checked(element.GetString(), path, "uncertainties", PmapFormat.UncertaintyMax) is { } text)
                kept.Add(text);
        }
        return [.. kept.Take(PmapFormat.UncertaintiesKept)];
    }
}
