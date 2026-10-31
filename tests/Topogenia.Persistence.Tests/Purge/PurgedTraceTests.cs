// PurgedTraceTests.cs
// A purged project leaves no trace in the SQLite data file nor in a backup taken after the purge (ADR 035, security round 4).

using System.Text;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Backup;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Sqlite;

namespace Topogenia.Persistence.Tests.Purge;

/// <summary>
/// The probe of the gate (round 4, security): before the correction, the name, the description and the labels of a
/// purged project stayed in the free pages of `data.db` and were copied into every later backup, 40 days after the purge
/// included, against ADR 035 (« a record erased from the file leaves every backup 30 days after its erasure at most »).
/// The texts are first found in the file, so that their absence afterwards means they were erased, not that the search
/// misses them. SQLite only: SQL Server keeps its pages, its backups and their erasure to the administrator (ADR 035).
/// </summary>
public sealed class PurgedTraceTests
{
    private const int Size = 25;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task On_sqlite_a_purged_project_leaves_no_trace_in_the_data_file_nor_in_a_backup_taken_after_the_purge()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.Sqlite);
        var engine = (SqliteEngine)database.Engine;
        var graph = await PurgeBench.GraphAsync(database);
        string[] texts = [graph.Project.Name, graph.Project.Description!, graph.Code];
        await engine.CloseAsync(Token);
        Assert.All(texts, text => Assert.True(Occurrences(database.Name, text) > 0, text));

        var trashedAt = graph.At(100);
        await PurgeBench.TrashAsync(database, graph, trashedAt);
        var plan = await PurgeBench.PlanAsync(database, graph.Project.Id, PurgeBench.AfterRetention(trashedAt));
        await PurgeBench.PurgeAsync(database, graph.Project.Id, plan, Size);
        await engine.CloseAsync(Token);
        var backups = new SqliteBackupService(engine, Path.Combine(Path.GetDirectoryName(database.Name)!, "backups"), new FixedClock(), SqliteFiles.LocalMachine);
        var backup = await backups.BackupAsync(BackupReason.Daily, Token);

        Assert.True(backup.Done, backup.Failure);
        Assert.Null(await PurgeBench.LoadAsync(database, graph.Project.Id));
        foreach (var file in new[] { database.Name, backup.File! })
            Assert.All(texts, text => Assert.Equal(0, Occurrences(file, text)));
    }

    /// <summary>How many times the UTF-8 bytes of <paramref name="text"/> appear in <paramref name="file"/>.</summary>
    private static int Occurrences(string file, string text)
    {
        var bytes = File.ReadAllBytes(file).AsSpan();
        var needle = Encoding.UTF8.GetBytes(text);
        var count = 0;
        for (var at = bytes.IndexOf(needle); at >= 0; at = bytes.IndexOf(needle))
        {
            count++;
            bytes = bytes[(at + needle.Length)..];
        }
        return count;
    }
}
