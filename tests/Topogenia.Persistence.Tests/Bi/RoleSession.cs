// RoleSession.cs
// A context of the common model whose connection runs with the rights of one database role, through a user without login.

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Bi;

/// <summary>
/// Opens a context on a SQL Server test database, creates a user without login (never a login of the server) member of
/// the role, and impersonates it on the open connection: every later command of the context, those of EF Core included,
/// runs with the rights of the role, as the account of the application or of the reports would. The user stays in the
/// test database, which the test removes.
/// </summary>
internal sealed class RoleSession : IAsyncDisposable
{
    private static int counter;

    private RoleSession(AppDbContext context, string user)
    {
        Context = context;
        User = user;
    }

    public AppDbContext Context { get; }

    public string User { get; }

    public DbConnection Connection => Context.Database.GetDbConnection();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task<RoleSession> OpenAsync(TestDatabase database, string role)
    {
        ArgumentNullException.ThrowIfNull(database);
        var user = NewUser(role);
        var context = database.Context();
        try
        {
            await context.Database.OpenConnectionAsync(Token);
            await SqlServerAccessProbe.CreateMemberAsync(context.Database.GetDbConnection(), user, role, Token);
            await SqlServerAccessProbe.ImpersonateAsync(context.Database.GetDbConnection(), user, Token);
            return new RoleSession(context, user);
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    /// <summary>A new name of a test user, unique in the process (letters, digits and underscores only).</summary>
    public static string NewUser(string role) => $"probe_{role}_{Environment.ProcessId}_{Interlocked.Increment(ref counter)}";

    public async ValueTask DisposeAsync()
    {
        try
        {
            await SqlServerAccessProbe.RevertAsync(Connection, CancellationToken.None);
        }
        finally
        {
            await Context.DisposeAsync();
        }
    }
}
