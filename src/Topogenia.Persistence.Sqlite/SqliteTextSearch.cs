// SqliteTextSearch.cs
// The search of a part of a text in SQLite (`Contains`): the same texts searched and the same rows as in SQL Server (ADR 038).

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Topogenia.Persistence.Model;

namespace Topogenia.Persistence.Sqlite;

/// <summary>
/// `column.Contains(text)` on a stored text. EF Core writes `instr(column, text) &gt; 0` and gives the searched text the
/// converter of the column, which refuses a text that ends with a space; SQL Server writes `LIKE '%text%'`, whose
/// pattern does not end with a space, and finds the rows. Here the searched text gets <see cref="SearchedTextConverter"/>:
/// refused as in SQL Server (NUL character, a character outside printable ASCII for a technical column), taken with its
/// trailing spaces, and compared by `instr` byte by byte, as the binary collation of SQL Server compares. Registered by
/// <see cref="TextSearchParity.UseTextSearchParity{TContext}"/> (ADR 038).
/// </summary>
public sealed class SqliteTextSearch(ISqlExpressionFactory factory, IRelationalTypeMappingSource mappings)
    : IMethodCallTranslatorPlugin, IMethodCallTranslator
{
    private static readonly MethodInfo ContainsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    public IEnumerable<IMethodCallTranslator> Translators => [this];

    public SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(arguments);
        if (method != ContainsMethod || instance?.TypeMapping?.Converter is not StoredTextConverter stored)
            return null;
        var text = mappings.FindMapping(typeof(string))!;
        var searched = (RelationalTypeMapping)text.WithComposedConverter(new SearchedTextConverter(stored.Ascii));
        var position = factory.Function(
            "instr",
            [instance, factory.ApplyTypeMapping(arguments[0], searched)],
            nullable: true,
            argumentsPropagateNullability: [true, true],
            typeof(int));
        return factory.GreaterThan(position, factory.Constant(0));
    }
}
