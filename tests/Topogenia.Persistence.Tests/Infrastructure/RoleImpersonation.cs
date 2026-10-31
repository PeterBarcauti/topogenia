// RoleImpersonation.cs
// Runs every connection that a session opens with the rights of a database user: impersonated once open, reverted before it closes.

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.SqlServer;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// A connection interceptor for SQL Server sessions: every command of the session, those of EF Core included, runs as the
/// user (a user without login, member of a role), as the account of the application would. The impersonation is reverted
/// before the connection goes back to the pool, which refuses a connection still impersonating. The data permissions that
/// the user holds on `schema_history` are read once, when the first connection opens: they prove the impersonation.
/// </summary>
internal sealed class RoleImpersonation(string user) : DbConnectionInterceptor
{
    public IReadOnlyList<string>? HeldOnSchemaHistory { get; private set; }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await SqlServerAccessProbe.ImpersonateAsync(connection, user, cancellationToken);
        HeldOnSchemaHistory ??= await SqlServerAccessProbe.PermissionsAsync(connection, [$"{SqlServerEngine.Schema}.{TableNames.SchemaHistory}"], cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        ConnectionOpenedAsync(connection, eventData, CancellationToken.None).GetAwaiter().GetResult();

    public override async ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        await SqlServerAccessProbe.RevertAsync(connection, CancellationToken.None);
        return result;
    }

    public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        ConnectionClosingAsync(connection, eventData, result).AsTask().GetAwaiter().GetResult();
}
