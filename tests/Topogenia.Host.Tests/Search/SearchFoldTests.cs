// SearchFoldTests.cs
// The fold of the word searched on the host against the table of cases it shares with the interface (ADR 110 point 3): each case found or not, at its place in the text of origin; each query of several words found or not, with its marks.

using System.Text;
using System.Text.Json;
using Topogenia.Application.Search;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Search;

/// <summary>
/// `web/src/test/oracle/search-fold.json` is read by this class and by `fold.test.ts` of the interface: a rule of the fold
/// changed on one side alone makes one of them fail. Each case gives a text, a word, whether it is found and its place in
/// the text of origin (UTF-16 units, those of JavaScript), null when it is not found.
/// </summary>
public sealed class SearchFoldTests
{
    private static readonly Lazy<IReadOnlyDictionary<string, JsonElement>> Table = new(() =>
    {
        using var document = JsonDocument.Parse(RepositoryPaths.Text("web", "src", "test", "oracle", "search-fold.json"));
        return document.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(item => item.GetProperty("name").GetString()!, item => item.Clone(),
            StringComparer.Ordinal);
    });

    private static readonly Lazy<IReadOnlyDictionary<string, JsonElement>> Queries = new(() =>
    {
        using var document = JsonDocument.Parse(RepositoryPaths.Text("web", "src", "test", "oracle", "search-fold.json"));
        return document.RootElement.GetProperty("queries").EnumerateArray().ToDictionary(item => item.GetProperty("name").GetString()!, item => item.Clone(),
            StringComparer.Ordinal);
    });

    public static TheoryData<string> Cases => [.. Table.Value.Keys];

    public static TheoryData<string> QueryCases => [.. Queries.Value.Keys];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_case_of_the_shared_table_is_found_or_not_at_its_place(string name)
    {
        var item = Table.Value[name];
        var match = SearchWord.Of(item.GetProperty("word").GetString())?.In(item.GetProperty("text").GetString());

        Assert.Equal(item.GetProperty("found").GetBoolean(), match is not null);
        static int? Place(JsonElement value) => value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
        Assert.Equal((Place(item.GetProperty("start")), Place(item.GetProperty("length"))), (match?.Start, match?.Length));
    }

    /// <summary>A query of one word (no space inside) finds what the word alone finds, at the same place (retouch R9).</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void A_query_of_one_word_finds_each_case_as_the_word_alone(string name)
    {
        var item = Table.Value[name];
        var (word, text) = (item.GetProperty("word").GetString()!, item.GetProperty("text").GetString());
        if (word.Trim().EnumerateRunes().Any(Rune.IsWhiteSpace))
            return;
        Assert.Equal(SearchWord.Of(word)?.In(text), SearchTerms.Of(word)?.In(text));
    }

    /// <summary>
    /// A query of several words (retouch R9) is found when each word is in the text, the marks by place, merged when they
    /// overlap or touch (R9b): the interface holds the same cut of the words for the filter of the home page.
    /// </summary>
    [Theory]
    [MemberData(nameof(QueryCases))]
    public void Every_query_of_the_shared_table_is_found_or_not_with_its_marks(string name)
    {
        var item = Queries.Value[name];
        var marks = SearchTerms.Of(item.GetProperty("query").GetString())?.Marks(item.GetProperty("text").GetString());

        Assert.Equal(item.GetProperty("found").GetBoolean(), marks is not null);
        IReadOnlyList<TextMatch>? expected = item.GetProperty("marks").ValueKind == JsonValueKind.Array
            ? item.GetProperty("marks").EnumerateArray().Select(mark => new TextMatch(mark[0].GetInt32(), mark[1].GetInt32())).ToList()
            : null;
        Assert.Equal(expected, marks);
    }

    [Fact]
    public void The_table_holds_the_cases_the_decision_names_each_once()
    {
        Assert.True(Table.Value.Count >= 17, $"The shared table holds {Table.Value.Count} cases.");
        var texts = Table.Value.Values.Select(item => item.GetProperty("text").GetString()).ToHashSet(StringComparer.Ordinal);
        Assert.Superset(new HashSet<string?>(StringComparer.Ordinal) { "Équipe", "cœur", "Straße", "İstanbul", "kapı", "Łódź", "が", "発注", "Ёлка" }, texts);
        Assert.True(Queries.Value.Count >= 10, $"The shared table holds {Queries.Value.Count} queries.");
    }
}
