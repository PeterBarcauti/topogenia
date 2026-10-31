// SqliteGuards.cs
// Writes the guards of the common model (spec 4.2) in the SQL of SQLite and adds them to its model (spec 3.2, ADR 025).

using Microsoft.EntityFrameworkCore;
using Topogenia.Persistence.Model;

namespace Topogenia.Persistence.Sqlite;

/// <summary>
/// The SQL of the filters of the partial unique indexes and of the CHECK constraints of <see cref="ModelGuards"/> in
/// SQLite: `"column"`, text literals in single quotes, the operators of a condition. EF Core stores a decimal as text in
/// SQLite; a text compared with a number would be compared as text ("12.5" &gt; "100000"), so a decimal column is compared
/// as a number. The stored text is the value at the scale of its column (<see cref="ScaledDecimalConverter"/>, ADR 039),
/// the value that SQL Server compares. The guard of a text column (<see cref="ModelGuards.TextChecks"/>, ADR 038): SQLite
/// keeps a text of any length and compares its trailing spaces, so the width of the column and the absence of a trailing
/// space are written here; `length` counts the characters (code points), `GLOB` compares its range by code point. The common <see cref="GuardWriter"/> walks the guards (ADR 025).
/// </summary>
public sealed class SqliteGuards : GuardWriter
{
    private SqliteGuards()
    {
    }

    /// <summary>The writer of SQLite.</summary>
    public static SqliteGuards Writer { get; } = new();

    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        foreach (var (index, filter) in Writer.Filters(modelBuilder.Model).ToList())
            index.SetFilter(filter);
        foreach (var (record, name, sql) in Writer.Checks(modelBuilder.Model).ToList())
            modelBuilder.Entity(record).ToTable(table => table.HasCheckConstraint(name, sql));
        foreach (var (record, name, sql) in Writer.TextChecks(modelBuilder.Model).ToList())
            modelBuilder.Entity(record).ToTable(table => table.HasCheckConstraint(name, sql));
    }

    protected override string Delimited(string column) => Enclosed(column, '"', '"');

    protected override string ComparedDecimal(string column) => $"CAST({column} AS REAL)";

    protected override string Text(string value) => Enclosed(value, '\'', '\'');

    protected override string IsNull(string column) => $"{column} IS NULL";

    protected override string IsNotNull(string column) => $"{column} IS NOT NULL";

    protected override string InList(string column, IReadOnlyList<string> texts) => $"{column} IN ({string.Join(", ", texts)})";

    protected override string EqualsText(string column, string text) => $"{column} = {text}";

    protected override string Between(string column, string minimum, string maximum) => $"{column} BETWEEN {minimum} AND {maximum}";

    protected override string AtLeast(string column, string minimum) => $"{column} >= {minimum}";

    protected override string Differ(string column, string other) => $"{column} <> {other}";

    protected override string NoTrailingSpace(string column) => $"length({column}) = length(rtrim({column}))";

    protected override string TextLength(string column, int maximum, bool exact) => $"length({column}) {(exact ? "=" : "<=")} {maximum}";

    protected override string PrintableAscii(string column) => $"{column} NOT GLOB '*[^ -~]*'";

    protected override string AllOf(IReadOnlyList<string> conditions) => "(" + string.Join(" AND ", conditions) + ")";

    protected override string AnyOf(IReadOnlyList<string> conditions) => "(" + string.Join(" OR ", conditions) + ")";
}
