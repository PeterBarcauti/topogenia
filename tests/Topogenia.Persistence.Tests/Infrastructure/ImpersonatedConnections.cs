// ImpersonatedConnections.cs
// Runs every connection that a context opens with the rights of a user without login of a SQL Server test database.

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Persistence.SqlServer;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// An interceptor for the components that open their own contexts (a session, a purge): each connection, once open, is
/// impersonated as the user (`EXECUTE AS USER`, <see cref="SqlServerAccessProbe"/>), and reverted before it closes, so that
/// every command of EF Core runs with the rights of the roles of that user. The user is created by the test in its
/// `Topogenia_Test_*` database, member of a role, never as a login of the server.
/// </summary>
public sealed class ImpersonatedConnections(string user) : DbConnectionInterceptor
{
    public string User { get; } = user;

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) =>
        SqlServerAccessProbe.ImpersonateAsync(connection, User, cancellationToken);

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        SqlServerAccessProbe.ImpersonateAsync(connection, User, CancellationToken.None).GetAwaiter().GetResult();

    public override async ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State == System.Data.ConnectionState.Open)
            await SqlServerAccessProbe.RevertAsync(connection, CancellationToken.None);
        return result;
    }

    public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State == System.Data.ConnectionState.Open)
            SqlServerAccessProbe.RevertAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    /// <summary>Creates a user without login, member of <paramref name="role"/>, in the test database, and its interceptor.</summary>
    public static async Task<ImpersonatedConnections> MemberOfAsync(TestDatabase database, string role, string name)
    {
        ArgumentNullException.ThrowIfNull(database);
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await SqlServerAccessProbe.CreateMemberAsync(context.Database.GetDbConnection(), name, role, TestContext.Current.CancellationToken);
        return new ImpersonatedConnections(name);
    }
}
