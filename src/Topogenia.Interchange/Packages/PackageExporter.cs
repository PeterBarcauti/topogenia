// PackageExporter.cs
// Adapter of the port of the export: the package written by PackageWriter, its file named by the rule of the 1.x export.

using System.Text;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Files;

namespace Topogenia.Interchange.Packages;

/// <summary><see cref="IPackageExporter"/> on <see cref="PackageWriter"/> (ADR 051).</summary>
public sealed class PackageExporter : IPackageExporter
{
    public IReadOnlySet<Guid> UsersNamedBy(ProjectWorkspace workspace) => PackageWriter.UsersNamedBy(workspace);

    public byte[] Write(PackageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PackageWriter.Write(request.Workspace, request.Logins,
            new PackageOptions(request.WithoutPeople)
            {
                BaselineProjectCode = request.BaselineProjectCode, Comments = request.Comments, FlowComments = request.FlowComments,
                Voices = request.Voices,
            },
            new PackageStamp(request.ExportedAt, request.ExportedBy));
    }

    /// <summary>
    /// The entries without the people, the translations, the photos nor the source of a target state (ADR 095, P3d), and
    /// without what is kept beside the workspace, which the writer is not given here: the comments of presentation
    /// (ADR 099) and the voices of the project (ADR 103).
    /// </summary>
    public IReadOnlyList<PackageText> Describe(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return PackageWriter.Entries(workspace, new Dictionary<Guid, string>(), new PackageOptions(WithoutPeople: true))
            .Where(entry => entry.Path != PackageFormat.Translations)
            .Select(entry => new PackageText(entry.Path,
                Encoding.UTF8.GetString(entry.Path == PackageFormat.Project ? SnapshotEntries.Without(entry.Content) : entry.Content)))
            .ToList();
    }

    public string FileName(Project project, DateTime exportedAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        return ExportFileName.Of(project.Code, project.Name, exportedAt, PackageFormat.Extension);
    }
}
