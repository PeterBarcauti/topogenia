// ProjectCodeProposal.cs
// The code proposed from the name of a new project (3.4, lot 3, ADR 111 point 7): a pure rule on the folded name.

using Topogenia.Application.Search;

namespace Topogenia.Application.Projects;

/// <summary>
/// The code proposed from a name, before the free code is looked for (<see cref="ProposeProjectCodeQuery"/>). The rule of
/// Jira for the key of a project: the name folded by <see cref="SearchText.Fold"/> (accents, ligatures, ß, ł, ı), in capitals;
/// its words are the runs of letters A to Z (digits and the other scripts are left out, a code holds letters only); the
/// words of fewer than <see cref="ShortWord"/> letters are left out while a longer one remains, and make the code when none
/// does (« La RH » gives « LARH »); one word gives its first six letters, two words three letters of each, three to six
/// words their initials, more the initials of the first six. No word, or a code of fewer than <see cref="CodeMin"/>
/// letters (« RH », « 2026 »): no proposal, as the creation would refuse it; the person types the code (arbitration 8).
/// </summary>
public static class ProjectCodeProposal
{
    /// <summary>The words shorter than this are left out (« de », « la », « of », « to ») while a longer one remains.</summary>
    public const int ShortWord = 3;

    /// <summary>The fewest letters of a code (annex A 2.1): a shorter proposal is none.</summary>
    public const int CodeMin = 3;

    /// <summary>The most letters of a code (annex A 2.1), and the most words whose initials make one.</summary>
    public const int CodeMax = 6;

    public static string? FromName(string? name)
    {
        var words = Words(SearchText.Fold(name ?? string.Empty).ToUpperInvariant());
        var kept = words.Where(word => word.Length >= ShortWord).ToList();
        if (kept.Count == 0)
            kept = words;
        var code = kept.Count switch
        {
            0 => string.Empty,
            1 => Start(kept[0], CodeMax),
            2 => Start(kept[0], CodeMax / 2) + Start(kept[1], CodeMax / 2),
            _ => string.Concat(kept.Take(CodeMax).Select(word => word[0])),
        };
        return code.Length >= CodeMin ? code : null;
    }

    /// <summary>The runs of letters A to Z of a folded text in capitals, in their order.</summary>
    private static List<string> Words(string folded)
    {
        var words = new List<string>();
        var start = -1;
        for (var at = 0; at <= folded.Length; at++)
        {
            var letter = at < folded.Length && folded[at] is >= 'A' and <= 'Z';
            if (letter && start < 0)
                start = at;
            else if (!letter && start >= 0)
            {
                words.Add(folded[start..at]);
                start = -1;
            }
        }
        return words;
    }

    private static string Start(string word, int length) => word[..Math.Min(word.Length, length)];
}
