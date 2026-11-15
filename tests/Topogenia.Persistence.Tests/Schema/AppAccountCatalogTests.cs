// AppAccountCatalogTests.cs
// Under the account of the application, the start sees and refuses the changes that SQL Server hid from it before VIEW DEFINITION on the database (ADR 032).

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Persistence.SqlServer.SqlServerAccessProbe;

namespace Topogenia.Persistence.Tests.Schema;

/// <summary>
/// SQL Server only (accounts and rights are those of the server mode). The start of the server mode checks the schema
/// under the account of the application, a member of `topogenia_app`. SQL Server hides the metadata of what an account
/// holds no right on: a trigger of the database, an object outside the two schemas of the application and a right given
/// to another user were seen by an administrator only (`--migrate`). With `VIEW DEFINITION` on the database (migration
/// `ApplicationViewDefinition`) the account sees them, and the start refuses each one as a changed schema; once the change
/// is undone, the database is accepted again.
/// </summary>
public sealed class AppAccountCatalogTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task AsOwnerAsync(TestDatabase database, Func<DbConnection, Task> change)
    {
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        await change(context.Database.GetDbConnection());
    }

    private static Task<SchemaState> CheckAsync(TestDatabase database, IInterceptor account) =>
        new SchemaMigrations(database.Engine, new FixedClock(), account).CheckAsync(Token);

    /// <summary>Makes the change as an administrator, checks the refusal under the account of the application, undoes it.</summary>
    private async Task AssertRefusedUnderTheApplicationAsync(Func<DbConnection, Task> apply, Func<DbConnection, Task> undo)
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var application = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ApplicationRole, Bi.RoleSession.NewUser("app"));
        Assert.Equal(SchemaStatus.UpToDate, (await CheckAsync(database, application)).Status);
        await AsOwnerAsync(database, apply);
        try
        {
            var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => CheckAsync(database, application));
            Assert.Equal(SchemaCodes.SchemaChanged, refusal.Code);
        }
        finally
        {
            await AsOwnerAsync(database, undo);
        }
        Assert.Equal(SchemaStatus.UpToDate, (await CheckAsync(database, application)).Status);
    }

    [Theory]
    [InlineData(nameof(CatalogChange.DatabaseTrigger))]
    [InlineData(nameof(CatalogChange.SynonymOutsideTheSchemas))]
    public Task A_trigger_of_the_database_or_an_object_outside_the_two_schemas_is_refused_under_the_account_of_the_application(string name)
    {
        var change = Enum.Parse<CatalogChange>(name);
        return AssertRefusedUnderTheApplicationAsync(
            connection => SqlServerAccessProbe.ApplyAsync(connection, change, Token),
            connection => SqlServerAccessProbe.UndoAsync(connection, change, Token));
    }

    [Fact]
    public async Task A_right_given_to_a_user_is_refused_under_the_account_of_the_application()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var reports = Bi.RoleSession.NewUser("bi");
        await AsOwnerAsync(database, connection => SqlServerAccessProbe.CreateMemberAsync(connection, reports, SqlServerAccess.ReportRole, Token));
        await AssertRefusedUnderTheApplicationAsync(
            connection => SqlServerAccessProbe.GrantOnDatabaseAsync(connection, "SELECT", reports, Token),
            connection => SqlServerAccessProbe.RevokeOnDatabaseAsync(connection, "SELECT", reports, Token));
    }
}
