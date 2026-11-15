// ProviderMessages.cs
// What a connector relays of a refusal of its provider: the code of a ceiling refused, and the message of the provider without the key nor a part of it, bounded (ADR 098).

namespace Topogenia.Application.Assistant;

/// <summary>
/// The codes of failure of a connector added for the providers of ADR 098, beside those of
/// <see cref="Ports.AssistantFailures"/>: the interface names them in its language as it names the others.
/// </summary>
public static class ProviderFailures
{
    /// <summary>
    /// The provider refuses the ceiling of a reply of the settings (a 400 on `max_tokens` or `max_completion_tokens`:
    /// above what the model writes): to lower in the settings.
    /// </summary>
    public const string CeilingRefused = "ceiling_refused";
}

/// <summary>
/// The message of a provider as it may be shown: a provider writes what it wants in a refusal, the key it refuses
/// included (whole, or its start and its end around a mask). A connector cleans what it relays, and the requests clean
/// again whatever a connector gave them: the key never reaches the interface.
/// </summary>
public static class ProviderMessages
{
    /// <summary>The longest message relayed, in characters (the length the interface shows).</summary>
    public const int Max = 300;

    /// <summary>The characters of the key in a row that make a word a part of it: the fixed start of the keys of a provider is that long at least.</summary>
    public const int PartLength = 6;

    /// <summary>What stands in the place of the key, or of a word that holds a part of it.</summary>
    public const string Removed = "[...]";

    /// <summary>
    /// The message on one line, without the key (whatever its case) nor a word that holds a part of it
    /// (<see cref="PartLength"/> characters of it in a row, or its end: the characters the hint of the settings shows,
    /// which some providers repeat after a mask), then cut at <see cref="Max"/>: the key is removed before the cut, so
    /// that no start of it stays at the end. Null for no message.
    /// </summary>
    public static string? Relayed(string? message, string? secret)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;
        var line = message.ReplaceLineEndings(" ").Trim();
        if (!string.IsNullOrEmpty(secret))
            line = WithoutParts(line.Replace(secret, Removed, StringComparison.OrdinalIgnoreCase), secret);
        return line.Length <= Max ? line : line[..Max];
    }

    /// <summary>The line whose words that hold a part of the key or its end are removed (a key shorter than a part, or than its end, has none).</summary>
    private static string WithoutParts(string line, string secret)
    {
        var parts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var start = 0; start + PartLength <= secret.Length; start++)
            parts.Add(secret.Substring(start, PartLength));
        var end = secret.Length >= AssistantRules.HintLength ? secret[^AssistantRules.HintLength..] : null;
        var words = line.Split(' ');
        for (var index = 0; index < words.Length; index++)
        {
            if (HoldsPart(words[index], parts) || (end is not null && words[index].Contains(end, StringComparison.OrdinalIgnoreCase)))
                words[index] = Removed;
        }
        return string.Join(' ', words);
    }

    private static bool HoldsPart(string word, HashSet<string> parts)
    {
        for (var start = 0; start + PartLength <= word.Length; start++)
        {
            if (parts.Contains(word.Substring(start, PartLength)))
                return true;
        }
        return false;
    }
}
