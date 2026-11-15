// VersionCheck.cs
// The If-Match of a request: the version a client read of one record, checked before the domain runs (spec 4.7, annex A 2.16).

using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Commands;

/// <summary>
/// The version a client read of a record (`If-Match`, D06). The template checks it on the workspace loaded under the lock
/// of the project, before the domain runs: a record changed since is refused with CONFLIT_VERSION and the author and time
/// of its last change (annex A 2.16; the host turns them into « by whom, how long ago »). The version <see cref="NoRecord"/>
/// says that the client read no record (a use of a document not ticked yet): accepted while the record does not exist,
/// a conflict once someone created it (ADR 045).
/// </summary>
public sealed class VersionCheck
{
    private readonly Func<ProjectWorkspace, IVersionedRecord?> find;
    private readonly bool goneIsReached;

    private VersionCheck(RecordKey key, int expected, Func<ProjectWorkspace, IVersionedRecord?> find, bool goneIsReached = false)
    {
        Key = key;
        Expected = expected;
        this.find = find;
        this.goneIsReached = goneIsReached;
    }

    /// <summary>The version of a record the client did not read, since it did not exist: the records start at 1.</summary>
    public const int NoRecord = 0;

    public RecordKey Key { get; }

    public int Expected { get; }

    /// <summary>VERSION_REQUISE (428) when the request lacks the version its mode requires (<see cref="IVersionRequirement"/>).</summary>
    public static void EnsureRead(IVersionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (requirement.IsMissing)
            throw new DomainException(ErrorCodes.VersionRequired, RefusalKind.PreconditionRequired);
    }

    /// <summary>The expected version of a record of type <typeparamref name="T"/>.</summary>
    public static VersionCheck Of<T>(RecordKey key, int expected)
        where T : class, IVersionedRecord => new(key, expected, workspace => workspace.Find<T>(key));

    /// <summary>
    /// The expected version of a record the request erases: a record already gone is the state asked, whatever version the
    /// client read (AR-107); a record still there is checked as by <see cref="Of{T}"/>.
    /// </summary>
    public static VersionCheck OfErasure<T>(RecordKey key, int expected)
        where T : class, IVersionedRecord => new(key, expected, workspace => workspace.Find<T>(key), goneIsReached: true);

    /// <summary>
    /// INTROUVABLE when the record is not in the workspace (unless the client read none, or the request erases it),
    /// CONFLIT_VERSION when its version moved on.
    /// </summary>
    public void Ensure(ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var record = find(workspace);
        if (record is null && (Expected == NoRecord || goneIsReached))
            return;
        if (record is null)
            throw DomainException.NotFound();
        if (record.Version == Expected)
            return;
        throw DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
        {
            ["object"] = Key.Type,
            ["updated_by"] = record.UpdatedBy ?? (record as ICreationAudited)?.CreatedBy,
            ["updated_at"] = record.UpdatedAt ?? (record as ICreationAudited)?.CreatedAt,
        });
    }
}
