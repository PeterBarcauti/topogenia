// ChangeSetJsonTests.cs
// The JSON of the operation log gives back every record exactly, named by the type of its key (spec 4.6, ADR 042).

using System.Reflection;
using System.Text.Json;
using Topogenia.Application.OperationLog;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.OperationLog;

public sealed class ChangeSetJsonTests
{
    /// <summary>Every record type of the model, except the entries of the log itself.</summary>
    public static TheoryData<Type> RecordTypes() =>
        new(typeof(IRecord).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IRecord).IsAssignableFrom(type) && type != typeof(OperationLogEntry))
            .OrderBy(type => type.Name, StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(RecordTypes))]
    public void Every_record_type_is_named_by_the_type_of_its_key(Type type)
    {
        var record = Sample(type, 1);
        Assert.Equal(type, ChangeSetJson.RecordTypes[record.Key.Type]);
    }

    [Fact]
    public void The_names_cover_the_model_and_nothing_else() =>
        Assert.Equal(RecordTypes().Select(row => row.Data).OrderBy(type => type.Name, StringComparer.Ordinal),
            ChangeSetJson.RecordTypes.Values.OrderBy(type => type.Name, StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(RecordTypes))]
    public void A_record_comes_back_with_every_field_in_each_kind_of_change(Type type)
    {
        var before = Sample(type, 1);
        var after = Sample(type, 2);
        var changes = new ChangeSet([
            new RecordChange(ChangeKind.Created, null, after),
            new RecordChange(ChangeKind.Modified, before, after),
            new RecordChange(ChangeKind.Removed, before, null),
        ]);
        var read = ChangeSetJson.Read(ChangeSetJson.Write(changes));
        Assert.Equal(changes.Changes.Select(change => change.Kind), read.Changes.Select(change => change.Kind));
        for (var index = 0; index < changes.Changes.Count; index++)
        {
            Assert.Equal(Fields(changes.Changes[index].Before), Fields(read.Changes[index].Before));
            Assert.Equal(Fields(changes.Changes[index].After), Fields(read.Changes[index].After));
            Assert.Equal(changes.Changes[index].After?.GetType(), read.Changes[index].After?.GetType());
        }
    }

    [Fact]
    public void Dates_come_back_in_utc_with_their_full_precision_and_closed_lists_by_name()
    {
        var at = new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        var lane = new Lane(Guid.NewGuid(), Guid.NewGuid(), "PRJ-C01", "Achats", "fr", Domain.Values.LaneType.System, null, "#E3F2FD", 0, null,
            at, Guid.NewGuid(), null, null, null, null, 1);
        var json = ChangeSetJson.Write(new ChangeSet([new RecordChange(ChangeKind.Created, null, lane)]));
        var read = (Lane)ChangeSetJson.Read(json).Changes.Single().After!;
        Assert.Equal(at, read.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, read.CreatedAt.Kind);
        Assert.Contains("\"lane_type\":\"System\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"key\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_type_is_refused()
    {
        const string json = """[{"change":"created","type":"unknown","before":null,"after":{}}]""";
        Assert.Throws<JsonException>(() => ChangeSetJson.Read(json));
    }

    [Fact]
    public void The_touched_objects_are_written_with_their_version_after_the_operation()
    {
        var lane = (Lane)Sample(typeof(Lane), 1) with { Version = 4 };
        var json = ChangeSetJson.WriteTouched(new ChangeSet([new RecordChange(ChangeKind.Created, null, lane)]));
        Assert.Equal($$"""[{"type":"lane","key":"{{lane.Id:N}}","version":4}]""", json);
    }

    /// <summary>The value of every constructor parameter, read back from the record (its fields).</summary>
    private static string? Fields(IRecord? record) =>
        record is null ? null
        : string.Join("|", record.GetType().GetConstructors().Single().GetParameters()
            .Select(parameter => $"{parameter.Name}={Value(record.GetType().GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!.GetValue(record))}")
            .Concat(InitOnly(record)));

    private static IEnumerable<string> InitOnly(IRecord record) =>
        record.GetType().GetProperties().Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => $"{property.Name}={Value(property.GetValue(record))}");

    private static string Value(object? value) => value switch
    {
        null => "null",
        DateTime date => $"{date.Ticks}:{date.Kind}",
        decimal number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>A record of the type with every field set from a seed: different seeds give different values.</summary>
    internal static IRecord Sample(Type type, int seed)
    {
        var constructor = type.GetConstructors().Single();
        var arguments = constructor.GetParameters().Select((parameter, index) => SampleValue(parameter.ParameterType, seed * 100 + index)).ToArray();
        return (IRecord)constructor.Invoke(arguments);
    }

    private static object? SampleValue(Type type, int seed)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(Guid))
            return new Guid($"00000000-0000-0000-0000-{seed:x12}");
        if (underlying == typeof(string))
            return seed % 2 == 0 ? "fr" : "en";
        if (underlying == typeof(int))
            return seed % 7 + 1;
        if (underlying == typeof(decimal))
            return 1.25m + seed % 5;
        if (underlying == typeof(bool))
            return seed % 2 == 0;
        if (underlying == typeof(DateTime))
            return new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(seed * 1234567L);
        if (underlying.IsEnum)
        {
            var values = Enum.GetValues(underlying);
            return values.GetValue(seed % values.Length);
        }
        throw new NotSupportedException($"No sample for {type}.");
    }
}
