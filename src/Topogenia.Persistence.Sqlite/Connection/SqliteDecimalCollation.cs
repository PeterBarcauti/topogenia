// SqliteDecimalCollation.cs
// The collation EF_DECIMAL with which EF Core sorts the decimal columns of SQLite, read in the invariant culture (data M3).

using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Topogenia.Persistence.Sqlite.Connection;

/// <summary>
/// SQLite has no decimal type: EF Core writes a decimal as text in the invariant culture (`12.5`) and sorts a decimal
/// column with the collation `EF_DECIMAL`, which it registers on each connection. EF Core 10.0.13 reads the two texts with
/// `decimal.Parse` in the culture of the thread: in French the point is no decimal separator and the sort fails with a
/// <see cref="FormatException"/>; in German the point separates thousands and `12.5` is read 125, a wrong order without
/// any error. The functions of EF Core for `Min`, `Max`, the sums and the comparisons read their arguments through
/// Microsoft.Data.Sqlite, in the invariant culture: only the collation needs a replacement. It is registered again, under
/// the same name, on each opening (<see cref="SqliteConnectionSettings"/>), after EF Core registered its own.
/// </summary>
internal static class SqliteDecimalCollation
{
    /// <summary>The name that EF Core writes in `ORDER BY ... COLLATE`.</summary>
    public const string Name = "EF_DECIMAL";

    private const NumberStyles Styles = NumberStyles.Number | NumberStyles.AllowExponent;

    public static void Register(SqliteConnection connection) => connection.CreateCollation(Name, Compare);

    /// <summary>
    /// The order of two texts of a decimal column, by their value in the invariant culture. A text that is no number
    /// (written by hand) comes after the numbers, and two such texts compare as ordinal text: the order stays total and a
    /// sort never throws inside SQLite.
    /// </summary>
    public static int Compare(string? left, string? right)
    {
        var leftIsNumber = decimal.TryParse(left, Styles, CultureInfo.InvariantCulture, out var leftValue);
        var rightIsNumber = decimal.TryParse(right, Styles, CultureInfo.InvariantCulture, out var rightValue);
        return (leftIsNumber, rightIsNumber) switch
        {
            (true, true) => decimal.Compare(leftValue, rightValue),
            (true, false) => -1,
            (false, true) => 1,
            _ => string.CompareOrdinal(left, right),
        };
    }
}
