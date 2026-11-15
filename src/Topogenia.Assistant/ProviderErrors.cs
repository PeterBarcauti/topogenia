// ProviderErrors.cs
// The refusals of a provider turned into the codes the interface names: by the status, and for a request refused (400, 422) by what its message says: the key, the ceiling of the reply, the model, the credit (ADR 098).

using System.Text.RegularExpressions;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;

namespace Topogenia.Assistant;

/// <summary>
/// The code of a refusal. A request refused says why in its message only, and each provider words it in its way: a key
/// refused first (two makers answer 400 to a key that is none), then the ceiling (its message often names the model
/// too), then a model the provider does not have, then a credit spent. The word `model` alone decides nothing: a
/// provider names its model in many refusals (a context too long, a format or a parameter it does not take).
/// </summary>
internal static partial class ProviderErrors
{
    /// <summary>The most characters of a message that are read to class it.</summary>
    private const int Read = 4000;

    /// <summary>A message on the key: "Incorrect API key provided", "Please pass a valid API key", "Invalid Authentication".</summary>
    [GeneratedRegex(@"\bapi[ _-]?key\b|\binvalid authentication\b|\bauthentication fail", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Key();

    /// <summary>
    /// A message on the ceiling of the reply: the name of its parameter (`max_tokens`, `max_completion_tokens`,
    /// `maxOutputTokens`, `num_predict`), "at most N completion tokens", "the maximum tokens you requested", or a
    /// context exceeded by the room kept for the completion.
    /// </summary>
    [GeneratedRegex(
        @"max_?(completion_?|output_?|new_?)?tokens|num_predict|\bat most [\d,. ]+(\w+ ){0,2}tokens\b|" +
        @"\bmaximum (allowed )?(number of )?((output|completion|generated|new) )?tokens\b|\bmaximum context length\b.{0,300}\bcompletion\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Ceiling();

    /// <summary>
    /// A message that refuses a parameter itself, whatever its value ("Unsupported parameter: 'max_tokens' is not
    /// supported with this model"): the provider does not take that name, which lowering the ceiling would not mend.
    /// </summary>
    [GeneratedRegex(@"unsupported|not supported|unrecognized|unknown (parameter|argument|field)|extra inputs|extra_forbidden|not permitted", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ParameterRefused();

    /// <summary>A message that says the provider does not have the model: "invalid model", "the model X does not exist", "model not found".</summary>
    [GeneratedRegex(
        @"\b(invalid|unknown|unsupported|unrecognized|no such)\s+model\b|\bmodel\b.{0,80}?(\bdoes not exist|\bdoesn't exist|\bnot exist|\bnot found|\bis not available)|" +
        @"\bnot found the model\b|model_not_found|invalid_model",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnknownModel();

    [GeneratedRegex(@"\b(credits?|balance|quota)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Credit();

    /// <summary>What is said of a redirection, which no connector follows: its status, in the place of the message of the provider.</summary>
    public static string Redirection(int status) => $"HTTP {status}: the provider answered with a redirection, which is never followed.";

    /// <summary>The code of a refusal of the provider by its status, and by its message for a request refused (400, 422).</summary>
    public static string Of(int status, string? message) => status switch
    {
        401 or 403 => AssistantFailures.Refused,
        404 => AssistantFailures.ModelUnknown,
        402 or 429 => AssistantFailures.Limited,
        400 or 422 => OfBadRequest(message),
        _ => AssistantFailures.Other,
    };

    /// <summary>The code of a request refused, by what its message says.</summary>
    public static string OfBadRequest(string? message)
    {
        var said = Line(message);
        if (Key().IsMatch(said))
            return AssistantFailures.Refused;
        if (MentionsCeiling(said))
            return ProviderFailures.CeilingRefused;
        if (UnknownModel().IsMatch(said))
            return AssistantFailures.ModelUnknown;
        return Credit().IsMatch(said) ? AssistantFailures.Limited : AssistantFailures.Other;
    }

    /// <summary>Whether a message refuses the value of the ceiling of the reply (not the name of its parameter).</summary>
    public static bool MentionsCeiling(string? message)
    {
        var said = Line(message);
        return Ceiling().IsMatch(said) && !ParameterRefused().IsMatch(said);
    }

    private static string Line(string? message)
    {
        var line = message?.ReplaceLineEndings(" ") ?? string.Empty;
        return line.Length <= Read ? line : line[..Read];
    }
}
