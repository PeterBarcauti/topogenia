// DesktopDatabaseStartup.cs
// Start of the data file in desktop mode (spec 4.4, 5.2): check, backup before migration when behind, migration, daily backup.

using Topogenia.Application.Ports;

namespace Topogenia.Persistence.Sqlite.Startup;

/// <summary>What the start did: the schema afterwards, the backup before migration (null when none was needed), the daily backup.</summary>
public sealed record DesktopStartupReport(SchemaState Schema, BackupResult? BeforeMigration, BackupResult Daily);

/// <summary>The backup before a migration failed: the migration was not started and the application must stop (AR-14).</summary>
public sealed class MigrationBackupFailedException : Exception
{
    /// <summary>The failure of <paramref name="result"/> (codes only) in the message; the exception of the backup, if any, as inner exception.</summary>
    public MigrationBackupFailedException(BackupResult result)
        : base($"The backup before the migration failed, the database was not migrated: {result?.Failure}", result?.Cause)
    {
        Result = result;
    }

    public MigrationBackupFailedException()
    {
    }

    /// <summary>The failed backup, with its code (<see cref="Backup.BackupFailures"/>).</summary>
    public BackupResult? Result { get; }
}

/// <summary>
/// The sequence that the host of the desktop mode runs before serving (P4-T05), on the two ports only:
/// 1. <see cref="ISchemaMigrations.CheckAsync"/>: a database that must not be used is refused, nothing is written;
/// 2. a database behind that already holds migrations is backed up (<see cref="BackupReason.BeforeMigration"/>); if the
///    backup fails, <see cref="MigrationBackupFailedException"/> is thrown and the migration is not started (AR-14,
///    A-4-32); a new data file (no migration applied) has nothing to back up;
/// 3. the pending migrations are applied (<see cref="ISchemaMigrations.MigrateAsync"/>, which checks again);
/// 4. the daily backup is made, skipped when the one of the day exists; its failure does not stop the start (A-4-32):
///    it is in the report, for the log.
/// The server mode does not run it: a database behind is refused there until `--migrate` (spec 4.4, 6.3).
/// </summary>
public sealed class DesktopDatabaseStartup
{
    private readonly ISchemaMigrations migrations;
    private readonly IBackupService backups;

    public DesktopDatabaseStartup(ISchemaMigrations migrations, IBackupService backups)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(backups);
        this.migrations = migrations;
        this.backups = backups;
    }

    public async Task<DesktopStartupReport> RunAsync(CancellationToken cancellationToken)
    {
        var state = await migrations.CheckAsync(cancellationToken);
        BackupResult? before = null;
        if (state.Status == SchemaStatus.Behind)
        {
            if (state.Applied.Count > 0)
            {
                before = await backups.BackupAsync(BackupReason.BeforeMigration, cancellationToken);
                if (!before.Done)
                    throw new MigrationBackupFailedException(before);
            }
            state = await migrations.MigrateAsync(cancellationToken);
        }
        var daily = await backups.BackupAsync(BackupReason.Daily, cancellationToken);
        return new DesktopStartupReport(state, before, daily);
    }
}
