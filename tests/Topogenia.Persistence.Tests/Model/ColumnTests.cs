// ColumnTests.cs
// Conventions of the columns on both engines: snake_case names, keys given by the application, versions, codes, UTC dates (spec 4.1, D06-D08, spec 3.0 section 4).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Assistant;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Model;

public sealed partial class ColumnTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [System.Text.RegularExpressions.GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$")]
    private static partial System.Text.RegularExpressions.Regex SnakeCase();

    private async Task<AppDbContext> ContextAsync(EngineKind kind) => (await databases.OfAsync(kind)).Context();

    /// <summary>The records of the persistence that are modified, outside the closed core: versioned as the records of the domain.</summary>
    private static readonly Type[] VersionedRows = [typeof(AssistantSettingsRow)];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_table_and_column_is_named_in_snake_case(EngineKind kind)
    {
        await using var context = await ContextAsync(kind);
        foreach (var entity in context.Model.GetEntityTypes())
        {
            Assert.Matches(SnakeCase(), entity.GetTableName()!);
            Assert.All(entity.GetProperties(), property => Assert.Matches(SnakeCase(), property.GetColumnName()));
        }
    }

    /// <summary>
    /// Spec 4.3-3, A-5-05, ADR 038: every text column compares in binary on both engines. SQLite compares text in binary
    /// (no column has a collation of its own); SQL Server gives each text column, codes included, `Latin1_General_100_BIN2`
    /// rather than the collation of the database. Proven on the model, on the catalogue of the migrated database and by a
    /// comparison of each text column of a user with its value in another case.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_text_column_compares_in_binary_on_both_engines(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        await using var context = database.Context();
        var model = context.GetService<IDesignTimeModel>().Model;
        Assert.Null(model.GetCollation());
        foreach (var property in model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            var expected = kind == EngineKind.SqlServer && (type == typeof(string) || type.IsEnum) ? SqlServerModelCustomizer.BinaryCollation : null;
            Assert.True(expected == property.GetCollation(), $"{property.DeclaringType.ClrType.Name}.{property.Name}: {property.GetCollation()}");
        }

        await context.Database.OpenConnectionAsync(Token);
        var lines = await database.Engine.Catalog.DescribeAsync(context.Database.GetDbConnection(), null, Token);
        if (kind == EngineKind.SqlServer)
        {
            var text = lines.Select(line => line.Split('|')).Where(field => field[0] == "C" && field[3] is "char" or "varchar" or "nchar" or "nvarchar").ToList();
            Assert.Equal(model.GetEntityTypes().SelectMany(entity => entity.GetProperties()).Count(property => property.GetCollation() is not null), text.Count);
            Assert.All(text, field => Assert.Equal(SqlServerModelCustomizer.BinaryCollation, field[8]));
        }
        else
        {
            Assert.DoesNotContain(lines, line => line.Contains("COLLATE", StringComparison.OrdinalIgnoreCase));
        }

        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        var user = graph.Owner;
        foreach (var (column, value) in new[] { (nameof(AppUser.Login), user.Login), (nameof(AppUser.LoginNormalized), user.LoginNormalized),
            (nameof(AppUser.DisplayName), user.DisplayName), (nameof(AppUser.Email), user.Email!), (nameof(AppUser.Language), user.Language!) })
        {
            var other = value.ToUpperInvariant() == value ? value.ToLowerInvariant() : value.ToUpperInvariant();
            Assert.NotEqual(value, other);
            var found = await context.Set<AppUser>().Where(row => EF.Property<string>(row, column) == other && row.Id == user.Id).CountAsync(Token);
            Assert.True(found == 0, column);
            Assert.Equal(1, await context.Set<AppUser>().CountAsync(row => EF.Property<string>(row, column) == value && row.Id == user.Id, Token));
        }
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_key_is_given_by_the_application_and_never_by_the_database(EngineKind kind)
    {
        await using var context = await ContextAsync(kind);
        var keys = context.Model.GetEntityTypes().SelectMany(entity => entity.FindPrimaryKey()!.Properties).ToList();
        Assert.All(keys, property => Assert.Equal(ValueGenerated.Never, property.ValueGenerated));
        var model = context.GetService<IDesignTimeModel>().Model;
        Assert.All(model.GetEntityTypes().SelectMany(entity => entity.GetProperties()),
            property => Assert.False(property.FindAnnotation(RelationalAnnotationNames.DefaultValueSql) is not null
                || property.FindAnnotation(RelationalAnnotationNames.DefaultValue) is not null, property.Name));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_modifiable_record_has_an_integer_version_as_its_concurrency_token_and_none_has_a_rowversion(EngineKind kind)
    {
        await using var context = await ContextAsync(kind);
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var version = entity.FindProperty(nameof(IVersionedRecord.Version));
            if (typeof(IVersionedRecord).IsAssignableFrom(entity.ClrType) || VersionedRows.Contains(entity.ClrType))
            {
                Assert.NotNull(version);
                Assert.Equal(typeof(int), version.ClrType);
                Assert.True(version.IsConcurrencyToken, entity.ClrType.Name);
                Assert.Equal(ValueGenerated.Never, version.ValueGenerated);
            }
            else
            {
                Assert.Null(version);
            }
            Assert.DoesNotContain(entity.GetProperties(), property => property.ClrType == typeof(byte[]) || property.IsConcurrencyToken && property != version);
        }
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_code_of_a_closed_list_fits_its_column_and_every_date_is_kept_in_utc(EngineKind kind)
    {
        await using var context = await ContextAsync(kind);
        var properties = context.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()).ToList();
        foreach (var property in properties.Where(property => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType).IsEnum))
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            var codes = (IEnumerable<string>)typeof(Codes).GetMethod(nameof(Codes.All))!.MakeGenericMethod(type).Invoke(null, null)!;
            Assert.True(codes.Max(code => code.Length) <= property.GetMaxLength(), $"{property.DeclaringType.ClrType.Name}.{property.Name}");
            Assert.Equal(typeof(CodeConverter<>).MakeGenericType(type), property.GetValueConverter()?.GetType());
        }
        Assert.All(properties.Where(property => property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?)),
            property => Assert.IsType<UtcDateTimeConverter>(property.GetValueConverter()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_local_date_is_refused_before_anything_is_written(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        var local = graph.Records.Select(record => record is AuditEvent audit ? audit with { At = DateTime.Now } : record).ToArray();
        await using (var context = database.Context())
        {
            context.AddRange(local);
            var refusal = await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync(Token));
            Assert.IsType<ArgumentException>(refusal.GetBaseException(), exactMatch: false);
        }
        await using var reader = database.Context();
        Assert.False(await reader.Set<Project>().AnyAsync(project => project.Id == graph.Project.Id, Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Dates_compare_and_sort_the_same_on_both_engines(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var start = new DateTime(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        long[] offsets = [9_999_999, 1, 10_000, 3, 9_990_000, 0, 120_000_000_000];
        var events = offsets.Select(ticks => new AuditEvent(Guid.CreateVersion7(), start.AddTicks(ticks), null, "test.dates", null, null)).ToArray();
        await Refusals.SaveAsync(database, events);
        await using var context = database.Context();
        var threshold = start.AddTicks(3);
        var read = await context.Set<AuditEvent>().Where(audit => audit.Kind == "test.dates" && audit.At > threshold)
            .OrderBy(audit => audit.At).Select(audit => audit.At).ToListAsync(Token);
        Assert.Equal(events.Select(audit => audit.At).Where(at => at > threshold).Order(), read);
        Assert.Equal(offsets.Count(ticks => ticks > 3), read.Count);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_change_written_on_a_version_read_before_another_change_is_refused_as_a_conflict(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        await using var first = database.Context();
        await using var second = database.Context();
        var seenByFirst = await first.Set<Step>().SingleAsync(step => step.Id == graph.Steps[2].Id, Token);
        var seenBySecond = await second.Set<Step>().SingleAsync(step => step.Id == graph.Steps[2].Id, Token);
        second.Entry(seenBySecond).Property(step => step.Label).CurrentValue = "Renamed by the second";
        second.Entry(seenBySecond).Property(step => step.Version).CurrentValue = seenBySecond.Version + 1;
        await second.SaveChangesAsync(Token);
        first.Entry(seenByFirst).Property(step => step.Label).CurrentValue = "Renamed by the first";
        first.Entry(seenByFirst).Property(step => step.Version).CurrentValue = seenByFirst.Version + 1;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => first.SaveChangesAsync(Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_frequency_and_duration_of_a_validated_version_are_read_from_their_own_columns(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        await using var context = database.Context();
        var sheet = graph.Sheets[0].Id;
        var found = await context.Set<StepSheetVersion>()
            .Where(version => version.SheetId == sheet && version.Frequency == Frequency.Daily && version.DurationMinutes >= 12.5m)
            .Select(version => version.VersionNumber).ToListAsync(Token);
        Assert.Equal([1], found);
    }
}
