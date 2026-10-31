// StoreRefusals.cs
// Translation of a refusal of the engine into the stable codes of the domain: CONFLIT_UNICITE, CONFLIT_INTEGRITE, CONFLIT_VERSION (spec 4.3-5).

using Topogenia.Domain.Errors;

namespace Topogenia.Persistence.Errors;

/// <summary>
/// A guard of the database that refuses a row is a conflict with the current state (409), never an internal error
/// (A-4-53, AR-80): a unique index or key gives `CONFLIT_UNICITE` (a race between two users, A-2.16-11), a foreign key or
/// a CHECK gives `CONFLIT_INTEGRITE` (the map changed between the reading and the writing; a CHECK refused means that a
/// rule of the domain was not applied, ADR 030). The details name the guard (`constraint`), null when the engine does
/// not give it. A version that changed since the loading gives `CONFLIT_VERSION` with who changed it and when (A-2.16-06).
/// </summary>
public static class StoreRefusals
{
    public const string ConstraintDetail = "constraint";
    public const string ObjectDetail = "object";
    public const string UpdatedByDetail = "updated_by";
    public const string UpdatedAtDetail = "updated_at";

    /// <summary>The business refusal of a guard, or null for a failure that is not a refusal (deadlock, busy).</summary>
    public static DomainException? Of(StoreError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var details = new Dictionary<string, object?> { [ConstraintDetail] = error.Constraint };
        return error.Kind switch
        {
            StoreErrorKind.Unique => DomainException.Conflict(ErrorCodes.UniqueConflict, details),
            StoreErrorKind.ForeignKey or StoreErrorKind.Check => DomainException.Conflict(ErrorCodes.IntegrityConflict, details),
            _ => null,
        };
    }

    /// <summary>`CONFLIT_VERSION` on a record of the given type, with the author and the time of the change that blocks.</summary>
    public static DomainException VersionConflict(string objectType, Guid? updatedBy, DateTime? updatedAt) =>
        DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
        {
            [ObjectDetail] = objectType,
            [UpdatedByDetail] = updatedBy,
            [UpdatedAtDetail] = updatedAt,
        });
}
