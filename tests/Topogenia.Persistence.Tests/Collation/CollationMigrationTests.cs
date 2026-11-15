// CollationMigrationTests.cs
// The SQL Server migration to binary text columns applies to a database in use (rows, statistics) and reverts to the previous schema.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Collation;

/// <summary>
/// A database of the previous version holds rows and the statistics that SQL Server creates on the text columns that its
/// queries filter. The change of collation of a column fails while a key, an index, a CHECK or a statistics object of an
/// administrator depends on it (error 5074): `BinaryTextCollation` drops them and creates them again. The migrated
/// database has the schema of a new one and all its rows, and reverting the migration gives back the previous schema
/// (ADR 038). SQL Server only: SQLite has no such migration, nothing changes there.
/// </summary>
public sealed class CollationMigrationTests
{
    private const string PreviousMigration = "20261023102021_ReportMapDepthBound";

    private const string CollationMigration = "20261023113221_BinaryTextCollation";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<List<string>> DescribeAsync(TestDatabase database)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        return (await database.Engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token)).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Migrates to <paramref name="target"/> on a context of the migrations, with their delay (ADR 041).</summary>
    private static async Task MigrateAsync(TestDatabase database, string? target)
    {
        await using var context = database.Engine.CreateMigrationContext();
        await context.GetService<IMigrator>().MigrateAsync(target, Token);
    }

    /// <summary>Queries that filter text columns without an index: SQL Server creates its statistics on them as it compiles them.</summary>
    private static async Task UseAsync(TestDatabase database, ProjectGraph graph)
    {
        await using var context = database.Context();
        Assert.Equal(1, await context.Set<Step>().CountAsync(step => step.Label == graph.Steps[0].Label && step.SourceLanguage == "fr" || step.Id == graph.Steps[0].Id, Token));
        Assert.Equal(1, await context.Set<AppUser>().CountAsync(user => user.Email == graph.Owner.Email && user.DisplayName == graph.Owner.DisplayName, Token));
        Assert.NotEqual(0, await context.Set<Lane>().CountAsync(lane => lane.Name != "" && lane.Color != "", Token));
        Assert.NotEqual(0, await context.Set<StepRaci>().CountAsync(raci => raci.Role == RaciRole.A, Token));
        Assert.NotEqual(0, await context.Set<Translation>().CountAsync(translation => translation.Language != "" && translation.Text != "", Token));
    }

    [Fact]
    public async Task A_database_in_use_migrates_to_binary_text_columns_and_reverts_to_its_previous_schema()
    {
        // Each base is created by the bounded and retried path of the tests (ADR 040), then migrated as its preparation.
        await using var fresh = await TestDatabase.PreparedAsync(EngineKind.SqlServer, created => MigrateAsync(created, null));
        await using var database = await TestDatabase.PreparedAsync(EngineKind.SqlServer, created => MigrateAsync(created, PreviousMigration));
        var previous = await DescribeAsync(database);
        // The rows of the previous version: written with the model of today on the last schema, which names columns that came
        // after the previous migration (ADR 065), then the database taken down to it with them.
        var graph = ProjectGraph.BeforeGlossary();
        await MigrateAsync(database, null);
        await Refusals.SaveAsync(database, [.. graph.Records]);
        await MigrateAsync(database, PreviousMigration);
        Assert.Equal(previous, await DescribeAsync(database));
        await UseAsync(database, graph);

        await MigrateAsync(database, null);

        Assert.Equal(await DescribeAsync(fresh), await DescribeAsync(database));
        await using (var context = database.Context())
        {
            var lower = graph.Owner.LoginNormalized.ToLowerInvariant();
            Assert.False(await context.Set<AppUser>().AnyAsync(user => user.LoginNormalized == lower, Token));
            Assert.Equal(graph.Records.Count, await CountAsync(context));
        }
        await UseAsync(database, graph);
        await MigrateAsync(database, PreviousMigration);
        Assert.Equal(previous, await DescribeAsync(database));
        await MigrateAsync(database, CollationMigration);
    }

    /// <summary>The rows of the graph, read back after the migration: none is lost.</summary>
    private static async Task<int> CountAsync(AppDbContext context)
    {
        var count = 0;
        foreach (var type in context.Model.GetEntityTypes().Select(entity => entity.ClrType).Where(type => type != typeof(Persistence.Schema.SchemaHistoryEntry)))
        {
            var set = (IQueryable<object>)typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(type).Invoke(context, null)!;
            count += await set.CountAsync(Token);
        }
        return count;
    }
}
