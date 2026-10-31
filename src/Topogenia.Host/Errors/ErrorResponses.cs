// ErrorResponses.cs
// A refusal turned into its HTTP status and its body {code, message, details}, author and time written for a person (annex A 1, 2.16).

using System.Globalization;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Translations;

namespace Topogenia.Host.Errors;

/// <summary>The body of every error of the API (annex A 1): the stable code, the message in the language of the user, the values.</summary>
public sealed record ErrorBody(string Code, string Message, IReadOnlyDictionary<string, object?> Details);

/// <summary>
/// Turns a refusal into its HTTP status (400, 401, 403, 404, 409, 428 by its kind) and its body. A refusal that names who
/// changed an object and when (`updated_by`, `updated_at`: CONFLIT_VERSION, OPERATION_DEPASSEE) also gets `by` (the
/// display name, or « someone » when the user is unknown), `when` (how long ago, 1.x `Durees.IlYa`) and `when_iso`, which
/// its message uses (A-2.16-06, A-2.16-07). Any other failure is an internal error whose body says nothing of its cause
/// (ADR 036: never the message of an exception).
/// </summary>
public sealed class ErrorResponses
{
    private readonly ErrorMessages messages;

    public ErrorResponses(ErrorMessages messages)
    {
        this.messages = messages;
    }

    public static int StatusOf(DomainException refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        if (refusal.Code == ErrorCodes.NotAuthenticated)
            return 401;
        return refusal.Kind switch
        {
            RefusalKind.NotFound => 404,
            RefusalKind.Forbidden => 403,
            RefusalKind.Conflict => 409,
            RefusalKind.PreconditionRequired => 428,
            RefusalKind.TooLarge => 413,
            _ => 400,
        };
    }

    /// <summary>The body of a refusal, for a user of a language, at a time, with the display names of the users.</summary>
    public ErrorBody BodyOf(DomainException refusal, string language, DateTime now, Func<Guid, string?> displayNameOf)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        ArgumentNullException.ThrowIfNull(displayNameOf);
        var details = new Dictionary<string, object?>(refusal.Details, StringComparer.Ordinal);
        if (details.TryGetValue("updated_by", out var by) || details.ContainsKey("updated_at"))
        {
            details["by"] = (by is Guid user ? displayNameOf(user) : null) ?? Someone(language);
            if (details.TryGetValue("updated_at", out var at) && at is DateTime when)
            {
                details["when"] = RelativeTime.Ago(when, now, language, messages);
                details["when_iso"] = when.ToString("O", CultureInfo.InvariantCulture);
            }
        }
        if (!details.ContainsKey("by") && ErrorMessages.PlaceholdersOf(messages.Of(language).GetValueOrDefault(refusal.Code, "")).Contains("by"))
            details["by"] = Someone(language);
        if (details.TryGetValue("errors", out var errors) && errors is IEnumerable<CommandLineError> lines)
            details["errors"] = lines.Select(line => LineError(line, language)).ToList();
        else if (errors is IEnumerable<TranslationRowError> rows)
            details["errors"] = rows.Select(row => RowError(row, language)).ToList();
        else if (errors is IEnumerable<FileError> faults)
            details["errors"] = faults.Select(fault => FileFault(fault, language)).ToList();
        return new ErrorBody(refusal.Code, messages.Format(refusal.Code, language, details), details);
    }

    /// <summary>
    /// An error of a line of the command bar (COMMANDE_ERREURS, A-2.6-28): its line, code and detail, and its message
    /// « L{n} : » followed by the message of its code, whose `{code}` is the detail (the code of the step).
    /// </summary>
    private Dictionary<string, object?> LineError(CommandLineError line, string language)
    {
        var values = new Dictionary<string, object?> { ["line"] = line.Line, ["code"] = line.Detail };
        var message = messages.Format(line.Code, language, values);
        var template = messages.Of(language).GetValueOrDefault(line.Code, "");
        if (!ErrorMessages.PlaceholdersOf(template).Contains("line"))
            message = LinePrefixed(line.Line, language, message);
        return new Dictionary<string, object?> { ["line"] = line.Line, ["code"] = line.Code, ["detail"] = line.Detail, ["message"] = message };
    }

    /// <summary>
    /// A rejected translation of a workbook (TRADUCTIONS_INVALIDES, A-2.14-22): its line, language, code and maximum, and
    /// its message « L{n} : » followed by the message of its code, whose field is the language in capitals (1.x
    /// `ServiceTraductions.cs` 124-128, A-6-18).
    /// </summary>
    private Dictionary<string, object?> RowError(TranslationRowError row, string language)
    {
        var values = new Dictionary<string, object?> { ["field"] = row.Language.ToUpperInvariant(), ["max"] = row.Max };
        var message = LinePrefixed(row.Line, language, messages.Format(row.Code, language, values));
        return new Dictionary<string, object?>
        {
            ["line"] = row.Line, ["language"] = row.Language, ["code"] = row.Code, ["max"] = row.Max, ["message"] = message,
        };
    }

    /// <summary>
    /// A fault of a `.pmap` file (FICHIER_INVALIDE, A-1.2-19): its place in the file, its code and its message in the
    /// language of the user, from the message files (A-3-72: the 1.x wrote them in its format).
    /// </summary>
    private Dictionary<string, object?> FileFault(FileError fault, string language) =>
        new() { ["path"] = fault.Path, ["code"] = fault.Code, ["message"] = messages.Format(fault.Code, language, fault.Values) };

    private static string LinePrefixed(int line, string language, string message) =>
        string.Create(CultureInfo.InvariantCulture, $"L{line}{(language == "fr" ? " : " : ": ")}{message}");

    /// <summary>The body of a failure that is not a refusal: the internal error, without any detail.</summary>
    public ErrorBody InternalError(string language) =>
        new(ErrorCodes.InternalError, messages.Format(ErrorCodes.InternalError, language, null), new Dictionary<string, object?>());

    private string Someone(string language) => messages.Format(TimeWords.Someone, language, null);
}

