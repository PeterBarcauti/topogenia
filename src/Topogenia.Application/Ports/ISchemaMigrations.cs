// ISchemaMigrations.cs
// Port of the schema of the database: state against the migrations of the application, migration with fingerprints (spec 4.4).

namespace Topogenia.Application.Ports;

/// <summary>
/// Up to date: every migration of the application is applied; behind: some are still to apply (a new SQLite file too;
/// SQL Server refuses an absent database as unreachable, ADR 036).
/// </summary>
public enum SchemaStatus
{
    UpToDate,
    Behind,
}

/// <summary>The migrations of the application applied to the database and those still to apply, in their order.</summary>
public sealed record SchemaState(SchemaStatus Status, IReadOnlyList<string> Applied, IReadOnlyList<string> Pending);

/// <summary>
/// The schema of the database against the migrations of the application (spec 4.4). Each applied migration is recorded
/// with the fingerprint of its source and the fingerprint of the schema it left; both are checked before any use. A
/// database newer than the application, an applied migration that changed since, an applied migration without its
/// fingerprints or a schema changed outside the migrations is refused with <see cref="SchemaRefusedException"/>. The
/// host decides what a database behind means: migrated after a backup in desktop mode, refused in server mode until
/// `--migrate` (spec 4.4, 6.3).
/// </summary>
public interface ISchemaMigrations
{
    /// <summary>Reads the state of the schema without changing anything; refuses a database it must not use.</summary>
    Task<SchemaState> CheckAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies the pending migrations (creating the database when it does not exist yet), each recorded with its
    /// fingerprints in the transaction that applies it, after the same refusals as <see cref="CheckAsync"/> except for an
    /// absent database, which it creates; two processes never migrate at the same time. Returns the state afterwards,
    /// checked again.
    /// </summary>
    Task<SchemaState> MigrateAsync(CancellationToken cancellationToken);
}
