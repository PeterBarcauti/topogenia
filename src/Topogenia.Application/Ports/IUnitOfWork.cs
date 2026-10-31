// IUnitOfWork.cs
// Port of the unit of work of one request: one write transaction, the project lock, the commit (spec 3.4, 4.7).

namespace Topogenia.Application.Ports;

/// <summary>
/// The write transaction of one command (spec 3.4, steps 2 and 5). The Application opens it, takes the lock of the
/// project before any structure operation, loads and writes through <see cref="IWorkspaceStore"/>, then commits;
/// disposing it without a commit rolls everything back (a refused undo is thrown away, AR-69). The persistence implements
/// it (P3-T04): the lock is the portable `structure_version + 1` of spec 4.7, the retries on a deadlock or a busy
/// database (spec 4.3-5) wrap the whole unit, which is why a handler runs its unit through <see cref="RunAsync{T}"/>.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>Opens the write transaction of the request.</summary>
    Task BeginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Serialises the structure operations of a project (spec 4.7): moves its structure version on by one inside the
    /// transaction and returns the new value. Two structure operations on one project never interleave.
    /// </summary>
    Task<int> LockProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Commits the transaction; the unit cannot be used afterwards.</summary>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs the whole unit: opens the write transaction, runs <paramref name="work"/> (lock, load, domain, write), commits.
    /// On a deadlock, a busy database (spec 4.3-5) or a creation lost to a concurrent unit on the natural key of a user, a
    /// translation or a use (ADR 033), everything is rolled back and the whole unit runs again from the start,
    /// three attempts at most, after 50 then 100 ms; the work must therefore do nothing outside the database before it
    /// succeeds (as the 1.x `EnTransaction`). A business refusal is never retried.
    /// </summary>
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