/// <summary>
/// The words of a time and of an unknown author in the message files (ADR 083: every language of the interface), beside
/// the codes: « just now », a count of minutes, hours or days in each plural form (`_ONE`, `_FEW`, `_MANY`, `_OTHER`,
/// with `{n}`), and « someone ».
/// </summary>
public static class TimeWords
{
    public const string JustNow = "TEMPS_INSTANT";
    public const string Minutes = "TEMPS_MINUTES";
    public const string Hours = "TEMPS_HEURES";
    public const string Days = "TEMPS_JOURS";
    public const string Someone = "QUELQU_UN";

    /// <summary>The plural forms a count is written in: those of the languages of the interface for whole numbers.</summary>
    public static readonly IReadOnlyList<string> Forms = ["ONE", "FEW", "MANY", "OTHER"];

    /// <summary>Every key of the words, which each message file holds next to the codes.</summary>
    public static IEnumerable<string> Keys =>
        new[] { Minutes, Hours, Days }.SelectMany(unit => Forms.Select(form => $"{unit}_{form}")).Append(JustNow).Append(Someone);
}

/// <summary>How long ago a change happened (1.x `Durees.IlYa`), in the language of the user, its plural form chosen by the count.</summary>
public static class RelativeTime
{
    public static string Ago(DateTime at, DateTime now, string language, ErrorMessages messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var elapsed = now - at;
        if (elapsed.TotalMinutes < 1)
            return messages.Format(TimeWords.JustNow, language, null);
        if (elapsed.TotalHours < 1)
            return Count(TimeWords.Minutes, (int)elapsed.TotalMinutes, language, messages);
        if (elapsed.TotalDays < 1)
            return Count(TimeWords.Hours, (int)elapsed.TotalHours, language, messages);
        return Count(TimeWords.Days, (int)elapsed.TotalDays, language, messages);
    }

    private static string Count(string unit, int value, string language, ErrorMessages messages) =>
        messages.Format($"{unit}_{PluralForm(language, value)}", language, new Dictionary<string, object?> { ["n"] = value });

    /// <summary>
    /// The plural form of a whole count (the CLDR rules of the languages of the interface): one form in Japanese and
    /// Chinese, 0 and 1 together in French and Portuguese, few and many in the Slavic languages, few in Romanian; one and
    /// other elsewhere, the rule of a language added without its own.
    /// </summary>
    public static string PluralForm(string language, int count)
    {
        var tens = count % 100;
        var units = count % 10;
        var few = units is >= 2 and <= 4 && tens is < 12 or > 14;
        return language switch
        {
            "ja" or "zh" => "OTHER",
            "fr" or "pt" => count is 0 or 1 ? "ONE" : "OTHER",
            "cs" => count == 1 ? "ONE" : count is >= 2 and <= 4 ? "FEW" : "OTHER",
            "pl" => count == 1 ? "ONE" : few ? "FEW" : "MANY",
            "ru" or "uk" => units == 1 && tens != 11 ? "ONE" : few ? "FEW" : "MANY",
            "ro" => count == 1 ? "ONE" : count == 0 || tens is >= 1 and <= 19 ? "FEW" : "OTHER",
            _ => count == 1 ? "ONE" : "OTHER",
        };
    }
}
