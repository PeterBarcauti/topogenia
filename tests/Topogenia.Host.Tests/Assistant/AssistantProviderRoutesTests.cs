// AssistantProviderRoutesTests.cs
// The providers of ADR 098 through the whole host on both databases: a model of this machine tested and asked by the connector of the shipped code on a service of the loopback, a key given to its provider only, a reply that is no completion and a fault of a connector audited, the message of a provider relayed without the key, the cost of Claude by AWS Bedrock.

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Assistant;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantProviderRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>Keys of the tests: never sent to a real service, the connectors answer in memory or call the loopback.</summary>
    private const string Key = "sk-test-routes-0123456789-wxyz";

    private const string OtherKey = "sk-ant-test-routes-0123456789-abcd";

    private const string Answer = """{"form":"answer","message":"Trois étapes.","cited":[],"questions":[],"findings":[],"pmap":null}""";

    private const string Model = "llama3.1:8b";

    /// <summary>A handler in the place of the network of a provider on line: it keeps where each request went and its key, and answers a completion.</summary>
    private sealed class Recording : HttpMessageHandler
    {
        public ConcurrentQueue<(string Address, string? Bearer)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue((request.RequestUri!.ToString(), request.Headers.Authorization?.Parameter));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Completion(Answer).Body, Encoding.UTF8, "application/json") });
        }
    }

    /// <summary>A connector whose provider repeats the key it refuses, at a test and at an exchange; or whose exchange is the one given.</summary>
    private sealed class Scripted(AssistantConnector kind, Func<AssistantConnection, AssistantExchangeOutcome>? exchange = null) : IAssistantConnector
    {
        public AssistantConnector Kind => kind;

        public Task<AssistantTestOutcome> TestAsync(AssistantConnection connection, CancellationToken cancellationToken) =>
            Task.FromResult(AssistantTestOutcome.Failed(AssistantFailures.Refused, 1, $"Incorrect API key provided: {connection.Secret}"));

        public Task<AssistantExchangeOutcome> ExchangeAsync(AssistantConnection connection, AssistantPrompt prompt, IProgress<AssistantProgress> progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(exchange is null
                ? AssistantExchangeOutcome.Failed(AssistantFailures.Refused, 1, null, $"Incorrect API key provided: {connection.Secret}")
                : exchange(connection));
    }

    private static LocalReply Completion(string content) => new(200, JsonSerializer.Serialize(new
    {
        model = Model,
        choices = new[] { new { index = 0, message = new { role = "assistant", content }, finish_reason = "stop" } },
        usage = new { prompt_tokens = 900, completion_tokens = 12 },
    }));

    private static List<JsonElement> Lines(ApiResponse response)
    {
        Assert.Equal(200, response.Status);
        return [.. response.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonDocument.Parse(line).RootElement.Clone())];
    }

    private static object Ask(string request) => new { intent = "free", request, selection = Array.Empty<string>(), history = Array.Empty<object>() };

    private static async Task<JsonElement> SetAsync(ApiClient admin, object settings)
    {
        var version = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
        return (await admin.PutAsync("/api/admin/assistant", settings, version)).Ok();
    }

    /// <summary>The details of the last event of a kind in the audit; an empty text when it has none.</summary>
    private static async Task<string> LastAsync(ApiClient admin, string kind)
    {
        var events = (await admin.GetAsync($"/api/admin/audit?kind={kind}")).Ok().EnumerateArray().ToList();
        return events.Count == 0 ? string.Empty : events[0].GetProperty("details").GetString()!;
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_model_of_this_machine_is_tested_and_asked_by_the_connector_of_the_shipped_code_at_the_port_of_the_settings(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        await using var program = new LocalChatService(Completion("OK"), Completion(Answer));
        await host.WithVariantAsync(services => services.AddSingleton<IAssistantConnector>(new OpenAiCompatibleConnector(AssistantConnector.Ollama)), async clientOf =>
        {
            using var admin = clientOf(Logins.Admin);
            var settings = new { connector = "ollama", model = Model, port = program.Port, max_output_tokens = 4096, allowed_classifications = new[] { "internal" } };
            var saved = await SetAsync(admin, settings);
            Assert.Equal((program.Port, false), (saved.GetProperty("port").GetInt32(), saved.GetProperty("has_secret").GetBoolean()));
            var tested = (await admin.PostAsync("/api/admin/assistant/test", settings)).Ok();
            Assert.Equal((true, Model, 900), (tested.GetProperty("succeeded").GetBoolean(), tested.GetProperty("model").GetString(), tested.GetProperty("input_tokens").GetInt32()));
            var lines = Lines(await admin.PostAsync("/api/assistant/exchanges", Ask("Combien d'étapes ?")));
            Assert.Equal(("ollama", "reply", "Trois étapes."), (lines[0].GetProperty("destination").GetProperty("connector").GetString(),
                lines[^1].GetProperty("type").GetString(), lines[^1].GetProperty("reply").GetProperty("message").GetString()));
            // The tokens are shown, no cost in dollars: the prices known are those of Claude.
            Assert.Equal((900, JsonValueKind.Null), (lines[^1].GetProperty("cost").GetProperty("input_tokens").GetInt32(), lines[^1].GetProperty("cost").GetProperty("estimated_usd").ValueKind));
            // What the program of the machine received: two calls at its port on the loopback, without a key, with the ceiling of the settings.
            Assert.Equal(2, program.Requests.Count);
            foreach (var request in program.Requests)
            {
                Assert.Equal(("POST /v1/chat/completions HTTP/1.1", $"127.0.0.1:{program.Port}", false),
                    (request.Line, request.Headers["Host"], request.Headers.ContainsKey("Authorization")));
                using var body = JsonDocument.Parse(request.Body);
                Assert.Equal((Model, 4096), (body.RootElement.GetProperty("model").GetString(), body.RootElement.GetProperty("max_tokens").GetInt32()));
            }
            using var asked = JsonDocument.Parse(program.Requests[1].Body);
            Assert.Contains("Combien d'étapes ?", asked.RootElement.GetProperty("messages")[1].GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.Contains("\"connector\":\"ollama\",\"model\":\"llama3.1:8b\",\"succeeded\":true", await LastAsync(admin, "assistant.connection_tested"), StringComparison.Ordinal);
            Assert.Contains("\"succeeded\":true", await LastAsync(admin, "assistant.exchanged"), StringComparison.Ordinal);
        });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_reply_that_is_no_completion_is_an_invalid_reply_of_the_test_and_of_the_exchange_each_audited(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        await using var program = new LocalChatService(new LocalReply(200, """{"choices":[]}"""));
        await host.WithVariantAsync(services => services.AddSingleton<IAssistantConnector>(new OpenAiCompatibleConnector(AssistantConnector.Ollama)), async clientOf =>
        {
            using var admin = clientOf(Logins.Admin);
            var settings = new { connector = "ollama", model = Model, port = program.Port, allowed_classifications = new[] { "internal" } };
            await SetAsync(admin, settings);
            var tested = (await admin.PostAsync("/api/admin/assistant/test", settings)).Ok();
            Assert.Equal((false, "invalid_reply"), (tested.GetProperty("succeeded").GetBoolean(), tested.GetProperty("failure").GetString()));
            Assert.Contains("\"connector\":\"ollama\",\"model\":\"llama3.1:8b\",\"succeeded\":false,\"failure\":\"invalid_reply\"", await LastAsync(admin, "assistant.connection_tested"),
                StringComparison.Ordinal);
            var lines = Lines(await admin.PostAsync("/api/assistant/exchanges", Ask("Combien d'étapes ?")));
            Assert.Equal(("failure", "invalid_reply"), (lines[^1].GetProperty("type").GetString(), lines[^1].GetProperty("failure").GetString()));
            // The project left for the program: the exchange is in the audit as any exchange that left (R5-06).
            var audited = await LastAsync(admin, "assistant.exchanged");
            Assert.Contains("\"connector\":\"ollama\"", audited, StringComparison.Ordinal);
            Assert.Contains("\"succeeded\":false,\"failure\":\"invalid_reply\"", audited, StringComparison.Ordinal);
        });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_key_is_given_to_its_provider_only_unprotected_for_the_call_as_a_bearer(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var network = new Recording();
        await host.WithVariantAsync(services => services.AddSingleton<IAssistantConnector>(new OpenAiCompatibleConnector(AssistantConnector.OpenAi, network)), async clientOf =>
        {
            using var admin = clientOf(Logins.Admin);
            await SetAsync(admin, new { connector = "anthropic", secret = OtherKey, allowed_classifications = new[] { "internal" } });
            // The key kept is the one of Anthropic: a test of OpenAI without a key typed finds none, and nothing is sent.
            var missing = (await admin.PostAsync("/api/admin/assistant/test", new { connector = "open_ai", model = "gpt-5.6-sol" })).Ok();
            Assert.Equal((false, "key_missing"), (missing.GetProperty("succeeded").GetBoolean(), missing.GetProperty("failure").GetString()));
            Assert.Empty(network.Requests);
            var saved = await SetAsync(admin, new { connector = "open_ai", model = "gpt-5.6-sol", secret = Key, allowed_classifications = new[] { "internal" } });
            Assert.Equal(("open_ai", "wxyz"), (saved.GetProperty("connector").GetString(), saved.GetProperty("secret_hint").GetString()));
            Assert.True((await admin.PostAsync("/api/admin/assistant/test", new { connector = "open_ai", model = "gpt-5.6-sol" })).Ok().GetProperty("succeeded").GetBoolean());
            var lines = Lines(await admin.PostAsync("/api/assistant/exchanges", Ask("Combien d'étapes ?")));
            Assert.Equal(("reply", "Trois étapes."), (lines[^1].GetProperty("type").GetString(), lines[^1].GetProperty("reply").GetProperty("message").GetString()));
            Assert.Equal([("https://api.openai.com/v1/chat/completions", Key), ("https://api.openai.com/v1/chat/completions", Key)], network.Requests);
        });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_message_of_a_provider_reaches_the_interface_without_the_key_whatever_its_connector_relays(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        await host.WithVariantAsync(services => services.AddSingleton<IAssistantConnector>(new Scripted(AssistantConnector.Mistral)), async clientOf =>
        {
            using var admin = clientOf(Logins.Admin);
            var settings = new { connector = "mistral", model = "mistral-large-latest", secret = Key, allowed_classifications = new[] { "internal" } };
            await SetAsync(admin, settings);
            var tested = await admin.PostAsync("/api/admin/assistant/test", settings);
            Assert.Equal(("refused", "Incorrect API key provided: [...]"), (tested.Ok().GetProperty("failure").GetString(), tested.Ok().GetProperty("detail").GetString()));
            var exchanged = await admin.PostAsync("/api/assistant/exchanges", Ask("Combien d'étapes ?"));
            var failure = Lines(exchanged)[^1];
            Assert.Equal(("failure", "refused", "Incorrect API key provided: [...]"),
                (failure.GetProperty("type").GetString(), failure.GetProperty("failure").GetString(), failure.GetProperty("detail").GetString()));
            foreach (var body in new[] { tested.Body, exchanged.Body })
                Assert.DoesNotContain("sk-test", body, StringComparison.Ordinal);
        });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_fault_of_a_connector_once_the_exchange_left_is_audited_as_any_exchange_that_left(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var faulty = new Scripted(AssistantConnector.Gemini, _ => throw new InvalidOperationException("A fault of the connector of the test."));
        await host.WithVariantAsync(services => services.AddSingleton<IAssistantConnector>(faulty), async clientOf =>
        {
            using var admin = clientOf(Logins.Admin);
            await SetAsync(admin, new { connector = "gemini", model = "gemini-3.6-flash", secret = Key, allowed_classifications = new[] { "internal" } });
            var lines = Lines(await admin.PostAsync("/api/assistant/exchanges", Ask("Combien d'étapes ?")));
            Assert.Equal(("failure", "other"), (lines[^1].GetProperty("type").GetString(), lines[^1].GetProperty("failure").GetString()));
            var audited = await LastAsync(admin, "assistant.exchanged");
            Assert.Contains("\"connector\":\"gemini\"", audited, StringComparison.Ordinal);
            Assert.Contains("\"succeeded\":false,\"failure\":\"other\"", audited, StringComparison.Ordinal);
        });
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_cost_of_an_exchange_with_claude_by_bedrock_is_estimated_for_the_administrator(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using var admin = host.As(Logins.Admin);
        await SetAsync(admin, new { connector = "bedrock", model = "global.anthropic.claude-sonnet-5-5", region = "eu-central-1", allowed_classifications = new[] { "internal" } });
        var lines = Lines(await admin.PostAsync("/api/assistant/exchanges", Ask("Combien d'étapes ?")));
        var cost = lines[^1].GetProperty("cost");
        // The identifier of Bedrock read as claude-sonnet-5-5: 1 000 tokens in at 2 $, 80 000 written to the cache at 2.50 $, 500 out at 10 $, per million.
        Assert.Equal(("global.anthropic.claude-sonnet-5-5", JsonValueKind.Number), (cost.GetProperty("model").GetString(), cost.GetProperty("estimated_usd").ValueKind));
        Assert.Equal(0.207m, cost.GetProperty("estimated_usd").GetDecimal());
    }
}
