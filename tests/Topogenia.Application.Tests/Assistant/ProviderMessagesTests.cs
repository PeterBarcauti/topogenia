// ProviderMessagesTests.cs
// The message of a provider as it is relayed to the interface: on one line, without the key nor a word that holds a part of it or its end, bounded (ADR 098, R5-13).

using Topogenia.Application.Assistant;

namespace Topogenia.Application.Tests.Assistant;

public sealed class ProviderMessagesTests
{
    private const string Key = "sk-test-0123456789-wxyz";

    [Theory]
    [InlineData("Incorrect API key provided: sk-test-0123456789-wxyz.", "Incorrect API key provided: [...].")]
    [InlineData("Incorrect API key provided: (sk-test-0123456789-wxyz)", "Incorrect API key provided: ([...])")]
    [InlineData("The key SK-TEST-0123456789-WXYZ is refused, sk-test-0123456789-wxyz too.", "The key [...] is refused, [...] too.")]
    [InlineData("Incorrect API key provided: sk-test-****wxyz. Check it.", "Incorrect API key provided: [...] Check it.")]
    [InlineData("Incorrect API key provided: sk-test-01...", "Incorrect API key provided: [...]")]
    [InlineData("Authorization: Bearer%20sk-test-0123456789", "Authorization: [...]")]
    [InlineData("  Rate limit reached:\r\n  try again in 20 s.  ", "Rate limit reached:   try again in 20 s.")]
    [InlineData("Authentication Fails, Your api key: ****wxyz is invalid (request_id: 0123)", "Authentication Fails, Your api key: [...] is invalid (request_id: 0123)")]
    [InlineData("The key ending in WXYZ. is refused", "The key ending in [...] is refused")]
    [InlineData("The request 0123 of the test was refused (xyz).", "The request 0123 of the test was refused (xyz).")]
    public void A_message_is_relayed_on_one_line_without_the_key_nor_a_word_that_holds_a_part_of_it(string message, string relayed) =>
        Assert.Equal(relayed, ProviderMessages.Relayed(message, Key));

    [Fact]
    public void A_message_is_cut_at_its_bound_once_the_key_is_removed_and_an_empty_one_is_none()
    {
        Assert.Equal(300, ProviderMessages.Max);
        Assert.Equal(new string('a', 300), ProviderMessages.Relayed(new string('a', 400), Key));
        // The key astride the bound: removed before the cut, no start of it is left.
        var astride = ProviderMessages.Relayed(new string('a', 290) + " " + Key + " and more", Key);
        Assert.Equal(new string('a', 290) + " [...] and", astride);
        Assert.Null(ProviderMessages.Relayed(null, Key));
        Assert.Null(ProviderMessages.Relayed(" \r\n ", Key));
    }

    [Fact]
    public void Without_a_key_a_message_is_relayed_as_it_is_and_a_few_characters_shared_with_the_key_remove_no_word()
    {
        Assert.Equal("The SSO session expired.", ProviderMessages.Relayed("The SSO session expired.", null));
        Assert.Equal("The SSO session expired.", ProviderMessages.Relayed("The SSO session expired.", ""));
        // Five characters in common (-api0), under the six that make a part of the key: the word stays.
        Assert.Equal("invalid x-api0-key", ProviderMessages.Relayed("invalid x-api0-key", "sk-ant-api03-0123456789abcdef"));
        Assert.Equal("invalid [...]", ProviderMessages.Relayed("invalid sk-ant-api03", "sk-ant-api03-0123456789abcdef"));
    }
}
