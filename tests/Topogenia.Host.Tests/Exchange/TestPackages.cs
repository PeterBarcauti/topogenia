// TestPackages.cs
// Packages for the tests of the import routes: the smallest valid project, written by the writer of the export.

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;

namespace Topogenia.Host.Tests.Exchange;

/// <summary>The smallest package a project can give: a root map with its first lane, without the people.</summary>
public static class TestPackages
{
    private static readonly DateTime At = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    public static byte[] Minimal(string code)
    {
        var project = Guid.CreateVersion7();
        var map = Guid.CreateVersion7();
        var author = Guid.CreateVersion7();
        IRecord[] records =
        [
            new Project(project, code, "Projet importé", "fr", null, null, Classification.Internal, null, null, 0, At, author, null, null, null, null, 1),
            new ProcessMap(map, project, null, "Projet importé", "fr", At, author, null, null, null, null, 1),
            new Lane(Guid.CreateVersion7(), map, code + "-C01", "Acteur 1", "fr", LaneType.Person, null, LaneColors.ForIndex(0), 0, null, At, author, null,
                null, null, null, 1),
            new ProjectCounter(project, CounterKind.Lane, 1),
        ];
        return PackageWriter.Write(new ProjectWorkspace(project, records), new Dictionary<Guid, string>(), new PackageOptions(WithoutPeople: true), new PackageStamp(At, null));
    }
}
