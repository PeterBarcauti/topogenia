// FailingCommit.cs
// Makes the commit of a given rank fail, as a crash of the process or of the server would in the middle of a purge.

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Purge;

/// <summary>
/// The commit number <paramref name="rank"/> (from 1) of the contexts that carry this interceptor fails with a
/// <see cref="SimulatedEngineFailure"/>, which no dialect classifies: the unit of work does not retry it, the transaction
/// rolls back, the commits before it stay.
/// </summary>
internal sealed class FailingCommit(int rank) : DbTransactionInterceptor
{
    private int commits;

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
        Interlocked.Increment(ref commits) == rank ? throw new SimulatedEngineFailure() : ValueTask.FromResult(result);
}
