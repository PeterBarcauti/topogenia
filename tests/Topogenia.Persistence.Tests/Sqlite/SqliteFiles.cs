// SqliteFiles.cs
// The temporary folder of one SQLite test: a data file and a backup folder, deleted with the folder at the end of the test.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Backup;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Sqlite.Files;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// A folder created for the test under the temporary folder of the user, holding the data file `data.db` (not created:
/// the migrations create it) and the backup folder `backups` (not created: the backup creates it). The connections are
/// pooled, as in the application. Disposing clears the pools and deletes the folder with everything in it. Its name
/// carries the process number (`topogenia-test-&lt;pid&gt;-sqlite-&lt;random&gt;`): a folder left by a process that no
/// longer runs is removed by the next run (<see cref="StaleDatabases.DropFolders"/>).
/// </summary>
public sealed class SqliteFiles : IAsyncDisposable
{
    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-sqlite-");

    public SqliteFiles()
    {
        DataFile = Path.Combine(folder.FullName, "data.db");
        BackupFolder = Path.Combine(folder.FullName, "backups");
        ConnectionString = new SqliteConnectionStringBuilder { DataSource = DataFile, Pooling = true }.ConnectionString;
        Engine = new SqliteEngine(ConnectionString);
    }

    public string Folder => folder.FullName;

    public string DataFile { get; }

    public string BackupFolder { get; }

    public string ConnectionString { get; }

    public SqliteEngine Engine { get; }

    /// <summary>Creates the data file with every migration of the application.</summary>
    public async Task<SqliteEngine> MigratedAsync()
    {
        await new SchemaMigrations(Engine, new FixedClock()).MigrateAsync(TestContext.Current.CancellationToken);
        return Engine;
    }

    /// <summary>A fictitious machine without synchronised folder nor network drive: the temporary folder is accepted.</summary>
    public static DataFileEnvironment LocalMachine { get; } = new([], _ => false);

    /// <summary>The backup service of this folder, on the given clock.</summary>
    public SqliteBackupService Backups(IClock clock) => new(Engine, BackupFolder, clock, LocalMachine);

    /// <summary>The files of the backup folder, by name; none when the folder does not exist.</summary>
    public IReadOnlyList<string> BackupFiles() =>
        Directory.Exists(BackupFolder)
            ? Directory.GetFiles(BackupFolder).Select(file => Path.GetFileName(file)!).Order(StringComparer.Ordinal).ToList()
            : [];

    /// <summary>A context of the model on a connection of the test, outside the engine: none of its settings nor its guard.</summary>
    public static AppDbContext PlainContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer, SqliteModelCustomizer>()
            .Options);

    /// <summary>
    /// References broken in a file by a connection of its own, outside the engine and with the foreign keys off: the
    /// projects removed from under their records. `integrity_check` still says ok, only `foreign_key_check` sees it. The
    /// connection is not pooled and leaves no journal once closed.
    /// </summary>
    public static async Task BreakReferencesAsync(string file)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false, ForeignKeys = false }.ConnectionString);
        await connection.OpenAsync(token);
        await using (var context = PlainContext(connection))
            Assert.True(await context.Set<Project>().ExecuteDeleteAsync(token) > 0);
        Assert.Empty(await SqlitePragmas.IntegrityProblemsAsync(connection, token));
        Assert.NotEmpty(await SqlitePragmas.ForeignKeyProblemsAsync(connection, token));
    }

    /// <summary>Closes every pooled connection and releases the files (the log is not checkpointed on close, ADR 029).</summary>
    public static void CloseConnections() => SqliteConnection.ClearAllPools();

    public ValueTask DisposeAsync()
    {
        CloseConnections();
        folder.Refresh();
        if (folder.Exists)
            folder.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }
}

/// <summary>A clock set by the test, in UTC: the day of a backup is the UTC day of the clock (D08).</summary>
public sealed class SettableClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}
