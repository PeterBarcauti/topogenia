// OperationLogStore.cs
// Adapter of the operation log port: entries read and written in the transaction of the unit, under the lock of their project (spec 4.6).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;

namespace Topogenia.Persistence.Writing;

/// <summary>
/// <see cref="IOperationLog"/> on a <see cref="StoreSession"/>. Every write runs inside the open unit of work, and the
/// entries of a user are also read outside it by the read of a map (its history, annex B 2.6): the entries
/// are written through <see cref="ChangeSetWriter"/>, like any record without version, so that an update or a removal
/// happens only under the lock of the project of the entry and against the entry as it was read (ADR 030), and a second
/// entry with the same sequence on a map is refused by its unique index (`CONFLIT_UNICITE`). A removal deletes whole
/// chains: EF Core orders the deletes of entries that refer to each other by `redone_from_id`.
/// </summary>
public sealed class OperationLogStore : IOperationLog
{
    private readonly StoreSession session;

    public OperationLogStore(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    public async Task<int> NextSequenceAsync(Guid mapId, CancellationToken cancellationToken)
    {
        var highest = await Entries().Where(entry => entry.MapId == mapId).MaxAsync(entry => (int?)entry.Sequence, cancellationToken);
        return highest is { } sequence ? sequence + 1 : 0;
    }

    /// <summary>Inside the unit under the lock for the template; outside, in a read transaction of its own for a read (ADR 031).</summary>
    public async Task<IReadOnlyList<OperationLogEntry>> EntriesAsync(Guid mapId, Guid userId, CancellationToken cancellationToken) =>
        await ConsistentRead.RunAsync(session, context => context.Set<OperationLogEntry>().AsNoTracking()
            .Where(entry => entry.MapId == mapId && entry.UserId == userId)
            .OrderByDescending(entry => entry.Sequence).ToListAsync(cancellationToken), cancellationToken);

    public Task AppendAsync(OperationLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return WriteAsync([new RecordChange(ChangeKind.Created, null, entry)], cancellationToken);
    }

    public async Task UpdateAsync(OperationLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var stored = await Entries().SingleOrDefaultAsync(row => row.Id == entry.Id, cancellationToken) ?? throw DomainException.NotFound();
        if (stored.ProjectId != entry.ProjectId || stored.MapId != entry.MapId || stored.Sequence != entry.Sequence)
            throw new InvalidOperationException("An entry of the operation log keeps its project, its map and its sequence.");
        await WriteAsync([new RecordChange(ChangeKind.Modified, stored, entry)], cancellationToken);
    }

    public async Task<int> RemoveAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entryIds);
        var removed = new List<OperationLogEntry>();
        foreach (var chunk in Chunks.Of(entryIds.Distinct()))
            removed.AddRange(await Entries().Where(entry => chunk.Contains(entry.Id)).ToListAsync(cancellationToken));
        await WriteAsync(removed.Select(entry => new RecordChange(ChangeKind.Removed, entry, null)).ToList(), cancellationToken);
        return removed.Count;
    }

    private IQueryable<OperationLogEntry> Entries() => OpenContext().Set<OperationLogEntry>().AsNoTracking();

    private Task WriteAsync(IReadOnlyList<RecordChange> changes, CancellationToken cancellationToken) =>
        ChangeSetWriter.WriteAsync(OpenContext(), session.Dialect, new ChangeSet(changes), session.LockedProjects, cancellationToken);

    private AppDbContext OpenContext() =>
        session.InUnit ? session.Context : throw new InvalidOperationException("The operation log is read and written inside a unit of work.");
}
