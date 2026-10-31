// PurgeRightsTests.cs
// The purge runs with the rights of the application role alone: it deletes the histories of the data, never updates them (ADR 032).

using Microsoft.Data.SqlClient;
using Topogenia.Persistence.Purge;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Bi;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Purge;

/// <summary>
/// SQL Server only: the roles exist in the server mode alone (ADR 032, D04). The 1.x purged through procedures owned by
/// `dbo`; in 2.0 the purge is C# under the account of the application, member of `topogenia_app`, which may delete the
/// versions of the sheets, the history of the pain points and the audit but may not update them (`DENY UPDATE`). A whole
/// purge run as a member of that role succeeds, which proves that it sends no update of a history and needs no other
/// right; the same purge as a member of `topogenia_bi` is refused by the server, which proves the impersonation.
/// </summary>
public sealed class PurgeRightsTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private const int PermissionDenied = 229;

    [Fact]
    public async Task A_whole_purge_runs_with_the_rights_of_the_application_role_and_not_with_those_of_the_reports()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var graph = await PurgeBench.GraphAsync(database);
        var trashedAt = graph.At(100);
        await PurgeBench.TrashAsync(database, graph, trashedAt);
        var now = PurgeBench.AfterRetention(trashedAt);
        var plan = await PurgeBench.PlanAsync(database, graph.Project.Id, now);
        var application = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ApplicationRole, RoleSession.NewUser("app"));
        var reports = await ImpersonatedConnections.MemberOfAsync(database, SqlServerAccess.ReportRole, RoleSession.NewUser("bi"));

        var denied = await Assert.ThrowsAnyAsync<Exception>(() => PurgeBench.PurgeAsync(database, graph.Project.Id, plan, 40, reports));
        await using (var session = database.Session(null, application))
            Assert.Contains(graph.Project.Id, await new TrashPurge(session).ProjectsToPurgeAsync(trashedAt.AddTicks(1), TestContext.Current.CancellationToken));
        await PurgeBench.PurgeAsync(database, graph.Project.Id, plan, 40, application);

        Assert.Equal(PermissionDenied, Assert.IsType<SqlException>(Inner(denied)).Number);
        Assert.Null(await PurgeBench.LoadAsync(database, graph.Project.Id));
        foreach (var record in graph.Records)
            Assert.Equal(PurgeBench.Survivors.Contains(record.GetType()), await PurgeBench.ExistsAsync(database, record));
    }

    private static Exception Inner(Exception exception)
    {
        var current = exception;
        while (current is not SqlException && current.InnerException is not null)
            current = current.InnerException;
        return current;
    }
}
