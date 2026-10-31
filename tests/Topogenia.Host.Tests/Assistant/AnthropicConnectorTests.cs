// AnthropicConnectorTests.cs
// The connector of the API of Anthropic on an HTTP client of the tests: the request it sends, the reply it reads, each failure of the provider turned into its code; no request leaves the machine (spec 3.0 sections 4, 8).

using System.Net;
using System.Text;
using System.Text.Json;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Assistant;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AnthropicConnectorTests
{
    private const string Key = "sk-ant-test-0123";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string Reply = """
        {"id":"msg_test","type":"message","role":"assistant","model":"claude-opus-5-5","content":[{"type":"text","text":"OK"}],
         "stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":12,"output_tokens":4}}
        """;

    /// <summary>A handler that answers each request with the status and the body given, and keeps the requests.</summary>
    private sealed class Answering(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No such host is known.");
    }

    private static AssistantConnection Connection(string? model = null) =>
        new(AssistantSettings.Default with { Connector = AssistantConnector.Anthropic, Model = model ?? "claude-opus-5-5" }, Key);

    private static Task<AssistantTestOutcome> TestAsync(HttpMessageHandler handler, string? model = null) =>
        new AnthropicConnector(new HttpClient(handler)).TestAsync(Connection(model), Token);

    [Fact]
    public async Task A_test_sends_one_short_question_with_the_key_at_a_low_effort_and_reads_the_model_and_the_tokens()
    {
        var handler = new Answering(HttpStatusCode.OK, Reply);
        var outcome = await TestAsync(handler);
        Assert.Equal((true, "claude-opus-5-5", 12, 4, (string?)null), (outcome.Succeeded, outcome.Model, outcome.InputTokens, outcome.OutputTokens, outcome.Failure));
        var (request, body) = Assert.Single(handler.Requests);
        // The SDK sends the key of the API as a bearer token (its documentation of `ApiKey`).
        Assert.Equal(("/v1/messages", "Bearer", Key), (request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter));
        using var sent = JsonDocument.Parse(body);
        Assert.Equal(("claude-opus-5-5", 1024, "low"), (sent.RootElement.GetProperty("model").GetString(), sent.RootElement.GetProperty("max_tokens").GetInt32(),
            sent.RootElement.GetProperty("output_config").GetProperty("effort").GetString()));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""", AssistantFailures.Refused)]
    [InlineData(HttpStatusCode.Forbidden, """{"type":"error","error":{"type":"permission_error","message":"not allowed"}}""", AssistantFailures.Refused)]
    [InlineData(HttpStatusCode.NotFound, """{"type":"error","error":{"type":"not_found_error","message":"model: claude-unknown"}}""", AssistantFailures.ModelUnknown)]
    [InlineData(HttpStatusCode.TooManyRequests, """{"type":"error","error":{"type":"rate_limit_error","message":"slow down"}}""", AssistantFailures.Limited)]
    [InlineData(HttpStatusCode.BadRequest, """{"type":"error","error":{"type":"invalid_request_error","message":"Your credit balance is too low"}}""", AssistantFailures.Limited)]
    public async Task A_refusal_of_the_provider_is_an_outcome_with_its_code_and_its_message_after_one_request_only(HttpStatusCode status, string body, string failure)
    {
        var handler = new Answering(status, body);
        var outcome = await TestAsync(handler);
        Assert.Equal((false, failure), (outcome.Succeeded, outcome.Failure));
        // The test of a connection says the first failure: the SDK makes no new attempt, even on a limit.
        Assert.Single(handler.Requests);
        Assert.DoesNotContain(Key, outcome.Detail ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_provider_that_cannot_be_reached_is_said_unreachable()
    {
        var outcome = await TestAsync(new Unreachable());
        Assert.Equal((false, AssistantFailures.Unreachable), (outcome.Succeeded, outcome.Failure));
    }
}
