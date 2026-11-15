// ConsistentRead.cs
// Runs a reading of several queries in one consistent transaction: the one of the unit of work, or a read transaction of its own (spec 3.4).

using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Loading;

/// <summary>
/// A reading made of several queries sees one state of the database (spec 3.4, A-5-11, faiblesse 10 of the 1.x, whose
/// `EnLecture` opened no transaction). Inside a unit of work, it runs in the write transaction of the unit, after the
/// lock of the project. Outside, it runs in a read transaction of the dialect (ADR 031), retried whole on a deadlock or a
/// busy database like a unit (spec 4.3-5), then ended.
/// </summary>
internal static class ConsistentRead
{
    public static async Task<T> RunAsync<T>(StoreSession session, Func<AppDbContext, Task<T>> read, CancellationToken cancellationToken)
    {
        if (session.InUnit)
            return await read(session.Context);
        return await session.Retry.RunAsync(session.Classify, async () =>
        {
            await session.ResetAsync();
            var context = session.Context;
            await using var transaction = await session.Dialect.BeginReadAsync(context, cancellationToken);
            var result = await read(context);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }, session.ResetAsync, cancellationToken);
    }
}
