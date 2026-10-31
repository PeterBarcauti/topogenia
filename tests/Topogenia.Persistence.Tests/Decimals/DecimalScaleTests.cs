// DecimalScaleTests.cs
// Decimals are rounded, checked and read back at the scale of their column, the same on both engines (ADR 025, 039).

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Decimals;

/// <summary>
/// SQL Server rounds a value beyond the scale of a `decimal(9,2)` column and gives it back with its trailing zeros; SQLite
/// kept the exact text. A duration of 100 000.004 minutes was refused by SQLite and stored as 100 000.00 by SQL Server,
/// 9.755 was read back 9.755 and 9.76, 12.5 was read back 12.5 and 12.50 (another JSON). Here both engines store, check,
/// read back and serialise the same value.
/// </summary>
public sealed class DecimalScaleTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A written duration and the value read back on both engines, as JSON.</summary>
    public static TheoryData<EngineKind, decimal, string> Durations()
    {
        var data = new TheoryData<EngineKind, decimal, string>();
        foreach (var kind in new[] { EngineKind.Sqlite, EngineKind.SqlServer })
        {
            data.Add(kind, 9.755m, "9.76");
            data.Add(kind, 12.5m, "12.5");
            data.Add(kind, 12.50m, "12.5");
            data.Add(kind, 0.005m, "0.01");
            data.Add(kind, 0.004m, "0");
            data.Add(kind, -0.004m, "0");
            data.Add(kind, 100_000m, "100000");
            data.Add(kind, 100_000.004m, "100000");
            data.Add(kind, 99_999.995m, "100000");
        }
        return data;
    }

    /// <summary>Durations that round beyond the bounds 0 and 100 000 of `CK_step_sheet_duration_minutes`.</summary>
    public static TheoryData<EngineKind, decimal> RefusedDurations()
    {
        var data = new TheoryData<EngineKind, decimal>();
        foreach (var kind in new[] { EngineKind.Sqlite, EngineKind.SqlServer })
        {
            data.Add(kind, 100_000.005m);
            data.Add(kind, 100_000.01m);
            data.Add(kind, -0.005m);
        }
        return data;
    }

    private async Task<(TestDatabase Database, ProjectGraph Graph)> WrittenAsync(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        return (database, graph);
    }

    private static StepSheet SheetOf(ProjectGraph graph, decimal minutes) =>
        graph.Sheets[0] with { Id = Guid.CreateVersion7(), StepId = graph.SubStep.Id, DurationMinutes = minutes };

    [Theory]
    [MemberData(nameof(Durations))]
    public async Task A_duration_is_stored_and_read_back_at_the_scale_of_its_column(EngineKind kind, decimal written, string json)
    {
        var (database, graph) = await WrittenAsync(kind);
        var sheet = SheetOf(graph, written);
        await Refusals.SaveAsync(database, sheet);

        await using var context = database.Context();
        var read = await context.Set<StepSheet>().AsNoTracking().SingleAsync(row => row.Id == sheet.Id, Token);
        Assert.Equal(json, JsonSerializer.Serialize(read.DurationMinutes));
        Assert.Equal(decimal.Parse(json, System.Globalization.CultureInfo.InvariantCulture), read.DurationMinutes);
        // The stored value is the one the CHECK and the queries compare.
        Assert.True(await context.Set<StepSheet>().AnyAsync(row => row.Id == sheet.Id && row.DurationMinutes == read.DurationMinutes, Token));
    }

    [Theory]
    [MemberData(nameof(RefusedDurations))]
    public async Task A_duration_that_rounds_beyond_a_bound_is_refused_by_the_check(EngineKind kind, decimal written)
    {
        var (database, graph) = await WrittenAsync(kind);
        await Refusals.AssertRefusedAsync(database, GuardKind.Check, "CK_step_sheet_duration_minutes", SheetOf(graph, written));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_decimal_of_one_place_is_rounded_to_its_place_and_read_back_without_trailing_zeros(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        var rounded = graph.MapPainPoint with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('I', 61), LostHoursPerMonth = 9.95m };
        var exact = graph.MapPainPoint with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('I', 62), LostHoursPerMonth = 0.05m };
        await Refusals.SaveAsync(database, rounded, exact);

        await using var context = database.Context();
        var read = await context.Set<PainPoint>().AsNoTracking().Where(row => row.Id == rounded.Id || row.Id == exact.Id)
            .ToDictionaryAsync(row => row.Id, row => JsonSerializer.Serialize(row.LostHoursPerMonth), Token);
        Assert.Equal("10", read[rounded.Id]);
        Assert.Equal("0.1", read[exact.Id]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_decimal_that_its_column_cannot_hold_is_refused_before_anything_is_written(EngineKind kind)
    {
        var (database, graph) = await WrittenAsync(kind);
        // decimal(9,2) holds less than 10 000 000; the CHECK of the waiting time of a use has no upper bound.
        var use = graph.Use with { StepId = graph.Steps[4].Id, PainPointId = null, WaitHours = 9_999_999.995m };
        await using (var context = database.Context())
        {
            context.Add(use);
            var refusal = await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync(Token));
            Assert.IsType<ArgumentOutOfRangeException>(refusal.GetBaseException());
        }
        await using var reader = database.Context();
        Assert.False(await reader.Set<DocumentUse>().AnyAsync(row => row.DocumentId == use.DocumentId && row.StepId == use.StepId, Token));
        await Refusals.SaveAsync(database, use with { WaitHours = 9_999_999.99m });
    }

    [Fact]
    public void The_conversion_rounds_half_away_from_zero_and_drops_the_trailing_zeros()
    {
        Assert.Equal("9.76", ScaledDecimals.ToStore(9.755m, 9, 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("-9.76", ScaledDecimals.ToStore(-9.755m, 9, 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("12.5", ScaledDecimals.Normalize(12.500m).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("100000", ScaledDecimals.Normalize(100_000.00m).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("0", ScaledDecimals.Normalize(-0.00m).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(99_999.9m, ScaledDecimals.ToStore(99_999.94m, 6, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScaledDecimals.ToStore(99_999.95m, 6, 1));
    }
}
