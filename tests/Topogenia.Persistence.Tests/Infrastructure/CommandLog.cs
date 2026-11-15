// CommandLog.cs
// Records every command that a context sends to the engine, so that a test counts the round trips of a reading or a writing.

using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// An interceptor of EF Core that keeps the source of each command executed by a context (one entry per round trip to the
/// engine, a batch of statements counting once). <see cref="Reads"/> are the queries of LINQ, <see cref="Writes"/> the
/// saves of changes and the bulk statements (the lock of the project, a deletion by condition), <see cref="Migrations"/>
/// the commands of the migrations and of their history. The commands that a provider sends itself on the connection (its
/// settings, its catalogue) are not commands of a context and are not seen.
/// </summary>
public sealed class CommandLog : DbCommandInterceptor
{
    private readonly ConcurrentQueue<CommandSource> commands = new();

    public IReadOnlyList<CommandSource> Commands => [.. commands];

    public int Reads => Commands.Count(source => source == CommandSource.LinqQuery);

    public int Writes => Commands.Count(source => source is CommandSource.SaveChanges or CommandSource.ExecuteUpdate or CommandSource.ExecuteDelete);

    public int Migrations => Commands.Count(source => source == CommandSource.Migrations);

    public void Clear() => commands.Clear();

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Record(eventData);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(eventData);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Record(eventData);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(eventData);
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData);
        return result;
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Record(eventData);
        return result;
    }

    private void Record(CommandEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        commands.Enqueue(eventData.CommandSource);
    }
}
