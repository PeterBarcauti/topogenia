// ReadAuditQuery.cs
// `GET /api/admin/audit`: the audit trail read by an administrator, the newest first, by period, project, user or kind (spec 5.5).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Audit;

/// <summary>
/// The reading of the audit trail, reserved to the administrator (spec 5.5): ACCES_REFUSE for anyone else before anything
/// is read. A period ends before its start, or a limit outside 1 to <see cref="MaxLimit"/>, is VALEUR_INVALIDE on its
/// field; without a limit, <see cref="DefaultLimit"/> events.
/// </summary>
[RequiresAdmin]
public sealed class ReadAuditQuery(DateTime? from, DateTime? to, Guid? projectId, Guid? userId, string? kind, int? limit)
    : UserRequest<IReadOnlyList<AuditEvent>>
{
    public const int DefaultLimit = 200;

    public const int MaxLimit = 1000;

    public override async Task<IReadOnlyList<AuditEvent>> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (limit is < 1 or > MaxLimit)
            throw DomainException.Invalid(ErrorCodes.InvalidValue, "limit");
        if (from is { } start && to is { } end && end < start)
            throw DomainException.Invalid(ErrorCodes.InvalidValue, "to");
        var query = new Ports.AuditQuery(from, to, projectId, userId, string.IsNullOrWhiteSpace(kind) ? null : kind.Trim(), limit ?? DefaultLimit);
        return await scope.Ports.Audit.ReadAsync(query, cancellationToken);
    }
}
