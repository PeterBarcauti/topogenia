// SqliteCatalogChangeTests.cs
// Objects added by hand to a SQLite file that the description of the schema once missed: a name that looks internal, an object on a history table.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Topogenia.Application.Ports;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Schema;

/// <summary>
/// SQLite only: the description reads `sqlite_master`, the internal objects of SQLite (named `sqlite_` and something) and
/// the two tables of EF Core aside. The underscore of a `LIKE` pattern matches any character, and leaving aside every
/// object attached to a table of EF Core leaves aside what a person attaches to it: both are proven on an index, which
/// the generator of the provider creates without any SQL in the test (spec 3.2); a trigger goes through the same filter
/// of the same query (`type` plays no part in it). Each object is refused as a changed schema, then dropped, and the file
/// is accepted again.
/// </summary>
public sealed class SqliteCatalogChangeTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<SchemaState> CheckAsync(TestDatabase database) => new SchemaMigrations(database.Engine, new FixedClock()).CheckAsync(Token);

    [Theory]
    [InlineData("sqlitex_probe", TableNames.Step, "code")]
    [InlineData("SQLITEXPROBE", TableNames.Step, "code")]
    [InlineData("IX_probe_history", TableNames.MigrationsHistory, "ProductVersion")]
    [InlineData("IX_probe_lock", TableNames.MigrationsLock, "Timestamp")]
    public async Task An_index_added_by_hand_is_refused_whatever_its_name_and_its_table(string index, string table, string column)
    {
        var database = await databases.OfAsync(EngineKind.Sqlite);
        Assert.Equal(SchemaStatus.UpToDate, (await CheckAsync(database)).Status);
        await using (var context = database.Context())
            await OutOfBand.ApplyAsync(context, new CreateIndexOperation { Name = index, Table = table, Columns = [column] });
        try
        {
            var refusal = await Assert.ThrowsAsync<SchemaRefusedException>(() => CheckAsync(database));
            Assert.Equal(SchemaCodes.SchemaChanged, refusal.Code);
        }
        finally
        {
            await using var context = database.Context();
            await OutOfBand.DropIndexAsync(context, table, index);
        }
        Assert.Equal(SchemaStatus.UpToDate, (await CheckAsync(database)).Status);
    }

    [Fact]
    public async Task The_internal_objects_of_sqlite_and_the_tables_of_ef_core_stay_out_of_the_description()
    {
        var database = await databases.OfAsync(EngineKind.Sqlite);
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        var lines = await database.Engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token);
        Assert.DoesNotContain(lines, line => line.Split('|')[1].StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, line => line.Split('|')[1] is TableNames.MigrationsHistory or TableNames.MigrationsLock);
        Assert.Contains(lines, line => line.StartsWith($"table|{TableNames.SchemaHistory}|", StringComparison.Ordinal));
    }
}
