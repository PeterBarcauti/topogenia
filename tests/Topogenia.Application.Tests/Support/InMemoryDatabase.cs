// InMemoryDatabase.cs
// In-memory fakes of the ports of the persistence for the handler tests: records, transaction, lock, log and access.

using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Support;

/// <summary>
/// The records of the projects and the operation log, with a transaction that is rolled back unless committed, and the
/// order of the calls of the template (`Calls`), so that the tests check the order of the steps (spec 3.4).
/// </summary>
public sealed class InMemoryDatabase
{
    private Dictionary<RecordKey, IRecord> records = [];
    private List<OperationLogEntry> entries = [];
    private Dictionary<RecordKey, IRecord>? savedRecords;
    private List<OperationLogEntry>? savedEntries;

    public List<string> Calls { get; } = [];

    public List<WorkspaceContent> LoadedContents { get; } = [];

    /// <summary>Failures to throw at the commit of the next units, as a deadlock would (the work then runs again).</summary>
    public Queue<Exception> CommitFailures { get; } = new();

    public int Commits { get; private set; }

    public IReadOnlyList<OperationLogEntry> Entries => entries;

    public void Add(params IRecord[] added)
    {
        foreach (var record in added)
            records[record.Key] = record;
    }

    public T Get<T>(RecordKey key)
        where T : class, IRecord => (T)records[key];

    public IEnumerable<T> All<T>() => records.Values.OfType<T>();

    public void AddEntry(OperationLogEntry entry) => entries.Add(entry);

    /// <summary>Removes the records that match, outside any unit (the removal of the audit past its retention).</summary>
    public int RemoveWhere<T>(Func<T, bool> match)
        where T : IRecord
    {
        var keys = records.Values.OfType<T>().Where(match).Select(record => record.Key).ToList();
        foreach (var key in keys)
            records.Remove(key);
        return keys.Count;
    }

    internal bool InTransaction => savedRecords is not null;

    internal void Begin()
    {
        if (InTransaction)
            throw new InvalidOperationException("A unit is already open.");
        savedRecords = new Dictionary<RecordKey, IRecord>(records);
        savedEntries = [.. entries];
    }

    internal void Commit()
    {
        EnsureTransaction();
        if (CommitFailures.TryDequeue(out var failure))
            throw failure;
        savedRecords = null;
        savedEntries = null;
        Commits++;
    }

    internal void Rollback()
    {
        if (!InTransaction)
            return;
        records = savedRecords!;
        entries = savedEntries!;
        savedRecords = null;
        savedEntries = null;
    }

    internal int Lock(Guid projectId)
    {
        EnsureTransaction();
        Calls.Add("lock");
        if (!records.TryGetValue(Project.KeyOf(projectId), out var found))
            throw DomainException.NotFound();
        var project = (Project)found;
        var locked = project with { StructureVersion = project.StructureVersion + 1 };
        records[locked.Key] = locked;
        return locked.StructureVersion;
    }

    /// <summary>Loads inside the unit (commands) or outside it (queries, in a read of their own).</summary>
    internal ProjectWorkspace? Load(Guid projectId, WorkspaceContent content)
    {
        Calls.Add("load");
        LoadedContents.Add(content);
        if (!records.ContainsKey(Project.KeyOf(projectId)))
            return null;
        return new ProjectWorkspace(projectId, records.Values.Where(record => record is not AppUser));
    }

    /// <summary>Writes a change set as the persistence does: a modified record must still have the version it had before (D06).</summary>
    internal void Save(ChangeSet changes)
    {
        EnsureTransaction();
        Calls.Add("save");
        foreach (var change in changes.Changes)
        {
            if (change.Before is IVersionedRecord before && records.TryGetValue(change.Key, out var stored)
                && stored is IVersionedRecord current && current.Version != before.Version)
                throw DomainException.Conflict(ErrorCodes.VersionConflict);
            if (change.After is null)
                records.Remove(change.Key);
            else
                records[change.Key] = change.After;
        }
    }

    internal List<OperationLogEntry> Log
    {
        get
        {
            EnsureTransaction();
            return entries;
        }
    }

    private void EnsureTransaction()
    {
        if (!InTransaction)
            throw new InvalidOperationException("No unit of work is open.");
    }
}

/// <summary>The unit of work over the in-memory database: rolled back on failure, run again after a failed commit.</summary>
public sealed class InMemoryUnitOfWork(InMemoryDatabase database) : IUnitOfWork
{
    public int Attempts { get; private set; }

    public Task BeginAsync(CancellationToken cancellationToken)
    {
        database.Begin();
        return Task.CompletedTask;
    }

    public Task<int> LockProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(database.Lock(projectId));

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        database.Commit();
        return Task.CompletedTask;
    }

    /// <summary>A failure of the commit marked transient (a <see cref="TimeoutException"/>) runs the whole work again, as ADR 030.</summary>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            Attempts = attempt;
            await BeginAsync(cancellationToken);
            try
            {
                var result = await work(cancellationToken);
                await CommitAsync(cancellationToken);
                return result;
            }
            catch (TimeoutException) when (attempt < 3)
            {
                database.Rollback();
            }
            catch
            {
                database.Rollback();
                throw;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        database.Rollback();
        return ValueTask.CompletedTask;
    }
}

public sealed class InMemoryWorkspaceStore(InMemoryDatabase database) : IWorkspaceStore
{
    public Task<ProjectWorkspace?> LoadAsync(Guid projectId, WorkspaceContent content, CancellationToken cancellationToken) =>
        Task.FromResult(database.Load(projectId, content));

