// RoundTripTests.cs
// The round trips of spec 7.4 through the API on both databases: the project of every function, exact with and without the people, and crossed.

using System.IO.Compression;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Exchange;

/// <summary>
/// The project of every function is the fictional project shipped in `samples/conges.topogenia` (ADR 055). Imported by the
/// administrator it gives a project P of the application; `export(import(export(P))) == export(P)`, the manifest aside. In
/// one database the import keeps the code of P by replacing it (a new project would need another code, which rewrites every
/// readable code); across the two databases, the import is a new project under the same code.
/// </summary>
public sealed class RoundTripTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static byte[] Sample => File.ReadAllBytes(RepositoryPaths.Of("samples", "conges.topogenia"));

    /// <summary>The content entries of a package, the manifest left out (spec 7.4: its metadata are outside the comparison).</summary>
    private static Dictionary<string, string> Content(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return archive.Entries.Where(entry => entry.FullName != "manifest.json").ToDictionary(entry => entry.FullName, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        });
    }

    private static string Flag(bool value) => value ? "true" : "false";

    /// <summary>A package imported as a new project by the administrator, under <paramref name="code"/>; its identifier and code.</summary>
    private static async Task<(Guid Id, string Code)> ImportAsync(ApiClient admin, byte[] package, bool withMembers, string code)
    {
        var reply = await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?with_members={Flag(withMembers)}&code={code}", package, null);
        Assert.True(reply.Status == 201, reply.Body);
        var imported = reply.Ok(201);
        return (imported.GetProperty("id").GetGuid(), imported.GetProperty("code").GetString()!);
    }

    /// <summary>A project replaced by a package, the If-Match of the project sent; the identifier of the project that takes its place.</summary>
    private static async Task<Guid> ReplaceAsync(ApiClient admin, Guid projectId, byte[] package, bool withMembers)
    {
        var version = (await admin.GetAsync($"/api/projects/{projectId}")).Ok().GetProperty("version").GetInt32();
        var reply = await admin.SendAsync(HttpMethod.Post, $"/api/projects/{projectId}/import?with_members={Flag(withMembers)}", package, version);
        Assert.True(reply.Status == 201, reply.Body);
        return reply.Ok(201).GetProperty("id").GetGuid();
    }

    private static async Task<byte[]> ExportAsync(ApiClient client, Guid projectId, bool withoutPeople)
    {
        var reply = await client.PostAsync($"/api/projects/{projectId}/export", new { without_people = withoutPeople });
        Assert.True(reply.Status == 200, reply.Body);
        return reply.Bytes;
    }

    /// <summary>The same entries, compared one by one so that a difference names its entry and its place.</summary>
    private static void AssertSameContent(byte[] expected, byte[] actual)
    {
        var (left, right) = (Content(expected), Content(actual));
        Assert.Equal(left.Keys.Order(StringComparer.Ordinal), right.Keys.Order(StringComparer.Ordinal));
        foreach (var (entry, text) in left)
            Assert.True(text == right[entry], $"{entry} differs: {Difference(text, right[entry])}");
    }

    private static string Difference(string left, string right)
    {
        var at = left.Zip(right).TakeWhile(pair => pair.First == pair.Second).Count();
        string Around(string text) => text.Substring(Math.Max(0, at - 300), Math.Min(text.Length - Math.Max(0, at - 300), 600));
        return string.Join(Environment.NewLine, $"at {at}", "--- expected ---", Around(left), "--- actual ---", Around(right));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_project_of_every_function_makes_an_exact_round_trip_with_and_without_the_people(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        var (project, _) = await ImportAsync(admin, Sample, withMembers: false, ApiProject.NewCode());

        foreach (var withoutPeople in new[] { true, false })
        {
            var exported = await ExportAsync(admin, project, withoutPeople);
            project = await ReplaceAsync(admin, project, exported, withMembers: !withoutPeople);

            AssertSameContent(exported, await ExportAsync(admin, project, withoutPeople));
        }
    }

    [Fact]
    public async Task The_project_of_every_function_crosses_from_sqlite_to_sql_server_and_back_unchanged()
    {
        using var desktop = (await hosts.OfAsync(EngineKind.Sqlite)).As(Logins.Admin);
        using var server = (await hosts.OfAsync(EngineKind.SqlServer)).As(Logins.Admin);
        var (onDesktop, code) = await ImportAsync(desktop, Sample, withMembers: false, ApiProject.NewCode());
        var fromDesktop = await ExportAsync(desktop, onDesktop, withoutPeople: false);

        var (onServer, serverCode) = await ImportAsync(server, fromDesktop, withMembers: true, code);
        var fromServer = await ExportAsync(server, onServer, withoutPeople: false);
        var back = await ReplaceAsync(desktop, onDesktop, fromServer, withMembers: true);

        Assert.Equal(code, serverCode);
        AssertSameContent(fromDesktop, fromServer);
        AssertSameContent(fromDesktop, await ExportAsync(desktop, back, withoutPeople: false));
        Assert.Equal(Content(Sample).Keys.Order(StringComparer.Ordinal), Content(await ExportAsync(desktop, back, withoutPeople: true)).Keys.Order(StringComparer.Ordinal));
    }
}
