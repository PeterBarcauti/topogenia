// SnapshotIsolationTests.cs
// The SQL Server migrations of the snapshot reads: options set outside the transaction, described by the fingerprint, histories right after a failure (ADR 031).

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Persistence.SqlServer.SqlServerOptionsProbe;

namespace Topogenia.Persistence.Tests.Schema;

/// <summary>
/// `SnapshotIsolation` sets `ALLOW_SNAPSHOT_ISOLATION` and `READ_COMMITTED_SNAPSHOT` outside any transaction (1.x
/// `db/003`); EF Core records it outside any transaction too, so `SnapshotIsolationCheck` checks the options in a
/// transaction whose commit records both migrations in `schema_history` (ADR 027). The options are described from the
/// second of the two on: a database of the previous version keeps its fingerprint, a database whose options were removed
/// by hand is refused at the start, and a run stopped anywhere leaves a database that the next run completes. A failure
/// is provoked by a guard added by hand to the history of EF Core, which the description of the schema leaves out.
/// </summary>
public sealed class SnapshotIsolationTests
{
    private const string RolesMigration = "20261023083307_RolesAndReportViews";

    private const string CheckMigration = "20261023093315_SnapshotIsolationCheck";


    /// <summary>SQL Server: a snapshot transaction in a database that does not allow it.</summary>
    private const int SnapshotNotAllowed = 3952;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SchemaMigrations Migrations(TestDatabase database) => new(database.Engine, new FixedClock());

    /// <summary>The migrations of SQL Server known to the application, in their order.</summary>
    private static List<string> Known()
    {
        using var context = TestDatabase.Create(EngineKind.SqlServer).Context();
        return [.. context.Database.GetMigrations()];
    }

    /// <summary>The known migrations after <paramref name="migration"/>: those that a database at it has pending.</summary>
    private static List<string> After(string migration)
    {
        var known = Known();
        return known[(known.IndexOf(migration) + 1)..];
    }

    /// <summary>The last known migration: the one that a refusal of a database migrated to the end names.</summary>
    private static string Last => Known()[^1];

    /// <summary>A new database migrated by the code under test, removed again if its preparation fails.</summary>
    private static Task<TestDatabase> MigratedAsync() => TestDatabase.MigratedAsync(EngineKind.SqlServer);

    /// <summary>A database at the roles and report views, recorded as the previous version of the application did (one transaction).</summary>
    private static Task<TestDatabase> AtRolesAsync() => TestDatabase.PreparedAsync(EngineKind.SqlServer, ToRolesAsync);

    private static async Task ToRolesAsync(TestDatabase database)
    {
        await using var context = database.Engine.CreateMigrationContext();
        await context.GetService<IMigrator>().MigrateAsync(RolesMigration, Token);
        var sources = new MigrationSources(database.Engine.MigrationsAssembly);
        var applied = (await context.Database.GetAppliedMigrationsAsync(Token)).ToList();
        var schema = Fingerprints.OfLines(await DescribeAsync(database));
        context.AddRange(applied.Select(id => new SchemaHistoryEntry(id, sources.FingerprintOf(id), id == RolesMigration ? schema : null,
            SchemaMigrations.ApplicationVersion, FixedClock.Time)));
        await context.SaveChangesAsync(Token);
    }