    public Task SaveAsync(ChangeSet changes, CancellationToken cancellationToken)
    {
        database.Save(changes);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryOperationLog(InMemoryDatabase database) : IOperationLog
{
    public Task<int> NextSequenceAsync(Guid mapId, CancellationToken cancellationToken)
    {
        var sequences = database.Log.Where(entry => entry.MapId == mapId).Select(entry => entry.Sequence).ToList();
        return Task.FromResult(sequences.Count == 0 ? 0 : sequences.Max() + 1);
    }

    /// <summary>Read inside the unit (commands) or outside it (the history of a map read).</summary>
    public Task<IReadOnlyList<OperationLogEntry>> EntriesAsync(Guid mapId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OperationLogEntry>>(database.Entries.Where(entry => entry.MapId == mapId && entry.UserId == userId)
            .OrderByDescending(entry => entry.Sequence).ToList());

    public Task AppendAsync(OperationLogEntry entry, CancellationToken cancellationToken)
    {
        database.Calls.Add("log");
        if (database.Log.Any(other => other.Id == entry.Id || (other.MapId == entry.MapId && other.Sequence == entry.Sequence)))
            throw DomainException.Conflict(ErrorCodes.UniqueConflict);
        database.Log.Add(entry);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(OperationLogEntry entry, CancellationToken cancellationToken)
    {
        var index = database.Log.FindIndex(other => other.Id == entry.Id);
        database.Log[index] = entry;
        return Task.CompletedTask;
    }

    /// <summary>Refuses, as the foreign key `redone_from_id` does, a removal that leaves an entry pointing to a removed one.</summary>
    public Task<int> RemoveAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken)
    {
        var removed = database.Log.RemoveAll(entry => entryIds.Contains(entry.Id));
        if (database.Log.Any(entry => entry.RedoneFromId is { } from && entryIds.Contains(from)))
            throw DomainException.Conflict(ErrorCodes.IntegrityConflict);
        return Task.FromResult(removed);
    }
}

/// <summary>Finds the project of a target in the records, and the role of the user from the members (annex A 1).</summary>
public sealed class InMemoryAccessReader(InMemoryDatabase database) : IAccessReader
{
    public Task<ProjectAccess?> FindAsync(AccessTarget target, Guid userId, CancellationToken cancellationToken)
    {
        database.Calls.Add("access");
        var projectId = ProjectOf(target);
        if (projectId is not { } id || database.All<Project>().SingleOrDefault(project => project.Id == id) is not { } project)
            return Task.FromResult<ProjectAccess?>(null);
        var role = database.All<ProjectMember>().SingleOrDefault(member => member.ProjectId == id && member.UserId == userId)?.Role;
        return Task.FromResult<ProjectAccess?>(new ProjectAccess(id, project.DeletedAt is not null, role));
    }

    private Guid? ProjectOf(AccessTarget target) => target.Kind switch
    {
        TargetKind.Project => target.Id,
        TargetKind.Map => MapProject(target.Id),
        TargetKind.Lane => database.All<Lane>().SingleOrDefault(lane => lane.Id == target.Id) is { } lane ? MapProject(lane.MapId) : null,
        TargetKind.Step => database.All<Step>().SingleOrDefault(step => step.Id == target.Id) is { } step ? MapProject(step.MapId) : null,
        TargetKind.Flow => database.All<Flow>().SingleOrDefault(flow => flow.Id == target.Id) is { } flow ? MapProject(flow.MapId) : null,
        TargetKind.Dissolution => database.All<Dissolution>().SingleOrDefault(item => item.Id == target.Id) is { } frame
            ? MapProject(frame.MapId) : null,
        _ => null,
    };

    private Guid? MapProject(Guid mapId) => database.All<ProcessMap>().SingleOrDefault(map => map.Id == mapId)?.ProjectId;
}

public sealed class FixedClock(DateTime now) : IClock
{
    public DateTime UtcNow { get; set; } = now;
}

/// <summary>The audit trail of an in-memory database: an event appended outside any unit, as the adapter does.</summary>
public sealed class InMemoryAuditStore(InMemoryDatabase database) : IAuditStore
{
    public Task AppendAsync(AuditEvent audit, CancellationToken cancellationToken)
    {
        if (database.InTransaction)
            throw new InvalidOperationException("An audit event of a unit of work is written with its change set.");
        database.Add(audit);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> ReadAsync(AuditQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>([.. database.All<AuditEvent>()
            .Where(audit => (query.From is null || audit.At >= query.From) && (query.To is null || audit.At < query.To)
                && (query.ProjectId is null || audit.ProjectId == query.ProjectId) && (query.UserId is null || audit.UserId == query.UserId)
                && (query.Kind is null || audit.Kind == query.Kind))
            .OrderByDescending(audit => audit.At).ThenBy(audit => audit.Kind, StringComparer.Ordinal).Take(query.Limit)]);

    public Task<int> RemoveBeforeAsync(DateTime limit, CancellationToken cancellationToken) =>
        Task.FromResult(database.RemoveWhere<AuditEvent>(audit => audit.At < limit));
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public CurrentUser? User { get; set; }
}

/// <summary>The If-Match the mode requires: a request lacks it when <see cref="IsMissing"/> is set (the server mode without the header).</summary>
public sealed class FakeVersionRequirement : IVersionRequirement
{
    public bool IsMissing { get; set; }
}
