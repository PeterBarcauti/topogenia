// SearchText.cs
// The rule of the words searched (ADR 110 point 3, retouch R9): the fold of a text rune by rune, each folded character keeping its place of origin, the place of a word in the text of origin, and the words of a query found together in one text.

using System.Globalization;
using System.Text;

namespace Topogenia.Application.Search;

/// <summary>The place of a word found in a text of origin, in UTF-16 units (those of JavaScript).</summary>
public readonly record struct TextMatch(int Start, int Length);

/// <summary>
/// A word folded once, to be found in many texts: folded as the texts are (<see cref="SearchText.Fold"/>) and cut of the
/// spaces at its edges; a word empty after that has no <see cref="SearchWord"/> and finds nothing.
/// </summary>
public sealed class SearchWord
{
    private SearchWord(string folded)
    {
        Folded = folded;
    }

    public string Folded { get; }

    public static SearchWord? Of(string? word)
    {
        var folded = SearchText.Fold(word ?? "").Trim(' ');
        return folded.Length == 0 ? null : new SearchWord(folded);
    }

    /// <summary>
    /// The first place of the word in a text, compared ordinally on the folded texts (« fact » finds « Facture »): the
    /// characters of origin it touches, whole, with the marks removed that directly follow its last letter; a place that
    /// starts or ends between a letter and a mark kept is passed over (« か » does not find « が »). Null when absent.
    /// </summary>
    public TextMatch? In(string? text)
    {
        // The fold without places first: most texts do not hold the word.
        if (string.IsNullOrEmpty(text) || !SearchText.Fold(text).Contains(Folded, StringComparison.Ordinal))
            return null;
        return In(new FoldedText(text, withPlaces: true));
    }

    /// <summary>The first place of the word in a text already folded with its places, or null.</summary>
    internal TextMatch? In(FoldedText folded)
    {
        var from = 0;
        while (from <= folded.Text.Length - Folded.Length)
        {
            var at = folded.Text.IndexOf(Folded, from, StringComparison.Ordinal);
            if (at < 0)
                return null;
            var end = at + Folded.Length;
            if (!(at > 0 && FoldedText.IsKeptMark(folded.Text, at)) && !(end < folded.Text.Length && FoldedText.IsKeptMark(folded.Text, end)))
                return new TextMatch(folded.Starts[at], folded.Ends[end - 1] - folded.Starts[at]);
            from = at + 1;
        }
        return null;
    }
}

/// <summary>
/// The words of a query (ADR 110 point 3, retouch R9): the text cut at its spaces, each part a <see cref="SearchWord"/>, a
/// word repeated kept once (folded: « Web » and « WEB » are one word); a word of one character, as a person counts them, is dropped when
/// a longer word is there, kept when all are of one character. A text holds the query when it holds each word, in any
/// order; a query of one word finds what that word finds, at the same place.
/// </summary>
public sealed class SearchTerms
{
    private SearchTerms(IReadOnlyList<SearchWord> words) => Words = words;

    public IReadOnlyList<SearchWord> Words { get; }

    /// <summary>The words of a query, or null when none is left (a blank query finds nothing).</summary>
    public static SearchTerms? Of(string? query)
    {
        var words = new List<(SearchWord Word, int Characters)>();
        foreach (var part in Parts(query ?? ""))
        {
            if (SearchWord.Of(part) is { } word && words.TrueForAll(seen => !string.Equals(seen.Word.Folded, word.Folded, StringComparison.Ordinal)))
                words.Add((word, new StringInfo(part).LengthInTextElements));
        }
        if (words.Count == 0)
            return null;
        var longer = words.Exists(word => word.Characters >= 2);
        return new SearchTerms([.. words.Where(word => !longer || word.Characters >= 2).Select(word => word.Word)]);
    }

    /// <summary>
    /// The marks of the words in a text, by place, those that overlap or touch merged into one (« site sit » marks « Site »
    /// once): the first place of each word, as <see cref="SearchWord.In(string?)"/> gives it. Null when a word is absent.
    /// </summary>
    public IReadOnlyList<TextMatch>? Marks(string? text)
    {
        // The fold without places first: most texts do not hold the words.
        if (string.IsNullOrEmpty(text))
            return null;
        var plain = SearchText.Fold(text);
        if (!Words.All(word => plain.Contains(word.Folded, StringComparison.Ordinal)))
            return null;
        var folded = new FoldedText(text, withPlaces: true);
        var marks = new List<TextMatch>(Words.Count);
        foreach (var word in Words)
        {
            if (word.In(folded) is not { } mark)
                return null;
            marks.Add(mark);
        }
        return Merged(marks);
    }

    /// <summary>The first mark of the words in a text (the place a result carries), or null when a word is absent.</summary>
    public TextMatch? In(string? text) => Marks(text) is { } marks ? marks[0] : null;

