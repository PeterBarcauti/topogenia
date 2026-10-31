// BedrockConnectorTests.cs
// The connector of AWS Bedrock on an HTTP factory and credentials of the tests: the request it signs and sends, the reply it reads, each failure of Bedrock or of the credentials turned into its code; no request leaves the machine and no profile of the machine is read (spec 3.0 sections 4, 8).

using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Assistant;

namespace Topogenia.Host.Tests.Assistant;

public sealed class BedrockConnectorTests
{
    /// <summary>Fictitious credentials: the signature is computed, nothing is sent out.</summary>
    private const string AccessKey = "TESTACCESSKEY";

    private const string Model = "global.anthropic.claude-sonnet-5-5";

    private const string Profile = "profil-fictif";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string Reply = """
        {"output":{"message":{"role":"assistant","content":[{"text":"OK"}]}},"stopReason":"end_turn",
         "usage":{"inputTokens":21,"outputTokens":4,"totalTokens":25},"metrics":{"latencyMs":300}}
        """;

    /// <summary>A handler that answers each request with the status, the type of error and the body given, and keeps the requests.</summary>
    private sealed class Answering(HttpStatusCode status, string body, string? errorType = null) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (errorType is not null)
                response.Headers.Add("x-amzn-ErrorType", errorType);
            return response;
        }
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No such host is known.");
    }

    /// <summary>The HTTP clients of the SDK, made on the handler of the test.</summary>
    private sealed class Factory(HttpMessageHandler handler) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(handler, disposeHandler: false);

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => true;
    }

    /// <summary>The credentials of a profile whose SSO session expired: the SDK cannot have them.</summary>
    private sealed class Expired : AWSCredentials
    {
        public override ImmutableCredentials GetCredentials() => throw new AmazonClientException("The SSO session associated with this profile has expired.");

        public override Task<ImmutableCredentials> GetCredentialsAsync() => throw new AmazonClientException("The SSO session associated with this profile has expired.");
    }

    private static AssistantConnection Connection(string? profile = Profile, string? region = "eu-west-3") =>
        new(AssistantSettings.Default with { Connector = AssistantConnector.Bedrock, Model = Model, Region = region, AwsProfile = profile }, null);

    private static Task<AssistantTestOutcome> TestAsync(HttpMessageHandler handler, Func<string?, AWSCredentials?>? credentials = null, AssistantConnection? connection = null) =>
        new BedrockConnector(new Factory(handler), credentials ?? (_ => new BasicAWSCredentials(AccessKey, "test-secret-key")))
            .TestAsync(connection ?? Connection(), Token);

    [Fact]
    public async Task A_test_sends_one_short_question_to_the_model_of_the_region_signed_by_the_credentials_of_the_profile_and_reads_the_tokens()
    {
        var handler = new Answering(HttpStatusCode.OK, Reply);
        var asked = new List<string?>();
        var outcome = await TestAsync(handler, profile =>
        {
            asked.Add(profile);
            return new BasicAWSCredentials(AccessKey, "test-secret-key");
        });
        Assert.Equal((true, Model, 21, 4, (string?)null), (outcome.Succeeded, outcome.Model, outcome.InputTokens, outcome.OutputTokens, outcome.Failure));
        Assert.Equal([Profile], asked);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(("bedrock-runtime.eu-west-3.amazonaws.com", $"/model/{Model}/converse"), (request.RequestUri!.Host, Uri.UnescapeDataString(request.RequestUri.AbsolutePath)));
        // The signature of AWS, with the access key of the credentials given (the SDK writes it without the typed header).
        Assert.StartsWith($"AWS4-HMAC-SHA256 Credential={AccessKey}/", string.Join(",", request.Headers.GetValues("Authorization")), StringComparison.Ordinal);
        using var sent = JsonDocument.Parse(body);
        Assert.Equal(("Réponds seulement par le mot OK.", 1024),
            (sent.RootElement.GetProperty("messages")[0].GetProperty("content")[0].GetProperty("text").GetString(),
                sent.RootElement.GetProperty("inferenceConfig").GetProperty("maxTokens").GetInt32()));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "AccessDeniedException", AssistantFailures.Refused)]
    [InlineData(HttpStatusCode.Forbidden, "ExpiredTokenException", AssistantFailures.SessionExpired)]
    [InlineData(HttpStatusCode.Forbidden, "UnrecognizedClientException", AssistantFailures.SessionExpired)]
    [InlineData(HttpStatusCode.BadRequest, "ValidationException", AssistantFailures.ModelUnknown)]
    [InlineData(HttpStatusCode.NotFound, "ResourceNotFoundException", AssistantFailures.ModelUnknown)]
    [InlineData(HttpStatusCode.TooManyRequests, "ThrottlingException", AssistantFailures.Limited)]
    [InlineData(HttpStatusCode.BadRequest, "ServiceQuotaExceededException", AssistantFailures.Limited)]
    [InlineData(HttpStatusCode.InternalServerError, "InternalServerException", AssistantFailures.Other)]
    public async Task A_refusal_of_bedrock_is_an_outcome_with_its_code_and_its_message_after_one_request_only(HttpStatusCode status, string errorType, string failure)
    {
        var handler = new Answering(status, """{"message":"Refused by the test."}""", errorType);
        var outcome = await TestAsync(handler);
        Assert.Equal((false, failure), (outcome.Succeeded, outcome.Failure));
        Assert.Contains("Refused by the test.", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("test-secret-key", outcome.Detail ?? "", StringComparison.Ordinal);
        // The test of a connection says the first failure: the SDK makes no new attempt, even on a limit.
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_profile_the_machine_does_not_have_is_said_missing_and_nothing_is_sent()
    {
        var handler = new Answering(HttpStatusCode.OK, Reply);
        var outcome = await TestAsync(handler, _ => null);
        Assert.Equal((false, AssistantFailures.ProfileMissing), (outcome.Succeeded, outcome.Failure));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Credentials_that_cannot_be_had_are_a_session_to_open_again_and_nothing_is_sent()
    {
        var handler = new Answering(HttpStatusCode.OK, Reply);
        var outcome = await TestAsync(handler, _ => new Expired());
        Assert.Equal((false, AssistantFailures.SessionExpired), (outcome.Succeeded, outcome.Failure));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Without_a_profile_the_identity_of_the_host_is_asked_and_without_a_region_nothing_is_asked()
    {
        var asked = new List<string?>();
        AWSCredentials Host(string? profile)
        {
            asked.Add(profile);
            return new BasicAWSCredentials(AccessKey, "test-secret-key");
        }
        Assert.True((await TestAsync(new Answering(HttpStatusCode.OK, Reply), Host, Connection(profile: null))).Succeeded);
        Assert.Equal([(string?)null], asked);
        var handler = new Answering(HttpStatusCode.OK, Reply);
        Assert.Equal(AssistantFailures.ModelUnknown, (await TestAsync(handler, Host, Connection(region: null))).Failure);
        Assert.Equal([(string?)null], asked);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Bedrock_that_cannot_be_reached_is_said_unreachable()
    {
        var outcome = await TestAsync(new Unreachable());
        Assert.Equal((false, AssistantFailures.Unreachable), (outcome.Succeeded, outcome.Failure));
    }
}
