// RoleTests.cs
// The two database roles of the server mode proved by users without login: rights of the application and of the reports (A-4-40, A-4-41).

using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Bi;

/// <summary>
/// Each test acts as a member of one role (<see cref="RoleSession"/>). The effective permissions are computed by the server
/// on every table and every view; the real reads and writes prove them; the rules are those of the 1.x (`db/004`): the
/// application reads and writes the data, never the histories of the migrations, and only appends to the histories of the
/// data; the reports read the report views and nothing else.
/// </summary>
public sealed class RoleTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    /// <summary>SQL Server error of a permission refused on an object.</summary>
    private const int PermissionDenied = 229;

    /// <summary>The application reads them, the migrations alone write them (1.x `DENY` on `schema_version`).</summary>
    private static readonly string[] MigrationHistories = [TableNames.SchemaHistory, TableNames.MigrationsHistory];

    /// <summary>The application appends to them and may purge them, never rewrites them (1.x `DENY UPDATE`).</summary>
    private static readonly string[] DataHistories = [TableNames.StepSheetVersion, TableNames.PainPointStatusHistory, TableNames.AuditEvent];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IReadOnlyList<string> Tables(AppDbContext context) =>
        context.Model.GetEntityTypes().Select(entity => entity.GetTableName()!).Append(TableNames.MigrationsHistory)
            .Select(table => $"{SqlServerEngine.Schema}.{table}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    private static IEnumerable<string> ReportViews => ReportViewOracle.Views.Select(view => $"{SqlServerAccess.ReportSchema}.{view}");

    private static bool ApplicationMay(string table, string permission)
    {
        var name = table[(table.IndexOf('.', StringComparison.Ordinal) + 1)..];
        if (MigrationHistories.Contains(name, StringComparer.Ordinal))
            return permission == "SELECT";
        return !(DataHistories.Contains(name, StringComparer.Ordinal) && permission == "UPDATE");
    }

    private static async Task AssertDeniedAsync(Func<Task> action)
    {
        Exception? error = null;
        try
        {
            await action();
        }
        catch (Exception caught) when (caught is DbException or DbUpdateException)
        {
            error = caught;
        }
        var denied = error is DbUpdateException { InnerException: SqlException inner } ? inner : error as SqlException;
        Assert.NotNull(denied);
        Assert.Equal(PermissionDenied, denied.Number);
    }

    [Fact]
    public async Task The_application_role_holds_exactly_the_data_permissions_of_its_rules_and_none_on_the_report_views()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        await using var session = await RoleSession.OpenAsync(database, SqlServerAccess.ApplicationRole);
        var tables = Tables(session.Context);
        var held = await SqlServerAccessProbe.PermissionsAsync(session.Connection, tables.Concat(ReportViews), Token);
        var expected = tables.SelectMany(table => SqlServerAccessProbe.DataPermissions.Where(permission => ApplicationMay(table, permission))
            .Select(permission => $"{table}|{permission}"));
        Assert.Equal(expected.Order(StringComparer.Ordinal), held);
        Assert.Equal(26, tables.Count);
    }

    [Fact]
    public async Task The_report_role_holds_the_select_of_the_report_views_and_nothing_on_the_tables()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        await using var session = await RoleSession.OpenAsync(database, SqlServerAccess.ReportRole);
        var held = await SqlServerAccessProbe.PermissionsAsync(session.Connection, Tables(session.Context).Concat(ReportViews), Token);
        Assert.Equal(ReportViews.Select(view => $"{view}|SELECT").Order(StringComparer.Ordinal), held);
    }

    [Fact]
    public async Task The_application_role_writes_reads_changes_and_removes_the_rows_of_a_project()
    {
        var graph = ProjectGraph.New();
        var database = await databases.OfAsync(EngineKind.SqlServer);
        await using var session = await RoleSession.OpenAsync(database, SqlServerAccess.ApplicationRole);
        var context = session.Context;
        context.AddRange(graph.Records);
        await context.SaveChangesAsync(Token);
        context.ChangeTracker.Clear();

        var step = await context.Set<Step>().SingleAsync(item => item.Id == graph.Steps[0].Id, Token);
        context.Entry(step).Property(item => item.Label).CurrentValue = "Renamed by the application";
        context.Entry(step).Property(item => item.Version).CurrentValue = step.Version + 1;
        context.Remove(await context.Set<DocumentUse>().SingleAsync(use => use.DocumentId == graph.Use.DocumentId && use.StepId == graph.Use.StepId, Token));
        await context.SaveChangesAsync(Token);
        context.ChangeTracker.Clear();

        Assert.Equal("Renamed by the application", (await context.Set<Step>().AsNoTracking().SingleAsync(item => item.Id == graph.Steps[0].Id, Token)).Label);
        Assert.False(await context.Set<DocumentUse>().AnyAsync(use => use.DocumentId == graph.Use.DocumentId && use.StepId == graph.Use.StepId, Token));
        Assert.Equal(graph.Of<Lane>().Count, await context.Set<Lane>().CountAsync(lane => lane.MapId == graph.RootMap.Id || lane.MapId == graph.SubMap.Id, Token));
    }

    [Fact]
    public async Task The_application_role_reads_the_histories_of_the_migrations_and_cannot_write_them()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.SqlServer);
        await using (var session = await RoleSession.OpenAsync(database, SqlServerAccess.ApplicationRole))
        {
            var context = session.Context;
            var applied = (await context.Database.GetAppliedMigrationsAsync(Token)).ToList();
            Assert.Equal(context.Database.GetMigrations(), applied);
            Assert.Equal(applied, (await context.Set<SchemaHistoryEntry>().AsNoTracking().ToListAsync(Token)).Select(entry => entry.MigrationId).Order(StringComparer.Ordinal));

            context.Add(new SchemaHistoryEntry("29991231235959_Future", new string('0', Fingerprints.Length), null, "9.0.0", FixedClock.Time));
            await AssertDeniedAsync(() => context.SaveChangesAsync(Token));
            context.ChangeTracker.Clear();
            var entry = await context.Set<SchemaHistoryEntry>().FirstAsync(Token);
            context.Entry(entry).Property(item => item.ApplicationVersion).CurrentValue = "9.9.9";
            await AssertDeniedAsync(() => context.SaveChangesAsync(Token));
            context.ChangeTracker.Clear();
            context.Remove(entry);
            await AssertDeniedAsync(() => context.SaveChangesAsync(Token));
            context.ChangeTracker.Clear();

            await AssertDeniedAsync(() => OutOfBand.RecordInEfHistoryAsync(context, "29991231235959_Future"));
            await AssertDeniedAsync(() => OutOfBand.RemoveFromEfHistoryAsync(context, applied[^1]));
        }
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token)).Status);
    }

    [Fact]
    public async Task The_application_role_appends_to_the_histories_of_the_data_and_purges_them_but_never_rewrites_them()
    {
        var graph = ProjectGraph.New();
        var database = await databases.OfAsync(EngineKind.SqlServer);
        await Refusals.SaveAsync(database, [.. graph.Records]);
        await using var session = await RoleSession.OpenAsync(database, SqlServerAccess.ApplicationRole);
        var context = session.Context;

        var change = await context.Set<PainPointStatusChange>().FirstAsync(item => item.PainPointId == graph.PainPoint.Id && item.Comment != null, Token);
        context.Entry(change).Property(item => item.Comment).CurrentValue = "Rewritten";
        await AssertDeniedAsync(() => context.SaveChangesAsync(Token));
        context.ChangeTracker.Clear();
        var version = await context.Set<StepSheetVersion>().FirstAsync(item => item.SheetId == graph.Sheets[0].Id, Token);
        context.Entry(version).Property(item => item.ContentJson).CurrentValue = "{}";
        await AssertDeniedAsync(() => context.SaveChangesAsync(Token));
        context.ChangeTracker.Clear();
        var audit = await context.Set<AuditEvent>().FirstAsync(item => item.ProjectId == graph.Project.Id, Token);
        context.Entry(audit).Property(item => item.Kind).CurrentValue = "project.rewritten";
        await AssertDeniedAsync(() => context.SaveChangesAsync(Token));
        context.ChangeTracker.Clear();

        context.Add(new PainPointStatusChange(Guid.CreateVersion7(), graph.PainPoint.Id, PainPointStatus.Dropped, PainPointStatus.Reported, "Reopened",
            graph.Owner.Id, graph.At(20)));
        var appended = new AuditEvent(Guid.CreateVersion7(), graph.At(20), graph.Owner.Id, "project.export", graph.Project.Id, null);
        context.Add(appended);
        context.RemoveRange(change, version, audit);
        await context.SaveChangesAsync(Token);
        context.ChangeTracker.Clear();
        Assert.False(await context.Set<StepSheetVersion>().AnyAsync(item => item.Id == version.Id, Token));
        Assert.Equal(appended.Id, (await context.Set<AuditEvent>().SingleAsync(item => item.ProjectId == graph.Project.Id, Token)).Id);
        Assert.Equal(PainPointStatus.Reported, (await context.Set<PainPointStatusChange>()
            .Where(item => item.PainPointId == graph.PainPoint.Id).OrderBy(item => item.ChangedAt).LastAsync(Token)).StatusAfter);
    }

    [Fact]
    public async Task The_report_role_reads_every_report_view_and_no_table()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        await using var session = await RoleSession.OpenAsync(database, SqlServerAccess.ReportRole);
        foreach (var view in ReportViewOracle.Views)
            Assert.Equal(ReportViewOracle.Columns[view].Count, (await SqlServerAccessProbe.ReadViewAsync(session.Connection, view, Token)).Columns.Count);
        var context = session.Context;
        await AssertDeniedAsync(() => context.Set<Project>().AsNoTracking().ToListAsync(Token));
        await AssertDeniedAsync(() => context.Set<Step>().AsNoTracking().ToListAsync(Token));
        await AssertDeniedAsync(() => context.Set<StepSheet>().AsNoTracking().ToListAsync(Token));
        await AssertDeniedAsync(() => context.Set<AuditEvent>().AsNoTracking().ToListAsync(Token));
        await AssertDeniedAsync(() => context.Set<SchemaHistoryEntry>().AsNoTracking().ToListAsync(Token));
        context.Add(new AuditEvent(Guid.CreateVersion7(), FixedClock.Time, null, "access.denied", null, null));
        await AssertDeniedAsync(() => context.SaveChangesAsync(Token));
    }

    /// <summary>
    /// The check of the start (spec 4.4) runs under the account of the application: the role must see the whole catalogue
    /// the fingerprint describes (views, permissions of the other role), or a sound database would be refused.
    /// </summary>
    [Fact]
    public async Task The_schema_check_run_by_the_application_role_sees_the_schema_the_migrations_recorded()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        IReadOnlyList<string> asOwner;
        await using (var owner = database.Context())
        {
            await owner.Database.OpenConnectionAsync(Token);
            asOwner = await database.Engine.Catalog.DescribeAsync(owner.Database.GetDbConnection(), null, Token);
        }
        await using var session = await RoleSession.OpenAsync(database, SqlServerAccess.ApplicationRole);
        var context = session.Context;
        await using var exclusive = await context.GetService<IHistoryRepository>().AcquireDatabaseLockAsync(Token);
        var applied = (await context.Database.GetAppliedMigrationsAsync(Token)).ToList();
        var history = await database.Engine.Catalog.HistoryAsync(session.Connection, null, Token);
        var described = await database.Engine.Catalog.DescribeAsync(session.Connection, null, Token);
        Assert.Equal(asOwner.Order(StringComparer.Ordinal), described.Order(StringComparer.Ordinal));
        var state = SchemaVerification.Verify(context.Database.GetMigrations().ToList(), new MigrationSources(database.Engine.MigrationsAssembly),
            new DatabaseSchema(applied, history, Fingerprints.OfLines(described)));
        Assert.Equal(SchemaStatus.UpToDate, state.Status);
    }
}
