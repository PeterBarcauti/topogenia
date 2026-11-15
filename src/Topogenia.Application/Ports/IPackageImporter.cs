// IPackageImporter.cs
// Port of the reading of a received exchange package (spec 7.3, 5.4): its guards, what it holds, the records it rebuilds.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Ports;

/// <summary>A member of a package: his login and the name the package gives him.</summary>
public sealed record PackageMember(string Login, string DisplayName);

/// <summary>What a package holds, counted for the preview (spec 7.3).</summary>
public sealed record PackageCounts(int Maps, int Lanes, int Steps, int Flows, int Sheets, int Documents, int PainPoints, int Translations);

/// <summary>
/// A package that passed every guard (ADR 052, section 3) and is consistent in itself: what the application decides on
/// before anything is written. The interchange gives it; the application never sees the format.
/// </summary>
public interface IReadPackage
{
    /// <summary>The code of the exported project, the prefix of every readable code of the package.</summary>
    string ProjectCode { get; }

    /// <summary>True when the package carries the people: its members and the logins of its authors.</summary>
    bool WithPeople { get; }

    /// <summary>Every login the package names (members, authors, holders), each once, case ignored.</summary>
    IReadOnlyList<string> Logins { get; }

    /// <summary>The members of the package, in its order; empty without the people.</summary>
    IReadOnlyList<PackageMember> Members { get; }

    PackageCounts Counts { get; }

    /// <summary>
    /// The code of the project a target state was created from (Makigami M6, ADR 095), null for any other project: the
    /// application links it again to a live project of that code that the importer may see, or leaves it unlinked.
    /// </summary>
    string? BaselineProjectCode { get; }
}

/// <summary>
/// How a read package is rebuilt: the context of the import (its actor is the importer and the author of whatever has no
/// author in the package), the code of the new project (the code of the package, or another one: every readable code is
/// rewritten under it), the user of a login (asked only for the logins the rebuilt records name; null leaves the author
/// to the importer) and whether the members are taken (spec 7.3).
/// </summary>
public sealed record ImportPlan(OperationContext Context, string ProjectCode, Func<string, Guid?> User, bool WithMembers);

/// <summary>
/// The records of a package rebuilt (<see cref="IPackageImporter.Build"/>) and the comments of presentation of its live
/// steps (format 2.4, ADR 099), under the same new identifiers: each written by the actor of the plan, at its time, in
/// version 1. They are not records of the closed core: the application writes them beside the steps.
/// </summary>
public sealed record RebuiltPackage(IReadOnlyList<IRecord> Records, IReadOnlyList<PresentationComment> Comments)
{
    /// <summary>The comments of presentation of its flows (format 2.4, ADR 099, lot J10), rebuilt as those of its steps and written beside the flows: none by default.</summary>
    public IReadOnlyList<FlowPresentationComment> FlowComments { get; init; } = [];

    /// <summary>
    /// The voices its project keeps (format 2.4, ADR 103), rebuilt on the new project as the comments on its steps and
    /// written beside it: none by default.
    /// </summary>
    public IReadOnlyList<PresentationVoice> Voices { get; init; } = [];
}

/// <summary>
/// Reads a received package (spec 7.3, 5.4). The interchange implements it (ADR 052): every refusal is
/// `PAQUET_INVALIDE` (or `PAQUET_TROP_RECENT`) and comes before anything is written.
/// </summary>
public interface IPackageImporter
{
    /// <summary>Checks the archive, its entries, sizes, hashes and schema, then the references of its content.</summary>
    IReadPackage Read(byte[] content);

    /// <summary>
    /// The records of the project the package describes, under new identifiers: the project first, then every other
    /// record in an order where each one comes after what it names.
    /// </summary>
    IReadOnlyList<IRecord> Build(IReadPackage package, ImportPlan plan);

    /// <summary>
    /// The records of <see cref="Build"/> and, beside them, the comments of presentation of the steps and of the flows they
    /// rebuild (ADR 099) and the voices of their project (ADR 103).
    /// </summary>
    RebuiltPackage Rebuild(IReadPackage package, ImportPlan plan);
}
