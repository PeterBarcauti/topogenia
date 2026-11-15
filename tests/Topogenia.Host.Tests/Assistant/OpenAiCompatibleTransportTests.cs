// OpenAiCompatibleTransportTests.cs
// The calls of the connector of the Chat Completions API as they leave, on the loopback of the machine with the handler of the shipped code: a redirection never followed, a model of this machine never called through the proxy of the system, the settings of the handler of each provider (ADR 098).

using System.Net;
using System.Text.Json;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Assistant;

namespace Topogenia.Host.Tests.Assistant;

public sealed class OpenAiCompatibleTransportTests
{
    private const string Envelope = """{"form":"answer","message":"OK","cited":[],"questions":[],"findings":[],"pmap":null}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Silent : IProgress<AssistantProgress>
    {
        public void Report(AssistantProgress value)
        {
        }
    }

    /// <summary>
    /// A proxy of the system for the calls to one port of the loopback only, any other call left to the proxy that was
    /// there: the tests that run beside this one keep their route. Turned off, it is the former proxy for every call.
    /// </summary>
    private sealed class ProxyOf(int port, Uri proxy, IWebProxy former) : IWebProxy
    {
        private volatile bool on = true;

        public ICredentials? Credentials { get; set; }

        public void Off() => on = false;

        public Uri? GetProxy(Uri destination) => on && destination.Port == port ? proxy : former.GetProxy(destination);

        public bool IsBypassed(Uri host) => !(on && host.Port == port) && former.IsBypassed(host);
    }

    private static LocalReply Completion(string content) => new(200, JsonSerializer.Serialize(new
    {
        model = "llama3.1:8b",
        choices = new[] { new { index = 0, message = new { role = "assistant", content }, finish_reason = "stop" } },
        usage = new { prompt_tokens = 10, completion_tokens = 2 },
    }));

    private static AssistantConnection Local(int port) =>
        new(AssistantSettings.Default with { Connector = AssistantConnector.Ollama, Model = "llama3.1:8b", Port = port }, null);

    private static AssistantPrompt Prompt() =>
        new(AssistantIntent.Ask, AssistantScope.Map, [AssistantForm.Answer], "=== project.json ===\n{}", AssistantSchemaFormat.Package, [], "Facturation",
            """{"format":"topogenia"}""", [], "Combien d'étapes ?", 4096);

    [Fact]
    public async Task A_redirection_is_a_failure_said_as_such_and_nothing_is_sent_where_it_points()
    {
        await using var elsewhere = new LocalChatService(Completion(Envelope));
        await using var program = new LocalChatService(new LocalReply(307, "", elsewhere.Address));
        var outcome = await new OpenAiCompatibleConnector(AssistantConnector.Ollama).ExchangeAsync(Local(program.Port), Prompt(), new Silent(), Token);
        Assert.StartsWith("POST /v1/chat/completions ", Assert.Single(program.Requests).Line, StringComparison.Ordinal);
        // The project was sent once, to the program of the settings: the place the redirection names received nothing.
        Assert.Empty(elsewhere.Requests);
        Assert.Equal((false, AssistantFailures.Unreachable), (outcome.Succeeded, outcome.Failure));
        Assert.Contains("307", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_model_of_this_machine_is_never_called_through_the_proxy_of_the_system()
    {
        await using var proxy = new LocalChatService(Completion("from the proxy"));
        await using var program = new LocalChatService(Completion("OK"));
        var former = HttpClient.DefaultProxy;
        var only = new ProxyOf(program.Port, new Uri($"http://127.0.0.1:{proxy.Port}"), former);
        HttpClient.DefaultProxy = only;
        try
        {
            // The witness: a client with its default settings does call that port through the proxy of the system.
            using (var plain = new HttpClient())
                (await plain.GetAsync(new Uri($"http://127.0.0.1:{program.Port}/"), Token)).Dispose();
            Assert.Equal((1, 0), (proxy.Requests.Count, program.Requests.Count));
            var outcome = await new OpenAiCompatibleConnector(AssistantConnector.Ollama).TestAsync(Local(program.Port), Token);
            Assert.Equal((1, 1), (proxy.Requests.Count, program.Requests.Count));
            Assert.StartsWith("POST /v1/chat/completions ", program.Requests[0].Line, StringComparison.Ordinal);
            Assert.True(outcome.Succeeded);
        }
        finally
        {
            only.Off();
            HttpClient.DefaultProxy = former;
        }
    }

    [Fact]
    public void No_provider_has_its_redirections_followed_and_only_a_provider_on_line_is_called_through_the_proxy_of_the_system()
    {
        foreach (var provider in AssistantProviders.All)
        {
            using var handler = OpenAiCompatibleConnector.HandlerOf(provider.Kind);
            Assert.False(handler.AllowAutoRedirect, provider.Kind.ToString());
            Assert.Equal(!AssistantConnectors.OnThisMachine(provider.Kind), handler.UseProxy);
        }
        Assert.Equal([AssistantConnector.Ollama, AssistantConnector.LmStudio],
            AssistantProviders.All.Select(provider => provider.Kind).Where(kind => !OpenAiCompatibleConnector.HandlerOf(kind).UseProxy));
    }
}
