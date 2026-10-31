// UnitOfWork.cs
// Adapter of the unit of work: write transaction, portable project lock (structure_version + 1), commit, retried whole (spec 4.3-5, 4.7).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Errors;

namespace Topogenia.Persistence.Writing;

/// <summary>
/// <see cref="IUnitOfWork"/> on a <see cref="StoreSession"/>. The lock of a project is the statement of spec 4.7,
/// `UPDATE project SET structure_version = structure_version + 1 WHERE id = @id`, written by EF Core without any hint
/// proper to an engine: it takes the write lock of the row of the project in SQL Server and the write lock of the
/// database in SQLite (whose write transaction is immediate), so that two structure operations on one project never
/// interleave (A-2.16-08); the unit remembers the projects it locked, the only ones whose records without version it
/// may modify or remove (<see cref="UnversionedCheck"/>, ADR 030). A refusal of a guard of the database becomes a code of the domain (<see cref="StoreRefusals"/>);
/// a deadlock, a busy database or a lost creation race (ADR 033) makes <see cref="RunAsync{T}"/> run the whole unit again
/// (spec 4.3-5).
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly StoreSession session;

    public UnitOfWork(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (session.InUnit)
            throw new InvalidOperationException("A unit of work is already open on this session.");
        await session.ResetAsync();
        session.ForgetCommits();
        session.Transaction = await Translated(() => session.Dialect.BeginWriteAsync(session.Context, cancellationToken));
    }

    public async Task<int> LockProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var context = OpenContext();
        var project = context.Set<Project>().Where(record => record.Id == projectId);
        var locked = await Translated(() => project.ExecuteUpdateAsync(
            row => row.SetProperty(record => record.StructureVersion, record => record.StructureVersion + 1), cancellationToken));
        if (locked == 0)
            throw DomainException.NotFound();
        session.LockedProjects.Add(projectId);
        return await project.Select(record => record.StructureVersion).SingleAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var transaction = session.Transaction ?? throw new InvalidOperationException("No unit of work is open on this session.");
        await Translated(async () =>
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
        await session.ResetAsync();
    }

    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        return session.Retry.RunAsync(Retried, async () =>
        {
            try
            {
                await BeginAsync(cancellationToken);
                var result = await work(cancellationToken);
                await CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                // Rolled back at once; the context stays for the classification of the failure, then the retry resets it.
                if (session.Transaction is not null)
                {
                    var transaction = session.Transaction;
                    session.Transaction = null;
                    await transaction.DisposeAsync();
                }
                throw;
            }
        }, session.ResetAsync, cancellationToken);
    }

    /// <summary>
    /// What makes the whole unit run again: a deadlock or a busy database (spec 4.3-5), or a creation lost to a concurrent
    /// unit on the natural key of a record created or updated by its key (<see cref="CreationRaces"/>, ADR 033). Never a
    /// failure met once the engine committed the unit (the clean-up of its transaction, the closing of its connection):
    /// it is thrown as it is and the work is never done twice (data B1 of round 4).
    /// </summary>
    private StoreError? Retried(Exception failure) =>
        session.Committed ? null : session.Classify(failure) ?? CreationRaces.Of(failure, session.Context.Model);

    /// <summary>Disposed without a commit, the unit rolls everything back (AR-69).</summary>
    public ValueTask DisposeAsync() => session.ResetAsync();

    private AppDbContext OpenContext() =>
        session.InUnit ? session.Context : throw new InvalidOperationException("No unit of work is open on this session.");

    /// <summary>Runs a statement of the unit and turns a refusal of a guard into its code; any other failure is thrown as it is.</summary>
    private async Task<T> Translated<T>(Func<Task<T>> statement)
    {
        try
        {
            return await statement();
        }
        catch (Exception failure) when (session.Classify(failure) is { } error && StoreRefusals.Of(error) is { } refusal)
        {
            throw refusal;
        }
    }
}
