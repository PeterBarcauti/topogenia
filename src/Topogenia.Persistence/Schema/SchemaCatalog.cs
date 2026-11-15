// SchemaCatalog.cs
// What each provider reads and writes in its own catalogue for the history of the schema (spec 4.4); the SQL lives in the providers.

using System.Data.Common;

namespace Topogenia.Persistence.Schema;

/// <summary>
/// The provider side of the history of the schema (ADR 025: the SQL is in the providers). Every method works on an open
/// connection and, when given, inside its transaction: the record of a migration is written in the transaction that
/// applies it.
/// </summary>
public abstract class SchemaCatalog
{
    /// <summary>The rows of `schema_history`, none when the table does not exist yet.</summary>
    public abstract Task<IReadOnlyList<SchemaHistoryEntry>> HistoryAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken);

    /// <summary>Adds a row to `schema_history`.</summary>
    public abstract Task RecordAsync(DbConnection connection, DbTransaction transaction, SchemaHistoryEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// The schema of the application as the catalogue describes it, one line per object (tables, columns and their
    /// types, keys, indexes and their filters, foreign keys, CHECK constraints, views), the two history tables of the
    /// migrations aside: the lines of the schema fingerprint.
    /// </summary>
    public abstract Task<IReadOnlyList<string>> DescribeAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Refuses, with <c>DROITS_INSUFFISANTS</c>, an account that cannot read the histories of the migrations or the data of
    /// an existing database: such an account would see empty histories and take the database for a new one (A-5-10). A
    /// table that does not exist yet is not a refusal. Nothing to check by default (a file has no account).
    /// </summary>
    public virtual Task CheckAccessAsync(DbConnection connection, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>The code of <c>SchemaCodes</c> that a failure of the engine met by the check or the migration stands for, or null.</summary>
    public virtual string? RefusalOf(Exception exception) => null;

    /// <summary>
    /// The numbers of the first failure of the engine in the chain, without its message (which names the login, the
    /// database or the file): what a refusal keeps of it for the logs (spec 5.2, ADR 036). None by default.
    /// </summary>
    public virtual IReadOnlyList<int> NumbersOf(Exception exception) => [];

    /// <summary>
    /// The code of the check of a database that EF Core says does not exist, or null when such a database is simply
    /// behind (a file not created yet). Never used by the migration, which creates the database (ADR 036).
    /// </summary>
    public virtual string? AbsentDatabaseRefusal => null;

    /// <summary>
    /// The mark of the holder of the lock of the migrations, read just before the wait for it, or null when nobody holds
    /// it or when the lock leaves no mark (a lock of a session). None by default.
    /// </summary>
    public virtual Task<string?> LockMarkAsync(DbConnection connection, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    /// <summary>
    /// A lock for the check that only reads, which the engine releases by itself when the process stops, or null to take the
    /// lock of the migrations of EF Core (the default: on SQL Server it is an application lock of the session, gone with
    /// it). A migration that holds or waits for its own lock is never seen in part.
    /// </summary>
    public virtual Task<IAsyncDisposable?> CheckLockAsync(DbConnection connection, CancellationToken cancellationToken) => Task.FromResult<IAsyncDisposable?>(null);

    /// <summary>
    /// After a wait for the lock of the migrations past their delay: true when the lock was left by a holder that stopped,
    /// so that it never goes by itself and the wait is <c>VERROU_MIGRATIONS</c>, not transient; false when a holder may be
    /// alive, the wait being then <c>BASE_OCCUPEE</c> (ADR 041). <paramref name="before"/> is the mark read by
    /// <see cref="LockMarkAsync"/> before the wait. False by default: a lock of a session goes with its holder.
    /// </summary>
    public virtual Task<bool> LockLeftByAStoppedHolderAsync(DbConnection connection, string? before, CancellationToken cancellationToken) => Task.FromResult(false);
}
