// OrderParityTests.cs
// Two orders that differ from one engine to the other, pinned with the rule that avoids them: characters outside the basic plane, identifiers (ADR 038).

using System.Data.SqlTypes;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Collation;

/// <summary>
/// SQL Server sorts an `nvarchar` in binary by UTF-16 code unit (a character outside the basic plane, a surrogate pair
/// from U+D800, comes before U+E000), SQLite by code point; SQL Server sorts a `uniqueidentifier` by its last six bytes
/// first, SQLite the text of the identifier, in the order of its creation for a Guid v7. Neither is aligned: a sort of
/// labels for display is done in memory (spec 4.3-3), and no query sorts by an identifier (gate P3, round 2, data findings m1
/// and m2). These tests pin each order, so that a change of an engine shows here.
/// </summary>
public sealed partial class OrderParityTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [GeneratedRegex(@"\.(?:OrderBy|OrderByDescending|ThenBy|ThenByDescending)\(\s*(?<row>\w+)\s*=>\s*\k<row>\.\w*Id\s*\)")]
    private static partial Regex OrderedByIdentifier();

    private async Task<(TestDatabase Database, ProjectGraph Graph)> WrittenAsync(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        return (database, graph);
    }

    private static AppUser UserNamed(ProjectGraph graph, string normalized) =>
        graph.Reader with { Id = Guid.CreateVersion7(), Login = normalized, LoginNormalized = normalized };

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Characters_outside_the_basic_plane_sort_by_code_unit_on_sql_server_and_by_code_point_on_sqlite(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        string[] names = ["\U0001F600", "！", "", "z"];
        var users = names.Select((name, index) => UserNamed(graph, $@"FICTITIOUS\{graph.Code}PLANE{index}") with { DisplayName = name }).ToArray();
        await Refusals.SaveAsync(database, users);

        await using var context = database.Context();
        var ids = users.Select(user => user.Id).ToList();
        var sorted = await context.Set<AppUser>().Where(user => ids.Contains(user.Id)).OrderBy(user => user.DisplayName)
            .Select(user => user.DisplayName).ToListAsync(Token);

        string[] byCodeUnit = ["z", "\U0001F600", "", "！"];
        string[] byCodePoint = ["z", "", "！", "\U0001F600"];
        Assert.Equal(byCodeUnit, names.Order(StringComparer.Ordinal));
        Assert.Equal(kind == EngineKind.SqlServer ? byCodeUnit : byCodePoint, sorted);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Identifiers_sort_by_their_last_bytes_on_sql_server_and_by_their_text_on_sqlite(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        var users = Enumerable.Range(0, 6)
            .Select(index => UserNamed(graph, $@"FICTITIOUS\{graph.Code}GUID{index}") with { Id = Guid.CreateVersion7(graph.Time.AddSeconds(index * 1000)) })
            .Append(UserNamed(graph, $@"FICTITIOUS\{graph.Code}GUIDA") with { Id = Guid.Parse("00000000-0000-7000-8000-000000000002") })
            .Append(UserNamed(graph, $@"FICTITIOUS\{graph.Code}GUIDB") with { Id = Guid.Parse("00000000-0000-7001-8000-000000000001") })
            .ToArray();
        await Refusals.SaveAsync(database, users);

        await using var context = database.Context();
        var ids = users.Select(user => user.Id).ToList();
        var sorted = await context.Set<AppUser>().Where(user => ids.Contains(user.Id)).OrderBy(user => user.Id).Select(user => user.Id).ToListAsync(Token);

        var bySqlServer = ids.OrderBy(id => new SqlGuid(id)).ToList();
        var byText = ids.OrderBy(id => id.ToString().ToUpperInvariant(), StringComparer.Ordinal).ToList();
        Assert.NotEqual(bySqlServer, byText);
        Assert.Equal(kind == EngineKind.SqlServer ? bySqlServer : byText, sorted);
    }

    [Fact]
    public void No_code_of_the_persistence_sorts_by_an_identifier()
    {
        var root = RepositoryRoot();
        var offending = Directory.EnumerateDirectories(Path.Combine(root, "src"), "Topogenia.Persistence*")
            .SelectMany(project => Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            .Where(file => !Segments(root, file).Any(segment => segment is "bin" or "obj" or "Migrations"))
            .Where(file => OrderedByIdentifier().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(root, file))
            .ToList();

        Assert.Empty(offending);
        Assert.Matches(OrderedByIdentifier(), "rows.OrderBy(row => row.Id)");
        Assert.Matches(OrderedByIdentifier(), "steps.ThenByDescending(step => step.LaneId)");
    }

    private static string[] Segments(string root, string file) => Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>The folder of the solution, above this source file (the test output may be elsewhere, `--artifacts-path`).</summary>
    private static string RepositoryRoot([CallerFilePath] string source = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Topogenia.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Topogenia.sln not found above the source of the test.");
    }
}
