// StoreError.cs
// What a failure of the database engine means for the application: a guard refused a row, or the engine asks to try again (spec 4.3-5).

namespace Topogenia.Persistence.Errors;

/// <summary>The failures of an engine that the persistence knows; any other failure is left as it is (an internal error).</summary>
public enum StoreErrorKind
{
    /// <summary>A unique index or key refused a row (SQL Server 2601, 2627; SQLite unique or primary key).</summary>
    Unique,

    /// <summary>A foreign key refused a row (SQL Server 547 on an `FK_` constraint; SQLite foreign key).</summary>
    ForeignKey,

    /// <summary>A CHECK constraint refused a row (SQL Server 547 on a `CK_` constraint; SQLite check).</summary>
    Check,

    /// <summary>The transaction was chosen as the victim of a deadlock (SQL Server 1205): it can run again.</summary>
    Deadlock,

    /// <summary>The database is busy or locked by another connection (SQLite `SQLITE_BUSY`, `SQLITE_LOCKED`): it can run again.</summary>
    Busy,

    /// <summary>
    /// A record that the unit creates or updates by its natural key (a user by its login, a translation, a use) was created
    /// by a concurrent unit since the loading: run again, the unit finds it and updates it (ADR 033, 1.x `MERGE ... HOLDLOCK`).
    /// </summary>
    CreationRace,
}

/// <summary>
/// A classified failure of the engine, with the name of the guard when the engine gives it (`docs/modele-donnees.md` § 5:
/// `UX_`, `UQ_`, `PK_`, `FK_`, `CK_`). SQLite names no foreign key.
/// </summary>
public sealed record StoreError(StoreErrorKind Kind, string? Constraint)
{
    /// <summary>True for a deadlock, a busy database or a lost creation race: the whole unit of work runs again (spec 4.3-5, ADR 033).</summary>
    public bool IsTransient => Kind is StoreErrorKind.Deadlock or StoreErrorKind.Busy or StoreErrorKind.CreationRace;
}