    private static async Task<List<string>> DescribeAsync(TestDatabase database)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        return (await database.Engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token)).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Runs a change on an open connection of the owner of the database, as an administrator would.</summary>
    private static async Task<T> AsOwnerAsync<T>(TestDatabase database, Func<System.Data.Common.DbConnection, Task<T>> change)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        return await change(context.Database.GetDbConnection());
    }

    private static Task<bool> AsOwnerAsync(TestDatabase database, Func<System.Data.Common.DbConnection, Task> change) =>
        AsOwnerAsync(database, async connection =>
        {
            await change(connection);
            return true;
        });

    /// <summary>
    /// Removes the snapshot isolation by hand, as an administrator could, on a context with the delay of the migrations
    /// (ADR 041). The change waits until every transaction that writes in a versioned database of the instance, when it
    /// starts, has ended: those of the other tests and of the other runs (ADR 040). SQL Server refuses `WITH ROLLBACK
    /// IMMEDIATE` on a change of the versioning (5083), and the sessions of this base are not the ones it waits for.
    /// </summary>
    private static async Task RemoveSnapshotIsolationByHandAsync(DatabaseEngine engine)
    {
        await using var context = engine.CreateMigrationContext();
        await context.Database.OpenConnectionAsync(Token);
        await SqlServerOptionsProbe.SetSnapshotIsolationAsync(context.Database.GetDbConnection(), false, Token);
    }

    private static Task<DatabaseOptions> OptionsAsync(TestDatabase database) =>
        AsOwnerAsync(database, connection => SqlServerOptionsProbe.OptionsAsync(connection, Token));

    private static async Task<(List<string> Applied, List<string> Recorded)> HistoriesAsync(TestDatabase database)
    {
        await using var context = database.Context();
        var applied = (await context.Database.GetAppliedMigrationsAsync(Token)).ToList();
        var recorded = await context.Set<SchemaHistoryEntry>().AsNoTracking().OrderBy(entry => entry.MigrationId).Select(entry => entry.MigrationId).ToListAsync(Token);
        return (applied, recorded);
    }

    private static async Task AssertRefusedAsync(TestDatabase database, string code, string migration)
    {
        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => Migrations(database).CheckAsync(Token));
        Assert.Equal((code, migration), (refusal.Code, refusal.Migration));
    }

    /// <summary>The database is up to date, recorded in full, with both options set and described.</summary>
    private static async Task AssertMigratedAsync(TestDatabase database)
    {
        Assert.Equal(SchemaStatus.UpToDate, (await Migrations(database).CheckAsync(Token)).Status);
        Assert.Equal(new DatabaseOptions("ON", true), await OptionsAsync(database));
        var (applied, recorded) = await HistoriesAsync(database);
        await using var context = database.Context();
        Assert.Equal(context.Database.GetMigrations(), applied);
        Assert.Equal(applied, recorded);
    }

    [Fact]
    public async Task Migrating_sets_the_snapshot_isolation_and_the_row_versions_and_the_fingerprint_describes_them()
    {
        await using var database = await MigratedAsync();
        await AssertMigratedAsync(database);
        var lines = await DescribeAsync(database);
        Assert.Equal("O|ON|True", Assert.Single(lines, line => line.StartsWith("O|", StringComparison.Ordinal)));
        await using var context = database.Context();
        var history = await context.Set<SchemaHistoryEntry>().AsNoTracking().OrderBy(entry => entry.MigrationId).ToListAsync(Token);
        // EF Core records SnapshotIsolation outside any transaction: the transaction of the check records it, the
        // fingerprint of the schema on the check.
        var options = history.FindIndex(entry => entry.MigrationId == SqlServerSchemaCatalog.OptionsMigration);
        Assert.Null(history[options].SchemaFingerprint);
        Assert.Equal(CheckMigration, history[options + 1].MigrationId);
        Assert.NotNull(history[options + 1].SchemaFingerprint);
        Assert.Equal((Last, Fingerprints.OfLines(lines)), (history[^1].MigrationId, history[^1].SchemaFingerprint));
    }

    [Fact]
    public async Task A_database_whose_snapshot_isolation_was_removed_by_hand_is_refused_rather_than_failing_at_each_read()
    {
        await using var database = await MigratedAsync();
        var scenario = await StoreScenario.CreateAsync(database);
        await RemoveSnapshotIsolationByHandAsync(database.Engine);

        await AssertRefusedAsync(database, SchemaCodes.SchemaChanged, Last);
        // What the refusal at the start avoids: every loading would fail.
        await using var session = database.Session();
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => new WorkspaceStore(session).LoadAsync(scenario.ProjectId, WorkspaceContent.All, Token));
        Assert.Equal(SnapshotNotAllowed, Assert.IsType<SqlException>(Innermost(failure)).Number);
    }

    [Fact]
    public async Task Removing_the_snapshot_isolation_by_hand_waits_for_the_writes_in_progress_in_another_versioned_database()
    {
        await using var database = await MigratedAsync();
        await using var other = await MigratedAsync();
        var scenario = await StoreScenario.CreateAsync(other);
        // An engine whose ordinary statements wait 1 s, and its migrations the default delay.
        var engine = database.EngineWith(DatabaseEngine.DefaultMigrationTimeout, 1);
        await using var blocker = other.Context();
        await using (var held = await blocker.Database.BeginTransactionAsync(Token))
        {
            await blocker.Set<Project>().Where(project => project.Id == scenario.ProjectId)
                .ExecuteUpdateAsync(row => row.SetProperty(project => project.Name, project => project.Name), Token);

            await using (var ordinary = engine.CreateContext())
            {
                await ordinary.Database.OpenConnectionAsync(Token);
                var waited = await Assert.ThrowsAsync<SqlException>(() =>
                    SqlServerOptionsProbe.SetSnapshotIsolationAsync(ordinary.Database.GetDbConnection(), false, Token));
                Assert.Equal(-2, waited.Number);
            }
            var removal = RemoveSnapshotIsolationByHandAsync(engine);
            await Task.Delay(TimeSpan.FromSeconds(2), Token);
            Assert.False(removal.IsCompleted, $"The removal did not wait for the write of the other base: {removal.Exception}");
            await held.CommitAsync(Token);
            await removal.WaitAsync(DatabaseEngine.DefaultMigrationTimeout, Token);
        }
        Assert.Equal(new DatabaseOptions("OFF", true), await OptionsAsync(database));
    }

    [Fact]
    public async Task A_database_whose_row_versions_were_removed_by_hand_is_refused_as_a_changed_schema()
    {
        await using var database = await MigratedAsync();
        await AsOwnerAsync(database, connection => SqlServerOptionsProbe.SetReadCommittedSnapshotAsync(connection, false, Token));
        await AssertRefusedAsync(database, SchemaCodes.SchemaChanged, Last);
    }

    [Fact]
    public async Task A_database_of_the_previous_version_keeps_its_fingerprint_whatever_its_options_and_migrates_to_the_snapshot_reads()
    {
        await using var database = await AtRolesAsync();
        var before = await DescribeAsync(database);
        await AsOwnerAsync(database, connection => SqlServerOptionsProbe.SetSnapshotIsolationAsync(connection, true, Token));

        Assert.Equal(before, await DescribeAsync(database));
        Assert.DoesNotContain(before, line => line.StartsWith("O|", StringComparison.Ordinal));
        var state = await Migrations(database).CheckAsync(Token);
        Assert.Equal(SchemaStatus.Behind, state.Status);
        Assert.Equal(After(RolesMigration), state.Pending);
        Assert.Equal([SqlServerSchemaCatalog.OptionsMigration, CheckMigration], state.Pending.Take(2));
        await Migrations(database).MigrateAsync(Token);
        await AssertMigratedAsync(database);
    }

    [Fact]
    public async Task A_migration_that_fails_after_setting_the_options_records_nothing_leaves_the_database_behind_and_runs_again()
    {
        await using var database = await AtRolesAsync();
        // The record of SnapshotIsolation by EF Core, after its two statements, fails.
        await AsOwnerAsync(database, connection =>
            SqlServerOptionsProbe.RefuseRecordAsync(connection, TableNames.MigrationsHistory, SqlServerSchemaCatalog.OptionsMigration, Token));

        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => Migrations(database).MigrateAsync(Token));

        // The options are set (no transaction undoes them); neither history lists the migration.
        Assert.Equal(new DatabaseOptions("ON", true), await OptionsAsync(database));
        var (applied, recorded) = await HistoriesAsync(database);
        Assert.Equal(applied, recorded);
        Assert.Equal(RolesMigration, applied[^1]);
        var state = await Migrations(database).CheckAsync(Token);
        Assert.Equal(SchemaStatus.Behind, state.Status);
        Assert.Equal(After(RolesMigration), state.Pending);
        Assert.Equal(SqlServerSchemaCatalog.OptionsMigration, state.Pending[0]);

        await AsOwnerAsync(database, connection => SqlServerOptionsProbe.AcceptRecordsAsync(connection, TableNames.MigrationsHistory, Token));
        await Migrations(database).MigrateAsync(Token);
        await AssertMigratedAsync(database);
    }

    /// <summary>
    /// The state left by a run stopped once EF Core has recorded SnapshotIsolation outside any transaction, before the
    /// check commits: the record of the check by EF Core fails, the check is undone with it, and schema_history, written
    /// at the commit of that transaction, records nothing.
    /// </summary>
    private static Task<TestDatabase> StoppedAfterTheOptionsAsync() => TestDatabase.PreparedAsync(EngineKind.SqlServer, async database =>
    {
        await ToRolesAsync(database);
        await AsOwnerAsync(database, connection =>
            SqlServerOptionsProbe.RefuseRecordAsync(connection, TableNames.MigrationsHistory, CheckMigration, Token));
        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => Migrations(database).MigrateAsync(Token));
        var (applied, recorded) = await HistoriesAsync(database);
        Assert.Equal(SqlServerSchemaCatalog.OptionsMigration, applied[^1]);
        Assert.Equal(RolesMigration, recorded[^1]);
        // The failure ends: the guard goes.
        await AsOwnerAsync(database, connection => SqlServerOptionsProbe.AcceptRecordsAsync(connection, TableNames.MigrationsHistory, Token));
    });

    [Fact]
    public async Task A_run_stopped_once_ef_core_has_recorded_the_options_leaves_the_database_behind_and_the_next_run_records_everything()
    {
        await using var database = await StoppedAfterTheOptionsAsync();

        // No false record: SnapshotIsolation awaits its record, which the commit of the check writes.
        var state = await Migrations(database).CheckAsync(Token);
        Assert.Equal(SchemaStatus.Behind, state.Status);
        Assert.Equal(After(SqlServerSchemaCatalog.OptionsMigration), state.Pending);
        Assert.Equal(CheckMigration, state.Pending[0]);

        await Migrations(database).MigrateAsync(Token);
        await AssertMigratedAsync(database);
        await using var context = database.Context();
        var history = await context.Set<SchemaHistoryEntry>().AsNoTracking().OrderBy(entry => entry.MigrationId).ToListAsync(Token);
        // The first transaction of the next run (EF Core creates its history when missing) records SnapshotIsolation,
        // with the schema read then: the one recorded before the options, which they do not change until the check.
        var options = history.FindIndex(entry => entry.MigrationId == SqlServerSchemaCatalog.OptionsMigration);
        Assert.Equal(RolesMigration, history[options - 1].MigrationId);
        Assert.Equal(history[options - 1].SchemaFingerprint, history[options].SchemaFingerprint);
        Assert.Equal(Fingerprints.OfLines(await DescribeAsync(database)), history[^1].SchemaFingerprint);
    }

    [Fact]
    public async Task A_run_stopped_once_ef_core_has_recorded_the_options_is_still_refused_when_the_schema_was_changed_by_hand()
    {
        await using var database = await StoppedAfterTheOptionsAsync();
        await using (var context = database.Context())
            await OutOfBand.AddColumnAsync(context, TableNames.Step, "added_by_hand");

        // The schema is checked against the record of the migration before the options.
        await AssertRefusedAsync(database, SchemaCodes.SchemaChanged, RolesMigration);
        await Assert.ThrowsAsync<SchemaRefusedException>(() => Migrations(database).MigrateAsync(Token));
    }

    [Fact]
    public async Task The_options_migration_without_its_record_before_a_recorded_migration_is_refused_as_a_missing_fingerprint()
    {
        await using var database = await MigratedAsync();
        await using (var context = database.Context())
        {
            context.Remove(await context.Set<SchemaHistoryEntry>().SingleAsync(entry => entry.MigrationId == SqlServerSchemaCatalog.OptionsMigration, Token));
            await context.SaveChangesAsync(Token);
        }
        // Only the last applied migration may await its record: here the check is applied after it.
        await AssertRefusedAsync(database, SchemaCodes.FingerprintMissing, SqlServerSchemaCatalog.OptionsMigration);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_migration_recorded_outside_a_transaction_is_followed_by_one_in_a_transaction(EngineKind kind)
    {
        await using var database = TestDatabase.Create(kind);
        await using var context = database.Context();
        var outside = MigrationTransactions.OutsideTransaction(context);
        var known = context.Database.GetMigrations().ToList();
        // SQLite rebuilds its tables in the transaction of the migration (data B2): none of its migrations runs outside one.
        string[] expected = kind == EngineKind.SqlServer ? [SqlServerSchemaCatalog.OptionsMigration] : [];
        Assert.Equal(expected, outside);
        Assert.All(outside, id => Assert.DoesNotContain(known[known.IndexOf(id) + 1], outside));
        Assert.DoesNotContain(known[^1], outside);
    }

    [Fact]
    public async Task Reverting_the_two_migrations_removes_both_options_and_gives_back_the_previous_schema()
    {
        await using var previous = await AtRolesAsync();
        await using var reverted = await MigratedAsync();
        await using (var context = reverted.Engine.CreateMigrationContext())
            await context.GetService<IMigrator>().MigrateAsync(RolesMigration, Token);
        Assert.Equal(new DatabaseOptions("OFF", false), await OptionsAsync(reverted));
        Assert.Equal(await DescribeAsync(previous), await DescribeAsync(reverted));
    }

    private static Exception Innermost(Exception exception)
    {
        var current = exception;
        while (current is not SqlException && current.InnerException is { } inner)
            current = inner;
        return current;
    }
}
