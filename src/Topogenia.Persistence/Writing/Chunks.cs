// Chunks.cs
// Lists of identifiers cut into packets of 500 before they become parameters of a query (spec 4.3-2).

namespace Topogenia.Persistence.Writing;

/// <summary>
/// SQL Server takes at most 2 100 parameters in a statement, SQLite 32 766 (spec 4.3-2): every list of identifiers sent to
/// the database is cut into packets of <see cref="Size"/> (the 1.x cut at 1 000 for Dapper, annex A 5).
/// </summary>
public static class Chunks
{
    public const int Size = 500;

    /// <summary>The distinct values in packets of at most <see cref="Size"/>, in their first order.</summary>
    public static IEnumerable<List<T>> Of<T>(IEnumerable<T> values) => values.Distinct().Chunk(Size).Select(chunk => chunk.ToList());
}
