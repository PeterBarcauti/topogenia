// TestServer.cs
// The SQL Server instance of the tests: its connection settings, and the creation and the removal of a test base, bounded and retried.

using System.Data;
using System.Runtime.ExceptionServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Topogenia.Persistence.SqlServer;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// The instance of `TEST_SQLSERVER_CONNECTION` (the CI) or the local one with Windows authentication. Several runs of the
/// suites share it (the agents, the CI): a creation takes the exclusive lock of `model`, a removal waits for the sessions
/// of its base, and both queue behind each other on the instance. So, in a process, at most <see cref="ParallelChanges"/>
/// creations or removals run at once (<see cref="gate"/>), and the creations of every process of the session one at a
/// time (<see cref="ModelLock"/>); each is retried on a passing failure
/// (<see cref="TransientFailures"/>), never on another one; the delays are those of a loaded instance
/// (<see cref="CommandTimeout"/>, <see cref="ConnectTimeout"/>); and a failed opening never blocks the pool of the base
/// (`PoolBlockingPeriod=NeverBlock`, otherwise every opening of the next seconds fails with the same error). Removing a
/// base clears the pool of that base only, never every pool of the process (which <c>EnsureDeleted</c> does, cutting the
/// connections of the other tests). The statements are those of the generator of EF Core (the tests write no SQL,
/// spec 3.2), run on the default base of the account. Before its first creation, the process removes the bases left by
/// processes that no longer exist (<see cref="StaleDatabases"/>); a cleaning that fails or lasts is left to the next run
/// (<see cref="QuietlyAsync"/>), it never fails a test.
/// </summary>
public static class TestServer
{
    /// <summary>Creations and removals of bases run at once by one process at most.</summary>
    public const int ParallelChanges = 2;

    /// <summary>The delay of a statement: 120 s instead of 30 (a creation can wait for `model` behind those of other runs).</summary>
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(120);

    /// <summary>The delay of an opening: 60 s instead of 15.</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The longest the removal of the stale bases delays the first creation of a process: what is left then waits for the next run.</summary>
    public static readonly TimeSpan CleaningBound = TimeSpan.FromSeconds(60);

    private static readonly SemaphoreSlim gate = new(ParallelChanges, ParallelChanges);

    private static readonly Lazy<Task> cleaned = new(
        () => QuietlyAsync(StaleDatabases.DropAsync, CleaningBound), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The settings of the instance as given, without base.</summary>
    public static string Server => Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION")
        ?? "Server=.;Integrated Security=True;TrustServerCertificate=True";

    /// <summary>The connection to <paramref name="database"/> (or to the default base of the account), with the delays of the tests.</summary>
    public static string ConnectionString(string? database)
    {
        var builder = new SqlConnectionStringBuilder(Server)
        {
            CommandTimeout = (int)CommandTimeout.TotalSeconds,
            ConnectTimeout = (int)ConnectTimeout.TotalSeconds,
            PoolBlockingPeriod = PoolBlockingPeriod.NeverBlock,
        };
        if (database is not null)
            builder.InitialCatalog = database;
        return builder.ConnectionString;
    }

    /// <summary>
    /// The names of the bases of the instance, once the creations and removals in progress are done (they hold their row):
    /// a committed read of the whole list, which waits for every creation of the instance (see <see cref="ExistsAsync"/>);
    /// a read that fails for a moment (<see cref="TransientFailures.OfReading(Exception)"/>) is read again, bounded.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DatabasesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> names = [];
        await TransientFailures.RetriedAsync(TransientFailures.OfReading, async _ =>
        {
            await using var connection = new SqlConnection(ConnectionString(null));
            await connection.OpenAsync(cancellationToken);
            names = await NamesAsync(connection, cancellationToken);
        }, cancellationToken);
        return names;
    }

    /// <summary>
    /// The names of the bases of the instance read without waiting for the creations and removals in progress, those
    /// included: the list of `master` read uncommitted, on a connection of its own (outside the pool, so that its
    /// isolation never reaches another connection). A `CREATE DATABASE` holds the row of its base until it ends, which a
    /// committed read waits for (`LCK_M_S`), and a creation can wait for `model` behind those of other runs. The
    /// isolation of a transaction stays the one of its session once it ends: the list, read after, is uncommitted.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DatabasesWithoutWaitingAsync(CancellationToken cancellationToken)
    {
        var settings = new SqlConnectionStringBuilder(ConnectionString(null)) { Pooling = false };
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var uncommitted = await connection.BeginTransactionAsync(IsolationLevel.ReadUncommitted, cancellationToken))
            await uncommitted.CommitAsync(cancellationToken);
        return await NamesAsync(connection, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> NamesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var table = await connection.GetSchemaAsync("Databases", cancellationToken);
        return [.. table.Rows.Cast<DataRow>().Select(row => (string)row["database_name"])];
    }

    /// <summary>
    /// Runs a cleaning that must never fail a test: a failure, a cancellation, or a cleaning still running after
    /// <paramref name="bound"/> (cancelled then, and no longer awaited) is left to the next run.
    /// </summary>
    public static async Task QuietlyAsync(Func<CancellationToken, Task> cleaning, TimeSpan bound)
    {
        ArgumentNullException.ThrowIfNull(cleaning);
        using var stop = new CancellationTokenSource(bound);
        try
        {
            await cleaning(stop.Token).WaitAsync(bound, CancellationToken.None);
        }
        catch (Exception)
        {
            // Left to the next run: the bases of a gone process wait, none of the tests does.
        }
    }

