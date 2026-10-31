// RestoreSourceTests.cs
// The backup given to the restoration: another path of the data file, a path of an unsupported form, a read-only file, a file with its own log (security MAJ-2, data m1, m2).

using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Backup;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// The restoration compared the two paths as text: the data file given under another form (`\\?\`, `\\.\`, a hard
/// link) was restored over itself and its journal deleted, the work of the journal lost. A read-only backup threw and
/// left its partial file; a backup in the write-ahead log with its own log was restored without the log. Real files of
/// a folder of the test; the data file holds a first project in the file and a second one in its log, as a run that
/// stopped without closing leaves it.
/// </summary>
public sealed class RestoreSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(@"\\?\")]
    [InlineData(@"\\.\")]
    [InlineData("link")]
    public async Task The_data_file_given_as_the_backup_under_another_path_is_refused_and_its_journal_is_kept(string form)
    {
        await using var files = new SqliteFiles();
        var (first, second) = await StoppedWithWorkInItsLogAsync(files);
        var backup = form == "link" ? HardLink(files.DataFile, Path.Combine(files.Folder, "other-name.db")) : form + files.DataFile;

        var result = await files.Backups(new FixedClock()).RestoreAsync(backup, Token);

        Assert.False(result.Done);
        Assert.Equal(RestoreFailures.Impossible + ": the backup is the data file", result.Failure);
        Assert.True(new FileInfo(files.DataFile + "-wal").Length > 0);
        Assert.Equal([first, second], (await ProjectsAsync(files.Engine)).Order());
    }

    [Theory]
    [InlineData(@"backups\data_1_monday.db")]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolume1\data.db")]
    [InlineData("C:\\data\\da\0ta.db")]
    public async Task A_backup_named_by_a_path_of_another_form_is_refused_by_its_code_and_never_opened(string path)
    {
        await using var files = new SqliteFiles();
        var (first, second) = await StoppedWithWorkInItsLogAsync(files);

        var result = await files.Backups(new FixedClock()).RestoreAsync(path, Token);

        Assert.False(result.Done);
        Assert.Equal(RestoreFailures.UnsupportedPath + ": FICHIER_CHEMIN_NON_PRIS_EN_CHARGE", result.Failure);
        Assert.Equal([first, second], (await ProjectsAsync(files.Engine)).Order());
    }

    [Fact]
    public async Task A_read_only_backup_is_restored_and_leaves_neither_a_partial_file_nor_a_read_only_data_file()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        var first = await WriteAsync(engine);
        var backup = await files.Backups(new FixedClock()).BackupAsync(BackupReason.Daily, Token);
        Assert.True(backup.Done, backup.Failure);
        await WriteAsync(engine);
        File.SetAttributes(backup.File!, FileAttributes.ReadOnly);
        try
        {
            var result = await files.Backups(new FixedClock()).RestoreAsync(backup.File!, Token);

            Assert.True(result.Done, result.Failure);
            Assert.False(File.GetAttributes(files.DataFile).HasFlag(FileAttributes.ReadOnly));
            // No partial file: the only files named after the data file are the replaced one and its log, kept aside (AR-96).
            Assert.All(Directory.GetFiles(files.Folder).Select(Path.GetFileName).Where(file => file!.StartsWith("data.db.", StringComparison.Ordinal)),
                file => Assert.True(file == result.KeptFile || file == result.KeptFile + "-wal", file));
            Assert.Equal([first], await ProjectsAsync(engine));
            Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock()).MigrateAsync(Token)).Status);
            await WriteAsync(engine);
        }
        finally
        {
            File.SetAttributes(backup.File!, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task A_copy_in_the_write_ahead_log_with_its_own_log_is_restored_with_the_work_of_its_log()
    {
        await using var source = new SqliteFiles();
        var (first, second) = await StoppedWithWorkInItsLogAsync(source);
        await using var files = new SqliteFiles();
        await WriteAsync(await files.MigratedAsync());
        var copy = Path.Combine(files.Folder, "copy-of-a-stopped-run.db");
        File.Copy(source.DataFile, copy);
        File.Copy(source.DataFile + "-wal", copy + "-wal");

        var result = await files.Backups(new FixedClock()).RestoreAsync(copy, Token);

        Assert.True(result.Done, result.Failure);
        Assert.Equal([first, second], (await ProjectsAsync(files.Engine)).Order());
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(files.Engine, new FixedClock()).CheckAsync(Token)).Status);
    }

    /// <summary>A first project written back into the file by a clean stop, a second one left in the log of a run that stopped.</summary>
    private static async Task<(Guid First, Guid Second)> StoppedWithWorkInItsLogAsync(SqliteFiles files)
    {
        var engine = await files.MigratedAsync();
        var first = await WriteAsync(engine);
        await engine.CloseAsync(Token);
        var second = await WriteAsync(engine);
        SqliteFiles.CloseConnections();
        Assert.True(new FileInfo(files.DataFile + "-wal").Length > 0);
        return (first, second);
    }

    private static async Task<Guid> WriteAsync(SqliteEngine engine)
    {
        var graph = ProjectGraph.New();
        await using var context = engine.CreateContext();
        context.AddRange([.. graph.Records]);
        await context.SaveChangesAsync(Token);
        return graph.Project.Id;
    }

    private static async Task<List<Guid>> ProjectsAsync(SqliteEngine engine)
    {
        await using var context = engine.CreateContext();
        return await context.Set<Project>().Select(project => project.Id).ToListAsync(Token);
    }

    /// <summary>A second name of the file in the same folder (`mklink /H`).</summary>
    private static string HardLink(string file, string link)
    {
        Assert.True(CreateHardLink(link, file, IntPtr.Zero), $"CreateHardLink failed: {Marshal.GetLastPInvokeError()}");
        return link;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newFile, string existingFile, IntPtr securityAttributes);
}
