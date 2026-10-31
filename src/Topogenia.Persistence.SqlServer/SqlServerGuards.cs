// SqlServerGuards.cs
// Writes the guards of the common model (spec 4.2) in the SQL of SQL Server and adds them to its model (spec 3.2, ADR 025).

using Microsoft.EntityFrameworkCore;
using Topogenia.Persistence.Model;

namespace Topogenia.Persistence.SqlServer;

/// <summary>
/// The SQL of the filters of the partial unique indexes and of the CHECK constraints of <see cref="ModelGuards"/> in SQL
/// Server: `[column]`, text literals in single quotes, the operators of a condition. A decimal column is compared as it
/// is: SQL Server stores a `decimal(p,s)`. The guard of a text column (<see cref="ModelGuards.TextChecks"/>, ADR 038):
/// SQL Server pads a comparison with spaces, so `RTRIM` and an equality would always agree, the lengths in bytes
/// (`DATALENGTH`) do not; `LEN` counts the UTF-16 code units without the trailing spaces (none here), the column type
/// already refuses a longer text (2628) and the CHECK states it for both engines; the range `[^ -~]` of `LIKE` compares by
/// code in the binary collation of the column. The common <see cref="GuardWriter"/> walks the guards (ADR 025).
/// </summary>
public sealed class SqlServerGuards : GuardWriter
{
    private SqlServerGuards()
    {
    }

    /// <summary>The writer of SQL Server.</summary>
    public static SqlServerGuards Writer { get; } = new();

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

    protected override string Delimited(string column) => Enclosed(column, '[', ']');

    protected override string Text(string value) => Enclosed(value, '\'', '\'');

    protected override string IsNull(string column) => $"{column} IS NULL";

    protected override string IsNotNull(string column) => $"{column} IS NOT NULL";

    protected override string InList(string column, IReadOnlyList<string> texts) => $"{column} IN ({string.Join(", ", texts)})";

    protected override string EqualsText(string column, string text) => $"{column} = {text}";

    protected override string Between(string column, string minimum, string maximum) => $"{column} BETWEEN {minimum} AND {maximum}";

    protected override string AtLeast(string column, string minimum) => $"{column} >= {minimum}";

    protected override string Differ(string column, string other) => $"{column} <> {other}";

    protected override string NoTrailingSpace(string column) => $"DATALENGTH({column}) = DATALENGTH(RTRIM({column}))";

    protected override string TextLength(string column, int maximum, bool exact) => $"LEN({column}) {(exact ? "=" : "<=")} {maximum}";

    protected override string PrintableAscii(string column) => $"{column} NOT LIKE '%[^ -~]%'";

    protected override string AllOf(IReadOnlyList<string> conditions) => "(" + string.Join(" AND ", conditions) + ")";

    protected override string AnyOf(IReadOnlyList<string> conditions) => "(" + string.Join(" OR ", conditions) + ")";
}
