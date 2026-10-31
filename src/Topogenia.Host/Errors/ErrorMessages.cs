// ErrorMessages.cs
// The message of each error code in each language of the interface, filled with the values of its details (spec 3.6, annex A 3).

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Topogenia.Domain.Errors;

namespace Topogenia.Host.Errors;

/// <summary>
/// The messages of the error codes, one embedded file per language (`Errors/messages.{language}.json`, from the 1.x
/// `Messages.cs`): the domain and the application return codes and values only, the host writes the sentence (spec
/// 3.3). A placeholder `{key}` takes the value of the detail of the same key, written in the invariant culture; a
/// placeholder without its detail is removed. An unknown code gets the message of ERREUR_INTERNE; a language without
/// messages gets the English ones.
/// </summary>
public sealed partial class ErrorMessages
{
    /// <summary>The language whose messages stand in for a language that has none.</summary>
    public const string FallbackLanguage = "en";

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> languages;

    private ErrorMessages(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> languages)
    {
        this.languages = languages;
    }

    /// <summary>The languages that have messages.</summary>
    public IReadOnlyCollection<string> Languages => [.. languages.Keys];

    /// <summary>Reads the embedded message files of the errors of the API.</summary>
    public static ErrorMessages Embedded() => Embedded($"{typeof(ErrorMessages).Namespace}.messages.");

    /// <summary>
    /// Reads the embedded message files whose resource names start with <paramref name="prefix"/> (another catalogue of
    /// the host: the messages of the start, `Startup/messages.*.json`); each catalogue holds ERREUR_INTERNE.
    /// </summary>
    public static ErrorMessages Embedded(string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        var assembly = typeof(ErrorMessages).Assembly;
        var languages = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            languages[name[prefix.Length..^".json".Length]] =
                JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? throw new InvalidOperationException($"Empty messages {name}.");
        }
        return new ErrorMessages(languages);
    }

    /// <summary>The templates of a language, code by code.</summary>
    public IReadOnlyDictionary<string, string> Of(string language) =>
        languages.TryGetValue(language, out var messages) ? messages : languages[FallbackLanguage];

    /// <summary>The message of a code in a language, its placeholders filled from the details.</summary>
    public string Format(string code, string language, IReadOnlyDictionary<string, object?>? details)
    {
        var messages = Of(language);
        var template = messages.TryGetValue(code, out var known) ? known : messages[ErrorCodes.InternalError];
        var filled = Placeholder().Replace(template, match =>
            details is not null && details.TryGetValue(match.Groups[1].Value, out var value) ? Text(value) : "");
        return Spaces().Replace(filled, " ").Replace(" .", ".", StringComparison.Ordinal).Trim();
    }

    /// <summary>The keys of the placeholders of a template.</summary>
    public static IReadOnlyList<string> PlaceholdersOf(string template) =>
        [.. Placeholder().Matches(template).Select(match => match.Groups[1].Value)];

    private static string Text(object? value) => value switch
    {
        null => "",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    [GeneratedRegex(@"\{([a-z_]+)\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(" {2,}")]
    private static partial Regex Spaces();
}
