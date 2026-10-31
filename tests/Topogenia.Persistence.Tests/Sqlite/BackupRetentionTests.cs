// BackupRetentionTests.cs
// Retention of the backups of the desktop mode (security m4): files before a migration rotated and aged out, backup folder under the rule of the data file.

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Sqlite.Backup;
using Topogenia.Persistence.Sqlite.Files;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// The backups hold the projects, members and audit of the file when they were written: a project purged from the file
/// stays in them until they go. The 7 daily files replace themselves within a week; the files before a migration are kept
/// at most <see cref="SqliteBackupService.BeforeMigrationKept"/> and at most
/// <see cref="SqliteBackupService.BeforeMigrationRetention"/> (ADR 029, 035). The backup folder is refused on a share or
/// in a synchronised folder, as the data file is. Real files in a temporary folder; older files before a migration are
/// written by the test, dated by it.
/// </summary>
public sealed class BackupRetentionTests
{
    private static readonly DateTime Monday = new(2026, 10, 19, 6, 45, 0, DateTimeKind.Utc);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string> TargetAsync(SqliteFiles files)
    {
        await using var context = files.Engine.CreateContext();
        return $"data_before_{context.Database.GetMigrations().Last()}.db";
    }

    /// <summary>A file of the backup folder that the test writes, dated as if the backup had written it then.</summary>
    private static void WriteOld(SqliteFiles files, string name, DateTime writtenAt)
    {
        Directory.CreateDirectory(files.BackupFolder);
        var file = Path.Combine(files.BackupFolder, name);
        File.WriteAllText(file, "an older backup");
        File.SetLastWriteTimeUtc(file, writtenAt);
    }

    [Fact]
    public async Task Only_the_two_most_recent_files_before_a_migration_are_kept()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        WriteOld(files, "data_before_20260101000000_First.db", Monday.AddDays(-3));
        WriteOld(files, "data_before_20260201000000_Second.db", Monday.AddDays(-2));
        WriteOld(files, "other_before_20260101000000_First.db", Monday.AddDays(-3));
        WriteOld(files, "data_1_monday.db", Monday.AddDays(-7));

        var result = await files.Backups(new SettableClock(Monday)).BackupAsync(BackupReason.BeforeMigration, Token);

        Assert.True(result.Done);
        var target = await TargetAsync(files);
        Assert.Equal(new[] { "data_1_monday.db", "data_before_20260201000000_Second.db", target, "other_before_20260101000000_First.db" }.Order(StringComparer.Ordinal),
            files.BackupFiles());
    }

    [Fact]
    public async Task A_file_before_a_migration_older_than_the_retention_is_deleted_by_the_daily_backup_even_the_last_one()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var clock = new SettableClock(Monday);
        var backups = files.Backups(clock);
        Assert.True((await backups.BackupAsync(BackupReason.BeforeMigration, Token)).Done);
        var target = await TargetAsync(files);

        clock.UtcNow = Monday + SqliteBackupService.BeforeMigrationRetention;
        Assert.True((await backups.BackupAsync(BackupReason.Daily, Token)).Done);
        Assert.Contains(target, files.BackupFiles());

        clock.UtcNow = Monday + SqliteBackupService.BeforeMigrationRetention + TimeSpan.FromDays(1);
        Assert.True((await backups.BackupAsync(BackupReason.Daily, Token)).Done);
        Assert.DoesNotContain(target, files.BackupFiles());
        Assert.Equal(2, files.BackupFiles().Count);
        Assert.Equal(TimeSpan.FromDays(30), SqliteBackupService.BeforeMigrationRetention);
        Assert.Equal(2, SqliteBackupService.BeforeMigrationKept);
    }

    [Fact]
    public async Task A_skipped_daily_backup_still_ages_out_the_files_before_a_migration()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var later = Monday.AddDays(40);
        var backups = files.Backups(new SettableClock(later));
        Assert.True((await backups.BackupAsync(BackupReason.Daily, Token)).Done);
        WriteOld(files, "data_before_20260101000000_First.db", Monday);

        var skipped = await backups.BackupAsync(BackupReason.Daily, Token);

        Assert.True(skipped.Skipped);
        Assert.Equal([BackupFileNames.Daily(files.DataFile, later)], files.BackupFiles());
    }

    public static TheoryData<string, string> RefusedFolders => new()
    {
        { "OneDrive - Fictitious Org", DataFileRefusals.SynchronisedFolder },
        { "SharePoint", DataFileRefusals.SynchronisedFolder },
    };

    [Theory]
    [MemberData(nameof(RefusedFolders))]
    public async Task A_backup_folder_in_a_folder_named_as_a_synchronised_one_is_refused_and_nothing_is_written(string parent, string code)
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var folder = Path.Combine(files.Folder, parent, "backups");

        var result = await new SqliteBackupService(files.Engine, folder, new SettableClock(Monday), SqliteFiles.LocalMachine)
            .BackupAsync(BackupReason.BeforeMigration, Token);

        Assert.Equal(new BackupResult(false, false, null, BackupFailures.FolderRefused + ": " + code), result);
        Assert.False(Directory.Exists(Path.Combine(files.Folder, parent)));
    }

    [Fact]
    public async Task A_backup_folder_under_a_synchronised_folder_of_the_machine_or_on_a_network_drive_is_refused()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var synchronised = DataFileEnvironment.From(name => name == "OneDrive" ? files.Folder : null, _ => false);
        var network = new DataFileEnvironment([], letter => letter == char.ToUpperInvariant(files.Folder[0]));

        var inSynchronised = await new SqliteBackupService(files.Engine, files.BackupFolder, new SettableClock(Monday), synchronised)
            .BackupAsync(BackupReason.Daily, Token);
        var onNetwork = await new SqliteBackupService(files.Engine, files.BackupFolder, new SettableClock(Monday), network)
            .BackupAsync(BackupReason.Daily, Token);

        Assert.Equal(BackupFailures.FolderRefused + ": " + DataFileRefusals.SynchronisedFolder, inSynchronised.Failure);
        Assert.Equal(BackupFailures.FolderRefused + ": " + DataFileRefusals.NetworkShare, onNetwork.Failure);
        Assert.False(Directory.Exists(files.BackupFolder));
    }

    [Theory]
    [InlineData(@"\\?\GLOBALROOT\Device\Mup\fileserver\projects\backups", DataFileRefusals.NetworkShare)]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolume3\backups", DataFileRefusals.UnsupportedPath)]
    public async Task A_backup_folder_through_a_device_of_windows_is_refused_by_its_code_and_never_thrown(string folder, string code)
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();

        var result = await new SqliteBackupService(files.Engine, folder, new SettableClock(Monday), SqliteFiles.LocalMachine)
            .BackupAsync(BackupReason.Daily, Token);

        Assert.Equal(new BackupResult(false, false, null, BackupFailures.FolderRefused + ": " + code), result);
    }
}
