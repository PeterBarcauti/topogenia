// IProjectReader.cs
// Port of the reads that span several projects: the list of the projects, the trash, the codes in use (spec 3.4, annex A 1, 2.1, 2.15).

using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Ports;

/// <summary>
/// The projects a user may see in a list: those of which he is a member, or every one for an administrator (annex A 1,
/// `Droits.cs:30`); decided once by the template of the user requests, never by a handler.
/// </summary>
public sealed record ProjectVisibility(Guid UserId, bool Everything);

/// <summary>
/// A live project of the list (1.x `ProjetResume`): its record, the translations of its name by language, its root map,
/// the role of the user as a member (null for an administrator who is not one), its live steps on live maps and its live maps.
/// </summary>
public sealed record ProjectListRow(
    Project Project,
    IReadOnlyDictionary<string, string> NameTranslations,
    Guid RootMapId,
    MemberRole? Role,
    int StepCount,
    int MapCount);

/// <summary>A project in the trash (1.x `ProjetCorbeille`): its record, the name of who deleted it, its live steps on live maps.</summary>
public sealed record TrashRow(Project Project, string? DeletedByName, int StepCount);

/// <summary>
/// Reads across the projects, each call in one consistent read (spec 3.4: dedicated queries that project straight to the
/// replies; ADR 031), inside the transaction of a unit of work when one is open. A project in the trash is never in the
/// list of the live projects, and the reverse.
/// </summary>
public interface IProjectReader
{
    /// <summary>The live projects that <paramref name="visibility"/> lets the user see, in no particular order.</summary>
    Task<IReadOnlyList<ProjectListRow>> LiveProjectsAsync(ProjectVisibility visibility, CancellationToken cancellationToken);

    /// <summary>
    /// The projects in the trash that the user facilitated (every one when <see cref="ProjectVisibility.Everything"/>),
    /// annex A 2.15, A-2.15-15; in no particular order.
    /// </summary>
    Task<IReadOnlyList<TrashRow>> TrashAsync(ProjectVisibility visibility, CancellationToken cancellationToken);

    /// <summary>
    /// The projects, live or in the trash, whose code is <paramref name="code"/> (a code stays reserved until the purge,
    /// A-2.1-03): what the creation of a project checks the uniqueness of its code against.
    /// </summary>
    Task<IReadOnlyList<ExistingProjectCode>> CodesAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// The codes of the projects, live or in the trash, that start with <paramref name="prefix"/>: what the code given to a
    /// project replaced by an import is chosen against (`ProjectCodeRules.TrashCode`, ADR 052).
    /// </summary>
    Task<IReadOnlyList<string>> CodesStartingWithAsync(string prefix, CancellationToken cancellationToken);
}
