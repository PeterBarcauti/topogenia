// DecimalCultureTests.cs
// Decimal columns of SQLite sorted, bounded and compared by their value whatever the culture of the thread (data M3, phase 4 desktop mode).

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Sqlite;

/// <summary>
/// SQLite stores a decimal as text; EF Core sorts it with the collation `EF_DECIMAL` and computes `Min`, `Max` and the
/// comparisons with its own functions. A desktop of the factory runs in French (decimal comma, narrow no-break space
/// between thousands) or in German (decimal comma, point between thousands): the text `12.5` must still be read as
/// twelve and a half. SQLite only: SQL Server compares its `decimal` columns natively, with no text in between.
/// </summary>
public sealed class DecimalCultureTests
{
    /// <summary>Values whose order as text, or as read with a point between thousands, differs from their order as numbers.</summary>
    private static readonly decimal[] Durations = [100.25m, 9.75m, 1000.5m, 12.5m, 0.5m, 250m, 99.99m];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> CommaCultures => ["fr-FR", "de-DE"];

    /// <summary>A migrated file holding the sheets of a fictitious project, with <see cref="Durations"/> as their durations.</summary>
    private static async Task<IReadOnlyList<decimal>> WriteDurationsAsync(SqliteFiles files)
    {
        await files.MigratedAsync();
        var graph = ProjectGraph.New();
        await using var context = files.Engine.CreateContext();
        context.AddRange(graph.Records);
        await context.SaveChangesAsync(Token);
        var written = new List<decimal>();
        for (var i = 0; i < graph.Sheets.Count; i++)
        {
            var (id, duration) = (graph.Sheets[i].Id, Durations[i % Durations.Length]);
            await context.Set<StepSheet>().Where(sheet => sheet.Id == id)
                .ExecuteUpdateAsync(row => row.SetProperty(sheet => sheet.DurationMinutes, duration), Token);
            written.Add(duration);
        }
        Assert.True(written.Count >= Durations.Length);
        return written;
    }

    /// <summary>Runs <paramref name="read"/> with <paramref name="culture"/> as the culture of the thread, then puts the previous one back.</summary>
    private static async Task<T> InCultureAsync<T>(string culture, Func<Task<T>> read)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return await read();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static IQueryable<decimal?> DurationsOf(AppDbContext context) =>
        context.Set<StepSheet>().Where(sheet => sheet.DurationMinutes != null).Select(sheet => sheet.DurationMinutes);

    [Theory]
    [MemberData(nameof(CommaCultures))]
    public async Task A_decimal_column_is_sorted_by_its_value_in_a_culture_with_a_decimal_comma(string culture)
    {
        await using var files = new SqliteFiles();
        var written = await WriteDurationsAsync(files);
        await using var context = files.Engine.CreateContext();

        var (ascending, descending) = await InCultureAsync(culture, async () =>
            (await DurationsOf(context).OrderBy(value => value).ToListAsync(Token),
             await DurationsOf(context).OrderByDescending(value => value).ToListAsync(Token)));

        var expected = written.Cast<decimal?>().Order().ToList();
        Assert.Equal(expected, ascending);
        Assert.Equal(Enumerable.Reverse(expected), descending);
    }

    [Theory]
    [MemberData(nameof(CommaCultures))]
    public async Task Min_max_and_comparisons_of_a_decimal_column_follow_its_value_in_a_culture_with_a_decimal_comma(string culture)
    {
        await using var files = new SqliteFiles();
        var written = await WriteDurationsAsync(files);
        await using var context = files.Engine.CreateContext();

        var (minimum, maximum, above, below) = await InCultureAsync(culture, async () =>
            (await DurationsOf(context).MinAsync(Token),
             await DurationsOf(context).MaxAsync(Token),
             await DurationsOf(context).Where(value => value > 12.5m).OrderBy(value => value).ToListAsync(Token),
             await DurationsOf(context).CountAsync(value => value < 100.25m, Token)));

        Assert.Equal(0.5m, minimum);
        Assert.Equal(1000.5m, maximum);
        Assert.Equal(written.Where(value => value > 12.5m).Order().Cast<decimal?>(), above);
        Assert.Equal(written.Count(value => value < 100.25m), below);
    }
}
