// RestoreTests.cs
// The restoration of a backup of the desktop mode, and a backup copied by hand next to the write-ahead log of the replaced file (data B1).

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
/// A backup is a file in the rollback journal (ADR 029). Copied by hand in place of the data file, it lies next to the
/// write-ahead log and the shared memory of the replaced file when that file was not closed cleanly: SQLite reads that
/// log with the restored file, which then shows work done after the backup or pages of another layout (a corrupt file).
/// The start refuses such a file before SQLite reads it (`BASE_INACCESSIBLE`); <see cref="IBackupService.RestoreAsync"/>
/// replaces the file and its journal together, both kept aside under a name of their own (AR-96). The scenarios are those
/// of the probe of the gate (same layout, layout changed by later work); SQLite only: the server mode restores by the plan
/// of SQL Server (spec 6.2).
/// </summary>
public sealed class RestoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_backup_copied_by_hand_next_to_the_journal_of_the_replaced_file_is_refused_by_the_check_and_restored_by_the_service(bool layoutChanged)
    {
        await using var scenario = await Scenario.PreparedAsync(layoutChanged);
        File.Copy(scenario.Backup, scenario.Files.DataFile, overwrite: true);
        scenario.PutBackJournal();

        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(
            () => new SchemaMigrations(scenario.Files.Engine, new FixedClock()).CheckAsync(Token));
        Assert.Equal(SchemaCodes.DatabaseUnreachable, refusal.Code);
        Assert.Equal([26, 26], Assert.IsType<EngineFailureException>(refusal.InnerException).Numbers);
        // Nothing was read nor written: the journal is still there, the file is still the backup.
        Assert.True(File.Exists(scenario.Files.DataFile + "-wal"));
        Assert.Equal(await File.ReadAllBytesAsync(scenario.Backup, Token), await File.ReadAllBytesAsync(scenario.Files.DataFile, Token));

        var restored = await scenario.Files.Backups(new FixedClock()).RestoreAsync(scenario.Backup, Token);

        Assert.True(restored.Done, restored.Failure);
        await scenario.AssertRestoredAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_data_file_stopped_with_work_in_its_journal_is_replaced_by_the_backup_and_the_journal_goes_with_it(bool layoutChanged)
    {
        await using var scenario = await Scenario.PreparedAsync(layoutChanged);
        scenario.PutBackStoppedFile();

        var restored = await scenario.Files.Backups(new FixedClock()).RestoreAsync(scenario.Backup, Token);

        Assert.True(restored.Done, restored.Failure);
        await scenario.AssertRestoredAsync();
        // AR-96: the replaced file is kept next to it with its log, which still holds the work done after the backup.
        Assert.Equal(BackupFileNames.Refused(scenario.Files.DataFile, new FixedClock().UtcNow), restored.KeptFile);
        var kept = Path.Combine(scenario.Files.Folder, restored.KeptFile!);
        Assert.True(new FileInfo(kept + "-wal").Length > 0);
        var keptEngine = new SqliteEngine(new SqliteConnectionStringBuilder { DataSource = kept, Pooling = false }.ConnectionString);
        await using (var context = keptEngine.CreateContext())
        {
            var projects = await context.Set<Project>().Select(project => project.Id).ToListAsync(Token);
            Assert.Contains(scenario.First, projects);
            Assert.Contains(scenario.Second, projects);
        }
    }

    /// <summary>
    /// AR-96: the replaced file and its log are moved aside before the restored copy takes their place; when a move fails,
    /// whatever was moved comes back, and the data file is the stopped file with the work of its log.
    /// </summary>
    [Theory]
    [InlineData(".restoring", "the restored copy cannot take the place")]
    [InlineData("-wal", "the log cannot be moved aside after the file")]
    public async Task A_restoration_that_fails_while_the_files_move_puts_the_data_file_and_its_log_back(string failing, string because)
    {
        await using var scenario = await Scenario.PreparedAsync(layoutChanged: false);
        scenario.PutBackStoppedFile();
        var restore = new SqliteRestore(scenario.Files.Engine, new FixedClock())
        {
            MoveFile = (source, target) =>
            {
                // The move that fails: the restored copy to the data file, or the log of the data file to its kept name.
                if (source.EndsWith(failing, StringComparison.Ordinal) && (failing == ".restoring" || target.Contains(".refused-", StringComparison.Ordinal)))
                    throw new IOException("Simulated failure: " + because);
                File.Move(source, target);
            },
        };

        var result = await restore.RestoreAsync(scenario.Backup, Token);

        Assert.False(result.Done, because);
        Assert.StartsWith(RestoreFailures.Impossible, result.Failure, StringComparison.Ordinal);
        Assert.True(File.Exists(scenario.Files.DataFile));
        Assert.True(new FileInfo(scenario.Files.DataFile + "-wal").Length > 0);
        Assert.DoesNotContain(Directory.GetFiles(scenario.Files.Folder), file => Path.GetFileName(file).Contains(".refused-", StringComparison.Ordinal));
        await using var context = scenario.Files.Engine.CreateContext();
        Assert.Equal(new[] { scenario.First, scenario.Second }.Order(), (await context.Set<Project>().Select(project => project.Id).ToListAsync(Token)).Order());
    }

    [Fact]
    public async Task A_second_restoration_in_the_same_second_keeps_the_replaced_file_under_a_name_of_its_own()
    {
        await using var scenario = await Scenario.PreparedAsync(layoutChanged: false);
        var backups = scenario.Files.Backups(new FixedClock());

        var first = await backups.RestoreAsync(scenario.Backup, Token);
        var second = await backups.RestoreAsync(scenario.Backup, Token);

        Assert.True(first.Done && second.Done, first.Failure ?? second.Failure);
        Assert.Equal(BackupFileNames.Refused(scenario.Files.DataFile, new FixedClock().UtcNow, 2), second.KeptFile);
        Assert.True(File.Exists(Path.Combine(scenario.Files.Folder, first.KeptFile!)) && File.Exists(Path.Combine(scenario.Files.Folder, second.KeptFile!)));
        await scenario.AssertRestoredAsync();
    }

    [Fact]
    public async Task The_idle_connections_of_the_engine_are_released_and_a_connection_open_elsewhere_refuses_the_restoration()
    {
        await using var scenario = await Scenario.PreparedAsync(false);
        await using (var context = scenario.Files.Engine.CreateContext())
            Assert.Equal(2, await context.Set<Project>().CountAsync(Token));
        var service = scenario.Files.Backups(new FixedClock());
        await using (var other = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = scenario.Files.DataFile, Pooling = false }.ConnectionString))
        {
            await other.OpenAsync(Token);
            var refused = await service.RestoreAsync(scenario.Backup, Token);
            AssertFailure(refused, RestoreFailures.DataFileOpen);
        }
        await using (var context = scenario.Files.Engine.CreateContext())
            Assert.Equal(2, await context.Set<Project>().CountAsync(Token));

        // The pooled connections of the engine itself are idle: they are closed by the restoration.
        var restored = await service.RestoreAsync(scenario.Backup, Token);
        Assert.True(restored.Done, restored.Failure);
        await scenario.AssertRestoredAsync();
    }

    [Fact]
    public async Task A_missing_damaged_or_refused_backup_leaves_the_data_file_as_it_was()
    {
        await using var scenario = await Scenario.PreparedAsync(false);
        var service = scenario.Files.Backups(new FixedClock());
        var text = Path.Combine(scenario.Files.Folder, "notes.db");
        await File.WriteAllTextAsync(text, "Not a database of the application.", Token);
        var empty = Path.Combine(scenario.Files.Folder, "empty.db");
        await using (var created = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = empty, Pooling = false }.ConnectionString))
            await created.OpenAsync(Token);
        var unrecorded = Path.Combine(scenario.Files.Folder, "unrecorded.db");
        File.Copy(scenario.Backup, unrecorded);
        await using (var context = new SqliteEngine(new SqliteConnectionStringBuilder { DataSource = unrecorded, Pooling = false }.ConnectionString).CreateContext())
            await context.Set<SchemaHistoryEntry>().ExecuteDeleteAsync(Token);

        AssertFailure(await service.RestoreAsync(Path.Combine(scenario.Files.Folder, "missing.db"), Token), RestoreFailures.BackupMissing);
        AssertFailure(await service.RestoreAsync(text, Token), RestoreFailures.BackupCorrupted);
        AssertFailure(await service.RestoreAsync(empty, Token), RestoreFailures.BackupRefused);
        var refused = await service.RestoreAsync(unrecorded, Token);
        AssertFailure(refused, RestoreFailures.BackupRefused);
        Assert.Contains(SchemaCodes.FingerprintMissing, refused.Failure, StringComparison.Ordinal);
        AssertFailure(await service.RestoreAsync(scenario.Files.DataFile, Token), RestoreFailures.Impossible);

        await using var check = scenario.Files.Engine.CreateContext();
        Assert.Equal(2, await check.Set<Project>().CountAsync(Token));
        Assert.DoesNotContain(Directory.GetFiles(scenario.Files.Folder), file => Path.GetFileName(file).StartsWith("data.db.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_backup_with_a_broken_reference_is_refused_as_corrupted_and_the_data_file_stays_as_it_was()
    {
        await using var scenario = await Scenario.PreparedAsync(false);
        var broken = Path.Combine(scenario.Files.Folder, "broken.db");
        File.Copy(scenario.Backup, broken);
        // Sound pages, but records whose project is gone: only `foreign_key_check` tells the backup is damaged.
        await SqliteFiles.BreakReferencesAsync(broken);
        var before = await File.ReadAllBytesAsync(scenario.Files.DataFile, Token);

        var refused = await scenario.Files.Backups(new FixedClock()).RestoreAsync(broken, Token);

        AssertFailure(refused, RestoreFailures.BackupCorrupted);
        Assert.Contains("foreign_key_check", refused.Failure, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllBytesAsync(scenario.Files.DataFile, Token));
        await using var check = scenario.Files.Engine.CreateContext();
        Assert.Equal(2, await check.Set<Project>().CountAsync(Token));
        Assert.DoesNotContain(Directory.GetFiles(scenario.Files.Folder), file => Path.GetFileName(file).StartsWith("data.db.", StringComparison.Ordinal));
    }

    /// <summary>A failure with its code first and no path: the folder of the test holds the login of the account.</summary>
    private static void AssertFailure(RestoreResult result, string code)
    {
        Assert.False(result.Done);
        Assert.StartsWith(code + ": ", result.Failure, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetTempPath().TrimEnd('\\'), result.Failure, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A data file with a first project, its daily backup, then later work: with <c>layoutChanged</c>, many projects
    /// written then their audit removed and written back to the file (its pages no longer those of the backup); then a
    /// second project left in the write-ahead log by a connection that keeps the file open, as a stop of the process
    /// leaves it. The file, its log and its shared memory are kept as they were then.
    /// </summary>
    private sealed class Scenario : IAsyncDisposable
    {
        private Scenario(SqliteFiles files) => Files = files;

        public SqliteFiles Files { get; }

        public string Backup { get; private set; } = null!;

        public Guid First { get; private set; }

        public Guid Second { get; private set; }

        private string Kept(string suffix) => Path.Combine(Files.Folder, "kept" + suffix);

        public static async Task<Scenario> PreparedAsync(bool layoutChanged)
        {
            var scenario = new Scenario(new SqliteFiles());
            try
            {
                await scenario.PrepareAsync(layoutChanged);
                return scenario;
            }
            catch
            {
                await scenario.DisposeAsync();
                throw;
            }
        }

        private async Task PrepareAsync(bool layoutChanged)
        {
            var engine = await Files.MigratedAsync();
            First = (await WriteAsync(engine)).Project.Id;
            var backup = await Files.Backups(new FixedClock()).BackupAsync(BackupReason.Daily, Token);
            Assert.True(backup.Done, backup.Failure);
            Backup = backup.File!;
            if (layoutChanged)
            {
                for (var i = 0; i < 20; i++)
                    await WriteAsync(engine);
                await using (var context = engine.CreateContext())
                    await context.Set<AuditEvent>().ExecuteDeleteAsync(Token);
                SqliteFiles.CloseConnections();
            }
            var holding = new SqliteEngine(new SqliteConnectionStringBuilder { DataSource = Files.DataFile, Pooling = false }.ConnectionString);
            await using (var holder = holding.CreateContext())
            {
                // A reader keeps its shared lock on the file: the log is not written back when the others close.
                await holder.Database.OpenConnectionAsync(Token);
                await holder.Set<Project>().CountAsync(Token);
                Second = (await WriteAsync(engine)).Project.Id;
                SqliteFiles.CloseConnections();
                Assert.True(new FileInfo(Files.DataFile + "-wal").Length > 0);
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                    File.Copy(Files.DataFile + suffix, Kept(suffix));
            }
            // The clean stop of the application writes the log back and empties it (data B1).
            await engine.CloseAsync(Token);
            Assert.False(new FileInfo(Files.DataFile + "-wal") is { Exists: true, Length: > 0 });
        }

        private static async Task<ProjectGraph> WriteAsync(SqliteEngine engine)
        {
            var graph = ProjectGraph.New();
            await using var context = engine.CreateContext();
            context.AddRange([.. graph.Records]);
            await context.SaveChangesAsync(Token);
            return graph;
        }

        /// <summary>The log and the shared memory of the stopped file, next to whatever the data file is now.</summary>
        public void PutBackJournal()
        {
            File.Copy(Kept("-wal"), Files.DataFile + "-wal", overwrite: true);
            File.Copy(Kept("-shm"), Files.DataFile + "-shm", overwrite: true);
        }

        /// <summary>The data file as the stop left it: the file, its log and its shared memory.</summary>
        public void PutBackStoppedFile()
        {
            File.Copy(Kept(""), Files.DataFile, overwrite: true);
            PutBackJournal();
        }

        /// <summary>The data file is the backup: the first project only, a sound file, accepted by the start.</summary>
        public async Task AssertRestoredAsync()
        {
            Assert.False(File.Exists(Files.DataFile + "-wal"));
            Assert.False(File.Exists(Files.DataFile + "-shm"));
            Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(Files.Engine, new FixedClock()).CheckAsync(Token)).Status);
            await using var context = Files.Engine.CreateContext();
            Assert.Equal([First], await context.Set<Project>().Select(project => project.Id).ToListAsync(Token));
            await context.Database.OpenConnectionAsync(Token);
            Assert.Empty(await SqlitePragmas.IntegrityProblemsAsync(context.Database.GetDbConnection(), Token));
            Assert.Empty(await SqlitePragmas.ForeignKeyProblemsAsync(context.Database.GetDbConnection(), Token));
        }

        public ValueTask DisposeAsync() => Files.DisposeAsync();
    }
}
