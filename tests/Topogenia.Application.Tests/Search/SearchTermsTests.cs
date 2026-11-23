// SearchTermsTests.cs
// The words of a search (ADR 110 point 3, retouch R9): a text is found when it holds each word of the query, in any order, folded as one word is; each word marked once, the marks that overlap merged; a word of one character dropped beside a longer one; one word found as before.

using Topogenia.Application.Search;

namespace Topogenia.Application.Tests.Search;

public sealed class SearchTermsTests
{
    private const string WebSales = "Site web Livres à jour";

    private static IReadOnlyList<(int Start, int Length)>? Marks(string query, string text) =>
        SearchTerms.Of(query)?.Marks(text)?.Select(mark => (mark.Start, mark.Length)).ToList();

    [Fact]
    public void A_text_is_found_when_it_holds_each_word_in_any_order()
    {
        Assert.Equal([(5, 3), (9, 5)], Marks("livre web", WebSales));
        Assert.Equal([(5, 3), (9, 5)], Marks("web livre", WebSales));
        Assert.Equal([(5, 3), (9, 5)], Marks("  web \t  livre  ", WebSales));
        Assert.Null(Marks("livre mobile", WebSales));
        Assert.Null(Marks("mobile", WebSales));
        Assert.Equal(new TextMatch(5, 3), SearchTerms.Of("livre web")!.In(WebSales));
        Assert.Null(SearchTerms.Of("livre mobile")!.In(WebSales));
    }

    [Fact]
    public void Accents_and_case_do_not_matter_for_any_word()
    {
        Assert.Equal([(0, 6), (7, 6)], Marks("equipe MOBILE", "Équipe mobile"));
        Assert.Equal([(0, 8), (11, 6)], Marks("ÉQUIPE prevenir", "Prévenir l’équipe"));
        Assert.Equal([(0, 6), (7, 4)], Marks("strasse coeur", "Straße cœur"));
        Assert.Null(Marks("equipe strase", "Équipe Straße"));
    }

    [Fact]
    public void Each_word_is_marked_once_and_marks_that_overlap_or_touch_are_merged()
    {
        Assert.Equal([(0, 4)], Marks("site sit", "Site web"));
        Assert.Equal([(0, 4)], Marks("sit site", "Site web"));
        Assert.Equal([(0, 4), (5, 3)], Marks("web site", "Site web"));
        Assert.Equal([(0, 7)], Marks("site web", "siteweb"));
        Assert.Equal([(0, 6)], Marks("liv res", "Livres"));
        Assert.Equal([(5, 3)], Marks("web WEB wéb", "Site web web"));
        Assert.Single(SearchTerms.Of("web WEB wéb")!.Words);
    }

    [Fact]
    public void A_word_of_one_character_is_dropped_beside_a_longer_word_and_kept_among_words_of_one_character()
    {
        Assert.Equal(["livre"], SearchTerms.Of("a livre x")!.Words.Select(word => word.Folded));
        Assert.Equal([(0, 5)], Marks("a livre", "Livres"));
        Assert.Equal(["a", "b"], SearchTerms.Of("a b")!.Words.Select(word => word.Folded));
        Assert.Equal([(0, 2)], Marks("a b", "bac"));
        Assert.Null(Marks("a b", "Livres"));
        Assert.Equal(["注"], SearchTerms.Of("注")!.Words.Select(word => word.Folded));
        Assert.Equal([(1, 1)], Marks("注", "発注"));
        Assert.Null(SearchTerms.Of("   "));
        Assert.Null(SearchTerms.Of(null));
    }

    [Theory]
    [InlineData("Équipe mobile", "equipe")]
    [InlineData("cœur de l’équipe", "COEUR")]
    [InlineData("Straße du Nord", "du")]
    [InlineData("か", "が")]
    [InlineData("が", "か")]
    [InlineData("café", "cafe")]
    [InlineData("Site web Livres à jour", "livre")]
    [InlineData("Site web Livres à jour", "  jour ")]
    [InlineData("Site web Livres à jour", "mobile")]
    [InlineData("a¨b", "a¨b")]
    [InlineData("équipe equipe", "equipe")]
    public void One_word_finds_what_it_found_before_at_the_same_place(string text, string word)
    {
        Assert.Equal(SearchWord.Of(word)!.In(text), SearchTerms.Of(word)!.In(text));
        IReadOnlyList<TextMatch>? before = SearchWord.Of(word)!.In(text) is { } place ? [place] : null;
        Assert.Equal(before, SearchTerms.Of(word)!.Marks(text));
    }
}
