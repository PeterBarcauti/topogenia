// PackageOptions.cs
// Options and stamp of an export (spec 7.2, 5.4): with or without the members and the authors, time and author of the export.

namespace Topogenia.Interchange.Packages;

/// <summary>
/// The options of an export. WithoutPeople leaves out the members (members.json) and every login of the package: the
/// authors of the contributions, the actor of a lane, the assignee of a step, the author of the export (spec 5.4, 7.2).
/// The interface ticks it by default; the API and the command line require it, never a silent default (AR-34).
/// </summary>
public sealed record PackageOptions(bool WithoutPeople);

/// <summary>When and by whom the package was exported (manifest metadata, outside the compared content, spec 7.4).</summary>
public sealed record PackageStamp(DateTime ExportedAt, string? ExportedBy);
