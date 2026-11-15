// FailingOpenings.cs
// Openings of the connections of a context that fail as a loaded instance makes them fail, so that the code under test meets the failure as it meets a real one.

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// Makes the next <paramref name="times"/> openings of a connection fail with the failure that <paramref name="failure"/>
/// builds, before the driver reaches the server; the openings after go through. Given to the code under test (for instance
/// <see cref="Topogenia.Persistence.Schema.SchemaMigrations"/>), the failure goes up through its own translation exactly
/// as one of the instance would: the tests of the setup see what the setup really receives, never a failure built by hand.
/// </summary>
public sealed class FailingOpenings(Func<Exception> failure, int times) : DbConnectionInterceptor
{
    private int left = times;
    private int failed;

    /// <summary>The openings made to fail so far.</summary>
    public int Failed => Volatile.Read(ref failed);

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        Fails() ? throw failure() : result;

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
        Fails() ? throw failure() : ValueTask.FromResult(result);

    private bool Fails()
    {
        if (Interlocked.Decrement(ref left) < 0)
            return false;
        Interlocked.Increment(ref failed);
        return true;
    }
}
