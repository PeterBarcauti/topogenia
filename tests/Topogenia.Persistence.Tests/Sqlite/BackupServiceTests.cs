// BackupServiceTests.cs
// Online backup of the SQLite data file (spec 5.2, 4.4): daily rotation of 7 files, backup before migration, checks of the copy.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Backup;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// The backup of the desktop mode, on real files in a temporary folder. The copy is made by the online backup API of
/// SQLite (rows still in the write-ahead log are in it), checked by `integrity_check` and `foreign_key_check` before it
/// replaces anything, and written as a self-contained file. SQLite only: the server mode is backed up by the plan of SQL
/// Server (spec 6.2), there is no SQL Server side to run in the matrix.
/// </summary>
public sealed class BackupServiceTests
{
    /// <summary>A Monday, in UTC.</summary>
    private static readonly DateTime Monday = new(2026, 10, 19, 6, 45, 0, DateTimeKind.Utc);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ProjectGraph> WriteGraphAsync(SqliteFiles files)
    {
        var graph = ProjectGraph.New();
        await using var context = files.Engine.CreateContext();
        context.AddRange(graph.Records);
        await context.SaveChangesAsync(Token);
        return graph;
    }

    private static SqliteEngine CopyEngine(string file) =>
        new(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false, Mode = SqliteOpenMode.ReadWrite }.ConnectionString);

    private static async Task<bool> HoldsProjectAsync(string file, Guid project)
    {
        await using var context = CopyEngine(file).CreateContext();
        return await context.Set<Project>().AnyAsync(item => item.Id == project, Token);
    }

    [Fact]
    public async Task The_daily_backup_is_an_online_copy_that_holds_the_rows_still_in_the_write_ahead_log()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        // A connection held open (another user of the file) keeps the write-ahead log, whatever the pools do.
        await using var holder = files.Engine.CreateContext();
        await holder.Database.OpenConnectionAsync(Token);
        var graph = await WriteGraphAsync(files);
        Assert.True(new FileInfo(files.DataFile + "-wal").Length > 0);
        Assert.False(Directory.Exists(files.BackupFolder));

        var result = await files.Backups(new SettableClock(Monday)).BackupAsync(BackupReason.Daily, Token);

        Assert.Equal(new BackupResult(true, false, Path.Combine(files.BackupFolder, "data_1_monday.db"), null), result);
        Assert.Equal(["data_1_monday.db"], files.BackupFiles());
        Assert.Equal(Monday, File.GetLastWriteTimeUtc(result.File!));
        await using (var copy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = result.File, Pooling = false }.ConnectionString))
        {
            await copy.OpenAsync(Token);
            Assert.Equal(SqliteJournalMode.Delete, (await SqliteSettingsProbe.ReadAsync(copy, Token)).JournalMode);
        }
        Assert.True(await HoldsProjectAsync(result.File!, graph.Project.Id));
        var state = await new SchemaMigrations(CopyEngine(result.File!), new FixedClock()).CheckAsync(Token);
        Assert.Equal(SchemaStatus.UpToDate, state.Status);
    }

    [Fact]
    public async Task The_daily_backup_is_skipped_when_the_backup_of_the_day_exists()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var clock = new SettableClock(Monday);
        var backups = files.Backups(clock);
        var first = await backups.BackupAsync(BackupReason.Daily, Token);
        var later = await WriteGraphAsync(files);
        clock.UtcNow = Monday.AddHours(16);

        var second = await backups.BackupAsync(BackupReason.Daily, Token);

        Assert.Equal(new BackupResult(false, true, first.File, null), second);
        Assert.Equal(Monday, File.GetLastWriteTimeUtc(first.File!));
        Assert.False(await HoldsProjectAsync(first.File!, later.Project.Id));
    }

    [Fact]
    public async Task Seven_daily_files_rotate_one_per_day_of_the_week()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var clock = new SettableClock(Monday);
        var backups = files.Backups(clock);
        for (var day = 0; day < 8; day++)
        {
            clock.UtcNow = Monday.AddDays(day);
            Assert.True((await backups.BackupAsync(BackupReason.Daily, Token)).Done);
        }
        Assert.Equal(
            ["data_1_monday.db", "data_2_tuesday.db", "data_3_wednesday.db", "data_4_thursday.db", "data_5_friday.db", "data_6_saturday.db", "data_7_sunday.db"],
            files.BackupFiles());
        // The eighth day is a Monday again: it replaced the file of the first Monday.
        Assert.Equal(Monday.AddDays(7), File.GetLastWriteTimeUtc(Path.Combine(files.BackupFolder, "data_1_monday.db")));
        Assert.Equal(Monday.AddDays(6), File.GetLastWriteTimeUtc(Path.Combine(files.BackupFolder, "data_7_sunday.db")));
    }

    [Fact]
    public async Task The_backup_before_a_migration_is_made_in_its_own_file_even_when_the_daily_backup_exists()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        string target;
        await using (var context = files.Engine.CreateContext())
            target = context.Database.GetMigrations().Last();
        var backups = files.Backups(new SettableClock(Monday));
        Assert.True((await backups.BackupAsync(BackupReason.Daily, Token)).Done);

        var before = await backups.BackupAsync(BackupReason.BeforeMigration, Token);
        var again = await backups.BackupAsync(BackupReason.BeforeMigration, Token);

        var expected = new BackupResult(true, false, Path.Combine(files.BackupFolder, $"data_before_{target}.db"), null);
        Assert.Equal(expected, before);
        Assert.Equal(expected, again);
        Assert.Equal(["data_1_monday.db", $"data_before_{target}.db"], files.BackupFiles());
    }

    [Theory]
    [InlineData(BackupReason.Daily)]
    [InlineData(BackupReason.BeforeMigration)]
    public async Task A_missing_data_file_fails_the_backup_and_creates_nothing(BackupReason reason)
    {
        await using var files = new SqliteFiles();

        var result = await files.Backups(new SettableClock(Monday)).BackupAsync(reason, Token);

        Assert.False(result.Done);
        Assert.False(result.Skipped);
        Assert.Null(result.File);
        Assert.Equal(BackupFailures.DataFileMissing + ": no data file", result.Failure);
        Assert.Null(result.Cause);
        Assert.False(File.Exists(files.DataFile));
        Assert.False(Directory.Exists(files.BackupFolder));
    }

    [Fact]
    public async Task A_backup_folder_that_cannot_be_created_fails_the_backup()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        await File.WriteAllTextAsync(files.BackupFolder, "a file where the folder should be", Token);

        var result = await files.Backups(new SettableClock(Monday)).BackupAsync(BackupReason.BeforeMigration, Token);

        Assert.False(result.Done);
        Assert.Null(result.File);
        Assert.Matches("^" + BackupFailures.CopyFailed + ": IOException 0x[0-9A-F]{8}$", result.Failure);
        AssertHoldsNoPath(result.Failure!, files);
        // The exception stays for the local diagnosis, with the path that the failure leaves out.
        Assert.Contains(files.Folder, Assert.IsType<IOException>(result.Cause, exactMatch: false).Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Neither the folder of the test, nor its parent (the profile of the user), nor a drive path is in the text.</summary>
    internal static void AssertHoldsNoPath(string text, SqliteFiles files)
    {
        Assert.DoesNotContain(files.Folder, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFileName(files.Folder), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.UserName, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"[A-Za-z]:\\|\\\\", text);
    }

    [Fact]
    public async Task A_copy_that_fails_the_integrity_check_is_not_kept_and_the_previous_file_stays()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        var graph = await WriteGraphAsync(files);
        var clock = new SettableClock(Monday);
        var backups = files.Backups(clock);
        var previous = await backups.BackupAsync(BackupReason.Daily, Token);
        var bytes = await File.ReadAllBytesAsync(previous.File!, Token);
        await CorruptPagesAsync(files, graph.Code);
        clock.UtcNow = Monday.AddDays(7);

        var result = await backups.BackupAsync(BackupReason.Daily, Token);

        Assert.False(result.Done);
        Assert.StartsWith(BackupFailures.CopyCorrupted + ": integrity_check", result.Failure, StringComparison.Ordinal);
        AssertHoldsNoPath(result.Failure!, files);
        Assert.Equal(["data_1_monday.db"], files.BackupFiles());
        Assert.Equal(bytes, await File.ReadAllBytesAsync(previous.File!, Token));
        Assert.Equal(Monday, File.GetLastWriteTimeUtc(previous.File!));
    }

    [Fact]
    public async Task A_copy_with_a_broken_reference_fails_the_foreign_key_check()
    {
        await using var files = new SqliteFiles();
        await files.MigratedAsync();
        await WriteDanglingMemberAsync(files);

        var result = await files.Backups(new SettableClock(Monday)).BackupAsync(BackupReason.Daily, Token);

        Assert.False(result.Done);
        Assert.StartsWith(BackupFailures.CopyCorrupted + ": foreign_key_check", result.Failure, StringComparison.Ordinal);
        Assert.Empty(files.BackupFiles());
    }

    /// <summary>
    /// Writes garbage over the header of the pages that hold the code of the project (its row and its index, written
    /// after the schema; the rebuild of the migrations leaves free pages that later rows take, so the last pages of the
    /// file may be those of the schema), once the engine has put the log back into the file: the schema still reads, the
    /// online backup copies the pages as they are, and the integrity check of the copy fails.
    /// </summary>
    private static async Task CorruptPagesAsync(SqliteFiles files, string code)
    {
        // The clean stop of the application writes the log back and empties it (data B1).
        await files.Engine.CloseAsync(Token);
        Assert.False(new FileInfo(files.DataFile + "-wal") is { Exists: true, Length: > 0 });
        const int pageSize = 4096;
        var content = await File.ReadAllBytesAsync(files.DataFile, Token);
        var needle = System.Text.Encoding.ASCII.GetBytes(code);
        var pages = Enumerable.Range(1, content.Length / pageSize - 1)
            .Where(page => content.AsSpan(page * pageSize, pageSize).IndexOf(needle) >= 0).ToList();
        Assert.NotEmpty(pages);
        await using var stream = new FileStream(files.DataFile, FileMode.Open, FileAccess.ReadWrite);
        var garbage = Enumerable.Repeat((byte)0x5A, 12).ToArray();
        foreach (var page in pages)
        {
            stream.Position = (long)page * pageSize;
            await stream.WriteAsync(garbage, Token);
        }
    }

    /// <summary>A member whose project and user do not exist, written by a context of the test with the foreign keys off.</summary>
    private static async Task WriteDanglingMemberAsync(SqliteFiles files)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder(files.ConnectionString) { ForeignKeys = false, Pooling = false }.ConnectionString)
            .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer, SqliteModelCustomizer>()
            .Options;
        await using var context = new AppDbContext(options);
        context.Add(new ProjectMember(Guid.CreateVersion7(), Guid.CreateVersion7(), Domain.Values.MemberRole.Reader, false, false, 0,
            FixedClock.Time, Guid.CreateVersion7(), null, null));
        await context.SaveChangesAsync(Token);
    }
}
