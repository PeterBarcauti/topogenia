// SchemaMigrationTests.cs
// Creation of the schema by the migrations of each provider and its history with fingerprints (spec 4.1, 4.4, A-4-33 to A-4-37).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Schema;

public sealed class SchemaMigrationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SchemaMigrations Migrations(TestDatabase database) => new(database.Engine, new FixedClock());

    private static async Task<IReadOnlyList<string>> DescribeAsync(TestDatabase database)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        return await database.Engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_new_database_is_behind_on_sqlite_and_refused_as_unreachable_on_sql_server_and_the_check_never_creates_it(EngineKind kind)
    {
        await using var database = TestDatabase.Create(kind);
        await using var context = database.Context();
        if (kind == EngineKind.SqlServer)
        {
            // EF Core answers 4060 for an absent database and for one the account may not open alike (ADR 036, round 2).
            var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => Migrations(database).CheckAsync(Token));
            Assert.Equal(SchemaCodes.DatabaseUnreachable, refusal.Code);
        }
        else
        {
            var state = await Migrations(database).CheckAsync(Token);
            Assert.Equal(SchemaStatus.Behind, state.Status);
            Assert.Empty(state.Applied);
            Assert.Equal(context.Database.GetMigrations(), state.Pending);
        }
        Assert.False(await context.GetService<IRelationalDatabaseCreator>().ExistsAsync(Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Migrating_creates_the_database_and_records_each_migration_with_its_fingerprints(EngineKind kind)
    {
        await using var database = TestDatabase.Create(kind);
        var state = await database.CreatingAsync(() => Migrations(database).MigrateAsync(Token));
        await using var context = database.Context();
        var known = context.Database.GetMigrations().ToList();
        Assert.Equal(SchemaStatus.UpToDate, state.Status);
        Assert.Equal(known, state.Applied);
        Assert.Empty(state.Pending);
        var history = await context.Set<SchemaHistoryEntry>().AsNoTracking().OrderBy(entry => entry.MigrationId).ToListAsync(Token);
        var sources = new MigrationSources(database.Engine.MigrationsAssembly);
        Assert.Equal(known, history.Select(entry => entry.MigrationId));
        Assert.All(history, entry => Assert.Equal(sources.FingerprintOf(entry.MigrationId), entry.MigrationFingerprint));
        Assert.All(history, entry => Assert.Equal(SchemaMigrations.ApplicationVersion, entry.ApplicationVersion));
        Assert.All(history, entry => Assert.Equal(FixedClock.Time, entry.AppliedAt));
        Assert.Equal(Fingerprints.OfLines(await DescribeAsync(database)), history[^1].SchemaFingerprint);
        Assert.Equal("3.0.0", SchemaMigrations.ApplicationVersion);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Migrating_an_up_to_date_database_changes_nothing(EngineKind kind)
    {
        await using var database = TestDatabase.Create(kind);
        var first = await database.CreatingAsync(() => Migrations(database).MigrateAsync(Token));
        var schema = await DescribeAsync(database);
        var second = await new SchemaMigrations(database.Engine, new LaterClock()).MigrateAsync(Token);
        Assert.Equal(first.Applied, second.Applied);
        Assert.Equal(SchemaStatus.UpToDate, second.Status);
        Assert.Equal(schema.Order(StringComparer.Ordinal), (await DescribeAsync(database)).Order(StringComparer.Ordinal));
        await using var context = database.Context();
        Assert.All(await context.Set<SchemaHistoryEntry>().ToListAsync(Token), entry => Assert.Equal(FixedClock.Time, entry.AppliedAt));
    }

    /// <summary>
    /// Spec 4.1: the tables live in the schema `topogenia`, a fixed technical name that the migrations froze, as the
    /// namespaces are; renaming the displayed product changes neither the schema nor the roles (ADR 013).
    /// </summary>
    [Fact]
    public async Task On_sql_server_the_tables_live_in_the_fixed_technical_schema()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.SqlServer);
        await using var context = database.Context();
        var schema = TechnicalNames.Schema;
        Assert.Equal("topogenia", schema);
        Assert.Equal(schema, context.Model.GetDefaultSchema());
        var tables = (await DescribeAsync(database)).Where(line => line.StartsWith("T|", StringComparison.Ordinal)).ToList();
        Assert.Equal(context.Model.GetEntityTypes().Select(entity => $"T|{schema}.{entity.GetTableName()}").Order(StringComparer.Ordinal),
            tables.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task On_sqlite_the_tables_have_no_schema_prefix()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.Sqlite);
        await using var context = database.Context();
        Assert.Null(context.Model.GetDefaultSchema());
        var tables = (await DescribeAsync(database)).Where(line => line.StartsWith("table|", StringComparison.Ordinal))
            .Select(line => line.Split('|')[1]).ToList();
        Assert.Equal(context.Model.GetEntityTypes().Select(entity => entity.GetTableName()!).Order(StringComparer.Ordinal), tables.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_migrations_of_each_provider_match_the_model_and_embed_their_source(EngineKind kind)
    {
        await using var database = TestDatabase.Create(kind);
        await using var context = database.Context();
        Assert.False(context.Database.HasPendingModelChanges());
        var known = context.Database.GetMigrations().ToList();
        Assert.NotEmpty(known);
        Assert.Equal(known, new MigrationSources(database.Engine.MigrationsAssembly).Embedded);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_guard_of_the_model_exists_in_the_migrated_database(EngineKind kind)
    {
        await using var database = await TestDatabase.MigratedAsync(kind);
        var schema = string.Join('\n', await DescribeAsync(database));
        Assert.All(ModelGuards.Checks, guard => Assert.Contains(guard.Name, schema, StringComparison.Ordinal));
        var lines = (await DescribeAsync(database)).ToList();
        foreach (var guard in ModelGuards.PartialIndexes)
        {
            if (kind == EngineKind.SqlServer)
            {
                // I|schema.table|index|type|unique|primary key|unique constraint|filter
                var index = Assert.Single(lines, line => line.StartsWith("I|", StringComparison.Ordinal) && line.Split('|')[2] == guard.IndexName).Split('|');
                Assert.Equal("True", index[4]);
                Assert.NotEqual("", index[7]);
            }
            else
            {
                var index = Assert.Single(lines, line => line.StartsWith($"index|{guard.IndexName}|", StringComparison.Ordinal));
                Assert.Contains(" UNIQUE ", index, StringComparison.Ordinal);
                Assert.Contains(" WHERE ", index, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_processes_that_migrate_at_once_apply_each_migration_once(EngineKind kind)
    {
        // Two processes that both find no database would both try to create it, which EF Core does not lock (the creation
        // of a base on a server is the work of `--migrate` or of the administrator): the race is on the migrations only.
        await using var database = kind == EngineKind.SqlServer ? await TestDatabase.CreatedAsync(kind) : TestDatabase.Create(kind);
        var states = await Task.WhenAll(Migrations(database).MigrateAsync(Token), Migrations(database).MigrateAsync(Token));
        Assert.All(states, state => Assert.Equal(SchemaStatus.UpToDate, state.Status));
        await using var context = database.Context();
        Assert.Equal(context.Database.GetMigrations(), (await context.Set<SchemaHistoryEntry>().ToListAsync(Token)).Select(entry => entry.MigrationId).Order(StringComparer.Ordinal));
        Assert.Equal(SchemaStatus.UpToDate, (await Migrations(database).CheckAsync(Token)).Status);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_migration_that_fails_leaves_the_database_as_it_was_and_records_nothing(EngineKind kind)
    {
        await using var database = await TestDatabase.CreatedAsync(kind);
        await using (var context = database.Context())
        {
            // A table of the migration already there: the migration fails half-way, after creating other tables.
            var schema = context.Model.GetDefaultSchema();
            await OutOfBand.ApplyAsync(context, schema is null ? [] : [new EnsureSchemaOperation { Name = schema }]);
            await OutOfBand.ApplyAsync(context, new CreateTableOperation
            {
                Schema = schema,
                Name = TableNames.Step,
                Columns = { new AddColumnOperation { Schema = schema, Table = TableNames.Step, Name = "id", ClrType = typeof(Guid) } },
            });
        }
        var before = await DescribeAsync(database);
        // The engine refuses the statement inside the migration: not a refusal of the schema check before it.
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => Migrations(database).MigrateAsync(Token));
        Assert.Equal(before.Order(StringComparer.Ordinal), (await DescribeAsync(database)).Order(StringComparer.Ordinal));
        var state = await Migrations(database).CheckAsync(Token);
        Assert.Empty(state.Applied);
        await using var reader = database.Context();
        await reader.Database.OpenConnectionAsync(Token);
        Assert.Empty(await database.Engine.Catalog.HistoryAsync(reader.Database.GetDbConnection(), null, Token));
    }

    private sealed class LaterClock : IClock
    {
        public DateTime UtcNow => FixedClock.Time.AddDays(1);
    }
}
