// IPackageExporter.cs
// Port of the writing of the exchange package of a project (spec 7.2): who it names, its bytes, the name of its file.

using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Ports;

/// <summary>
/// What an export asks of the writer: the workspace of the project with every group, the logins of the users it names
/// (empty without the people), the option without the people (spec 5.4, AR-34), the time and the author of the export.
/// </summary>
public sealed record PackageRequest(
    ProjectWorkspace Workspace,
    IReadOnlyDictionary<Guid, string> Logins,
    bool WithoutPeople,
    DateTime ExportedAt,
    string ExportedBy);

/// <summary>An entry of a package as a text: its path in the archive and its JSON.</summary>
public sealed record PackageText(string Path, string Content);

/// <summary>
/// Writes the package of a project (spec 7.2). The interchange implements it (ADR 051): the application never sees the
/// format, it hands the workspace and the logins and gets the bytes back.
/// </summary>
public interface IPackageExporter
{
    /// <summary>The users that the package names with the people: the logins the writer needs.</summary>
    IReadOnlySet<Guid> UsersNamedBy(ProjectWorkspace workspace);

    /// <summary>The bytes of the package.</summary>
    byte[] Write(PackageRequest request);

    /// <summary>
    /// The entries of the package of a project without the people and without its translations, as texts: the schema of the
    /// project given to the model of the assistant (spec 3.0 section 3.1, decision of the owner of the product of
    /// 29/10/2026), in the order of the archive.
    /// </summary>
    IReadOnlyList<PackageText> Describe(ProjectWorkspace workspace);

    /// <summary>The name of the file of a package (A-1.2-16, the rule of the 1.x export): code, short name, day.</summary>
    string FileName(Project project, DateTime exportedAt);
}
