// AuditApi.cs
// The route of the audit trail turned into its request: the events read by an administrator (spec 5.5).

using Topogenia.Application.Audit;
using Topogenia.Application.Commands;

namespace Topogenia.Host.Api.Audit;

/// <summary>One event of the audit trail as an administrator reads it: identifiers, kind, time, details (spec 5.5).</summary>
public sealed record AuditEntry(Guid Id, DateTime OccurredAt, Guid? UserId, string Kind, Guid? ProjectId, string? Details);

/// <summary>`GET /api/admin/audit`: the events, the newest first (<see cref="ReadAuditQuery"/>).</summary>
public sealed class AuditApi(UserRunner users)
{
    public async Task<IReadOnlyList<AuditEntry>> ReadAsync(DateTime? from, DateTime? to, Guid? projectId, Guid? userId, string? kind, int? limit,
        CancellationToken cancellationToken)
    {
        var events = await users.RunAsync(new ReadAuditQuery(from, to, projectId, userId, kind, limit), cancellationToken);
        return [.. events.Select(audit => new AuditEntry(audit.Id, audit.At, audit.UserId, audit.Kind, audit.ProjectId, audit.Details))];
    }
}
