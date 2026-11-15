// TestInfrastructureTests.cs
// The setup of the test bases under load: passing failures read and retried, failed preparations removed, stale bases cleaned.

using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// The suites run at the same time on one instance (agents, CI): the creation of a base can fail for a moment, and a run
/// stopped before its end leaves its bases. What the tests prove of their own setup: only the passing failures of the
/// instance are retried (1802 alone never is), within one budget however nested, a step of a test that creates its base
/// runs again only while the base does not exist, a preparation that fails removes its base, a class fixture keeps a base
/// only once migrated, and only the bases of the tests whose process is gone are cleaned, a failed cleaning failing no test.
/// A failure that the setup retries is produced as the code under test meets it: an opening that fails with the numbers
/// of the instance (<see cref="FailingOpenings"/>), translated by the real <see cref="SchemaMigrations"/>.
/// </summary>
public sealed class TestInfrastructureTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A real 4060 of the instance: the opening of a base that does not exist, wrapped as EF Core and a task would.</summary>
    private static async Task<Exception> CannotOpenAsync()
    {
        // Without the new attempt of the driver on a passing failure of the opening (10 s by default).
        var absent = new SqlConnectionStringBuilder(TestServer.ConnectionString(TestDatabase.Prefix + "absent_" + Guid.CreateVersion7().ToString("N"))) { ConnectRetryCount = 0 };
        await using var connection = new SqlConnection(absent.ConnectionString);
        var failure = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync(Token));
        SqlConnection.ClearPool(connection);
        return new AggregateException(new InvalidOperationException("Wrapped.", failure));
    }

    private static SqlException Failure(params int[] numbers) => Startup.TransientStartupTests.Failure(numbers);

    /// <summary>The next <paramref name="times"/> openings of the connections fail with <paramref name="numbers"/>.</summary>
    private static FailingOpenings Openings(int times, params int[] numbers) => new(() => Failure(numbers), times);

    private static SchemaMigrations Migrations(TestDatabase database, params IInterceptor[] interceptors) =>
        new(database.Engine, new FixedClock(), interceptors);

    /// <summary>
    /// What the migrations of the code under test throw when every opening of their connections fails with the SQL Server
    /// errors <paramref name="numbers"/>, as a preparation of the tests receives it; nothing reaches the server.
    /// </summary>
    private static async Task<Exception> MigrationFailureAsync(params int[] numbers)
    {
        await using var database = TestDatabase.Create(EngineKind.SqlServer);
        return await Assert.ThrowsAnyAsync<Exception>(() => Migrations(database, Openings(int.MaxValue, numbers)).MigrateAsync(Token));
    }

    /// <summary>A process number that runs on this machine, and one that does not (above every running one).</summary>
    private static (int Running, int Gone) Processes()
    {
        var running = System.Diagnostics.Process.GetProcesses().Select(process =>
        {
            using (process)
                return process.Id;
        }).ToHashSet();
        var gone = running.Max() + 4;
        while (running.Contains(gone))
            gone += 4;
        return (Environment.ProcessId, gone);
    }

    [Fact]
    public async Task The_numbers_of_every_sql_server_error_of_the_chain_are_read_through_wrappers_and_aggregates()
    {
        var failure = await CannotOpenAsync();

        Assert.Contains(TransientFailures.CannotOpen, TransientFailures.Numbers(failure));
        Assert.True(TransientFailures.OfOpening(failure));
        Assert.True(TransientFailures.OfSetup(failure));
        Assert.False(TransientFailures.OfCreation(failure));
        Assert.False(TransientFailures.OfRemoval(failure));
        Assert.Empty(TransientFailures.Numbers(new InvalidOperationException("Not a failure of the engine.")));
    }

    [Theory]
    [InlineData(new[] { 233 }, true)]
    [InlineData(new[] { 10054, 10053 }, true)]
    [InlineData(new[] { 4060, 18456 }, false)]
    [InlineData(new[] { 4060, 233 }, false)]
    [InlineData(new[] { -2 }, false)]
    public void Only_a_failure_made_of_cut_connections_is_without_an_answer_of_the_server(int[] numbers, bool unanswered)
    {
        var failure = Startup.TransientStartupTests.Failure(numbers);
        Assert.Equal(unanswered, TransientFailures.Unanswered(failure));
        Assert.Equal(unanswered, TransientFailures.Unanswered(new InvalidOperationException("Wrapped.", failure)));
        Assert.False(TransientFailures.Unanswered(new InvalidOperationException("Not a failure of the engine.")));
    }

    [Theory]
    [InlineData(new[] { 1802, 1807 }, true, false, true)]
    [InlineData(new[] { 1807 }, true, false, true)]
    [InlineData(new[] { 1802 }, false, false, false)]
    [InlineData(new[] { 262 }, false, false, false)]
    [InlineData(new[] { 229 }, false, false, false)]
    [InlineData(new[] { 4060 }, false, true, false)]
    [InlineData(new[] { -2 }, true, true, true)]
    [InlineData(new[] { 1205 }, true, false, true)]
    [InlineData(new[] { 3702 }, false, false, true)]
    [InlineData(new[] { 12408, 5069 }, false, false, true)]
    [InlineData(new[] { 10054 }, true, true, true)]
    [InlineData(new[] { 2627 }, false, false, false)]
    public void Only_the_passing_failures_of_the_instance_are_retried_and_1802_alone_never_is(int[] numbers, bool creation, bool opening, bool removal)
    {
        Assert.Equal((creation, opening, removal),
            (TransientFailures.OfCreation(numbers), TransientFailures.OfOpening(numbers), TransientFailures.OfRemoval(numbers)));
    }

    [Theory]
    [InlineData(new[] { 1807, 1802 }, SchemaCodes.DatabaseBusy, true)]
    [InlineData(new[] { 1205 }, SchemaCodes.DatabaseBusy, true)]
    [InlineData(new[] { 1222 }, SchemaCodes.DatabaseBusy, false)]
    [InlineData(new[] { 4060, 18456 }, SchemaCodes.DatabaseUnreachable, false)]
    [InlineData(new[] { 1802 }, SchemaCodes.InsufficientRights, false)]
    [InlineData(new[] { 229 }, SchemaCodes.InsufficientRights, false)]
    public async Task A_failure_of_the_instance_refused_by_the_migrations_is_passing_only_when_busy_with_a_passing_number(int[] numbers, string code, bool passing)
    {
        var failure = await MigrationFailureAsync(numbers);

        Assert.Equal(code, Assert.IsType<SchemaRefusedException>(failure).Code);
        Assert.Equal((passing, passing), (TransientFailures.OfSetup(failure), TransientFailures.OfCreation(failure)));
        // The catalogue of the provider gives the same refusal (SqlServerSchemaCatalog.RefusalOf, NumbersOf).
        var catalog = new SqlServerEngine(TestServer.ConnectionString(null)).Catalog;
        var raw = Failure(numbers);
        var translated = new SchemaRefusedException(catalog.RefusalOf(raw)!, null, new EngineFailureException(catalog.NumbersOf(raw)));
        Assert.Equal((code, passing), (translated.Code, TransientFailures.OfSetup(translated)));
    }

    [Theory]
    [InlineData(SchemaCodes.MigrationLockHeld)]
    [InlineData(SchemaCodes.DatabaseUnreachable)]
    [InlineData(SchemaCodes.InsufficientRights)]
    [InlineData(SchemaCodes.SchemaChanged)]
    public void The_numbers_that_a_refusal_keeps_are_read_under_a_busy_database_only(string code)
    {
        var engine = new EngineFailureException([TransientFailures.ModelLocked, TransientFailures.Timeout, 233]);

        Assert.Empty(TransientFailures.Numbers(new SchemaRefusedException(code, null, engine)));
        Assert.False(TransientFailures.OfSetup(new InvalidOperationException("Wrapped.", new SchemaRefusedException(code, null, engine))));
        Assert.Equal(engine.Numbers, TransientFailures.Numbers(new SchemaRefusedException(SchemaCodes.DatabaseBusy, null, engine)));
        Assert.True(TransientFailures.OfSetup(new InvalidOperationException("Wrapped.", new SchemaRefusedException(SchemaCodes.DatabaseBusy, null, engine))));
    }

    [Fact]
    public async Task A_step_is_retried_on_a_passing_failure_a_bounded_number_of_times_and_never_on_another_one()
    {
        var passing = await CannotOpenAsync();
        var runs = 0;
        await TransientFailures.RetriedAsync(TransientFailures.OfOpening, attempt =>
        {
            runs++;
            return attempt < 3 ? Task.FromException(passing) : Task.CompletedTask;
        }, Token);
        Assert.Equal(3, runs);

        runs = 0;
        var other = new InvalidOperationException("A real failure.");
        Assert.Same(other, await Assert.ThrowsAsync<InvalidOperationException>(() => TransientFailures.RetriedAsync(TransientFailures.OfOpening, _ =>
        {
            runs++;
            return Task.FromException(other);
        }, Token)));
        Assert.Equal(1, runs);
        Assert.Equal(TimeSpan.FromSeconds(8), TransientFailures.DelayAfter(TransientFailures.Attempts));
    }

    [Fact]
    public async Task Nested_retries_share_the_budget_of_the_outermost_one_and_none_starts_once_it_is_spent()
    {
        var passing = await CannotOpenAsync();
        var runs = 0;

        Assert.Same(passing, await Assert.ThrowsAsync<AggregateException>(() => TransientFailures.RetriedAsync(TransientFailures.OfOpening,
            _ => TransientFailures.RetriedAsync(TransientFailures.OfOpening, _ =>
            {
                runs++;
                return Task.FromException(passing);
            }, Token), TimeSpan.Zero, Token)));

        Assert.Equal(1, runs);
        Assert.Equal(TimeSpan.FromMinutes(10), TransientFailures.Budget);
        Assert.True(TransientFailures.MayRetry(TransientFailures.Attempts - 1));
        Assert.False(TransientFailures.MayRetry(TransientFailures.Attempts));
    }

    [Fact]
    public async Task A_step_of_a_test_that_creates_its_base_runs_again_only_on_a_passing_failure_of_the_creation_that_left_no_base()
    {
        await using var database = TestDatabase.Create(EngineKind.SqlServer);
        var busy = Openings(1, TransientFailures.ModelLocked, TransientFailures.CreationFailed);
        var runs = 0;

        var state = await database.CreatingAsync(() =>
        {
            runs++;
            return Migrations(database, busy).MigrateAsync(Token);
        });
        Assert.Equal((2, 1, SchemaStatus.UpToDate), (runs, busy.Failed, state.Status));
        Assert.True(await TestServer.ExistsAsync(database.Name, Token));

        // Any other refusal before the base exists is the one of the step: never run again.
        await using var refused = TestDatabase.Create(EngineKind.SqlServer);
        var unreachable = Openings(int.MaxValue, 4060, 18456);
        runs = 0;
        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => refused.CreatingAsync(() =>
        {
            runs++;
            return Migrations(refused, unreachable).MigrateAsync(Token);
        }));
        Assert.Equal((SchemaCodes.DatabaseUnreachable, 1), (refusal.Code, runs));
        Assert.False(await TestServer.ExistsAsync(refused.Name, Token));
    }

    [Fact]
    public async Task A_step_of_a_test_that_creates_its_base_never_runs_again_once_the_base_exists()
    {
        await using var database = TestDatabase.Create(EngineKind.SqlServer);
        var runs = 0;

        var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => database.CreatingAsync(async () =>
        {
            runs++;
            await Migrations(database).MigrateAsync(Token);
            return await Migrations(database, Openings(1, TransientFailures.ModelLocked, TransientFailures.CreationFailed)).CheckAsync(Token);
        }));

        Assert.Equal((SchemaCodes.DatabaseBusy, 1), (refusal.Code, runs));
        Assert.True(TransientFailures.OfCreation(refusal));
    }

    [Fact]
    public async Task A_cleaning_that_fails_or_lasts_is_left_to_the_next_run_and_never_fails_a_test()
    {
        var passing = await CannotOpenAsync();
        var clock = Stopwatch.StartNew();

        await TestServer.QuietlyAsync(_ => Task.FromException(passing), TimeSpan.FromSeconds(30));
        await TestServer.QuietlyAsync(_ => throw new InvalidOperationException("A cleaning that fails at once."), TimeSpan.FromSeconds(30));
        await TestServer.QuietlyAsync(token => Task.Delay(Timeout.InfiniteTimeSpan, token), TimeSpan.FromMilliseconds(200));
        // A cleaning deaf to its cancellation is no longer awaited.
        await TestServer.QuietlyAsync(_ => new TaskCompletionSource().Task, TimeSpan.FromMilliseconds(200));

        Assert.InRange(clock.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task The_list_read_without_waiting_names_the_bases_as_the_committed_one_does()
    {
        await using var database = await TestDatabase.CreatedAsync(EngineKind.SqlServer);

        Assert.Contains(database.Name, await TestServer.DatabasesWithoutWaitingAsync(Token));
        Assert.Contains(database.Name, await TestServer.DatabasesAsync(Token));
        Assert.True(await TestServer.ExistsAsync(database.Name, Token));
    }

    [Fact]
    public async Task A_base_exists_once_created_and_no_longer_once_a_creation_cancelled_at_any_moment_is_rolled_back()
    {
        Assert.False(await TestServer.ExistsAsync(TestDatabase.Prefix + "absent_" + Guid.CreateVersion7().ToString("N"), Token));
        foreach (var delay in new[] { 0, 20, 60, 150, 400, 1000 })
        {
            await using var database = TestDatabase.Create(EngineKind.SqlServer);
            using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(delay));
            try
            {
                await TestServer.CreateAsync(database.Name, stop.Token);
            }
            catch (Exception) when (stop.IsCancellationRequested)
            {
                // Cancelled before, during or after its statement: the base exists or not, as the list says once it ends.
            }

            // The opening waits for the end of the creation of this base; the list, read after, is final for it.
            var exists = await TestServer.ExistsAsync(database.Name, Token);
            Assert.Equal((await TestServer.DatabasesWithoutWaitingAsync(Token)).Contains(database.Name), exists);
        }
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_preparation_that_fails_removes_its_database_and_is_not_retried_unless_the_failure_is_passing(EngineKind kind)
    {
        var names = new List<string>();
        var refusal = new InvalidOperationException("A real failure of the preparation.");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => TestDatabase.PreparedAsync(kind, database =>
        {
            names.Add(database.Name);
            return Task.FromException(refusal);
        }));

        Assert.Same(refusal, thrown);
        Assert.Single(names);
        Assert.False(await ExistsAsync(kind, names[0]));
    }

    [Fact]
    public async Task On_sql_server_a_preparation_that_fails_for_a_moment_starts_again_on_a_new_database()
    {
        var deadlock = Openings(1, TransientFailures.Deadlock);
        var names = new List<string>();

        await using var database = await TestDatabase.PreparedAsync(EngineKind.SqlServer, candidate =>
        {
            names.Add(candidate.Name);
            return Migrations(candidate, deadlock).MigrateAsync(Token);
        });

        Assert.Equal((2, 1), (names.Count, deadlock.Failed));
        Assert.Equal(names[1], database.Name);
        Assert.False(await TestServer.ExistsAsync(names[0], Token));
        Assert.True(await TestServer.ExistsAsync(names[1], Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_class_fixture_keeps_a_database_only_once_migrated_and_the_next_test_gets_a_new_one(EngineKind kind)
    {
        var made = new List<TestDatabase>();
        var fixture = new MigratedDatabases(async engine =>
        {
            // The first preparation fails, as a migration refused by a loaded instance would.
            if (made.Count == 0)
            {
                made.Add(await TestDatabase.CreatedAsync(engine));
                await made[0].DisposeAsync();
                throw new InvalidOperationException("The migration failed.");
            }
            var database = await TestDatabase.MigratedAsync(engine);
            made.Add(database);
            return database;
        });
        await using (fixture)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.OfAsync(kind));
            var kept = await fixture.OfAsync(kind);

            Assert.Same(made[1], kept);
            Assert.NotEqual(made[0].Name, kept.Name);
            Assert.Same(kept, await fixture.OfAsync(kind));
            await using var context = kept.Context();
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(Token));
        }
        Assert.False(await ExistsAsync(kind, made[1].Name));
    }

    [Theory]
    [InlineData("Topogenia_Test_{gone}_1", true)]
    [InlineData("Topogenia_Test_{gone}_123456", true)]
    [InlineData("Topogenia_Test_{running}_1", false)]
    [InlineData("Topogenia_Test_{gone}", false)]
    [InlineData("Topogenia_Test_{gone}_1_2", true)]
    [InlineData("Topogenia_Test_{gone}_1_kept", true)]
    [InlineData("Topogenia_Test_{running}_1_kept", false)]
    [InlineData("Topogenia_Test_{gone}_1_", false)]
    [InlineData("Topogenia_Test_{gone}_1_a_b", false)]
    [InlineData("Topogenia_Test_{gone}_1_k-1", false)]
    [InlineData("Topogenia_Test_{gone}_x_kept", false)]
    [InlineData("Topogenia_Test_{gone}_x", false)]
    [InlineData("Topogenia_Test_{gone}_", false)]
    [InlineData("Topogenia_Test__1", false)]
    [InlineData("Topogenia_Test_-{gone}_1", false)]
    [InlineData("Topogenia_Test_{gone}_١", false)]
    [InlineData("topogenia_test_{gone}_1", false)]
    [InlineData("Topogenia_Dev", false)]
    [InlineData("Topogenia_E2E", false)]
    [InlineData("Ariane_Test_{gone}_1", false)]
    [InlineData("ARIANE_ESSAI", false)]
    [InlineData("x_Topogenia_Test_{gone}_1", false)]
    public void Only_a_test_database_whose_process_is_gone_is_stale(string pattern, bool stale)
    {
        var (running, gone) = Processes();
        var name = pattern.Replace("{gone}", gone.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{running}", running.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        Assert.Equal(stale, StaleDatabases.Of([name], process => process != gone).Contains(name));
    }

    [Theory]
    [InlineData(".", true)]
    [InlineData(@".\SQLEXPRESS", true)]
    [InlineData("(local)", true)]
    [InlineData("localhost,1433", true)]
    [InlineData("tcp:127.0.0.1,1433", true)]
    [InlineData("np:.", true)]
    [InlineData("db.example.org", false)]
    [InlineData(@"tcp:shared-server\TEST,1433", false)]
    public void The_stale_databases_are_cleaned_on_a_local_instance_only(string dataSource, bool local)
    {
        Assert.Equal(local, StaleDatabases.IsLocal(dataSource));
        Assert.True(StaleDatabases.IsLocal(Environment.MachineName + @"\SQLEXPRESS"));
    }

    [Fact]
    public async Task On_sql_server_the_bases_of_a_gone_process_are_removed_and_every_other_base_is_kept()
    {
        var (_, gone) = Processes();
        var stale = $"{TestDatabase.Prefix}{gone}_{Environment.ProcessId}";
        string[] gones = [stale, $"{stale}_kept"];
        await using var mine = await TestDatabase.CreatedAsync(EngineKind.SqlServer);
        // The bases that no test creates nor removes stay as they are while the test runs.
        var others = (await TestServer.DatabasesWithoutWaitingAsync(Token)).Where(name => !name.StartsWith("Topogenia_", StringComparison.Ordinal)).ToList();
        try
        {
            foreach (var name in gones)
                await TestServer.CreateAsync(name, Token);

            await StaleDatabases.DropAsync(Token);

            foreach (var name in gones)
                Assert.False(await TestServer.ExistsAsync(name, Token), name);
            Assert.True(await TestServer.ExistsAsync(mine.Name, Token));
            Assert.Empty(others.Except(await TestServer.DatabasesWithoutWaitingAsync(Token), StringComparer.Ordinal));
        }
        finally
        {
            await DroppedAsync(gones);
        }
    }

    /// <summary>Removes every base of <paramref name="names"/>, each whatever the removal of another does; the failures go up together.</summary>
    private static async Task DroppedAsync(IEnumerable<string> names)
    {
        var failures = new List<Exception>();
        foreach (var name in names)
        {
            try
            {
                await TestServer.DropAsync(name, Token);
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }
        if (failures.Count > 0)
            throw new AggregateException("Some test databases could not be removed.", failures);
    }

    [Fact]
    public void A_user_made_from_a_user_of_a_graph_never_takes_the_login_of_another_graph()
    {
        var graph = ProjectGraph.New();
        foreach (var user in new[] { graph.Owner, graph.Contributor, graph.Reader })
        {
            Assert.True(ProjectGraph.IsLoginOfAGraph(user.LoginNormalized));
            var other = ProjectGraph.OtherUser(user, "former");
            Assert.NotEqual(user.Id, other.Id);
            Assert.False(ProjectGraph.IsLoginOfAGraph(other.Login), other.Login);
            Assert.Equal(Topogenia.Domain.Values.Login.Normalize(other.Login), other.LoginNormalized);
        }
        Assert.Equal(graph.Of<AppUser>().Count, graph.Of<AppUser>().Select(user => user.LoginNormalized).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_folders_of_a_gone_process_are_removed_and_every_other_folder_is_kept()
    {
        var (running, gone) = Processes();
        var root = Directory.CreateTempSubdirectory($"{TestDatabase.FolderPrefix}{Environment.ProcessId}-stale-folders-");
        try
        {
            string[] stale = [$"topogenia-test-{gone}-abc.def"];
            string[] kept = [$"topogenia-test-{running}-abc.def", "topogenia-test-abcdefgh.ijk", $"topogenia-test-{gone}-", $"topogenia-test-x{gone}-a", $"other-{gone}-a"];
            foreach (var name in stale.Concat(kept))
                File.WriteAllText(Path.Combine(root.CreateSubdirectory(name).FullName, "data.db"), "");

            Assert.Equal(stale, StaleDatabases.DropFolders(root.FullName));

            Assert.Equal(kept.Order(StringComparer.Ordinal), root.EnumerateDirectories().Select(folder => folder.Name).Order(StringComparer.Ordinal));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task The_folder_of_the_sqlite_files_of_a_test_carries_its_process_number_as_every_cleaned_folder()
    {
        await using var files = new Sqlite.SqliteFiles();
        Assert.Equal(Environment.ProcessId, StaleDatabases.ProcessOfFolder(Path.GetFileName(files.Folder)));
    }

    private static async Task<bool> ExistsAsync(EngineKind kind, string name) =>
        kind == EngineKind.SqlServer ? await TestServer.ExistsAsync(name, Token) : Directory.Exists(Path.GetDirectoryName(name));
}
