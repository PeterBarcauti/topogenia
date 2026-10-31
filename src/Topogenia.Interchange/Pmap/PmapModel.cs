// PmapModel.cs
// The .pmap document in memory (1.2, 1.1 read): project, maps, lanes, steps, flows, sheets, as read from a file or built from a project.

namespace Topogenia.Interchange.Pmap;

/// <summary>A `.pmap` document (ADR 054): the project, its root map and the points an author asks a person to check.</summary>
public sealed record PmapDocument(PmapProject Project, PmapMap Map, IReadOnlyList<string> Uncertainties);

/// <summary>The project of a file: a code (null when absent or malformed: the application asks for it), its name and language.</summary>
public sealed record PmapProject(string? Code, string Name, string Language, string? Description, IReadOnlyDictionary<string, string> Translations)
{
    /// <summary>The working day of the project, in hours (1.2, spec 14.2, S6); absent: the default.</summary>
    public decimal? WorkdayHours { get; init; }

    /// <summary>The hours of a full-time equivalent in a month (1.2); absent: the default.</summary>
    public decimal? FteHoursPerMonth { get; init; }
}

/// <summary>A map: its lanes from top to bottom, its steps, its flows.</summary>
public sealed record PmapMap(IReadOnlyList<PmapLane> Lanes, IReadOnlyList<PmapStep> Steps, IReadOnlyList<PmapFlow> Flows);

/// <summary>A lane, by its identifier in the file; type and colour as codes of the application.</summary>
public sealed record PmapLane(string Id, string Name, string Type, string? Color, IReadOnlyDictionary<string, string> Translations)
{
    /// <summary>The headcount of a lane of people (1.2, spec 14.2, S4).</summary>
    public int? Headcount { get; init; }
}

/// <summary>The incoming volume of a start step (1.2, spec 14.2, S1): a quantity and a period code.</summary>
public sealed record PmapVolume(decimal Quantity, string Period);

/// <summary>The lead time of a sheet (1.2, spec 14.2, S3): a value and a unit code.</summary>
public sealed record PmapLeadTime(decimal Value, string Unit);

/// <summary>
/// A step: its lane in the same map, label, type and automation as codes, column (null for the automatic placement),
/// row, the RACI roles of the other lanes by their identifier, its sub-map, its translations and its sheet (1.1).
/// </summary>
public sealed record PmapStep(
    string Id,
    string Lane,
    string Label,
    string Type,
    int? Column,
    int Row,
    string Automation,
    IReadOnlyDictionary<string, string> Raci,
    PmapMap? SubMap,
    IReadOnlyDictionary<string, string> Translations,
    PmapSheet? Sheet)
{
    /// <summary>The incoming volume of a start step (1.2).</summary>
    public PmapVolume? Volume { get; init; }
}

/// <summary>A flow between two steps of the same map, by their identifiers, with its label and its translations (1.1).</summary>
public sealed record PmapFlow(string Source, string Target, string? Label, IReadOnlyDictionary<string, string> Translations)
{
    /// <summary>The share of the cases of an exit of a decision or an inclusive gateway, in percent (1.2, spec 14.2, S2).</summary>
    public decimal? Share { get; init; }
}

/// <summary>
/// The sheet block of a step (AR-93), every part optional: the fields of the form, the procedure, the free text, the
/// documents the step produces and receives from outside, and the outputs of other steps it uses.
/// </summary>
public sealed record PmapSheet(
    string? Actor,
    string? Purpose,
    string? Tools,
    string? Frequency,
    decimal? DurationMinutes,
    IReadOnlyList<PmapTask> Procedure,
    string? FreeText,
    IReadOnlyList<PmapDocumentEntry> Outputs,
    IReadOnlyList<PmapDocumentEntry> Inputs,
    IReadOnlyList<PmapUse> Uses)
{
    /// <summary>The lead time (1.2).</summary>
    public PmapLeadTime? LeadTime { get; init; }

    public bool IsEmpty => Actor is null && Purpose is null && Tools is null && Frequency is null && DurationMinutes is null && LeadTime is null
        && Procedure.Count == 0 && FreeText is null && Outputs.Count == 0 && Inputs.Count == 0 && Uses.Count == 0;
}

/// <summary>A line of the procedure: what is done, with which tool, and its nature code.</summary>
public sealed record PmapTask(string Text, string? Tool, string? Nature);

/// <summary>
/// A document of a step: its identifier in the file (needed only for an output that another step uses), its name, its
/// link, whether its share is confirmed, the waiting time of an external input.
/// </summary>
public sealed record PmapDocumentEntry(string? Id, string Name, string? Location, bool ShareConfirmed, decimal? WaitHours);

/// <summary>
/// The use by a step of an output of another step of the same map, named by the identifier of the output in the file,
/// with the waiting time, the part usable as is (%C&amp;A) and the pain point it raises on the producing step.
/// </summary>
public sealed record PmapUse(string Document, decimal? WaitHours, int? PercentCompleteAccurate, string? PainPoint, decimal? LostHoursPerMonth);
