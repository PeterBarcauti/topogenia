// StoreSession.cs
// The database state of one request shared by its unit of work, its workspace store and its counters: one context, one write transaction.

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Schema;

namespace Topogenia.Persistence.Writing;

/// <summary>
/// One request (scoped): the engine, its dialect, the retry policy, and the context with the write transaction of the
/// current unit of work. <see cref="UnitOfWork"/> and <see cref="Loading.WorkspaceStore"/> of one request share it, so
/// that the loading and the writing happen in the transaction of the unit. A new attempt of the unit starts from a new
/// context (<see cref="ResetAsync"/>).
/// </summary>
public sealed class StoreSession : IAsyncDisposable
{
    private readonly IInterceptor[] interceptors;
    private readonly CommitWatch commits = new();
    private AppDbContext? context;

    public StoreSession(DatabaseEngine engine, EngineDialect dialect, RetryPolicy? retry = null, params IInterceptor[] interceptors)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(dialect);
        Engine = engine;
        Dialect = dialect;
        Retry = retry ?? RetryPolicy.Default;
        this.interceptors = [commits, .. interceptors];
    }

    /// <summary>
    /// True once the engine committed a transaction of the context since <see cref="ForgetCommits"/>: a failure met after
    /// it (the clean-up of the transaction, the closing of the connection) comes after work that is done (data B1 of round 4).
    /// </summary>
    internal bool Committed => commits.Committed;

    internal void ForgetCommits() => commits.Committed = false;

    public DatabaseEngine Engine { get; }

    public EngineDialect Dialect { get; }

    public RetryPolicy Retry { get; }

    /// <summary>The context of the request, created at the first use.</summary>
    internal AppDbContext Context => context ??= Engine.CreateContext(interceptors);

    /// <summary>The write transaction of the current unit of work, null outside a unit.</summary>
    internal StoreTransaction? Transaction { get; set; }

    internal bool InUnit => Transaction is not null;

    /// <summary>The projects whose lock the current unit of work holds (<see cref="UnitOfWork.LockProjectAsync"/>), empty outside a unit.</summary>
    internal HashSet<Guid> LockedProjects { get; } = [];

    /// <summary>The failure of the engine carried by an exception, read on the model of the context.</summary>
    internal StoreError? Classify(Exception exception) => Dialect.Classify(exception, Context.Model);

    /// <summary>Throws away the transaction (rolled back unless committed) and the context; the next use starts afresh.</summary>
    internal async ValueTask ResetAsync()
    {
        LockedProjects.Clear();
        if (Transaction is not null)
        {
            var transaction = Transaction;
            Transaction = null;
            await transaction.DisposeAsync();
        }
        if (context is not null)
        {
            var current = context;
            context = null;
            await current.DisposeAsync();
        }
    }

    public ValueTask DisposeAsync() => ResetAsync();

    /// <summary>Notes the commits of the engine, told by EF Core before it cleans up the transaction.</summary>
    private sealed class CommitWatch : DbTransactionInterceptor
    {
        public bool Committed { get; set; }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Committed = true;

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }
    }
}
