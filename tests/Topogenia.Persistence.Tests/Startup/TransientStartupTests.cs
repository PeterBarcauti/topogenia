// TransientStartupTests.cs
// A contention met by the check or the migration of the start is a transient failure, never missing rights (ADR 036, annex A 5).

using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Startup;

/// <summary>
/// SQL Server only (the busy file of SQLite met by the start is proven in <see cref="SqliteStartupErrorTests"/>). SQL Server
/// gives several errors in one failure: CREATE DATABASE refused for a lock on `model` held by another creation (1807)
/// comes with 1802 (CREATE DATABASE failed), which also comes with a full disk or a wrong path. 1802 alone is what the
/// 1.x read as missing rights (`SqlException.Number`, the first error); with a transient error, the failure is a
/// contention, read as <see cref="SchemaCodes.DatabaseBusy"/>, which a new run clears. The real 1807 needs the exclusive
/// lock of `model` held by another session of the instance for the length of a CREATE DATABASE: every creation of the
/// instance would then fail, those of the other test processes included, and no test may hold a lock on a system
/// database (prompt 4-2). The failures are therefore built as the engine gives them (the numbers in the order of the
/// server, through the internal factory of the driver), and sent through an interceptor in place of a real command, so
/// that the path of the check and of the migration is the real one.
/// </summary>
public sealed class TransientStartupTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A failure of SQL Server with these errors, the first one giving <see cref="SqlException.Number"/>.</summary>
    public static SqlException Failure(params int[] numbers)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        var add = typeof(SqlErrorCollection).GetMethod("Add", flags)!;
        foreach (var number in numbers)
            add.Invoke(errors, [Error(number)]);
        var create = typeof(SqlException).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).First(method =>
            method.Name == "CreateException" && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual([typeof(SqlErrorCollection), typeof(string)]));
        return (SqlException)create.Invoke(null, [errors, "17.0.0"])!;
    }

    private static SqlError Error(int number)
    {
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(candidate => candidate.GetParameters() is [{ ParameterType: var first }, ..] && first == typeof(int))
            .OrderBy(candidate => candidate.GetParameters().Length)
            .First();
        var arguments = constructor.GetParameters().Select((parameter, index) => index == 0 ? number : Default(parameter.ParameterType, number)).ToArray();
        return (SqlError)constructor.Invoke(arguments);
    }

    private static object? Default(Type type, int number) =>
        type == typeof(byte) ? (byte)16 : type == typeof(string) ? $"Error {number}." : type == typeof(int) ? 0 : type == typeof(uint) ? 0u : null;

    [Theory]
    [InlineData(new[] { 1807, 1802 }, SchemaCodes.DatabaseBusy)]
    [InlineData(new[] { 1802, 1807 }, SchemaCodes.DatabaseBusy)]
    [InlineData(new[] { 5061, 5069 }, SchemaCodes.DatabaseBusy)]
    [InlineData(new[] { 1205 }, SchemaCodes.DatabaseBusy)]
    [InlineData(new[] { 1222 }, SchemaCodes.DatabaseBusy)]
    [InlineData(new[] { -2 }, SchemaCodes.DatabaseBusy)]
    [InlineData(new[] { 10054 }, SchemaCodes.DatabaseBusy)]
    [InlineData(new[] { 1802 }, SchemaCodes.InsufficientRights)]
    [InlineData(new[] { 262 }, SchemaCodes.InsufficientRights)]
    [InlineData(new[] { 229 }, SchemaCodes.InsufficientRights)]
    [InlineData(new[] { 4060, 18456 }, SchemaCodes.DatabaseUnreachable)]
    [InlineData(new[] { 4060 }, SchemaCodes.DatabaseUnreachable)]
    [InlineData(new[] { 18456 }, SchemaCodes.InsufficientRights)]
    [InlineData(new[] { 5133, 1802 }, null)]
    [InlineData(new[] { 1802, 1101 }, null)]
    [InlineData(new[] { 547 }, null)]
    public void The_errors_of_one_failure_are_read_together_a_contention_first(int[] numbers, string? code)
    {
        var catalog = new SqlServerEngine("Server=.").Catalog;
        var failure = Failure(numbers);
        Assert.Equal(numbers[0], failure.Number);
        Assert.Equal(code, catalog.RefusalOf(failure));
        Assert.Equal(code, catalog.RefusalOf(new InvalidOperationException("Wrapped by EF Core.", failure)));
    }

    [Fact]
    public async Task A_contention_of_the_check_and_of_the_migration_is_transient_and_a_new_run_succeeds()
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var contention = new FailingCommands(Failure(1807, 1802), 2);
        var migrations = new SchemaMigrations(database.Engine, new FixedClock(), contention);

        var check = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.CheckAsync(Token));
        var migration = await Assert.ThrowsAsync<SchemaRefusedException>(() => migrations.MigrateAsync(Token));

        Assert.All(new[] { check, migration }, refusal =>
        {
            Assert.Equal(SchemaCodes.DatabaseBusy, refusal.Code);
            Assert.True(refusal.IsTransient);
            // EF Core wraps the failure of the server as a failure that is likely transient; its numbers stay inside, in
            // the order of the server, without its message (ADR 036).
            Assert.Equal([1807, 1802], Assert.IsType<EngineFailureException>(refusal.InnerException).Numbers);
        });
        Assert.Equal(SchemaStatus.UpToDate, (await migrations.CheckAsync(Token)).Status);
        Assert.Equal(SchemaStatus.UpToDate, (await migrations.MigrateAsync(Token)).Status);
    }

    [Fact]
    public void Only_a_contention_is_transient()
    {
        Assert.True(new SchemaRefusedException(SchemaCodes.DatabaseBusy, null).IsTransient);
        Assert.All(
            new[] { SchemaCodes.InsufficientRights, SchemaCodes.DatabaseUnreachable, SchemaCodes.SchemaChanged, SchemaCodes.NewerDatabase, SchemaCodes.MigrationLockHeld },
            code => Assert.False(new SchemaRefusedException(code, null).IsTransient));
    }

    /// <summary>Fails the next <paramref name="count"/> commands with <paramref name="failure"/>, before the engine runs them.</summary>
    private sealed class FailingCommands(Exception failure, int count) : DbCommandInterceptor
    {
        private int count = count;

        private Exception? Next() => Interlocked.Decrement(ref count) >= 0 ? failure : null;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            Next() is { } next ? throw next : ValueTask.FromResult(result);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Next() is { } next ? throw next : ValueTask.FromResult(result);

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) =>
            Next() is { } next ? throw next : ValueTask.FromResult(result);
    }
}
