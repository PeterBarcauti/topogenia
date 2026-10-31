// AssistantRepliesTests.cs
// The reply of the model read as data: its four forms, a reply pasted with words or a fence around it, a .pmap alone, the texts and lists bounded, the replies refused; the rules of a request; the cost estimated (spec 3.0 sections 3.4, 5, 6).

using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantRepliesTests
{
    private static AssistantReply Read(string text) => AssistantReplyReader.Read(text, out var reason) ?? throw new InvalidOperationException(reason);

    [Fact]
    public void An_answer_is_read_with_its_codes_and_its_json_kept_as_written()
    {
        const string text = """{"form":"answer","message":"  Deux étapes : FAC-E001 et FAC-E002.  ","cited":["FAC-E001","FAC-E001","FAC-E002"],"questions":[],"findings":[],"pmap":null}""";
        var reply = Read(text);
        Assert.Equal((AssistantForm.Answer, "Deux étapes : FAC-E001 et FAC-E002.", (string?)null, text), (reply.Form, reply.Message, reply.Pmap, reply.Raw));
        Assert.Equal(["FAC-E001", "FAC-E002"], reply.Cited);
    }

    [Fact]
    public void Questions_and_findings_are_read_bounded_and_an_unknown_kind_is_another_finding()
    {
        var questions = Read("""{"form":"questions","message":"","cited":[],"questions":[{"question":"Qui valide ?","choices":["A","B"]},"Quel délai ?"],"findings":[],"pmap":null}""");
        Assert.Equal([("Qui valide ?", 2), ("Quel délai ?", 0)], questions.Questions.Select(question => (question.Question, question.Choices.Count)));
        var findings = Read("""
            {"form":"findings","message":"Deux constats.","cited":[],"questions":[],"pmap":null,
             "findings":[{"kind":"re_entry","title":"Ressaisie","detail":"La facture est ressaisie.","objects":["FAC-E002"],"suggestion":"Importer."},
                         {"kind":"invented","title":"Autre","detail":"","objects":[],"suggestion":""},{"kind":"wait","title":"","detail":"","objects":[],"suggestion":""}]}
            """);
        Assert.Equal([("re_entry", "Importer."), ("other", null)], findings.Findings.Select(finding => (finding.Kind, finding.Suggestion)));
        var many = Read($$"""{"form":"questions","message":"{{new string('x', AssistantBounds.MessageMax + 50)}}","questions":[{{string.Join(',', Enumerable.Repeat("\"Q ?\"", 30))}}]}""");
        Assert.Equal((AssistantBounds.MessageMax, AssistantBounds.QuestionsMax), (many.Message.Length, many.Questions.Count));
        Assert.EndsWith("…", many.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reply_pasted_with_words_or_a_fence_around_it_and_a_pmap_alone_are_read()
    {
        var fenced = Read("Voici la réponse :\n```json\n{\"form\":\"answer\",\"message\":\"OK\"}\n```\nBonne journée.");
        Assert.Equal((AssistantForm.Answer, "OK"), (fenced.Form, fenced.Message));
        const string pmap = """{"format":"topogenia","version":"1.1","project":{"name":"X"},"map":{"lanes":[]}}""";
        var bare = Read(pmap);
        Assert.Equal((AssistantForm.Proposal, pmap), (bare.Form, bare.Pmap));
        var nested = Read("""{"form":"proposal","message":"Une carte.","pmap":{"format":"topogenia","version":"1.1"}}""");
        Assert.Equal("""{"format":"topogenia","version":"1.1"}""", nested.Pmap);
    }

    [Theory]
    [InlineData("Je ne sais pas.")]
    [InlineData("{pas du json}")]
    [InlineData("""{"form":"poem","message":"Ô"}""")]
    [InlineData("""{"form":"answer","message":"   "}""")]
    [InlineData("""{"form":"questions","message":"Deux points.","questions":[]}""")]
    [InlineData("""{"form":"proposal","message":"Une carte.","pmap":null}""")]
    [InlineData("[1, 2]")]
    public void A_reply_that_is_not_one_of_the_four_forms_is_refused_with_its_reason(string text)
    {
        Assert.Null(AssistantReplyReader.Read(text, out var reason));
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void A_request_is_trimmed_bounded_and_its_former_turns_and_selection_counted()
    {
        Assert.Equal("Bonjour\n\tà tous", AssistantAskRules.RequestOf("  Bonjour\u0007\n\tà tous  "));
        Assert.Equal(ErrorCodes.RequiredField, Assert.Throws<DomainException>(() => AssistantAskRules.RequestOf(" \n ")).Code);
        Assert.Equal(ErrorCodes.TextTooLong, Assert.Throws<DomainException>(() => AssistantAskRules.RequestOf(new string('a', AssistantBounds.RequestMax + 1))).Code);
        Assert.Equal((AssistantIntent.Free, AssistantIntent.Analyze), (AssistantAskRules.IntentOf(null), AssistantAskRules.IntentOf("analyze")));
        Assert.Equal(ErrorCodes.InvalidValue, Assert.Throws<DomainException>(() => AssistantAskRules.IntentOf("dance")).Code);
        Assert.Equal(["A-E001", "B-E002"], AssistantAskRules.SelectionOf([" A-E001 ", "", "A-E001", "B-E002"]));
        var turns = Enumerable.Repeat(new AssistantTurn("Q", "R"), AssistantBounds.TurnsMax + 1).ToList();
        Assert.Equal(ErrorCodes.InvalidValue, Assert.Throws<DomainException>(() => AssistantAskRules.HistoryOf(turns)).Code);
        Assert.Equal(ErrorCodes.RequiredField, Assert.Throws<DomainException>(() => AssistantAskRules.HistoryOf([new AssistantTurn("Q", " ")])).Code);
    }

    [Fact]
    public void The_cost_counts_each_kind_of_token_at_its_price_and_is_unknown_for_another_model()
    {
        // claude-opus-5-5: 4 $ in, 20 $ out, 0.20 $ read from the cache, 1.25 times the input written to it, per million tokens.
        var usage = new AssistantUsage(1_000_000, 1_000_000, 1_000_000, 1_000_000);
        Assert.Equal(4m + 0.20m + 5m + 20m, AssistantPrices.Estimate("claude-opus-5-5", usage));
        Assert.Equal(0.4728m, AssistantPrices.Estimate("claude-opus-5-5", new AssistantUsage(4309, 0, 86079, 1256)));
        Assert.Null(AssistantPrices.Estimate("global.anthropic.claude-sonnet-5-5", usage));
        Assert.Null(AssistantPrices.Estimate(null, usage));
    }
}
