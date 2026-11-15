// PackageOptions.cs
// Options and stamp of an export (spec 7.2, 5.4): with or without the members and the authors, the code of the source of a target state, the comments of presentation of the steps and of the flows, the voices of the project, time and author of the export.

using Topogenia.Application.Presentation;

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The options of an export. WithoutPeople leaves out the members (members.json) and every login of the package: the
/// authors of the contributions, the actor of a lane, the assignee of a step, the author of the export (spec 5.4, 7.2).
/// The interface ticks it by default; the API and the command line require it, never a silent default (AR-34).
/// </summary>
public sealed record PackageOptions(bool WithoutPeople)
{
    /// <summary>
    /// The code of the project a target state was created from, when it is live and the exporter may see it (Makigami M6,
    /// ADR 095), read by the application outside the workspace, as the comments of presentation. Null otherwise.
    /// </summary>
    public string? BaselineProjectCode { get; init; }

    /// <summary>
    /// The comments of presentation of the steps (ADR 099), kept beside the workspace and read by the application: each one
    /// of a live exported step goes into the package with its step, the others are left out.
    /// </summary>
    public IReadOnlyList<PresentationComment> Comments { get; init; } = [];

    /// <summary>
    /// The comments of presentation of the flows (ADR 099, lot J10), kept beside the workspace and read by the application
    /// as those of the steps: each one of an exported flow goes into the package with its flow, the others are left out.
    /// </summary>
    public IReadOnlyList<FlowPresentationComment> FlowComments { get; init; } = [];

    /// <summary>
    /// The voices the project keeps for its presentation (ADR 103), kept beside the workspace and read by the application
    /// as the comments: those of the exported project go on it, with or without the people, the others are left out.
    /// </summary>
    public IReadOnlyList<PresentationVoice> Voices { get; init; } = [];
}

/// <summary>When and by whom the package was exported (manifest metadata, outside the compared content, spec 7.4).</summary>
public sealed record PackageStamp(DateTime ExportedAt, string? ExportedBy);
