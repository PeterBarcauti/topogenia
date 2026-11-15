// CatalogChangeTests.cs
// Objects and rights added by hand to a SQL Server database, each a way to read the data outside the two roles: refused at the start (ADR 032, 037).

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Persistence.SqlServer.SqlServerAccessProbe;

namespace Topogenia.Persistence.Tests.Schema;

/// <summary>
/// SQL Server only: SQLite has no account, no right and no object other than its tables, indexes, views and triggers,
/// all read from `sqlite_master` (<see cref="SqliteCatalogChangeTests"/>). Each test makes one change by hand in the
/// shared migrated database, as an administrator could, checks that the start refuses it as a changed schema, undoes it
/// and checks that the database is accepted again (the tests of a class run one after the other). Each change is also
/// refused when the check runs under the account of the application, as it does at the start of the server mode: that
/// account holds `VIEW DEFINITION` on the database (migration `ApplicationViewDefinition`, ADR 037 revised).
/// </summary>
public sealed class CatalogChangeTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task AsOwnerAsync(TestDatabase database, Func<DbConnection, Task> change)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        await change(context.Database.GetDbConnection());
    }

    private static Task<SchemaState> CheckAsync(TestDatabase database, params IInterceptor[] account) =>
        new SchemaMigrations(database.Engine, new FixedClock(), account).CheckAsync(Token);

    private static async Task AssertRefusedAsync(TestDatabase database, params IInterceptor[] account)
    {
        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => CheckAsync(database, account));
        Assert.Equal(SchemaCodes.SchemaChanged, refusal.Code);
    }

    /// <summary>Makes the change, checks the refusals, undoes the change and checks that the database is accepted again.</summary>
    private async Task AssertChangeRefusedAsync(Func<DbConnection, Task> apply, Func<DbConnection, Task> undo, bool seenByTheApplication)
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var application = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ApplicationRole, Bi.RoleSession.NewUser("app"));
        Assert.Equal(SchemaStatus.UpToDate, (await CheckAsync(database, application)).Status);
        await AsOwnerAsync(database, apply);
        try
        {
            await AssertRefusedAsync(database);
            if (seenByTheApplication)
                await AssertRefusedAsync(database, application);
        }
        finally
        {
            await AsOwnerAsync(database, undo);
        }
        Assert.Equal(SchemaStatus.UpToDate, (await CheckAsync(database)).Status);
        Assert.Equal(SchemaStatus.UpToDate, (await CheckAsync(database, application)).Status);
    }

    [Theory]
    [InlineData(nameof(CatalogChange.ReportFunction), true)]
    [InlineData(nameof(CatalogChange.ReportSynonym), true)]
    [InlineData(nameof(CatalogChange.TableTrigger), true)]
    [InlineData(nameof(CatalogChange.HistoryTrigger), true)]
    [InlineData(nameof(CatalogChange.Procedure), true)]
    [InlineData(nameof(CatalogChange.Sequence), true)]
    [InlineData(nameof(CatalogChange.UserType), true)]
    [InlineData(nameof(CatalogChange.PublicGrant), true)]
    [InlineData(nameof(CatalogChange.ReportViewNamedAsTheHistory), true)]
    [InlineData(nameof(CatalogChange.ReportTableNamedAsTheHistory), true)]
    [InlineData(nameof(CatalogChange.UsersOwnedByTheReports), true)]
    [InlineData(nameof(CatalogChange.ReportViewOwnedByTheReports), true)]
    [InlineData(nameof(CatalogChange.DatabaseTrigger), true)]
    [InlineData(nameof(CatalogChange.SynonymOutsideTheSchemas), true)]
    [InlineData(nameof(CatalogChange.SchemaOwnedByTheReports), true)]
    [InlineData(nameof(CatalogChange.ShippedFunctionOutsideTheSchemas), true)]
    [InlineData(nameof(CatalogChange.ShippedFunctionGivenToTheReports), true)]
    [InlineData(nameof(CatalogChange.ApplicationRoleAlone), true)]
    [InlineData(nameof(CatalogChange.ApplicationRoleInTheApplicationRole), true)]
    [InlineData(nameof(CatalogChange.ApplicationRoleInTheReportRole), true)]
    [InlineData(nameof(CatalogChange.DatabaseRoleInTheApplicationRole), true)]
    [InlineData(nameof(CatalogChange.DatabaseRoleInTheReportRole), true)]
    public Task An_object_or_a_right_added_by_hand_is_refused_as_a_changed_schema(string name, bool seenByTheApplication)
    {
        var change = Enum.Parse<CatalogChange>(name);
        return AssertChangeRefusedAsync(
            connection => SqlServerAccessProbe.ApplyAsync(connection, change, Token),
            connection => SqlServerAccessProbe.UndoAsync(connection, change, Token),
            seenByTheApplication);
    }

    [Fact]
    public async Task A_synonym_of_the_report_schema_reads_the_users_for_the_reports_and_is_refused()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        await AsOwnerAsync(database, connection => SqlServerAccessProbe.ApplyAsync(connection, CatalogChange.ReportSynonym, Token));
        try
        {
            await using (var reports = await Bi.RoleSession.OpenAsync(database, SqlServerAccess.ReportRole))
            {
                // The leak is real: the report role reads the users through the ownership chain of the synonym.
                var rows = await SqlServerAccessProbe.ReadViewAsync(reports.Connection, "users", Token);
                Assert.Contains("windows_login", rows.Columns);
            }
            await AssertRefusedAsync(database);
        }
        finally
        {
            await AsOwnerAsync(database, connection => SqlServerAccessProbe.UndoAsync(connection, CatalogChange.ReportSynonym, Token));
        }
    }

    [Fact]
    public async Task A_view_of_the_report_schema_named_as_the_history_of_ef_core_reads_the_users_for_the_reports_and_is_refused()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var application = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ApplicationRole, Bi.RoleSession.NewUser("app"));
        await AsOwnerAsync(database, connection => SqlServerAccessProbe.ApplyAsync(connection, CatalogChange.ReportViewNamedAsTheHistory, Token));
        try
        {
            await using (var reports = await Bi.RoleSession.OpenAsync(database, SqlServerAccess.ReportRole))
            {
                // The leak is real: the name differs from the history of EF Core by its case and its schema only.
                var rows = await SqlServerAccessProbe.ReadViewAsync(reports.Connection, SqlServerAccessProbe.HistoryInLowerCase, Token);
                Assert.Contains("windows_login", rows.Columns);
            }
            await AssertRefusedAsync(database);
            await AssertRefusedAsync(database, application);
        }
        finally
        {
            await AsOwnerAsync(database, connection => SqlServerAccessProbe.UndoAsync(connection, CatalogChange.ReportViewNamedAsTheHistory, Token));
        }
    }

    [Fact]
    public async Task An_application_role_in_the_role_of_the_application_opens_the_users_to_the_reports_and_is_refused()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var application = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ApplicationRole, Bi.RoleSession.NewUser("app"));
        await AsOwnerAsync(database, connection => SqlServerAccessProbe.ApplyAsync(connection, CatalogChange.ApplicationRoleInTheApplicationRole, Token));
        try
        {
            await using (var reports = await Bi.RoleSession.OpenAsync(database, SqlServerAccess.ReportRole))
            {
                // The leak is real: a member of the reports, refused on the users, reads them once it gives the password
                // of the application role.
                await Assert.ThrowsAnyAsync<DbException>(() => SqlServerAccessProbe.ReadUsersAsync(reports.Connection, Token));
                var cookie = await SqlServerAccessProbe.SetApplicationRoleAsync(reports.Connection, Token);
                var rows = await SqlServerAccessProbe.ReadUsersAsync(reports.Connection, Token);
                await SqlServerAccessProbe.UnsetApplicationRoleAsync(reports.Connection, cookie, Token);
                Assert.Contains("windows_login", rows.Columns);
            }
            await AssertRefusedAsync(database);
            await AssertRefusedAsync(database, application);
        }
        finally
        {
            await AsOwnerAsync(database, connection => SqlServerAccessProbe.UndoAsync(connection, CatalogChange.ApplicationRoleInTheApplicationRole, Token));
        }
        Assert.Equal(SchemaStatus.UpToDate, (await CheckAsync(database, application)).Status);
    }

    [Fact]
    public async Task The_users_given_to_the_report_role_as_their_owner_are_read_by_the_reports_and_refused()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var users = $"{SqlServerEngine.Schema}.{TableNames.AppUser}";
        await AsOwnerAsync(database, connection => SqlServerAccessProbe.ApplyAsync(connection, CatalogChange.UsersOwnedByTheReports, Token));
        try
        {
            await using (var reports = await Bi.RoleSession.OpenAsync(database, SqlServerAccess.ReportRole))
                Assert.Contains($"{users}|SELECT", await SqlServerAccessProbe.PermissionsAsync(reports.Connection, [users], Token));
            await AssertRefusedAsync(database);
        }
        finally
        {
            await AsOwnerAsync(database, connection => SqlServerAccessProbe.UndoAsync(connection, CatalogChange.UsersOwnedByTheReports, Token));
        }
        await using (var reports = await Bi.RoleSession.OpenAsync(database, SqlServerAccess.ReportRole))
            Assert.Empty(await SqlServerAccessProbe.PermissionsAsync(reports.Connection, [users], Token));
    }

    [Theory]
    [InlineData("db_datareader")]
    [InlineData("db_datawriter")]
    [InlineData("db_ddladmin")]
    [InlineData("db_securityadmin")]
    [InlineData("db_backupoperator")]
    public async Task An_account_of_the_reports_put_in_a_fixed_role_that_opens_the_data_is_refused(string role)
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var reports = Bi.RoleSession.NewUser("bi");
        await AsOwnerAsync(database, connection => SqlServerAccessProbe.CreateMemberAsync(connection, reports, SqlServerAccess.ReportRole, Token));
        await AssertChangeRefusedAsync(
            connection => SqlServerAccessProbe.AddMemberAsync(connection, role, reports, Token),
            connection => SqlServerAccessProbe.RemoveMemberAsync(connection, role, reports, Token),
            seenByTheApplication: true);
    }

    [Fact]
    public async Task An_account_of_the_reports_put_in_db_datareader_reads_every_table()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var reports = Bi.RoleSession.NewUser("bi");
        var users = $"{SqlServerEngine.Schema}.{TableNames.AppUser}";
        await AsOwnerAsync(database, async connection =>
        {
            await SqlServerAccessProbe.CreateMemberAsync(connection, reports, SqlServerAccess.ReportRole, Token);
            await SqlServerAccessProbe.AddMemberAsync(connection, "db_datareader", reports, Token);
        });
        try
        {
            IReadOnlyList<string> held = [];
            await AsOwnerAsync(database, async connection =>
            {
                await SqlServerAccessProbe.ImpersonateAsync(connection, reports, Token);
                held = await SqlServerAccessProbe.PermissionsAsync(connection, [users], Token);
                await SqlServerAccessProbe.RevertAsync(connection, Token);
            });
            Assert.Contains($"{users}|SELECT", held);
            await AssertRefusedAsync(database);
        }
        finally
        {
            await AsOwnerAsync(database, connection => SqlServerAccessProbe.RemoveMemberAsync(connection, "db_datareader", reports, Token));
        }
    }

    [Fact]
    public async Task The_fixed_roles_described_are_those_that_open_the_data()
    {
        Assert.Equal(
            ["db_backupoperator", "db_datareader", "db_datawriter", "db_ddladmin", "db_securityadmin"],
            SqlServerSchemaCatalog.DataRoles.Order(StringComparer.Ordinal));
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var administrator = Bi.RoleSession.NewUser("admin");
        // An administrator added to db_owner, or a user added to the roles of the application, changes nothing.
        await AssertAcceptedAsync(database, async connection =>
        {
            await SqlServerAccessProbe.CreateMemberAsync(connection, administrator, "db_owner", Token);
            await SqlServerAccessProbe.CreateMemberAsync(connection, Bi.RoleSession.NewUser("app"), SqlServerAccess.ApplicationRole, Token);
        });
    }

    [Fact]
    public async Task A_right_on_the_whole_database_given_to_an_account_is_refused()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var reports = Bi.RoleSession.NewUser("bi");
        await AsOwnerAsync(database, connection => SqlServerAccessProbe.CreateMemberAsync(connection, reports, SqlServerAccess.ReportRole, Token));
        await AssertChangeRefusedAsync(
            connection => SqlServerAccessProbe.GrantOnDatabaseAsync(connection, "SELECT", reports, Token),
            connection => SqlServerAccessProbe.RevokeOnDatabaseAsync(connection, "SELECT", reports, Token),
            seenByTheApplication: true);
    }

    private static async Task AssertAcceptedAsync(TestDatabase database, Func<DbConnection, Task> change)
    {
        await AsOwnerAsync(database, change);
        Assert.Equal(SchemaStatus.UpToDate, (await CheckAsync(database)).Status);
    }
}
