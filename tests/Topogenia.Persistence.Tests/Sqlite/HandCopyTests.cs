// HandCopyTests.cs
// A file copied by hand in place of the data file, while the application runs or next to the log of a stopped run, is never accepted by the start (data B1, M3).

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// The probes of the gate: a backup copied over the data file while a context holds it open, which writes again, then
/// the application closed and started again (accepted with the work of the log, or a corrupt file); a copy of the data
/// file made by the user put back next to the log of a run that stopped without closing (accepted with rows in excess,
/// or corrupt); a damaged file, by its pages or by a broken reference; a log left by another tool without a first
/// page. Each is refused by the start with its code, the file and the log left as they are, or read without any loss.
/// What the process knows of a file lives as long as the process: a start of the application is a new process, which
/// the tests stand for by <see cref="SqliteDataFileGuard.Forget"/>. SQLite only.
/// </summary>
public sealed class HandCopyTests
{
    private const int PageSize = 4096;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_backup_copied_over_the_data_file_while_the_application_runs_is_refused_at_the_next_start(bool layoutChanged)
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        var first = await WriteAsync(engine);
        var backup = await files.Backups(new FixedClock()).BackupAsync(BackupReason.Daily, Token);
        Assert.True(backup.Done, backup.Failure);
        if (layoutChanged)
            await ChangeLayoutAsync(engine);

        await using (var open = engine.CreateContext())
        {
            await open.Database.OpenConnectionAsync(Token);
            Assert.NotEqual(0, await open.Set<Project>().CountAsync(Token));
            await CopyOverAsync(backup.File!, files.DataFile);
            // The context that holds the file goes on and writes, or fails on the pages of the copy mixed with those of
            // the log (SQLITE_CORRUPT, the pages no longer match); a new one is refused at once.
            open.AddRange([.. ProjectGraph.New().Records]);
            var write = await Record.ExceptionAsync(() => open.SaveChangesAsync(Token));
            Assert.True(write is null || write.GetBaseException() is SqliteException { SqliteErrorCode: 11 }, write?.ToString());
            await AssertRefusedAsync(engine, SqliteJournalCheck.ForeignJournalCode);
        }
        SqliteFiles.CloseConnections();

        Restart(files);
        await AssertRefusedAsync(engine, SqliteJournalCheck.ForeignJournalCode);
        var restored = await files.Backups(new FixedClock()).RestoreAsync(backup.File!, Token);
        Assert.True(restored.Done, restored.Failure);
        Assert.Equal([first], await ProjectsAsync(engine));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_copy_of_the_data_file_from_an_earlier_run_put_next_to_the_log_of_a_stopped_run_is_refused_and_the_true_file_is_read_whole(bool layoutChanged)
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        var first = await WriteAsync(engine);
        await engine.CloseAsync(Token);
        var copy = Path.Combine(files.Folder, "my-copy.db");
        File.Copy(files.DataFile, copy);

        Restart(files);
        var later = new[] { await WriteAsync(engine), await WriteAsync(engine) };
        if (layoutChanged)
            await ChangeLayoutAsync(engine);
        SqliteFiles.CloseConnections();
        var stopped = Path.Combine(files.Folder, "stopped.db");
        File.Copy(files.DataFile, stopped);
        await CopyOverAsync(copy, files.DataFile);

        Restart(files);
        await AssertRefusedAsync(engine, SqliteDataFileGuard.ForeignLogCode);
        Assert.Equal(await File.ReadAllBytesAsync(copy, Token), await File.ReadAllBytesAsync(files.DataFile, Token));

