// ScriptedFailures.cs
// Transient failures of the engine simulated at the level of the engine: a commit or a query that fails as a deadlock or a busy database would.

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Topogenia.Persistence.Errors;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>The failure thrown by <see cref="ScriptedFailures"/> in place of the engine.</summary>
public sealed class SimulatedEngineFailure : Exception
{
    public SimulatedEngineFailure()
        : base("Simulated failure of the engine.")
    {
    }

    public SimulatedEngineFailure(string message)
        : base(message)
    {
    }

    public SimulatedEngineFailure(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Makes the next commits (or the next queries) fail, a given number of times, at the moment the engine would run them:
/// the commit is not done and the transaction rolls back, as after a deadlock or a busy database. The real failures of
/// both engines are proven by their own tests (<c>RetryTests</c>). <paramref name="closingsAfterCommit"/> makes the
/// closing of a connection fail once a commit is done, as a busy write-back of the log of SQLite did (data B1 of round 4):
/// the work of the unit is committed, the failure comes after it.
/// </summary>
public sealed class ScriptedFailures(int commits = 0, int queries = 0, int closingsAfterCommit = 0)
    : DbTransactionInterceptor, IDbCommandInterceptor, IDbConnectionInterceptor
{
    private int commits = commits;
    private int queries = queries;
    private int closingsAfterCommit = closingsAfterCommit;
    private int committed;

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
        Interlocked.Decrement(ref commits) >= 0 ? throw new SimulatedEngineFailure() : ValueTask.FromResult(result);

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref committed, 1);
        return Task.CompletedTask;
    }

    public InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        Volatile.Read(ref committed) == 1 && Interlocked.Decrement(ref closingsAfterCommit) >= 0 ? throw new SimulatedEngineFailure() : result;

    public ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        ValueTask.FromResult(ConnectionClosing(connection, eventData, result));

    public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
        Interlocked.Decrement(ref queries) >= 0 ? throw new SimulatedEngineFailure() : ValueTask.FromResult(result);
}

/// <summary>A dialect that reads a <see cref="SimulatedEngineFailure"/> as the given transient failure, and the rest as the real dialect.</summary>
public sealed class ScriptedDialect(EngineDialect engine, StoreErrorKind kind) : EngineDialect
{
    public override Task<StoreTransaction> BeginWriteAsync(AppDbContext context, CancellationToken cancellationToken) =>
        engine.BeginWriteAsync(context, cancellationToken);

    public override Task<StoreTransaction> BeginReadAsync(AppDbContext context, CancellationToken cancellationToken) =>
        engine.BeginReadAsync(context, cancellationToken);

    protected override StoreError? ClassifyOwn(Exception exception, IModel model) =>
        exception is SimulatedEngineFailure ? new StoreError(kind, null) : engine.Classify(exception, model);
}
