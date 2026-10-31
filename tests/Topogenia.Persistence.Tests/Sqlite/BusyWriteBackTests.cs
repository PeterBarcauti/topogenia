// BusyWriteBackTests.cs
// The write-back of the log as a connection closes, met by another connection that holds the write lock: the committed unit runs once and returns (data B1 of round 4).

using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// The probe of the gate (round 4, data B1): the log past <see cref="SqliteDataFileGuard.CheckpointLength"/>, a unit
/// commits, and at once another connection takes the write lock of the file for longer than the busy wait of SQLite and
/// of the provider (6.15 s, ADR 029), as another long unit starting at that instant. The connection of the unit, as it
/// closes, waits in vain to write the log back. Before the correction the busy refusal came out of the commit and the
/// whole unit ran again: its work written twice. Now the unit runs once and returns; the write-back is left to the next
/// connection that closes, which writes the log back once the lock is free. SQLite only.
/// </summary>
public sealed class BusyWriteBackTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(150);

    /// <summary>Longer than the busy wait of one statement (5 s of SQLite, then 1.15 s of the provider, ADR 029).</summary>
    private static readonly TimeSpan Held = TimeSpan.FromSeconds(8);

    /// <summary>The busy timeout of SQLite documented by ADR 029: the closing waited at least this long for the lock.</summary>
    private static readonly TimeSpan BusyTimeout = TimeSpan.FromSeconds(5);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Right after the first commit seen, another connection takes the write lock of the file for <see cref="Held"/>, by a
    /// write in a transaction kept open (as <c>RealBusyTests</c>), and notes the length of the log just before it lets go.
    /// </summary>
    private sealed class HoldWriteLockAfterCommit(TestDatabase database, Guid projectId) : DbTransactionInterceptor
    {
        private int fired;

        public Task Released { get; private set; } = Task.CompletedTask;

        public long LogBeforeRelease { get; private set; }

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref fired, 1) == 1)
                return;
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Released = Task.Run(async () =>
            {
                await using var blocker = database.Context();
                await using var write = await blocker.Database.BeginTransactionAsync(Token);
                await blocker.Set<Project>().Where(project => project.Id == projectId)
                    .ExecuteUpdateAsync(row => row.SetProperty(project => project.Name, project => project.Name), Token);
                held.SetResult();
                await Task.Delay(Held, Token);
                LogBeforeRelease = LogLength(database.Name);
                await write.RollbackAsync(Token);
            }, Token);
            await held.Task;
        }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
            TransactionCommittedAsync(transaction, eventData).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task A_unit_whose_write_back_on_close_finds_the_file_busy_runs_once_and_the_next_closing_writes_the_log_back()
    {
        await using var database = await TestDatabase.MigratedAsync(EngineKind.Sqlite);
        var scenario = await StoreScenario.CreateAsync(database);
        var file = database.Name;
        // The log past its length, written by a context kept open: its own closing comes after the unit.
        await using var filler = database.Context();
        await filler.Database.OpenConnectionAsync(Token);
        while (LogLength(file) < 2 * SqliteDataFileGuard.CheckpointLength)
        {
            for (var i = 0; i < 20; i++)
                filler.Add(new AuditEvent(Guid.CreateVersion7(), FixedClock.Time, null, "access.denied", null, new string('x', 1900)));
            await filler.SaveChangesAsync(Token);
            filler.ChangeTracker.Clear();
        }
        var holder = new HoldWriteLockAfterCommit(database, scenario.ProjectId);
        var retries = new ConcurrentQueue<RetryAttempt>();
        var runs = 0;
        var clock = Stopwatch.StartNew();

        await using (var session = database.Session(RetryPolicy.Standard(retrying: retries.Enqueue), holder))
        {
            await new UnitOfWork(session).RunAsync(async token =>
            {
                Interlocked.Increment(ref runs);
                var store = new WorkspaceStore(session);
                await new UnitOfWork(session).LockProjectAsync(scenario.ProjectId, token);
                var workspace = (await store.LoadAsync(scenario.ProjectId, WorkspaceContent.None, token))!;
                var context = scenario.Context();
                StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 0, "Once"));
                await store.SaveAsync(workspace.Commit(context), token);
                return true;
            }, Token).WaitAsync(Patience, Token);
        }
        var elapsed = clock.Elapsed;
        await holder.Released.WaitAsync(Patience, Token);

        // The closing met the lock and waited for it in vain: the log was as long as before, not written back.
        Assert.InRange(elapsed, BusyTimeout, Patience);
        Assert.True(holder.LogBeforeRelease >= 2 * SqliteDataFileGuard.CheckpointLength, $"log {holder.LogBeforeRelease}");
        Assert.Equal(1, runs);
        Assert.Empty(retries);
        await filler.Database.CloseConnectionAsync();
        Assert.True(LogLength(file) < SqliteDataFileGuard.CheckpointLength, $"log {LogLength(file)}");
        var stored = await scenario.LoadAsync(WorkspaceContent.None);
        Assert.Equal("Once", Assert.Single(stored.All<Step>()).Label);
        Assert.Equal(1, stored.Project.StructureVersion);
    }

    private static long LogLength(string file)
    {
        var log = new FileInfo(file + "-wal");
        return log.Exists ? log.Length : 0;
    }
}
