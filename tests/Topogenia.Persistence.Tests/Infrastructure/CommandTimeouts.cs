// CommandTimeouts.cs
// Records the command timeouts that a context gives the engine, and when it asks for the lock of the migrations.

using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// An interceptor of EF Core that keeps the timeout of each command that a context sends (<see cref="OfCommands"/>) and the
/// default timeout of each connection it opens (<see cref="OfConnections"/>: the one of the commands that a provider or the
/// catalogue creates on it, unseen by EF Core), read once the interceptors of the provider have run. <see cref="LockAsked"/>
/// is set when the context sends its first command: for a context of the check of <c>SchemaMigrations</c>, the request of
/// the lock of the migrations, the commands before it (the access of the account) being those of the catalogue.
/// </summary>
public sealed class CommandTimeouts : DbConnectionInterceptor, IDbCommandInterceptor
{
    private readonly ConcurrentQueue<int> commands = new();
    private readonly ConcurrentQueue<int> connections = new();

    public IReadOnlyList<int> OfCommands => [.. commands];

    public IReadOnlyList<int> OfConnections => [.. connections];

    public TaskCompletionSource LockAsked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => connections.Enqueue(DefaultOf(connection));

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        connections.Enqueue(DefaultOf(connection));
        return Task.CompletedTask;
    }

    public InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) =>
        Recorded(command, result);

    public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Recorded(command, result));

    public InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) =>
        Recorded(command, result);

    public ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Recorded(command, result));

    public InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) =>
        Recorded(command, result);

    public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Recorded(command, result));

    private T Recorded<T>(DbCommand command, T result)
    {
        commands.Enqueue(command.CommandTimeout);
        LockAsked.TrySetResult();
        return result;
    }

    private static int DefaultOf(DbConnection connection) => connection switch
    {
        SqlConnection sql => sql.CommandTimeout,
        SqliteConnection sqlite => sqlite.DefaultTimeout,
        _ => throw new InvalidOperationException($"Unexpected connection {connection.GetType().Name}."),
    };
}