    /// <summary>The parts of a query between its spaces (every white space of Unicode), in the text of origin.</summary>
    private static IEnumerable<string> Parts(string query)
    {
        var (index, from) = (0, 0);
        while (index < query.Length)
        {
            // A lone surrogate is one unit, never a space.
            var found = Rune.TryGetRuneAt(query, index, out var rune);
            var width = found ? rune.Utf16SequenceLength : 1;
            if (found && Rune.IsWhiteSpace(rune))
            {
                if (index > from)
                    yield return query[from..index];
                from = index + width;
            }
            index += width;
        }
        if (from < query.Length)
            yield return query[from..];
    }

    private static List<TextMatch> Merged(List<TextMatch> marks)
    {
        marks.Sort((left, right) => left.Start.CompareTo(right.Start));
        var merged = new List<TextMatch>(marks.Count);
        foreach (var mark in marks)
        {
            if (merged.Count > 0 && mark.Start <= merged[^1].Start + merged[^1].Length)
            {
                var last = merged[^1];
                merged[^1] = last with { Length = Math.Max(last.Start + last.Length, mark.Start + mark.Length) - last.Start };
            }
            else
            {
                merged.Add(mark);
            }
        }
        return merged;
    }
}

/// <summary>
/// The fold of a text, the single rule of the search on the host (the interface writes the same in `fold.ts`; both are
/// held equal by `web/src/test/oracle/search-fold.json`): rune by rune, decomposition NFKD; removal of the marks U+0300
/// to U+036F only (Latin, Greek and Cyrillic accents; the Japanese dakuten U+3099 is kept); invariant lower case; then
/// ß→ss, œ→oe, æ→ae, ø→o, ł→l, đ→d, ı→i, ς→σ, ’ and ‘ → ', – and — → -, every space → one space, spaces never merged.
/// </summary>
public static class SearchText
{
    public static string Fold(string text) => new FoldedText(text, withPlaces: false).Text;

    public static TextMatch? Find(string text, string word) => SearchWord.Of(word)?.In(text);

    /// <summary>The folded form of one rune, appended; nothing for a mark removed.</summary>
    internal static void AppendFolded(Rune rune, StringBuilder into)
    {
        if (rune.IsAscii)
        {
            into.Append(Rune.IsWhiteSpace(rune) ? ' ' : char.ToLowerInvariant((char)rune.Value));
            return;
        }
        foreach (var part in rune.ToString().Normalize(NormalizationForm.FormKD).EnumerateRunes())
        {
            if (part.Value is >= 0x0300 and <= 0x036F)
                continue;
            AppendLetter(Rune.ToLowerInvariant(part), into);
        }
    }

    private static void AppendLetter(Rune rune, StringBuilder into)
    {
        if (Rune.IsWhiteSpace(rune))
        {
            into.Append(' ');
            return;
        }
        var replaced = rune.Value switch
        {
            0x00DF => "ss",
            0x0153 => "oe",
            0x00E6 => "ae",
            0x00F8 => "o",
            0x0142 => "l",
            0x0111 => "d",
            0x0131 => "i",
            0x03C2 => "σ",
            0x2018 or 0x2019 => "'",
            0x2013 or 0x2014 => "-",
            _ => null,
        };
        if (replaced is not null)
        {
            into.Append(replaced);
            return;
        }
        Span<char> units = stackalloc char[2];
        into.Append(units[..rune.EncodeToUtf16(units)]);
    }
}

/// <summary>
/// A text folded, with, for each folded UTF-16 unit, the place of origin of the rune it comes from (start, end); the end of
/// the last rune is pushed over the removed marks that directly follow it.
/// </summary>
internal sealed class FoldedText
{
    private readonly List<int> starts = [];
    private readonly List<int> ends = [];

    public FoldedText(string text, bool withPlaces)
    {
        var builder = new StringBuilder(text.Length);
        var (index, lastFrom, lastEnd) = (0, -1, -1);
        while (index < text.Length)
        {
            // A lone surrogate is read as the replacement character, one unit long.
            var rune = Rune.TryGetRuneAt(text, index, out var found) ? found : Rune.ReplacementChar;
            var consumed = rune.Utf16SequenceLength;
            var from = builder.Length;
            SearchText.AppendFolded(rune, builder);
            var end = index + consumed;
            if (withPlaces && builder.Length > from)
            {
                starts.AddRange(Enumerable.Repeat(index, builder.Length - from));
                ends.AddRange(Enumerable.Repeat(end, builder.Length - from));
                (lastFrom, lastEnd) = (from, end);
            }
            else if (withPlaces && lastFrom >= 0 && lastEnd == index)
            {
                for (var unit = lastFrom; unit < ends.Count; unit++)
                    ends[unit] = end;
                lastEnd = end;
            }
            index = end;
        }
        Text = builder.ToString();
    }

    public string Text { get; }

    public IReadOnlyList<int> Starts => starts;

    public IReadOnlyList<int> Ends => ends;

    /// <summary>A mark kept by the fold (the dakuten, the marks of other scripts): it belongs to the letter before it.</summary>
    public static bool IsKeptMark(string folded, int index) =>
        CharUnicodeInfo.GetUnicodeCategory(folded, index) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
}
