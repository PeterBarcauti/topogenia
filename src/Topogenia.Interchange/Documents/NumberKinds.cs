// NumberKinds.cs
// The kinds of the numbers of an Excel cell told by their format (a number, a date, a time of day, a duration) and their value written as a person reads it (spec 3.0 section 3.4, lot 4).

using System.Globalization;
using System.Text;

namespace Topogenia.Interchange.Documents;

/// <summary>What a number of a cell is, by its format: Excel keeps a date or a time as a number of days.</summary>
internal enum NumberKind { Number, Date, Time, Duration }

internal static class NumberKinds
{
    /// <summary>
    /// The kind of a format: the built-in formats of dates (14 to 17, 22, 27 to 36, 50 to 58), of times (18 to 21, 45, 47)
    /// and of durations (46); a format of the workbook by its code (`[h]:mm` a duration, `dd/mm/yyyy` a date, `hh:mm` a time).
    /// </summary>
    public static NumberKind Of(int id, string? code)
    {
        if (code is null)
        {
            return id switch
            {
                >= 14 and <= 17 or 22 or >= 27 and <= 36 or >= 50 and <= 58 => NumberKind.Date,
                >= 18 and <= 21 or 45 or 47 => NumberKind.Time,
                46 => NumberKind.Duration,
                _ => NumberKind.Number,
            };
        }
        var lower = code.ToLowerInvariant();
        if (lower.Contains("[h", StringComparison.Ordinal) || lower.Contains("[m]", StringComparison.Ordinal) || lower.Contains("[mm]", StringComparison.Ordinal)
            || lower.Contains("[s", StringComparison.Ordinal))
            return NumberKind.Duration;
        var bare = Bare(lower);
        if (bare.Contains('y', StringComparison.Ordinal) || bare.Contains('d', StringComparison.Ordinal) || bare.Contains("mmm", StringComparison.Ordinal))
            return NumberKind.Date;
        return bare.Contains('h', StringComparison.Ordinal) || bare.Contains('s', StringComparison.Ordinal) ? NumberKind.Time : NumberKind.Number;
    }

    /// <summary>A code of format without its texts in quotes, its characters escaped and its sections in brackets (colours, locales).</summary>
    private static string Bare(string code)
    {
        var bare = new StringBuilder();
        var quoted = false;
        var bracket = false;
        for (var index = 0; index < code.Length; index++)
        {
            var c = code[index];
            if (quoted)
                quoted = c != '"';
            else if (bracket)
                bracket = c != ']';
            else if (c == '"')
                quoted = true;
            else if (c == '[')
                bracket = true;
            else if (c is '\\' or '_' or '*')
                index++;
            else
                bare.Append(c);
        }
        return bare.ToString();
    }

    /// <summary>The value of a number as its kind is read: an ISO date, a time of day, a duration in hours; a number without the noise of its binary form.</summary>
    public static string? Write(string? value, NumberKind kind, bool date1904)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            return value;
        switch (kind)
        {
            case NumberKind.Date when number is >= 0 and < 2_958_466:
                // A day of Excel counts from its first day, the 30th of December 1899 (or of 1904): a date written as read, the zone of no use.
                var date = new DateTime(date1904 ? 1904 : 1899, date1904 ? 1 : 12, date1904 ? 1 : 30, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(Math.Round(number * 86_400));
                return date.TimeOfDay == TimeSpan.Zero
                    ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : date.ToString(date.Second == 0 ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            case NumberKind.Time when number >= 0:
                var day = (long)Math.Round((number - Math.Floor(number)) * 86_400) % 86_400;
                return Clock(day / 3600, day);
            case NumberKind.Duration when Math.Abs(number) < 1_000_000:
                var total = (long)Math.Round(Math.Abs(number) * 86_400);
                return (number < 0 ? "-" : "") + Clock(total / 3600, total);
            default:
                return number.ToString("G15", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Hours and minutes, and the seconds when there are some.</summary>
    private static string Clock(long hours, long seconds)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"{hours:00}:{seconds / 60 % 60:00}");
        return seconds % 60 == 0 ? text : string.Create(CultureInfo.InvariantCulture, $"{text}:{seconds % 60:00}");
    }
}
