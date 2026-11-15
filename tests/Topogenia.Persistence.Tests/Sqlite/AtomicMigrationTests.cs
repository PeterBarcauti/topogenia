// AtomicMigrationTests.cs
// A SQLite migration that rebuilds its tables stopped at any of its statements or commits: the next start takes it again, data intact (data B2).

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Sqlite.Connection;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// `TextGuardsAndIntegerBounds` rebuilds the 22 tables that it gives CHECK constraints. EF Core ran the rebuild in three
/// transactions around `PRAGMA foreign_keys` outside any of them: a stop between them left copies `ef_temp_*`, tables
/// dropped, and a file refused for good (`SCHEMA_MODIFIE`). The provider now keeps each migration in one transaction
/// (<see cref="AtomicRebuildSqlGenerator"/>). The file starts at the initial migration with rows in every table, a
/// detailed map among them (a round of references); the migration is stopped before each of its statements in turn, and
/// before each commit, as a crash of the process would stop it (the transaction is rolled back); the next start must
/// read the file as behind, migrate it, and keep every row.
/// </summary>
public sealed class AtomicMigrationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_sqlite_migration_runs_in_a_transaction()
    {
        await using var database = TestDatabase.Create(EngineKind.Sqlite);
        await using var context = database.Context();

        Assert.Empty(MigrationTransactions.OutsideTransaction(context));
    }

    [Fact]
    public async Task A_migration_stopped_before_any_of_its_statements_or_commits_is_taken_again_by_the_next_start_with_its_data()
    {
        await using var template = await Template.PreparedAsync();
        var counting = new Stop(int.MaxValue, int.MaxValue);
        await using (var whole = template.Copy())
        {
            Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(whole.Engine, new FixedClock(), counting).MigrateAsync(Token)).Status);
            await template.AssertIntactAsync(whole);
        }
        // The rebuild alone has more than a hundred statements: every one of them, then every commit.
        Assert.True(counting.Statements > 100, $"{counting.Statements} statements");
        for (var rank = 1; rank <= counting.Statements; rank++)
            await StoppedThenStartedAsync(template, new Stop(rank, int.MaxValue), $"statement {rank}");
        for (var rank = 1; rank <= counting.Commits; rank++)
            await StoppedThenStartedAsync(template, new Stop(int.MaxValue, rank), $"commit {rank}");
    }

    /// <summary>
    /// A crash leaves the row of the lock of EF Core whatever the statement (`VERROU_MIGRATIONS`, ADR 041, apart from this
    /// test); EF Core removes it after most failures, not after one at the creation of its history nor at a commit, which
    /// it rolls back after it released the lock inside the transaction. When the row stays, it is removed as the host
    /// tells to do then, before the next start: the subject here is the schema and the rows.
    /// </summary>
    private static async Task StoppedThenStartedAsync(Template template, Stop stop, string where)
    {
        await using var files = template.Copy();
        // A lock of the migrations left behind would fail the step after a short delay, not after 10 minutes.
        var engine = new SqliteEngine(files.ConnectionString) { MigrationTimeout = TimeSpan.FromSeconds(20) };
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => new SchemaMigrations(engine, new FixedClock(), stop).MigrateAsync(Token));
        Assert.True(failure.GetBaseException() is StoppedException, $"{where}: {failure}");

        // Nothing half done: no copy of a table left, every table there, the file read as behind.
        var schema = await Template.DescriptionAsync(engine);
        Assert.DoesNotContain(schema, line => line.Contains(AtomicRebuildSqlGenerator.CopyPrefix, StringComparison.Ordinal));
        await RemoveLockLeftAsync(engine, where);
        SchemaState? state = null;
        var check = await Record.ExceptionAsync(async () => state = await new SchemaMigrations(engine, new FixedClock()).CheckAsync(Token));
        Assert.True(check is null, $"{where}: {check}");
        Assert.True(state?.Status == SchemaStatus.Behind, $"{where}: {state?.Status}");

        Assert.Equal(SchemaStatus.UpToDate, (await new SchemaMigrations(engine, new FixedClock()).MigrateAsync(Token)).Status);
        Assert.Equal(template.Migrated, await Template.DescriptionAsync(engine));
        await template.AssertIntactAsync(files);
    }

    private static async Task RemoveLockLeftAsync(SqliteEngine engine, string where)
    {
        await using var context = engine.CreateContext();
        await context.Database.OpenConnectionAsync(Token);
        if (await engine.Catalog.LockMarkAsync(context.Database.GetDbConnection(), Token) is null)
            return;
        await OutOfBand.ApplyAsync(context, new DeleteDataOperation
        {
            Table = TableNames.MigrationsLock,
            KeyColumns = ["Id"],
            KeyColumnTypes = ["INTEGER"],
            KeyValues = new object[,] { { 1 } },
        });
        var mark = await engine.Catalog.LockMarkAsync(context.Database.GetDbConnection(), Token);
        Assert.True(mark is null, $"{where}: lock {mark}");
    }

    /// <summary>
    /// Stops the migration before the statement of the given rank or before the commit of the given rank.
    /// </summary>
    private sealed class Stop(int statement, int commit) : DbCommandInterceptor, IDbTransactionInterceptor
    {
        private bool migrating;

        public int Statements { get; private set; }

        public int Commits { get; private set; }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Count(command);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count(command);
            return ValueTask.FromResult(result);
        }

        /// <summary>The commits of the transactions that ran a statement of the migrations, not only the lock.</summary>
        public InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
        {
            if (!migrating)
                return result;
            migrating = false;
            if (++Commits == commit)
                throw new StoppedException();
            return result;
        }

        public ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(TransactionCommitting(transaction, eventData, result));

        /// <summary>
        /// The statements that a transaction of the migrations runs; those of the lock of EF Core and the creation of its
        /// history run outside any (a stop there is a stop before the migration).
        /// </summary>
        private void Count(DbCommand command)
        {
            if (command.Transaction is null)
                return;
            migrating = true;
            if (++Statements == statement)
                throw new StoppedException();
        }
    }

    private sealed class StoppedException : Exception
    {
        public StoppedException()
            : base("The migration was stopped here.")
        {
        }
    }

    /// <summary>
    /// A data file at the initial migration, recorded, with the rows of a project graph (a detailed map among them),
    /// closed; each test step runs on a copy of it in a folder of its own.
    /// </summary>
    private sealed class Template : IAsyncDisposable
    {
        private readonly SqliteFiles files = new();

        private Template(ProjectGraph graph) => Graph = graph;

        public ProjectGraph Graph { get; }

        /// <summary>The schema of a file at the last migration.</summary>
        public IReadOnlyList<string> Migrated { get; private set; } = [];

        public static async Task<Template> PreparedAsync()
        {
            var template = new Template(ProjectGraph.BeforeGlossary());
            try
            {
                await template.PrepareAsync();
                return template;
            }
            catch
            {
                await template.DisposeAsync();
                throw;
            }
        }

        private async Task PrepareAsync()
        {
            await using (var fresh = new SqliteFiles())
                Migrated = await DescriptionAsync(await fresh.MigratedAsync());
            // Migrated, a project written, then taken down to the initial migration by the tools of EF Core: the schema and
            // the record that the initial migration leaves (data M2), with every row. The project is written before the
            // descent: the model of today names columns that came after the initial one (ADR 065).
            await files.MigratedAsync();
            await using (var context = files.Engine.CreateContext())
            {
                context.AddRange([.. Graph.Records]);
                await context.SaveChangesAsync(Token);
            }
            await using (var context = files.Engine.CreateMigrationContext())
                await context.GetService<IMigrator>().MigrateAsync("20261023074737_Initial", Token);
            var state = await new SchemaMigrations(files.Engine, new FixedClock()).CheckAsync(Token);
            Assert.Equal(["20261023074737_Initial"], state.Applied);
            SqliteFiles.CloseConnections();
        }

        /// <summary>A copy of the file and of its log, if it has one, in a new folder.</summary>
        public SqliteFiles Copy()
        {
            var copy = new SqliteFiles();
            foreach (var suffix in new[] { "", "-wal" })
            {
                if (File.Exists(files.DataFile + suffix))
                    File.Copy(files.DataFile + suffix, copy.DataFile + suffix);
            }
            return copy;
        }

        /// <summary>Every row of the graph is there, each type with its count, and the file is sound.</summary>
        public async Task AssertIntactAsync(SqliteFiles copy)
        {
            await using var context = copy.Engine.CreateContext();
            foreach (var group in Graph.Records.GroupBy(record => record.GetType()))
                Assert.Equal(group.Count(), await CountAsync(context, group.Key));
            Assert.Equal([Graph.Project.Id], await context.Set<Project>().Select(project => project.Id).ToListAsync(Token));
            await context.Database.OpenConnectionAsync(Token);
            Assert.Empty(await SqlitePragmas.IntegrityProblemsAsync(context.Database.GetDbConnection(), Token));
            Assert.Empty(await SqlitePragmas.ForeignKeyProblemsAsync(context.Database.GetDbConnection(), Token));
        }

        private static Task<int> CountAsync(DbContext context, Type type)
        {
            var set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(type).Invoke(context, null)!;
            var count = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .Single(method => method.Name == nameof(EntityFrameworkQueryableExtensions.CountAsync) && method.GetParameters().Length == 2)
                .MakeGenericMethod(type);
            return (Task<int>)count.Invoke(null, [set, Token])!;
        }

        public static async Task<IReadOnlyList<string>> DescriptionAsync(SqliteEngine engine)
        {
            await using var context = engine.CreateContext();
            await context.Database.OpenConnectionAsync(Token);
            return await engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token);
        }

        public ValueTask DisposeAsync() => files.DisposeAsync();
    }
}
