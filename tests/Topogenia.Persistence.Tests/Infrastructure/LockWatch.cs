// LockWatch.cs
// When a unit of work asks for the lock of its project and when it holds it, seen from the commands that its context sends.

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// Watches the first lock of a session. The lock of a project is the first statement of a structure unit, the
/// `structure_version + 1` of spec 4.7: in SQL Server the update waits for the row lock of a unit that holds it; in SQLite
/// the write transaction is immediate, so the unit already waits at its start, right after the opening of its
/// connection, for the write lock of the database (the provider of SQLite opens it, unseen by EF Core). The unit has
/// asked for the lock when that statement is sent (SQL Server) or its connection is open (SQLite), and holds it once the
/// statement has run. A test can so prove that a unit really waited: it asked while another one held the lock, and held
/// it only after the other committed.
/// </summary>
public sealed class LockWatch(EngineKind kind) : DbConnectionInterceptor, IDbCommandInterceptor
{
    /// <summary>Set when the unit asks for the lock.</summary>
    public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Set when the unit holds the lock.</summary>
    public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (kind == EngineKind.Sqlite)
            Asked.TrySetResult();
        return Task.CompletedTask;
    }

    public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Asked.TrySetResult();
        return ValueTask.FromResult(result);
    }

    public ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Held.TrySetResult();
        return ValueTask.FromResult(result);
    }
}
