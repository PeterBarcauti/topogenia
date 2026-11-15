// IAuditStore.cs
// Port of the audit trail: an event written on its own, the events read by an administrator, the events past the retention removed (spec 5.5).

using Topogenia.Domain.Model;

namespace Topogenia.Application.Ports;

/// <summary>
/// What an administrator asks of the audit trail (`GET /api/admin/audit`): the events of a period, of a project, of a user
/// or of a kind, the newest first, at most <see cref="Limit"/>.
/// </summary>
public sealed record AuditQuery(DateTime? From, DateTime? To, Guid? ProjectId, Guid? UserId, string? Kind, int Limit);

/// <summary>
/// The audit trail outside the change sets (spec 5.5). The events of a change are written with it, in its unit of work
/// (<see cref="Audit.AuditTrail"/>); the others (a purge, whose port runs its own transactions; repeated refusals of
/// access, whose request wrote nothing) are appended here on their own. The trail is only appended to and read; the
/// events past the retention are removed in one statement, never changed (the account of the application holds no
/// `UPDATE` on the table, ADR 032).
/// </summary>
public interface IAuditStore
{
    /// <summary>Writes one event, outside any unit of work.</summary>
    Task AppendAsync(AuditEvent audit, CancellationToken cancellationToken);

    /// <summary>The events of the query, the newest first.</summary>
    Task<IReadOnlyList<AuditEvent>> ReadAsync(AuditQuery query, CancellationToken cancellationToken);

    /// <summary>Removes the events that occurred before <paramref name="limit"/>; returns how many.</summary>
    Task<int> RemoveBeforeAsync(DateTime limit, CancellationToken cancellationToken);
}
