// SchemaRefusalTests.cs
// Refusal of a database newer than the application, of a changed migration, of a missing record and of a schema changed by hand (spec 4.4).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Schema;

/// <summary>
/// Each test migrates a new database, changes it as a newer application or a person would, then checks that both the
/// check and the migration refuse it with the expected code and change nothing (1.x `MiseAJourBase.Verifier`).
/// </summary>
public sealed class SchemaRefusalTests
{
    private const string FutureMigration = "29991231235959_Future";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SchemaMigrations Migrations(TestDatabase database) => new(database.Engine, new FixedClock());

    /// <summary>A new database migrated by the code under test, removed again if its preparation fails.</summary>
    private static Task<TestDatabase> MigratedAsync(EngineKind kind) => TestDatabase.MigratedAsync(kind);

    /// <summary>The check and the migration both refuse with the code, and the migration leaves the schema as it was.</summary>
    private static async Task AssertRefusedAsync(TestDatabase database, string code, string? migration)
    {
        var checkRefusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => Migrations(database).CheckAsync(Token));
        Assert.Equal(code, checkRefusal.Code);
        Assert.Equal(migration, checkRefusal.Migration);
        var before = await DescribeAsync(database);
        var migrateRefusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => Migrations(database).MigrateAsync(Token));
        Assert.Equal(code, migrateRefusal.Code);
        Assert.Equal(before, await DescribeAsync(database));
    }

    private static async Task<IReadOnlyList<string>> DescribeAsync(TestDatabase database)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        return (await database.Engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token)).Order(StringComparer.Ordinal).ToList();
    }

    private static async Task<string> LastMigrationAsync(TestDatabase database)
    {
        await using var context = database.Context();
        return context.Database.GetMigrations().Last();
    }

    private static async Task ChangeRecordAsync(TestDatabase database, Func<SchemaHistoryEntry, SchemaHistoryEntry?> change)
    {
        await using var context = database.Context();
        var entry = await context.Set<SchemaHistoryEntry>().AsNoTracking().OrderBy(item => item.MigrationId).LastAsync(Token);
        if (change(entry) is { } changed)
            context.Update(changed);
        else
            context.Remove(entry);
        await context.SaveChangesAsync(Token);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_database_whose_history_holds_a_migration_unknown_to_the_application_is_refused_as_newer(EngineKind kind)
    {
        await using var database = await MigratedAsync(kind);
        await using (var context = database.Context())
        {
            context.Add(new SchemaHistoryEntry(FutureMigration, new string('0', Fingerprints.Length), null, "9.0.0", FixedClock.Time));
            await context.SaveChangesAsync(Token);
        }
        await AssertRefusedAsync(database, SchemaCodes.NewerDatabase, FutureMigration);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_database_migrated_by_a_newer_application_is_refused_as_newer(EngineKind kind)
    {
        await using var database = await MigratedAsync(kind);
        await using (var context = database.Context())
            await OutOfBand.RecordInEfHistoryAsync(context, FutureMigration);
        await AssertRefusedAsync(database, SchemaCodes.NewerDatabase, FutureMigration);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_migration_changed_since_it_was_applied_is_refused(EngineKind kind)
    {
        await using var database = await MigratedAsync(kind);
        await ChangeRecordAsync(database, entry => entry with { MigrationFingerprint = new string('f', Fingerprints.Length) });
        await AssertRefusedAsync(database, SchemaCodes.MigrationChanged, await LastMigrationAsync(database));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_migration_applied_without_its_record_is_refused(EngineKind kind)
    {
        await using var database = await MigratedAsync(kind);
        await ChangeRecordAsync(database, _ => null);
        await AssertRefusedAsync(database, SchemaCodes.FingerprintMissing, await LastMigrationAsync(database));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_last_migration_recorded_without_the_fingerprint_of_its_schema_is_refused(EngineKind kind)
    {
        await using var database = await MigratedAsync(kind);
        await ChangeRecordAsync(database, entry => entry with { SchemaFingerprint = null });
        await AssertRefusedAsync(database, SchemaCodes.FingerprintMissing, await LastMigrationAsync(database));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_column_added_outside_the_migrations_is_refused_as_a_changed_schema(EngineKind kind)
    {
        await using var database = await MigratedAsync(kind);
        await using (var context = database.Context())
            await OutOfBand.AddColumnAsync(context, TableNames.Step, "added_by_hand");
        await AssertRefusedAsync(database, SchemaCodes.SchemaChanged, await LastMigrationAsync(database));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_index_dropped_outside_the_migrations_is_refused_as_a_changed_schema(EngineKind kind)
    {
        await using var database = await MigratedAsync(kind);
        await using (var context = database.Context())
            await OutOfBand.DropIndexAsync(context, TableNames.Step, IndexNames.LiveStepOfCell);
        await AssertRefusedAsync(database, SchemaCodes.SchemaChanged, await LastMigrationAsync(database));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_migration_removed_from_the_history_of_ef_core_is_refused_as_a_changed_schema(EngineKind kind)
    {
        await using var database = await MigratedAsync(kind);
        var last = await LastMigrationAsync(database);
        await using (var context = database.Context())
            await OutOfBand.RemoveFromEfHistoryAsync(context, last);
        await AssertRefusedAsync(database, SchemaCodes.SchemaChanged, last);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_database_left_as_the_migrations_made_it_is_accepted(EngineKind kind)
    {
        await using var database = await MigratedAsync(kind);
        await using (var context = database.Context())
        {
            // Rows change the data, never the schema: the fingerprint of the schema does not move.
            context.AddRange(ProjectGraph.New().Records);
            await context.SaveChangesAsync(Token);
        }
        var state = await Migrations(database).CheckAsync(Token);
        Assert.Equal(SchemaStatus.UpToDate, state.Status);
    }
}
