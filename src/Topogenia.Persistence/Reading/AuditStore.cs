// AuditStore.cs
// Adapter of the port of the audit trail: an event appended in a unit of its own, the events read by LINQ, those past the retention removed (spec 5.5).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Reading;

/// <summary>
/// <see cref="IAuditStore"/> on a <see cref="StoreSession"/>, by LINQ only: an event is written as the creation of a
/// change set in a unit of work of its own (never inside another unit: the events of a change go with its change set);
/// the reading is one consistent read, the newest first, then by kind (never by identifier, whose order differs between
/// the engines); the events past the retention go in one statement of their own unit, the only removal the trail knows
/// (spec 5.5).
/// </summary>
public sealed class AuditStore : IAuditStore
{
    private readonly StoreSession session;

    public AuditStore(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    public Task AppendAsync(AuditEvent audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        if (session.InUnit)
            throw new InvalidOperationException("An audit event of a unit of work is written with its change set.");
        return new UnitOfWork(session).RunAsync(async token =>
        {
            await ChangeSetWriter.WriteAsync(session.Context, session.Dialect, new ChangeSet([new RecordChange(ChangeKind.Created, null, audit)]),
                session.LockedProjects, token);
            return true;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<AuditEvent>> ReadAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ConsistentRead.RunAsync<IReadOnlyList<AuditEvent>>(session, async context =>
        {
            var events = context.Set<AuditEvent>().AsNoTracking();
            if (query.From is { } from)
                events = events.Where(audit => audit.At >= from);
            if (query.To is { } to)
                events = events.Where(audit => audit.At < to);
            if (query.ProjectId is { } projectId)
                events = events.Where(audit => audit.ProjectId == projectId);
            if (query.UserId is { } userId)
                events = events.Where(audit => audit.UserId == userId);
            if (query.Kind is { } kind)
                events = events.Where(audit => audit.Kind == kind);
            return await events.OrderByDescending(audit => audit.At).ThenBy(audit => audit.Kind).Take(query.Limit).ToListAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<int> RemoveBeforeAsync(DateTime limit, CancellationToken cancellationToken) =>
        new UnitOfWork(session).RunAsync(token => session.Context.Set<AuditEvent>().Where(audit => audit.At < limit).ExecuteDeleteAsync(token),
            cancellationToken);
}
