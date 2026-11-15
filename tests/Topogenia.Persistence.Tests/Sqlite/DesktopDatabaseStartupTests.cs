// DesktopDatabaseStartupTests.cs
// Start of the data file of the desktop mode: a database behind is backed up, then migrated; a failed backup stops it (AR-14).

using Topogenia.Application.Ports;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite.Startup;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// The sequence of spec 4.4 and 5.2 in desktop mode, on real files: check of the schema, backup before migration when an
/// existing database is behind, migration, then the daily backup. A database behind is made by a schema port that
/// reports one more migration to apply than the data file holds (the application has a single migration so far).
/// </summary>
public sealed class DesktopDatabaseStartupTests
{
    private static readonly DateTime Tuesday = new(2026, 10, 20, 7, 0, 0, DateTimeKind.Utc);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_data_file_is_created_by_the_migration_with_no_backup_before_and_gets_its_daily_backup()
    {
        await using var files = new SqliteFiles();
        var startup = new DesktopDatabaseStartup(new SchemaMigrations(files.Engine, new SettableClock(Tuesday)), files.Backups(new SettableClock(Tuesday)));

        var report = await startup.RunAsync(Token);

        Assert.Equal(SchemaStatus.UpToDate, report.Schema.Status);
        Assert.Null(report.BeforeMigration);
        Assert.True(report.Daily.Done);
        Assert.Equal(["data_2_tuesday.db"], files.BackupFiles());
    }

    [Fact]
    public async Task An_up_to_date_database_is_not_migrated_and_its_daily_backup_is_made_once_a_day()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var clock = new SettableClock(Tuesday);
        var migrations = new ScriptedMigrations(new SchemaMigrations(files.Engine, clock), behind: false);
        var startup = new DesktopDatabaseStartup(migrations, files.Backups(clock));

        var first = await startup.RunAsync(Token);
        clock.UtcNow = Tuesday.AddHours(3);
        var second = await startup.RunAsync(Token);

        Assert.Equal(0, migrations.Migrations);
        Assert.Null(first.BeforeMigration);
        Assert.True(first.Daily.Done);
        Assert.True(second.Daily.Skipped);
        Assert.Equal(["data_2_tuesday.db"], files.BackupFiles());
    }

    [Fact]
    public async Task A_database_behind_is_backed_up_before_it_is_migrated()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var clock = new SettableClock(Tuesday);
        var migrations = new ScriptedMigrations(new SchemaMigrations(files.Engine, clock), behind: true);
        // When the migration starts, the backup before it must already be on disk.
        migrations.BeforeMigrate = () => Assert.Contains(files.BackupFiles(), name => name.StartsWith("data_before_", StringComparison.Ordinal));
        var startup = new DesktopDatabaseStartup(migrations, files.Backups(clock));

        var report = await startup.RunAsync(Token);

        Assert.Equal(1, migrations.Migrations);
        Assert.True(report.BeforeMigration!.Done);
        Assert.True(report.Daily.Done);
        Assert.Equal(SchemaStatus.UpToDate, report.Schema.Status);
        Assert.Equal(2, files.BackupFiles().Count);
    }

    [Fact]
    public async Task A_failed_backup_before_a_migration_stops_the_migration()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        await File.WriteAllTextAsync(files.BackupFolder, "a file where the folder should be", Token);
        var clock = new SettableClock(Tuesday);
        var migrations = new ScriptedMigrations(new SchemaMigrations(files.Engine, clock), behind: true);
        var startup = new DesktopDatabaseStartup(migrations, files.Backups(clock));

        var refused = await Assert.ThrowsAsync<MigrationBackupFailedException>(() => startup.RunAsync(Token));

        Assert.Equal(0, migrations.Migrations);
        Assert.False(refused.Result!.Done);
        Assert.StartsWith(Topogenia.Persistence.Sqlite.Backup.BackupFailures.CopyFailed + ":", refused.Result.Failure, StringComparison.Ordinal);
        BackupServiceTests.AssertHoldsNoPath(refused.Message, files);
        Assert.Same(refused.Result.Cause, refused.InnerException);
        Assert.IsType<IOException>(refused.InnerException, exactMatch: false);
    }

    [Fact]
    public async Task A_refused_database_is_neither_backed_up_nor_migrated()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var clock = new SettableClock(Tuesday);
        var migrations = new ScriptedMigrations(new SchemaMigrations(files.Engine, clock), behind: true) { Refusal = SchemaCodes.NewerDatabase };
        var startup = new DesktopDatabaseStartup(migrations, files.Backups(clock));

        var refused = await Assert.ThrowsAsync<SchemaRefusedException>(() => startup.RunAsync(Token));

        Assert.Equal(SchemaCodes.NewerDatabase, refused.Code);
        Assert.Equal(0, migrations.Migrations);
        Assert.Empty(files.BackupFiles());
    }

    [Fact]
    public async Task A_failed_daily_backup_does_not_stop_the_start()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        await File.WriteAllTextAsync(files.BackupFolder, "a file where the folder should be", Token);
        var clock = new SettableClock(Tuesday);
        var startup = new DesktopDatabaseStartup(new SchemaMigrations(files.Engine, clock), files.Backups(clock));

        var report = await startup.RunAsync(Token);

        Assert.Equal(SchemaStatus.UpToDate, report.Schema.Status);
        Assert.False(report.Daily.Done);
        Assert.NotNull(report.Daily.Failure);
    }

    /// <summary>
    /// The schema port of a real data file, which can report one more migration to apply than the file holds (a database
    /// behind an application newer than it) or refuse the database; it counts the migrations it was asked to apply.
    /// </summary>
    private sealed class ScriptedMigrations(ISchemaMigrations real, bool behind) : ISchemaMigrations
    {
        private const string Next = "29991231000000_Next";
        private bool migrated;

        public int Migrations { get; private set; }

        public string? Refusal { get; init; }

        public Action BeforeMigrate { get; set; } = () => { };

        public async Task<SchemaState> CheckAsync(CancellationToken cancellationToken)
        {
            if (Refusal is not null)
                throw new SchemaRefusedException(Refusal, Next);
            var state = await real.CheckAsync(cancellationToken);
            return behind && !migrated ? new SchemaState(SchemaStatus.Behind, state.Applied, [.. state.Pending, Next]) : state;
        }

        public async Task<SchemaState> MigrateAsync(CancellationToken cancellationToken)
        {
            BeforeMigrate();
            Migrations++;
            migrated = true;
            return await real.MigrateAsync(cancellationToken);
        }
    }
}
