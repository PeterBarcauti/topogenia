// SqliteRebuildTests.cs
// The migrations that change the constraints make SQLite rebuild the tables: the rows, the keys and the indexes are kept, the guards become those of the model (spec 4.4).

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Purge;

namespace Topogenia.Persistence.Tests.Startup;

/// <summary>
/// SQLite only: SQL Server changes a constraint in place (`ALTER TABLE ... ADD CONSTRAINT`), SQLite cannot and rebuilds the
/// table (annex A 5, `db/005`, `006`, `008`). The migrations after the initial one (`TextGuardsAndIntegerBounds` and its
/// check) change the CHECK constraints of the tables; they run at the start of the desktop mode, after the backup (spec
/// 4.4). This test takes a file at the initial migration holding a complete project, with the record that the initial
/// migration leaves, then migrates it to the end by the code under test: every row is read back equal, each table
/// keeps its indexes (partial ones included) and its foreign keys, its CHECK constraints become exactly those of the
/// model, and the guards still hold after the rebuild. The tables created by a later migration (3.0: the settings of the
/// assistant) are there too, with the CHECK constraints of the model.
/// </summary>
public sealed class SqliteRebuildTests
{
    /// <summary>The tables that a migration after the initial one creates.</summary>
    private static readonly string[] CreatedAfterTheInitial = [TableNames.AssistantSettings];

    private const string InitialMigration = "20261023074737_Initial";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<string>> DescribeAsync(TestDatabase database)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        try
        {
            return await database.Engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>The index lines of the description (`index|name|table|sql`), in order.</summary>
    private static List<string> Indexes(IEnumerable<string> lines) =>
        [.. lines.Where(line => line.StartsWith("index|", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    /// <summary>The creation statement of each table, by its name.</summary>
    private static Dictionary<string, string> Tables(IEnumerable<string> lines) =>
        lines.Where(line => line.StartsWith("table|", StringComparison.Ordinal)).ToDictionary(line => line.Split('|')[1], StringComparer.Ordinal);

    /// <summary>The names of the constraints of a creation statement that start with <paramref name="prefix"/>, in order.</summary>
    private static List<string> Constraints(string table, string prefix) =>
        [.. Regex.Matches(table, "CONSTRAINT \"([A-Za-z0-9_]+)\"").Select(match => match.Groups[1].Value)
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    /// <summary>
    /// A file of the initial version holding a complete project: migrated by the code under test, written with the model of
    /// today, then taken down to the initial migration by the tools of EF Core, which leave the schema and the record of the
    /// initial migration (data M2) with every row. The project is written before the descent: the model of today names
    /// columns that came after the initial one (ADR 065), and it holds no value that the initial version could not hold.
    /// </summary>
    private static async Task<ProjectGraph> InitialWithProjectAsync(TestDatabase database)
    {
        await new SchemaMigrations(database.Engine, new FixedClock()).MigrateAsync(Token);
        var graph = ProjectGraph.BeforeGlossary();
        await using (var context = database.Context())
        {
            context.AddRange(graph.Records);
            await context.SaveChangesAsync(Token);
        }
        return graph;
    }

    private static async Task TakeDownToInitialAsync(TestDatabase database)
    {
        await using (var context = database.Engine.CreateMigrationContext())
            await context.GetService<IMigrator>().MigrateAsync(InitialMigration, Token);
        Assert.Equal([InitialMigration], (await new SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token)).Applied);
    }

    [Fact]
    public async Task The_migrations_after_the_initial_one_rebuild_the_tables_and_keep_every_row_index_and_foreign_key_with_the_guards_of_the_model()
    {
        await using var database = TestDatabase.Create(EngineKind.Sqlite);
        var graph = await InitialWithProjectAsync(database);
        var before = (await PurgeBench.LoadAsync(database, graph.Project.Id))!;
        await TakeDownToInitialAsync(database);
        var described = await DescribeAsync(database);

        var state = await new SchemaMigrations(database.Engine, new FixedClock()).MigrateAsync(Token);

        Assert.Equal(SchemaStatus.UpToDate, state.Status);
        WorkspaceRecords.AssertSame(before, (await PurgeBench.LoadAsync(database, graph.Project.Id))!);
        foreach (var record in graph.Records)
            Assert.True(await PurgeBench.ExistsAsync(database, record));
        var rebuilt = await DescribeAsync(database);
        Assert.Equal(Indexes(described), Indexes(rebuilt));
        var (old, tables) = (Tables(described), Tables(rebuilt));
        Assert.Equal(old.Keys.Concat(CreatedAfterTheInitial).Order(StringComparer.Ordinal), tables.Keys.Order(StringComparer.Ordinal));
        await using (var context = database.Context())
        {
            var model = context.GetService<IDesignTimeModel>().Model;
            foreach (var entity in model.GetEntityTypes().Where(entity => entity.GetTableName() is not null))
            {
                var name = entity.GetTableName()!;
                if (old.TryGetValue(name, out var initial))
                {
                    Assert.Equal(Constraints(initial, "FK_"), Constraints(tables[name], "FK_"));
                    Assert.Equal(Constraints(initial, "PK_"), Constraints(tables[name], "PK_"));
                }
                Assert.Equal(entity.GetCheckConstraints().Select(check => check.Name!).Order(StringComparer.Ordinal), Constraints(tables[name], "CK_"));
            }
        }
        // The table of the steps, the most referenced one, was rebuilt (its statement changed).
        Assert.NotEqual(old[TableNames.Step], tables[TableNames.Step]);
        var live = graph.Steps[0];
        await Refusals.AssertRefusedAsync(database, GuardKind.Unique, IndexNames.LiveStepOfCell,
            live with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('E', 99) });
        await Refusals.AssertRefusedAsync(database, GuardKind.ForeignKey, "FK_flow_source_step",
            graph.LiveFlow with { Id = Guid.CreateVersion7(), SourceStepId = Guid.CreateVersion7(), TargetStepId = graph.Steps[3].Id });
        await Refusals.AssertRefusedAsync(database, GuardKind.Check, "CK_step_column_index",
            live with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('E', 98), Column = -1 });
    }
}
