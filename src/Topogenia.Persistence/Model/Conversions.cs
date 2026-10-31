// Conversions.cs
// Value conversions of the model: UTC dates (D08), the stable codes of the closed lists (snake_case, as in the 1.x), decimals at the scale of their column (ADR 039), texts both engines compare alike (ADR 038).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Topogenia.Domain.Values;

namespace Topogenia.Persistence.Model;

/// <summary>
/// Dates are UTC everywhere (D08). A local date is a bug and is refused at the writing; a date read back is marked UTC
/// (the engines keep no kind). The full precision is kept on both engines (`datetime2(7)`, text in SQLite): a record
/// read back is equal to the record written, which the undo compares (AR-81).
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(value => UtcDates.ToStore(value), value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}

/// <summary>Writing side of <see cref="UtcDateTimeConverter"/>.</summary>
public static class UtcDates
{
    /// <summary>The date to store: a UTC date as it is; an unspecified one is taken as UTC; a local one is refused (D08).</summary>
    public static DateTime ToStore(DateTime value) =>
        value.Kind is DateTimeKind.Utc or DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : throw new ArgumentException("Dates are stored in UTC only (D08).", nameof(value));
}

/// <summary>A closed list stored as its stable code (`task`, `month_end_close`, `A`...), the code of <see cref="Codes"/>.</summary>
public sealed class CodeConverter<T> : ValueConverter<T, string>
    where T : struct, Enum
{
    public CodeConverter()
        : base(value => Codes.Of(value), code => FromCode(code))
    {
    }

    /// <summary>The value of a stored code; a code outside the list is a corrupted row and is refused.</summary>
    public static T FromCode(string code) =>
        Codes.TryParse<T>(code, out var value)
            ? value
            : throw new InvalidOperationException($"The stored code '{code}' is not a {typeof(T).Name}.");

    /// <summary>The longest code of the list: the width of its column.</summary>
    public static int MaxLength => Codes.All<T>().Max(code => code.Length);
}

/// <summary>
/// A decimal written and read at the scale of its column, the same on both engines (ADR 039). SQL Server rounds a value
/// beyond the scale of its `decimal(p,s)` column and gives it back with trailing zeros (`12.50`); SQLite stores the exact
/// text that EF Core writes and gives back what it got (`9.755`, `12.5`). Writing rounds half away from zero, as SQL
/// Server and the domain do (`Duration`), and refuses a value that the column cannot hold (SQL Server would fail on it,
/// SQLite would keep it); reading drops the trailing zeros. So both databases store, check and give back the same value.
/// </summary>
public sealed class ScaledDecimalConverter : ValueConverter<decimal, decimal>
{
    public ScaledDecimalConverter(int precision, int scale)
        : base(value => ScaledDecimals.ToStore(value, precision, scale), value => ScaledDecimals.Normalize(value))
    {
    }
}

/// <summary>The two sides of <see cref="ScaledDecimalConverter"/>.</summary>
public static class ScaledDecimals
{
    /// <summary>A one followed by the 28 decimal places of a decimal: dividing by it drops the trailing zeros.</summary>
    private const decimal One = 1.0000000000000000000000000000m;

    /// <summary>The value to store: rounded to <paramref name="scale"/> decimals, refused when it needs more than <paramref name="precision"/> digits.</summary>
    public static decimal ToStore(decimal value, int precision, int scale)
    {
        var rounded = Math.Round(value, scale, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded) >= Limit(precision, scale))
            throw new ArgumentOutOfRangeException(nameof(value), $"A decimal({precision},{scale}) column cannot hold this value.");
        return Normalize(rounded);
    }

    /// <summary>The same number without trailing zeros (`12.50` gives `12.5`, `100000.00` gives `100000`), zero without sign.</summary>
    public static decimal Normalize(decimal value) => value == 0m ? 0m : value / One;

    /// <summary>The first value that a `decimal(p,s)` column cannot hold: ten to the power of p minus s.</summary>
    private static decimal Limit(int precision, int scale)
    {
        var limit = 1m;
        for (var digit = 0; digit < precision - scale; digit++)
            limit *= 10m;
        return limit;
    }
}

/// <summary>
/// A text written as it is, refused before any writing when the two engines would not take it alike (ADR 038): SQL Server
/// ignores the trailing spaces of a comparison (`'X' = 'X '`, a unique index, a closed list, a search), SQLite does not;
/// SQL Server converts a character of a technical column (`varchar`) outside its code page before any CHECK sees it (`ł`
/// read back `l`), SQLite keeps it; SQLite measures and compares a text up to its first NUL character only (`length`,
/// `GLOB`), SQL Server the whole text. The domain never gives such a value (entered texts trimmed and without control
/// character, names of the directory trimmed after their cut, codes, languages and kinds in ASCII); a value compared in a
/// query goes through the same converter, so a search finds the same rows on both engines. A row written by hand meets
/// the CHECK of the column (<see cref="ModelGuards.TextChecks"/>).
/// </summary>
public sealed class StoredTextConverter : ValueConverter<string, string>
{
    public StoredTextConverter(bool ascii)
        : base(value => StoredTexts.ToStore(value, ascii), value => value)
    {
        Ascii = ascii;
    }

    /// <summary>True for a technical column (`varchar`): printable ASCII only.</summary>
    public bool Ascii { get; }
}

/// <summary>
/// The text searched for inside a stored text (`Contains` of SQLite, <see cref="TextSearchParity"/>): refused like a
/// stored text, except for its trailing spaces, which a search for a part of a text compares on both engines.
/// </summary>
public sealed class SearchedTextConverter : ValueConverter<string, string>
{
    public SearchedTextConverter(bool ascii)
        : base(value => StoredTexts.ToSearch(value, ascii), value => value)
    {
    }
}

/// <summary>Writing side of <see cref="StoredTextConverter"/>, and its place on every text column of the model.</summary>
public static class StoredTexts
{
    /// <summary>
    /// The text to store: refused when it ends with a space, holds a NUL character, or, for an ASCII column, holds
    /// anything but printable ASCII.
    /// </summary>
    public static string ToStore(string value, bool ascii)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.EndsWith(' '))
            throw new ArgumentException("A stored text never ends with a space (ADR 038).", nameof(value));
        return ToSearch(value, ascii);
    }

    /// <summary>The text searched for inside a stored text: refused when it holds a NUL character or, for an ASCII column, anything but printable ASCII.</summary>
    public static string ToSearch(string value, bool ascii)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0', StringComparison.Ordinal))
            throw new ArgumentException("A stored text never holds a NUL character (ADR 038).", nameof(value));
        if (ascii && !IsPrintableAscii(value))
            throw new ArgumentException("A technical text holds printable ASCII only (ADR 038).", nameof(value));
        return value;
    }

    /// <summary>True when every character is between a space and a tilde.</summary>
    public static bool IsPrintableAscii(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.All(character => character is >= ' ' and <= '~');
    }

    /// <summary>Gives every string column of the model the converter, ASCII for a column that is not Unicode (`varchar`).</summary>
    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            if (property.ClrType == typeof(string) && property.GetValueConverter() is null)
                property.SetValueConverter(new StoredTextConverter(property.IsUnicode() == false));
        }
    }
}
