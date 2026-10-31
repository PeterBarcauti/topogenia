// GuardConditions.cs
// Conditions of the guards kept in the database (spec 4.2), described without SQL; each provider writes them in its syntax (ADR 025).

namespace Topogenia.Persistence.Model;

/// <summary>
/// A condition on the columns of one table, named by the C# properties of its record. The common model only describes
/// it; each provider renders it in its own SQL (`[column]` or `"column"`, ADR 025), so that the two databases get the
/// same guards. A CHECK whose column is NULL passes, as in SQL: a nullable column needs no extra case.
/// </summary>
public abstract record GuardCondition;

/// <summary>The column is NULL.</summary>
public sealed record IsNull(string Property) : GuardCondition;

/// <summary>The column is not NULL.</summary>
public sealed record IsNotNull(string Property) : GuardCondition;

/// <summary>The column takes one of the codes of a closed list.</summary>
public sealed record InList(string Property, IReadOnlyList<string> Values) : GuardCondition;

/// <summary>The column equals a code.</summary>
public sealed record EqualsCode(string Property, string Value) : GuardCondition;

/// <summary>The column lies between two bounds, both included.</summary>
public sealed record Between(string Property, decimal Minimum, decimal Maximum) : GuardCondition;

/// <summary>The column is at least a bound.</summary>
public sealed record AtLeast(string Property, decimal Minimum) : GuardCondition;

/// <summary>Two columns differ.</summary>
public sealed record Differ(string Property, string Other) : GuardCondition;

/// <summary>The last character of the text is not a space (SQL Server ignores the trailing spaces of a comparison, ADR 038).</summary>
public sealed record NoTrailingSpace(string Property) : GuardCondition;

/// <summary>The text has at most <paramref name="Maximum"/> characters, exactly that many when <paramref name="Fixed"/> (`char(n)`).</summary>
public sealed record TextLength(string Property, int Maximum, bool Fixed) : GuardCondition;

/// <summary>Every character of the text is printable ASCII, a space to a tilde (a technical column, `varchar`, ADR 038).</summary>
public sealed record PrintableAscii(string Property) : GuardCondition;

/// <summary>Every condition holds.</summary>
public sealed record AllOf(IReadOnlyList<GuardCondition> Conditions) : GuardCondition;

/// <summary>At least one condition holds.</summary>
public sealed record AnyOf(IReadOnlyList<GuardCondition> Conditions) : GuardCondition;

/// <summary>Builders of the usual conditions.</summary>
public static class Guards
{
    public static AllOf All(params GuardCondition[] conditions) => new(conditions);

    public static AnyOf Any(params GuardCondition[] conditions) => new(conditions);

    /// <summary>Both columns are NULL, or both are set (`deleted_at` and `deletion_batch_id`, `closed_at` and `closed_by`...).</summary>
    public static AnyOf Together(string property, string other) =>
        Any(All(new IsNull(property), new IsNull(other)), All(new IsNotNull(property), new IsNotNull(other)));
}

/// <summary>A unique index of the common model that only holds for some rows (what is alive, spec 4.2).</summary>
public sealed record PartialIndexGuard(Type Record, string IndexName, GuardCondition Filter);

/// <summary>A CHECK constraint of a table (closed lists, bounds, coherent columns, spec 4.2).</summary>
public sealed record CheckGuard(Type Record, string Name, GuardCondition Condition);
