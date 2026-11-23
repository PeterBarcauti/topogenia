// PmapReader.cs
// Reading of a .pmap file (1.1 to 1.5) written by the application, a person or an AI: cleaning, every rule of annex A 2.17, faults by code.

using System.Text.Json;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Json;

namespace Topogenia.Interchange.Pmap;

/// <summary>
/// The settings of a read: the languages and the RACI letters configured; <see cref="Proposal"/> for the proposal of the
/// assistant, whose routes are not read (ADR 091: the drawing of a proposal is the drawing of the application).
/// </summary>
public sealed record PmapReadSettings(LanguageSettings Languages, string RaciLetters, bool Proposal = false)
{
    /// <summary>
    /// For a file inserted as a detailed step (3.3, F-208): a translation, or a line of a comment, in a language that is not
    /// configured is left out and its language listed (<see cref="PmapDocument.DroppedLanguages"/>), instead of a fault; the
    /// language of the file itself must still be configured.
    /// </summary>
    public bool DropUnknownLanguages { get; init; }
}

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
    private static readonly string[] ProjectProperties15 = [.. ProjectProperties12, GlossaryField];

    /// <summary>The file is of the version 1.2: the properties of the value stream are known (ADR 085).</summary>
    private bool valueStream;

    /// <summary>A file of the version 1.3: a flow may carry the route a user chose (ADR 091).</summary>
    private bool routes;

    /// <summary>The file is of the version 1.4, which may give a summary to a map (ADR 094).</summary>
    private bool summaries;

    private readonly List<FileError> errors = [];

    /// <summary>The languages of the translations left out by a read that drops them (<see cref="PmapReadSettings.DropUnknownLanguages"/>).</summary>
    private readonly SortedSet<string> dropped = new(StringComparer.Ordinal);

    /// <summary>
    /// A translation in a language that is not configured: left out and its language kept by a read that drops them, a fault
    /// at its place otherwise. True when the translation is to be read.
    /// </summary>
    private bool Configured(string language, string place)
    {
        if (settings.Languages.IsSupported(language))
            return true;
        if (settings.DropUnknownLanguages)
            dropped.Add(language);
        else
            Fault(place, FileErrorCodes.UnknownLanguage, ("language", language), ("possible", string.Join(", ", settings.Languages.Languages)));
        return false;
    }

    /// <summary>The kind of each identifier of the file (lane, step or document): one namespace for the whole file.</summary>
    private readonly Dictionary<string, string> ids = new(StringComparer.Ordinal);

    private int stepCount;

    /// <summary>The options of the parser, for the reading again that finds a key written twice.</summary>
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = PmapFormat.JsonDepthMax,
    };

    /// <summary>
    /// The JSON of a file, cleaned then parsed (trailing commas, comments, 256 levels, no key written twice in one object),
    /// its root an object whose every text reads; null with its fault otherwise. The same for a `.pmap` and for the
    /// envelope of a collection (ADR 114).
    /// </summary>
    private JsonDocument? Parse(string text)
    {
        var clean = PmapReader.Clean(text);
        if (clean.Length == 0)
        {
            Fault("", FileErrorCodes.Empty);
            return null;
        }
        JsonDocument json;
        try
        {
            // A key written twice in one object is refused, as the package does (PackageReader): the parser would keep the
            // last one, where a reader of the text sees the first, and a collection could show one text and open another.
            json = JsonDocument.Parse(clean, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                AllowDuplicateProperties = false,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = PmapFormat.JsonDepthMax,
            });
        }
        catch (JsonException exception)
        {
            // The check of the keys written twice follows the parsing and its fault has no line: the place is found again.
            var twice = exception.LineNumber is null ? JsonDuplicateKeys.Place(clean, ReaderOptions) : null;
            var (line, column) = twice ?? ((exception.LineNumber ?? 0) + 1, (exception.BytePositionInLine ?? 0) + 1);
            Fault("", FileErrorCodes.NotJson, ("line", line), ("column", column));
            return null;
        }
        catch (InvalidOperationException)
        {
            // Half of a surrogate pair in a name, met by the check of the keys written twice: a text that does not read.
            Fault("", FileErrorCodes.NotJson);
            return null;
        }
        var root = json.RootElement;
        var refusal = !JsonText.Reads(root) ? FileErrorCodes.NotJson : root.ValueKind != JsonValueKind.Object ? FileErrorCodes.NotObject : null;
        if (refusal is null)
            return json;
        json.Dispose();
        Fault("", refusal);
        return null;
    }

    public PmapRead Read(string text)
    {
        using var json = Parse(text);
        if (json is null)
            return new PmapRead(null, errors);
        var root = json.RootElement;
        Properties(root, "", DocumentProperties);
        var format = Text(root, "format", "", required: true);
        if (format is not null && format != PmapFormat.Name)
            return Refused("format", FileErrorCodes.WrongFormat, ("expected", PmapFormat.Name));
        var version = Text(root, "version", "", required: true);
        if (version is not null && !PmapFormat.Versions.Contains(version, StringComparer.Ordinal))
            return Refused("version", FileErrorCodes.WrongVersion, ("version", version), ("supported", string.Join(", ", PmapFormat.Versions)));
        valueStream = version is PmapFormat.VersionWithoutRoutes or PmapFormat.VersionWithoutSummaries or PmapFormat.VersionWithoutExtras or PmapFormat.Version;
        routes = version is PmapFormat.VersionWithoutSummaries or PmapFormat.VersionWithoutExtras or PmapFormat.Version;
        summaries = version is PmapFormat.VersionWithoutExtras or PmapFormat.Version;
        extras = version == PmapFormat.Version;
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
        return new PmapRead(new PmapDocument(project, map, uncertainties) { DroppedLanguages = [.. dropped] }, errors);
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
        Properties(project, "project", extras ? ProjectProperties15 : valueStream ? ProjectProperties12 : ProjectProperties);
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
        var workday = WorkNumber(project, WorkTime.WorkdayField, WorkTime.ParseWorkday);
        var fte = WorkNumber(project, WorkTime.FteField, WorkTime.ParseFte);
        var glossary = Glossary(project);
        return name is null ? null : new PmapProject(code, name, language, description, translations)
        {
            WorkdayHours = workday, FteHoursPerMonth = fte, Glossary = glossary,
        };
    }

    /// <summary>
    /// A setting of the work time of the project, null when absent or refused by the rule of the domain that the opening
    /// applies (a fault where it is broken: the model corrects its proposal, audit 4.5).
    /// </summary>
    private decimal? WorkNumber(JsonElement project, string name, Func<decimal, decimal> rule)
    {
        if (Number(project, name, "project") is not { } hours)
            return null;
        // In a file 1.1, the setting is already unknown (a fault of its own).
        if (!valueStream)
            return hours;
        try
        {
            rule(hours);
            return hours;
        }
        catch (DomainException refusal)
        {
            // At the path of the project, as the building of the project says it (PmapBuild); the field names the setting.
            Fault("project", refusal);
            return null;
        }
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
