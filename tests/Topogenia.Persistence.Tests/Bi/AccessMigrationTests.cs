// AccessMigrationTests.cs
// The SQL Server migration of the roles and report views: recorded, replayed without effect, applied to an initial base, refused when changed.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Bi;

/// <summary>
/// The second migration of SQL Server creates the two roles, the report schema and its views (1.x `db/004`, spec 4.4).
/// Its objects and the rights of the roles are part of the fingerprint of the schema (ADR 032): a right changed by hand
/// is refused like a column changed by hand, while the users that an administrator adds to the roles change nothing.
/// </summary>
public sealed class AccessMigrationTests
{
    private const string InitialMigration = "20261023074436_Initial";

    private const string RolesMigration = "20261023083307_RolesAndReportViews";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SchemaMigrations Migrations(TestDatabase database) => new(database.Engine, new FixedClock());

    /// <summary>A new database migrated by the code under test, removed again if its preparation fails.</summary>
    private static Task<TestDatabase> MigratedAsync() => TestDatabase.MigratedAsync(EngineKind.SqlServer);

    private static async Task<List<string>> DescribeAsync(TestDatabase database)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        return (await database.Engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token)).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Runs a change on an open connection of the owner of the database, as an administrator would.</summary>
    private static async Task AsOwnerAsync(TestDatabase database, Func<System.Data.Common.DbConnection, Task> change)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        await change(context.Database.GetDbConnection());
    }

    /// <summary>A database migrated to the initial migration only, recorded as the previous version of the application did.</summary>
    private static Task<TestDatabase> InitialAsync() => TestDatabase.PreparedAsync(EngineKind.SqlServer, async database =>
    {
        await using var context = database.Engine.CreateMigrationContext();
        await context.GetService<IMigrator>().MigrateAsync(InitialMigration, Token);
        var sources = new MigrationSources(database.Engine.MigrationsAssembly);
        context.Add(new SchemaHistoryEntry(InitialMigration, sources.FingerprintOf(InitialMigration), Fingerprints.OfLines(await DescribeAsync(database)),
            SchemaMigrations.ApplicationVersion, FixedClock.Time));
        await context.SaveChangesAsync(Token);
    });

    private static async Task AssertChangedSchemaAsync(TestDatabase database)
    {
        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => Migrations(database).CheckAsync(Token));
        Assert.Equal(SchemaCodes.SchemaChanged, refusal.Code);
        await using var context = database.Context();
        Assert.Equal(context.Database.GetMigrations().Last(), refusal.Migration);
    }

    [Fact]
    public async Task The_migration_creates_two_roles_the_report_schema_and_eleven_views_all_described_by_the_fingerprint()
    {
        await using var database = await MigratedAsync();
        var lines = await DescribeAsync(database);
        Assert.Equal(
            new[] { $"R|{SqlServerAccess.ApplicationRole}|dbo", $"R|{SqlServerAccess.ReportRole}|dbo" },
            lines.Where(line => line.StartsWith("R|", StringComparison.Ordinal)));
        Assert.Equal(
            new[] { $"S|{SqlServerAccess.ReportSchema}|dbo", $"S|{SqlServerEngine.Schema}|dbo" },
            lines.Where(line => line.StartsWith("S|", StringComparison.Ordinal)));
        Assert.Equal(
            ReportViewOracle.Views.Select(view => $"{SqlServerAccess.ReportSchema}.{view}").Order(StringComparer.Ordinal),
            lines.Where(line => line.StartsWith("V|", StringComparison.Ordinal)).Select(line => line.Split('|')[1]).Order(StringComparer.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("P|", StringComparison.Ordinal) && line.EndsWith($"|SELECT|GRANT|{SqlServerAccess.ReportRole}", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("P|", StringComparison.Ordinal) && line.EndsWith($"|INSERT|DENY|{SqlServerAccess.ApplicationRole}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Migrating_again_changes_neither_the_roles_nor_their_rights_nor_the_views()
    {
        await using var database = await MigratedAsync();
        var before = await DescribeAsync(database);
        var state = await Migrations(database).MigrateAsync(Token);
        Assert.Equal(SchemaStatus.UpToDate, state.Status);
        Assert.Empty(state.Pending);
        Assert.Equal(before, await DescribeAsync(database));
        await using var context = database.Context();
        // One record for each known migration (Initial, RolesAndReportViews, the two of ADR 031, and the next ones).
        Assert.Equal(context.Database.GetMigrations().Count(), await context.Set<SchemaHistoryEntry>().CountAsync(Token));
    }

    [Fact]
    public async Task A_database_at_the_initial_schema_is_behind_and_migrates_to_the_roles_and_the_report_views()
    {
        await using var database = await InitialAsync();
        var before = await Migrations(database).CheckAsync(Token);
        Assert.Equal(SchemaStatus.Behind, before.Status);
        Assert.Equal(new[] { InitialMigration }, before.Applied);
        await using var context = database.Context();
        var known = context.Database.GetMigrations().ToList();
        // The roles and report views, the two migrations of the snapshot reads (ADR 031), then the next ones.
        Assert.Equal(known[1..], before.Pending);
        Assert.Equal(new[] { RolesMigration, SqlServerSchemaCatalog.OptionsMigration, SqlServerSchemaCatalog.OptionsCheckMigration }, before.Pending.Take(3));
        var after = await Migrations(database).MigrateAsync(Token);
        Assert.Equal(SchemaStatus.UpToDate, after.Status);
        Assert.Equal(known, after.Applied);
        var history = await context.Set<SchemaHistoryEntry>().AsNoTracking().OrderBy(entry => entry.MigrationId).ToListAsync(Token);
        var sources = new MigrationSources(database.Engine.MigrationsAssembly);
        Assert.Equal(known, history.Select(entry => entry.MigrationId));
        Assert.All(history, entry => Assert.Equal(sources.FingerprintOf(entry.MigrationId), entry.MigrationFingerprint));
        Assert.NotNull(history.Single(entry => entry.MigrationId == RolesMigration).SchemaFingerprint);
        Assert.Equal(Fingerprints.OfLines(await DescribeAsync(database)), history[^1].SchemaFingerprint);
        Assert.Equal(11, (await DescribeAsync(database)).Count(line => line.StartsWith("V|", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Reverting_the_migration_gives_back_exactly_the_initial_schema()
    {
        await using var initial = await InitialAsync();
        await using var reverted = await MigratedAsync();
        await using (var context = reverted.Engine.CreateMigrationContext())
            await context.GetService<IMigrator>().MigrateAsync(InitialMigration, Token);
        Assert.Equal(await DescribeAsync(initial), await DescribeAsync(reverted));
    }

    [Fact]
    public async Task Users_added_to_the_roles_by_the_administrator_leave_the_schema_accepted()
    {
        await using var database = await MigratedAsync();
        await AsOwnerAsync(database, async connection =>
        {
            await SqlServerAccessProbe.CreateMemberAsync(connection, RoleSession.NewUser("app"), SqlServerAccess.ApplicationRole, Token);
            await SqlServerAccessProbe.CreateMemberAsync(connection, RoleSession.NewUser("bi"), SqlServerAccess.ReportRole, Token);
        });
        Assert.Equal(SchemaStatus.UpToDate, (await Migrations(database).CheckAsync(Token)).Status);
    }

    [Fact]
    public async Task A_table_granted_by_hand_to_the_report_role_is_refused_as_a_changed_schema()
    {
        await using var database = await MigratedAsync();
        await AsOwnerAsync(database, connection =>
            SqlServerAccessProbe.GrantAsync(connection, "SELECT", $"{SqlServerEngine.Schema}.{TableNames.AppUser}", SqlServerAccess.ReportRole, Token));
        await AssertChangedSchemaAsync(database);
    }

    [Fact]
    public async Task A_deny_removed_by_hand_from_the_application_role_is_refused_as_a_changed_schema()
    {
        await using var database = await MigratedAsync();
        await AsOwnerAsync(database, connection =>
            SqlServerAccessProbe.RevokeAsync(connection, "INSERT", $"{SqlServerEngine.Schema}.{TableNames.SchemaHistory}", SqlServerAccess.ApplicationRole, Token));
        await AssertChangedSchemaAsync(database);
    }

    [Fact]
    public async Task The_report_role_made_a_reader_of_every_table_by_hand_is_refused_as_a_changed_schema()
    {
        await using var database = await MigratedAsync();
        await AsOwnerAsync(database, connection => SqlServerAccessProbe.AddMemberAsync(connection, "db_datareader", SqlServerAccess.ReportRole, Token));
        await AssertChangedSchemaAsync(database);
    }
}
