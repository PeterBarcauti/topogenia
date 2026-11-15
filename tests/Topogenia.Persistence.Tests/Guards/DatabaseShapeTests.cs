// DatabaseShapeTests.cs
// One model for two providers, structural guards only, an integer version on every modifiable record, every test on both engines.

using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Guards;

/// <summary>
/// D02 (two providers on one model), D04 (the database keeps only structural guards: foreign keys without cascade, simple
/// CHECK, unique and partial unique indexes; no trigger, no procedure, no default value, views only in `bi`), D06 (an
/// integer version written by the application on every modifiable record, a stale version refused) and spec 3.1 (every
/// test of the persistence that takes an engine runs on both).
/// </summary>
public sealed class DatabaseShapeTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The model of one engine without its store types and filters: what both providers must share.</summary>
    private static List<string> Shape(TestDatabase database)
    {
        using var context = database.Context();
        var lines = new List<string>();
        foreach (var entity in context.GetService<IDesignTimeModel>().Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            lines.Add($"table {table} {entity.ClrType.Name} key {entity.FindPrimaryKey()!.GetName()}");
            lines.AddRange(entity.GetProperties().Select(property =>
                $"column {table}.{property.GetColumnName()} {property.ClrType.Name} {property.IsNullable} {property.GetMaxLength()} {property.IsConcurrencyToken}"));
            lines.AddRange(entity.GetIndexes().Select(index => $"index {table}.{index.GetDatabaseName()} {index.IsUnique} {index.GetFilter() is not null}"));
            lines.AddRange(entity.GetForeignKeys().Select(key => $"fk {table}.{key.GetConstraintName()} {key.PrincipalEntityType.GetTableName()} {key.DeleteBehavior}"));
            lines.AddRange(entity.GetCheckConstraints().Select(check => $"check {table}.{check.Name}"));
        }
        return lines.Order(StringComparer.Ordinal).ToList();
    }

    [Fact]
    public async Task Both_providers_map_one_model_with_the_same_tables_columns_keys_indexes_and_guards()
    {
        await using var sqlite = TestDatabase.Create(EngineKind.Sqlite);
        await using var sqlServer = TestDatabase.Create(EngineKind.SqlServer);
        await using (var context = sqlite.Context())
            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
        await using (var context = sqlServer.Context())
            Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);

        Assert.Equal(Shape(sqlite), Shape(sqlServer));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_database_keeps_structural_guards_only_without_trigger_default_value_nor_cascade(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        await using var context = database.Context();
        await context.Database.OpenConnectionAsync(Token);
        var lines = await database.Engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token);

        if (kind == EngineKind.Sqlite)
            Assert.All(lines, line => Assert.Contains(line.Split('|')[0], new[] { "table", "index" }));
        else
        {
            // No object of programming (trigger of a table, procedure, function, synonym, sequence: X), no trigger of the
            // database (TR), no type (Y): the catalogue describes each one, and a database of the migrations holds none.
            Assert.All(new[] { "X|", "TR|", "Y|" }, kind => Assert.DoesNotContain(lines, line => line.StartsWith(kind, StringComparison.Ordinal)));
            Assert.DoesNotContain(lines, line => line.StartsWith("D|", StringComparison.Ordinal));
            Assert.All(lines.Where(line => line.StartsWith("V|", StringComparison.Ordinal)), line => Assert.StartsWith("V|bi.", line, StringComparison.Ordinal));
        }
        var model = context.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToList();
        Assert.All(model.SelectMany(entity => entity.GetForeignKeys()), key => Assert.NotEqual(DeleteBehavior.Cascade, key.DeleteBehavior));
        Assert.Equal(ModelGuards.Checks.Concat(ModelGuards.TextChecks(context.GetService<IDesignTimeModel>().Model)).Select(check => check.Name).Order(), model.SelectMany(entity => entity.GetCheckConstraints()).Select(check => check.Name!).Order());
        Assert.Equal(ModelGuards.PartialIndexes.Select(index => index.IndexName).Order(),
            model.SelectMany(entity => entity.GetIndexes()).Where(index => index.GetFilter() is not null).Select(index => index.GetDatabaseName()!).Order());
        Assert.All(model.SelectMany(entity => entity.GetProperties()), property => Assert.Null(property.GetDefaultValueSql()));
    }

    public static TheoryData<EngineKind, string> EveryVersionedType()
    {
        var data = new TheoryData<EngineKind, string>();
        var types = typeof(IVersionedRecord).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IVersionedRecord).IsAssignableFrom(type)).Select(type => type.Name).Order();
        foreach (var kind in Enum.GetValues<EngineKind>())
        {
            foreach (var type in types)
                data.Add(kind, type);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryVersionedType))]
    public async Task Every_modifiable_record_is_written_with_the_next_version_and_a_stale_version_is_refused(EngineKind kind, string type)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        var scenario = await StoreScenario.CreateAsync(database);
        var record = Assert.Single(graph.Records.OfType<IVersionedRecord>().Where(candidate => candidate.GetType().Name == type).Take(1));
        var next = record.Stamp(record.Version + 1, FixedClock.Time, graph.Owner.Id);

        await scenario.WriteAsync(new ChangeSet([new RecordChange(ChangeKind.Modified, record, next)]));
        var stale = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.WriteAsync(new ChangeSet([new RecordChange(ChangeKind.Modified, record, record.Stamp(record.Version + 1, FixedClock.Time, null))])));

        Assert.Equal((ErrorCodes.VersionConflict, (object?)record.Key.Type), (stale.Code, stale.Details[StoreRefusals.ObjectDetail]));
        await using var context = database.Context();
        var entity = context.Model.FindEntityType(record.GetType())!;
        Assert.True(entity.FindProperty(nameof(IVersionedRecord.Version))!.IsConcurrencyToken);
        Assert.Equal(next, await FindAsync(context, entity, record));
    }

    [Fact]
    public void Every_theory_of_the_persistence_that_takes_an_engine_runs_on_both_engines()
    {
        var theories = typeof(DatabaseShapeTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<TheoryAttribute>() is not null
                && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(EngineKind)))
            .ToList();

        Assert.NotEmpty(theories);
        Assert.All(theories, method => Assert.True(EnginesOf(method).SetEquals(Enum.GetValues<EngineKind>()),
            $"{method.DeclaringType!.Name}.{method.Name} does not run on both engines."));
    }

    /// <summary>The engines given to a theory by its data (member data or inline data).</summary>
    private static HashSet<EngineKind> EnginesOf(MethodInfo method)
    {
        var position = Array.FindIndex(method.GetParameters(), parameter => parameter.ParameterType == typeof(EngineKind));
        var rows = new List<object?[]>();
        rows.AddRange(method.GetCustomAttributes<InlineDataAttribute>().Select(inline => inline.Data));
        foreach (var member in method.GetCustomAttributes<MemberDataAttribute>())
        {
            var owner = member.MemberType ?? method.DeclaringType!;
            var source = owner.GetMember(member.MemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy).Single();
            var value = source switch
            {
                PropertyInfo property => property.GetValue(null),
                MethodInfo factory => factory.Invoke(null, member.Arguments),
                FieldInfo field => field.GetValue(null),
                _ => null,
            };
            foreach (var row in (IEnumerable)value!)
                rows.Add(row is ITheoryDataRow data ? data.GetData() : (object?[])row);
        }
        return rows.Select(row => (EngineKind)row[position]!).ToHashSet();
    }

    private static async Task<object?> FindAsync(AppDbContext context, IEntityType entity, IRecord record)
    {
        var key = entity.FindPrimaryKey()!.Properties.Select(property => property.GetGetter().GetClrValue(record)).ToArray();
        var found = await context.FindAsync(record.GetType(), key);
        return found;
    }
}
