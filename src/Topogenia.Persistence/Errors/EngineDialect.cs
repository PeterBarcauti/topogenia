// EngineDialect.cs
// The common interface behind which each provider classifies the errors of its engine and opens its transactions (spec 4.3-5, 3.4).

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Topogenia.Persistence.Errors;

/// <summary>
/// What differs from one engine to the other in the unit of work and the loading (spec 4.3-5, S-4.3-01): the reading of
/// the errors of the engine (numbers of SQL Server, codes of SQLite) and the two kinds of transaction. The write
/// transaction of a unit of work; the read transaction of a loading done outside a unit, which must see one consistent
/// state of the database (spec 3.4, faiblesse 10). Each provider implements it (`SqlServerDialect`, `SqliteDialect`).
/// </summary>
public abstract class EngineDialect
{
    /// <summary>The failure of the engine carried by the exception or by one of its inner exceptions, or null.</summary>
    public StoreError? Classify(Exception exception, IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (ClassifyOwn(current, model) is { } error)
                return error;
        }
        return null;
    }

    /// <summary>Opens the write transaction of a unit of work on the connection of the context.</summary>
    public abstract Task<StoreTransaction> BeginWriteAsync(AppDbContext context, CancellationToken cancellationToken);

    /// <summary>Opens a read transaction that sees one consistent state of the database until it ends.</summary>
    public abstract Task<StoreTransaction> BeginReadAsync(AppDbContext context, CancellationToken cancellationToken);

    /// <summary>The failure of the engine carried by this exception itself (not its inner exceptions), or null.</summary>
    protected abstract StoreError? ClassifyOwn(Exception exception, IModel model);
}

/// <summary>
/// A transaction opened by a dialect: the transaction of EF Core and, when the provider opened the connection and the
/// transaction itself (a deferred SQLite transaction), what to release after it. Disposed without a commit, it rolls back.
/// </summary>
public sealed class StoreTransaction : IAsyncDisposable
{
    private readonly IDbContextTransaction transaction;
    private readonly Func<ValueTask>? release;

    public StoreTransaction(IDbContextTransaction transaction, Func<ValueTask>? release = null)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        this.transaction = transaction;
        this.release = release;
    }

    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        if (release is not null)
            await release();
    }
}