        // The file of the stopped run with its log: a stop without closing is read again with nothing lost.
        await CopyOverAsync(stopped, files.DataFile);
        Restart(files);
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token)).Status);
        var projects = await ProjectsAsync(engine);
        Assert.Contains(first, projects);
        Assert.All(later, project => Assert.Contains(project, projects));
    }

    [Fact]
    public async Task A_copy_of_the_data_file_taken_during_the_run_is_read_with_the_log_of_that_run_and_nothing_is_lost()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        var first = await WriteAsync(engine);
        var copy = Path.Combine(files.Folder, "my-copy.db");
        File.Copy(files.DataFile, copy);
        var second = await WriteAsync(engine);
        SqliteFiles.CloseConnections();
        await CopyOverAsync(copy, files.DataFile);

        Restart(files);

        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token)).Status);
        Assert.Equal([first, second], (await ProjectsAsync(engine)).Order());
        await AssertSoundAsync(engine);
    }

    [Fact]
    public async Task A_data_file_replaced_by_a_copy_while_the_application_runs_is_never_written_back_and_is_refused()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        await WriteAsync(engine);
        await engine.CloseAsync(Token);
        var copy = Path.Combine(files.Folder, "my-copy.db");
        File.Copy(files.DataFile, copy);

        Restart(files);
        await WriteAsync(engine);
        await CopyOverAsync(copy, files.DataFile);

        var refusal = await Assert.ThrowsAsync<SqliteException>(() => engine.CloseAsync(Token));
        Assert.Equal(SqliteDataFileGuard.ReplacedCode, refusal.SqliteExtendedErrorCode);
        Assert.True(new FileInfo(files.DataFile + "-wal").Length > 0);
        Assert.Equal(await File.ReadAllBytesAsync(copy, Token), ReadShared(files.DataFile));
        await AssertRefusedAsync(engine, SqliteDataFileGuard.ReplacedCode);
        SqliteFiles.CloseConnections();
        Restart(files);
        await AssertRefusedAsync(engine, SqliteDataFileGuard.ForeignLogCode);
    }

    [Fact]
    public async Task A_damaged_data_file_is_refused_at_its_first_opening_before_anything_is_written()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        var graph = ProjectGraph.New();
        await using (var context = engine.CreateContext())
        {
            context.AddRange([.. graph.Records]);
            await context.SaveChangesAsync(Token);
        }
        await engine.CloseAsync(Token);
        // The first page that holds the code of the project, a page of the data that the check of the schema never
        // reads, made unreadable: its type byte is no type of SQLite.
        var bytes = await File.ReadAllBytesAsync(files.DataFile, Token);
        var at = bytes.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(graph.Code));
        Assert.True(at >= PageSize, $"code at {at}");
        bytes[at / PageSize * PageSize] = 0xFF;
        await File.WriteAllBytesAsync(files.DataFile, bytes, Token);

        Restart(files);

        await AssertRefusedAsync(engine, SqliteDataFileGuard.DamagedCode);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(files.DataFile, Token));
    }

    [Fact]
    public async Task A_data_file_with_a_broken_reference_is_refused_at_its_first_opening_before_anything_is_written()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        await WriteAsync(engine);
        await engine.CloseAsync(Token);
        // Sound pages, but records whose project is gone: only `foreign_key_check` tells the file is damaged.
        await SqliteFiles.BreakReferencesAsync(files.DataFile);
        var bytes = await File.ReadAllBytesAsync(files.DataFile, Token);

        Restart(files);

        await AssertRefusedAsync(engine, SqliteDataFileGuard.DamagedCode);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(files.DataFile, Token));
        Assert.False(new FileInfo(files.DataFile + "-wal") is { Exists: true, Length: > 0 });
    }

    [Fact]
    public async Task A_log_left_by_another_tool_with_committed_frames_but_no_first_page_is_refused_and_kept()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        await WriteAsync(engine);
        await engine.CloseAsync(Token);
        // A tool of its own opens the file outside the engine, writes a row in its page (no page 1: the size of the file
        // and its schema stay) and closes without writing its log back: the log stays, no stamp in it to tell whose it is.
        await using (var tool = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = files.DataFile, Pooling = false }.ConnectionString))
        {
            await tool.OpenAsync(Token);
            Assert.Equal(SQLitePCL.raw.SQLITE_OK, SQLitePCL.raw.sqlite3_db_config(tool.Handle, 1006, 1, out _));
            foreach (var statement in SqlitePragmas.CheckpointSettings())
                SqlitePragmas.Execute(tool, statement);
            await using var context = SqliteFiles.PlainContext(tool);
            Assert.Equal(1, await context.Set<Project>().ExecuteUpdateAsync(set => set.SetProperty(project => project.Version, project => project.Version + 1), Token));
        }
        var log = SqliteWalFile.Read(files.DataFile + "-wal");
        Assert.True(log.HasFrames);
        Assert.Empty(log.Stamps);
        var bytes = await File.ReadAllBytesAsync(files.DataFile, Token);
        var logBytes = await File.ReadAllBytesAsync(files.DataFile + "-wal", Token);

        Restart(files);

        await AssertRefusedAsync(engine, SqliteDataFileGuard.UnverifiableLogCode);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(files.DataFile, Token));
        Assert.Equal(logBytes, await File.ReadAllBytesAsync(files.DataFile + "-wal", Token));
    }

    [Fact]
    public async Task A_clean_stop_leaves_no_log_and_a_backup_put_back_by_hand_then_is_the_data_file()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        var first = await WriteAsync(engine);
        var backup = await files.Backups(new FixedClock()).BackupAsync(BackupReason.Daily, Token);
        Assert.True(backup.Done, backup.Failure);
        await WriteAsync(engine);

        await engine.CloseAsync(Token);

        Assert.False(new FileInfo(files.DataFile + "-wal") is { Exists: true, Length: > 0 });
        await CopyOverAsync(backup.File!, files.DataFile);
        Restart(files);
        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token)).Status);
        Assert.Equal([first], await ProjectsAsync(engine));
    }

    [Fact]
    public async Task A_run_written_back_by_the_engine_and_stopped_without_closing_is_read_again_whole()
    {
        await using var files = new SqliteFiles();
        var engine = await files.MigratedAsync();
        var written = new List<Guid>();
        // Enough work for the log to pass its length several times: the engine writes it back as connections close.
        while (written.Count < 3 || new FileInfo(files.DataFile).Length < 3 * SqliteDataFileGuard.CheckpointLength)
            written.Add(await WriteAsync(engine));
        SqliteFiles.CloseConnections();

        Restart(files);

        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token)).Status);
        Assert.Equal(written.Order(), (await ProjectsAsync(engine)).Order());
        await AssertSoundAsync(engine);
    }

    /// <summary>
    /// A copy by hand over a file, as the explorer or `copy` make it: a scanner of the machine may hold a file just written
    /// for a moment (sharing violation), the copy is then made again a few times.
    /// </summary>
    private static async Task CopyOverAsync(string source, string target)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(source, target, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                await Task.Delay(250, Token);
            }
        }
    }

    /// <summary>The bytes of a file that connections of SQLite hold open.</summary>
    private static byte[] ReadShared(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    /// <summary>A new start of the application: a new process, which knows nothing yet of the file.</summary>
    private static void Restart(SqliteFiles files)
    {
        SqliteFiles.CloseConnections();
        SqliteDataFileGuard.Forget(files.DataFile);
    }

    private static async Task AssertRefusedAsync(SqliteEngine engine, int extendedCode)
    {
        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token));
        Assert.Equal(SchemaCodes.DatabaseUnreachable, refusal.Code);
        Assert.Equal([SqliteJournalCheck.ForeignJournalCode, extendedCode], Assert.IsType<EngineFailureException>(refusal.InnerException).Numbers);
    }

    private static async Task<Guid> WriteAsync(SqliteEngine engine)
    {
        var graph = ProjectGraph.New();
        await using var context = engine.CreateContext();
        context.AddRange([.. graph.Records]);
        await context.SaveChangesAsync(Token);
        return graph.Project.Id;
    }

    /// <summary>Many projects written, then their audit removed: the pages of the file are no longer those of the copy.</summary>
    private static async Task ChangeLayoutAsync(SqliteEngine engine)
    {
        for (var i = 0; i < 20; i++)
            await WriteAsync(engine);
        await using var context = engine.CreateContext();
        await context.Set<AuditEvent>().ExecuteDeleteAsync(Token);
    }

    private static async Task<List<Guid>> ProjectsAsync(SqliteEngine engine)
    {
        await using var context = engine.CreateContext();
        return await context.Set<Project>().Select(project => project.Id).ToListAsync(Token);
    }

    private static async Task AssertSoundAsync(SqliteEngine engine)
    {
        await using var context = engine.CreateContext();
        await context.Database.OpenConnectionAsync(Token);
        Assert.Empty(await SqlitePragmas.IntegrityProblemsAsync(context.Database.GetDbConnection(), Token));
        Assert.Empty(await SqlitePragmas.ForeignKeyProblemsAsync(context.Database.GetDbConnection(), Token));
    }
}