    /// <summary>
    /// True when the base <paramref name="name"/> exists, told by an opening of the base itself (outside the pool, without
    /// the new attempt of the driver): 4060 says that it does not exist, any other answer that it does. The list of the
    /// bases is no fit: <c>GetSchemaAsync("Databases")</c> runs <c>where (name = @Name or @Name is null)</c>, a scan of
    /// <c>master..sysdatabases</c> that waits in <c>LCK_M_S</c> for the row of every creation in progress on the instance,
    /// with a name or without (11.1 s behind one slow creation, against 54 ms for a seek on the name), and under the
    /// creations of several runs it meets a new one row after row until its delay ends. The opening waits for the
    /// creation or the removal of this base only, never of another: after a creation cancelled or cut, it answers once the
    /// base is created or rolled back, as the committed read did. A base kept by its only user
    /// (<see cref="TransientFailures.SingleUser"/>, a removal stopped after <c>SINGLE_USER</c>) exists. An opening that
    /// fails for a moment (<see cref="TransientFailures.OfReading(Exception)"/>) is made again, bounded (ADR 040).
    /// </summary>
    public static async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken)
    {
        EnsureTestName(name);
        var settings = new SqlConnectionStringBuilder(ConnectionString(name)) { Pooling = false, ConnectRetryCount = 0 };
        var exists = false;
        await TransientFailures.RetriedAsync(TransientFailures.OfReading, async _ =>
        {
            await using var connection = new SqlConnection(settings.ConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                exists = true;
            }
            catch (SqlException failure) when (TransientFailures.Numbers(failure) is var numbers
                && (numbers.Contains(TransientFailures.CannotOpen) || numbers.Contains(TransientFailures.SingleUser)))
            {
                exists = !numbers.Contains(TransientFailures.CannotOpen);
            }
        }, cancellationToken);
        return exists;
    }

    /// <summary>
    /// Creates the empty base <paramref name="name"/> as EF Core does (its own operation), then waits until it opens. An
    /// attempt after a failure first removes what the previous one may have created.
    /// </summary>
    public static async Task CreateAsync(string name, CancellationToken cancellationToken)
    {
        EnsureTestName(name);
        await cleaned.Value;
        await TransientFailures.RetriedAsync(TransientFailures.OfCreation, async attempt =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (attempt > 1 && await ExistsAsync(name, cancellationToken))
                    await RunOnServerAsync(new SqlServerDropDatabaseOperation { Name = name }, cancellationToken);
                using (await ModelLock.TakeAsync(cancellationToken))
                    await RunOnServerAsync(new SqlServerCreateDatabaseOperation { Name = name }, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken);
        await TransientFailures.RetriedAsync(TransientFailures.OfOpening, async _ =>
        {
            await using var connection = new SqlConnection(ConnectionString(name));
            try
            {
                await connection.OpenAsync(cancellationToken);
            }
            catch (SqlException)
            {
                SqlConnection.ClearPool(connection);
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Runs a step that creates the base itself (the migration of a new base by the code under test), one at a time with
    /// the creations of the process. Only its creation is ever run again: on a passing failure of the creation
    /// (<see cref="TransientFailures.OfCreation(Exception)"/>) that left no base, the step runs again; once the base
    /// exists, the operation that the test checks has begun, and any failure goes up as it is.
    /// </summary>
    public static async Task<T> CreatingAsync<T>(string name, Func<Task<T>> creation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(creation);
        EnsureTestName(name);
        await cleaned.Value;
        return await TransientFailures.WithinBudgetAsync(TransientFailures.Budget, async () =>
        {
            for (var attempt = 1; ; attempt++)
            {
                Exception failure;
                await gate.WaitAsync(cancellationToken);
                try
                {
                    using (await ModelLock.TakeAsync(cancellationToken))
                        return await creation();
                }
                catch (Exception caught) when (TransientFailures.MayRetry(attempt) && TransientFailures.OfCreation(caught))
                {
                    failure = caught;
                }
                finally
                {
                    gate.Release();
                }
                if (await ExistsAsync(name, cancellationToken))
                    ExceptionDispatchInfo.Throw(failure);
                await Task.Delay(TransientFailures.DelayAfter(attempt), cancellationToken);
            }
        });
    }

    /// <summary>
    /// Removes the base <paramref name="name"/> if it exists: the pool of its connections cleared, its sessions rolled back,
    /// then the base dropped (the operation of EF Core), retried on a passing failure. Only a test base is ever removed.
    /// </summary>
    public static async Task DropAsync(string name, CancellationToken cancellationToken)
    {
        EnsureTestName(name);
        await using (var pooled = new SqlConnection(ConnectionString(name)))
            SqlConnection.ClearPool(pooled);
        await TransientFailures.RetriedAsync(TransientFailures.OfRemoval, async _ =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (await ExistsAsync(name, cancellationToken))
                    await RunOnServerAsync(new SqlServerDropDatabaseOperation { Name = name }, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken);
    }

    /// <summary>Only a base of the tests is ever created or removed (prompt 4-2): its name starts with the prefix of the tests.</summary>
    public static void EnsureTestName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!name.StartsWith(TestDatabase.Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"{name} is not a test database.");
    }

    /// <summary>Runs an operation of the generator of EF Core for SQL Server on the default base of the account.</summary>
    private static async Task RunOnServerAsync(MigrationOperation operation, CancellationToken cancellationToken)
    {
        await using var context = new SqlServerEngine(ConnectionString(null)).CreateContext();
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate([operation]);
        await context.GetService<IMigrationCommandExecutor>()
            .ExecuteNonQueryAsync(commands, context.GetService<IRelationalConnection>(), cancellationToken);
    }
}
