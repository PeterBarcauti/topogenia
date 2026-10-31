// MigrationDescentTests.cs
// A database taken down by the tools of EF Core, one migration or back to the initial one, is behind and migrated up again (data M2).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Schema;

/// <summary>
/// The application never migrates down; `dotnet ef database update &lt;migration&gt;` does. EF Core removes its own row
/// with each way down and kept the record of <c>schema_history</c>: the start read that record without its migration as a
/// history changed by hand and refused the database for good (<c>SCHEMA_MODIFIE</c>), on both engines. The way down of
/// each migration written since removes its record (ADR 027, revision of round 3); on SQLite, the way down of the rebuild also
/// gives back the statements of the initial migration. The database holds a project graph, a detailed map among them.
/// </summary>
public sealed class MigrationDescentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_database_taken_down_one_migration_is_behind_and_migrated_up_again_with_its_data(EngineKind kind)
    {
        var graph = ProjectGraph.New();
        await using var database = await WithGraphAsync(kind, graph);
        var known = await KnownAsync(database);

        await DownToAsync(database, known[^2]);

        await AssertBehindThenUpAsync(database, graph, known, [known[^1]]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_database_taken_down_to_the_migration_before_the_text_guards_is_behind_and_migrated_up_again(EngineKind kind)
    {
        var graph = ProjectGraph.New();
        await using var database = await WithGraphAsync(kind, graph);
        var known = await KnownAsync(database);
        var guards = known.Single(id => id.EndsWith("_TextGuardsAndIntegerBounds", StringComparison.Ordinal));
        var before = known[known.IndexOf(guards) - 1];

        await DownToAsync(database, before);

        await AssertBehindThenUpAsync(database, graph, known, known[known.IndexOf(guards)..]);
    }

    private static async Task<TestDatabase> WithGraphAsync(EngineKind kind, ProjectGraph graph)
    {
        var database = await TestDatabase.MigratedAsync(kind);
        try
        {
            await using var context = database.Context();
            context.AddRange([.. graph.Records]);
            await context.SaveChangesAsync(Token);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    private static async Task<List<string>> KnownAsync(TestDatabase database)
    {
        await using var context = database.Context();
        return context.Database.GetMigrations().ToList();
    }

    /// <summary>The way down of the tools of EF Core, with the delay of the tests.</summary>
    private static async Task DownToAsync(TestDatabase database, string target)
    {
        await using var context = database.Engine.CreateMigrationContext();
        await context.GetService<IMigrator>().MigrateAsync(target, Token);
        Assert.Equal(target, (await context.Database.GetAppliedMigrationsAsync(Token)).Last());
    }

    private static async Task AssertBehindThenUpAsync(TestDatabase database, ProjectGraph graph, List<string> known, IReadOnlyList<string> pending)
    {
        var behind = await new SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token);
        Assert.Equal(SchemaStatus.Behind, behind.Status);
        Assert.Equal(pending, behind.Pending);

        var up = await new SchemaMigrations(database.Engine, new FixedClock()).MigrateAsync(Token);

        Assert.Equal(SchemaStatus.UpToDate, up.Status);
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token)).Status);
        await using var context = database.Context();
        var sources = new MigrationSources(database.Engine.MigrationsAssembly);
        var history = await context.Set<SchemaHistoryEntry>().AsNoTracking().OrderBy(entry => entry.MigrationId).ToListAsync(Token);
        Assert.Equal(known, history.Select(entry => entry.MigrationId));
        Assert.All(history, entry => Assert.Equal(sources.FingerprintOf(entry.MigrationId), entry.MigrationFingerprint));
        Assert.Equal([graph.Project.Id], await context.Set<Project>().Select(project => project.Id).ToListAsync(Token));
        Assert.Equal(graph.Of<Step>().Count, await context.Set<Step>().CountAsync(Token));
    }
}
