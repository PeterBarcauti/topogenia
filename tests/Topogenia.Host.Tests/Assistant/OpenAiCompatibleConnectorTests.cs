// OpenAiCompatibleConnectorTests.cs
// The connectors of the Chat Completions API on HTTP of the tests (ADR 098): the address and the key of each provider, the body with the schema of the reply or the schema told, the files, the reply read with its tokens and its bound, the refusals, the errors of a model of the machine that are not JSON, the cut replies, the replies that are no completion and the failures as codes, never with the key; the table of the providers.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Assistant;

namespace Topogenia.Host.Tests.Assistant;

public sealed class OpenAiCompatibleConnectorTests
{
    private const string Key = "sk-test-chat-0123";

    private const string Envelope = """{"form":"answer","message":"OK","cited":[],"questions":[],"findings":[],"pmap":null}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A handler that answers each request with the next of its replies (the last one again), and keeps the requests.</summary>
    private sealed class Answering(params (HttpStatusCode Status, string Body)[] replies) : HttpMessageHandler
    {
        public List<(Uri Address, string? Bearer, string? ApiKey, JsonNode Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var text = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            var apiKey = request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null;
            Requests.Add((request.RequestUri!, request.Headers.Authorization?.Parameter, apiKey, JsonNode.Parse(text)!));
            var (status, body) = replies[Math.Min(Requests.Count - 1, replies.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Failing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No such host is known.");
    }

    private sealed class Progress : IProgress<AssistantProgress>
    {
        public List<AssistantProgress> Reports { get; } = [];

        public void Report(AssistantProgress value) => Reports.Add(value);
    }

    /// <summary>A reply of the API: the text of its first choice, why it stopped, its tokens (800 of 1 000 read from the cache).</summary>
    private static string Completion(string? content, string stop = "stop", string? refusal = null) => JsonSerializer.Serialize(new
    {
        id = "chatcmpl-1", model = "gpt-5.6-sol",
        choices = new[] { new { index = 0, message = new { role = "assistant", content, refusal }, finish_reason = stop } },
        usage = new { prompt_tokens = 1000, completion_tokens = 50, prompt_tokens_details = new { cached_tokens = 800 } },
    });

    private static AssistantPrompt Prompt(IReadOnlyList<AssistantTurn>? history = null) =>
        new(AssistantIntent.Ask, AssistantScope.Map, [AssistantForm.Answer], "=== project.json ===\n{}", AssistantSchemaFormat.Package, history ?? [], "Facturation",
            """{"format":"topogenia"}""", ["FAC-E004"], "Combien d'étapes ?", 16000);

    private static AssistantConnection Connection(AssistantConnector kind, string? key = Key, string model = "gpt-5.6-sol", string? resource = null, int? port = null,
        int ceiling = AssistantSettings.DefaultMaxOutputTokens) =>
        new(AssistantSettings.Default with { Connector = kind, Model = model, Resource = resource, Port = port, MaxOutputTokens = ceiling }, key);

    private static Task<AssistantExchangeOutcome> ExchangeAsync(AssistantConnector kind, HttpMessageHandler handler, AssistantConnection? connection = null,
        AssistantPrompt? prompt = null, Progress? progress = null) =>
        new OpenAiCompatibleConnector(kind, handler).ExchangeAsync(connection ?? Connection(kind), prompt ?? Prompt(), progress ?? new Progress(), Token);

    [Fact]
    public async Task OpenAi_is_called_with_its_key_the_instructions_the_message_and_the_schema_of_the_reply_and_its_reply_read_with_its_tokens()
    {
        var handler = new Answering((HttpStatusCode.OK, Completion(Envelope)));
        var progress = new Progress();
        var outcome = await ExchangeAsync(AssistantConnector.OpenAi, handler, progress: progress);
        Assert.True(outcome.Succeeded);
        Assert.Equal(("gpt-5.6-sol", Envelope), (outcome.Model, outcome.Text));
        Assert.Equal(new AssistantUsage(200, 800, 0, 50), outcome.Usage);
        var (address, bearer, apiKey, body) = Assert.Single(handler.Requests);
        Assert.Equal(("https://api.openai.com/v1/chat/completions", Key, (string?)null), (address.ToString(), bearer, apiKey));
        Assert.Equal(16000, (int)body["max_completion_tokens"]!);
        Assert.Null(body["max_tokens"]);
        var messages = body["messages"]!.AsArray();
        Assert.Equal(["system", "user"], messages.Select(message => message!["role"]!.ToString()));
        Assert.StartsWith(AssistantInstructions.System, messages[0]!["content"]!.ToString(), StringComparison.Ordinal);
        Assert.Contains("=== project.json ===", messages[0]!["content"]!.ToString(), StringComparison.Ordinal);
        Assert.Contains("Combien d'étapes ?", messages[1]!["content"]!.ToString(), StringComparison.Ordinal);
        var format = body["response_format"]!;
        Assert.Equal(("json_schema", "reply", true), (format["type"]!.ToString(), format["json_schema"]!["name"]!.ToString(), (bool)format["json_schema"]!["strict"]!));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ReplySchema.For(Prompt())), format["json_schema"]!["schema"]));
        Assert.Equal([AssistantPhase.Waiting, AssistantPhase.Writing], progress.Reports.Select(report => report.Phase));
    }

    [Fact]
    public async Task DeepSeek_has_the_schema_told_in_its_instructions_and_a_json_object_asked()
    {
        var handler = new Answering((HttpStatusCode.OK, Completion(Envelope)));
        await ExchangeAsync(AssistantConnector.DeepSeek, handler, Connection(AssistantConnector.DeepSeek, model: "deepseek-chat"));
        var (address, _, _, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://api.deepseek.com/v1/chat/completions", address.ToString());
        Assert.Equal("json_object", body["response_format"]!["type"]!.ToString());
        Assert.Equal(16000, (int)body["max_tokens"]!);
        var system = body["messages"]![0]!["content"]!.ToString();
        Assert.Contains("# The schema of your reply", system, StringComparison.Ordinal);
        Assert.Contains(ReplySchema.For(Prompt()), system, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_schema_sent_and_the_schema_told_are_those_of_the_prompt_of_each_exchange_with_the_fields_of_its_forms_alone()
    {
        // One connector, two exchanges of different forms: the schema of the first is not kept for the second.
        var report = Prompt() with { Intent = AssistantIntent.Report, Forms = [AssistantForm.Report] };
        foreach (var kind in new[] { AssistantConnector.OpenAi, AssistantConnector.DeepSeek })
        {
            var handler = new Answering((HttpStatusCode.OK, Completion(Envelope)));
            var connector = new OpenAiCompatibleConnector(kind, handler);
            await connector.ExchangeAsync(Connection(kind), report, new Progress(), Token);
            await connector.ExchangeAsync(Connection(kind), Prompt(), new Progress(), Token);
            Assert.Equal(2, handler.Requests.Count);
            foreach (var (prompt, fields, body) in new[] { (report, "form,message,cited,report", handler.Requests[0].Body), (Prompt(), "form,message,cited", handler.Requests[1].Body) })
            {
                var expected = JsonNode.Parse(ReplySchema.For(prompt))!;
                Assert.Equal(fields.Split(','), expected["properties"]!.AsObject().Select(property => property.Key));
                if (kind == AssistantConnector.OpenAi)
                    Assert.True(JsonNode.DeepEquals(expected, body["response_format"]!["json_schema"]!["schema"]));
                else
                    Assert.EndsWith("nothing before or after it:\n\n" + ReplySchema.For(prompt), body["messages"]![0]!["content"]!.ToString(), StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData(AssistantConnector.Gemini, "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "json_schema")]
    [InlineData(AssistantConnector.Grok, "https://api.x.ai/v1/chat/completions", "json_schema")]
    [InlineData(AssistantConnector.Mistral, "https://api.mistral.ai/v1/chat/completions", "json_schema")]
    [InlineData(AssistantConnector.Kimi, "https://api.moonshot.ai/v1/chat/completions", "json_schema")]
    [InlineData(AssistantConnector.Qwen, "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions", "json_object")]
    public async Task Each_maker_is_called_at_its_address_with_its_key_as_a_bearer_and_its_form_of_structured_output(AssistantConnector kind, string expected, string format)
    {
        var handler = new Answering((HttpStatusCode.OK, Completion(Envelope)));
        await ExchangeAsync(kind, handler);
        var (address, bearer, apiKey, body) = Assert.Single(handler.Requests);
        Assert.Equal((expected, Key, (string?)null), (address.ToString(), bearer, apiKey));
        // Qwen writes a JSON object for nearly all its models, the schema for a part of them only: the schema is told, as for DeepSeek (R5-12).
        Assert.Equal(format, body["response_format"]!["type"]!.ToString());
        Assert.Equal(format == "json_object", body["messages"]![0]!["content"]!.ToString().Contains("# The schema of your reply\n\nReply with one JSON object", StringComparison.Ordinal));
        Assert.Equal(format == "json_schema", body["response_format"]!["json_schema"] is not null);
    }

    /// <summary>
    /// The name of the ceiling of a reply, by provider, written once in the table: OpenAI and Azure OpenAI refuse
    /// `max_tokens` for their recent models and take `max_completion_tokens`; every other provider takes `max_tokens`
    /// (R5-03). The same name for an exchange and for a test of the connection.
    /// </summary>
    [Theory]
    [InlineData(AssistantConnector.OpenAi, "max_completion_tokens", "max_tokens")]
    [InlineData(AssistantConnector.AzureOpenAi, "max_completion_tokens", "max_tokens")]
    [InlineData(AssistantConnector.Gemini, "max_tokens", "max_completion_tokens")]
    [InlineData(AssistantConnector.Grok, "max_tokens", "max_completion_tokens")]
    [InlineData(AssistantConnector.Mistral, "max_tokens", "max_completion_tokens")]
    [InlineData(AssistantConnector.DeepSeek, "max_tokens", "max_completion_tokens")]
    [InlineData(AssistantConnector.Kimi, "max_tokens", "max_completion_tokens")]
    [InlineData(AssistantConnector.Qwen, "max_tokens", "max_completion_tokens")]
    [InlineData(AssistantConnector.Ollama, "max_tokens", "max_completion_tokens")]
    [InlineData(AssistantConnector.LmStudio, "max_tokens", "max_completion_tokens")]
    public async Task The_ceiling_of_a_reply_is_sent_under_the_name_its_provider_takes_and_under_no_other(AssistantConnector kind, string name, string other)
    {
        var handler = new Answering((HttpStatusCode.OK, Completion(Envelope)));
        var connection = Connection(kind, key: AssistantConnectors.TakesKey(kind) ? Key : null, resource: kind == AssistantConnector.AzureOpenAi ? "contoso" : null, ceiling: 12000);
        await ExchangeAsync(kind, handler, connection, Prompt() with { MaxOutputTokens = 12000 });
        await new OpenAiCompatibleConnector(kind, handler).TestAsync(connection, Token);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal((12000, (JsonNode?)null), ((int)request.Body[name]!, request.Body[other])));
        Assert.Equal(name, AssistantProviders.Of(kind).CeilingParameter);
    }

    [Fact]
    public async Task Azure_is_called_at_the_resource_of_the_settings_with_its_key_in_the_header_of_azure()
    {
        var handler = new Answering((HttpStatusCode.OK, Completion(Envelope)));
        await ExchangeAsync(AssistantConnector.AzureOpenAi, handler, Connection(AssistantConnector.AzureOpenAi, model: "processus-gpt", resource: "contoso"));
        var (address, bearer, apiKey, body) = Assert.Single(handler.Requests);
        Assert.Equal(("https://contoso.openai.azure.com/openai/v1/chat/completions", (string?)null, Key), (address.ToString(), bearer, apiKey));
        Assert.Equal("processus-gpt", body["model"]!.ToString());
        Assert.Equal(16000, (int)body["max_completion_tokens"]!);
    }

    [Fact]
    public async Task A_model_of_this_machine_is_called_at_the_loopback_address_at_the_port_of_its_program_or_of_the_settings_without_a_key()
    {
        var handler = new Answering((HttpStatusCode.OK, Completion(Envelope)));
        await ExchangeAsync(AssistantConnector.Ollama, handler, Connection(AssistantConnector.Ollama, key: null, model: "llama3.1:8b"));
        await ExchangeAsync(AssistantConnector.LmStudio, handler, Connection(AssistantConnector.LmStudio, key: null, model: "qwen3", port: 1300));
        // The literal address, never the name `localhost`, which the machine may resolve to another listener (R5-08).
        Assert.Equal(["http://127.0.0.1:11434/v1/chat/completions", "http://127.0.0.1:1300/v1/chat/completions"], handler.Requests.Select(request => request.Address.ToString()));
        Assert.All(handler.Requests, request => Assert.Equal(((string?)null, (string?)null), (request.Bearer, request.ApiKey)));
    }

    [Fact]
    public async Task The_files_go_before_the_text_of_their_turn_an_image_as_an_image_a_pdf_as_a_file_a_document_as_its_text_and_the_turns_before_the_message()
    {
        var image = new AssistantAttachment("schema.png", AttachmentKind.Image, "image/png", 3, [1, 2, 3], null);
        var pdf = new AssistantAttachment("note.pdf", AttachmentKind.Pdf, "application/pdf", 2, [4, 5], null);
        var word = new AssistantAttachment("mode.docx", AttachmentKind.Word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 900, null, "Le mode opératoire.");
        var history = new[] { new AssistantTurn("Et avant ?", Envelope) { Attachments = [word] } };
        var handler = new Answering((HttpStatusCode.OK, Completion(Envelope)));
        await ExchangeAsync(AssistantConnector.OpenAi, handler, prompt: Prompt(history) with { Attachments = [image, pdf] });
        var messages = Assert.Single(handler.Requests).Body["messages"]!.AsArray();
        Assert.Equal(["system", "user", "assistant", "user"], messages.Select(message => message!["role"]!.ToString()));
        Assert.Equal(Envelope, messages[2]!["content"]!.ToString());
        var turn = messages[1]!["content"]!.AsArray();
        Assert.Equal(["text", "text"], turn.Select(part => part!["type"]!.ToString()));
        Assert.Contains("Le mode opératoire.", turn[0]!["text"]!.ToString(), StringComparison.Ordinal);
        var parts = messages[3]!["content"]!.AsArray();
        Assert.Equal(["text", "image_url", "text", "file", "text"], parts.Select(part => part!["type"]!.ToString()));
        Assert.Equal("data:image/png;base64,AQID", parts[1]!["image_url"]!["url"]!.ToString());
        Assert.Equal(("note.pdf", "data:application/pdf;base64,BAU="), (parts[3]!["file"]!["filename"]!.ToString(), parts[3]!["file"]!["file_data"]!.ToString()));
        Assert.Contains("Combien d'étapes ?", parts[4]!["text"]!.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("length", null, AssistantFailures.Truncated)]
    [InlineData("content_filter", null, AssistantFailures.Declined)]
    [InlineData("stop", "I cannot help with that.", AssistantFailures.Declined)]
    public async Task A_reply_cut_by_its_ceiling_filtered_or_refused_is_a_failure_with_its_tokens(string stop, string? refusal, string failure)
    {
        var outcome = await ExchangeAsync(AssistantConnector.OpenAi, new Answering((HttpStatusCode.OK, Completion(refusal is null ? "{\"form\"" : null, stop, refusal))));
        Assert.Equal((false, failure, 50), (outcome.Succeeded, outcome.Failure, outcome.Usage.OutputTokens));
        Assert.Equal(refusal, outcome.Detail);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"message":"Incorrect API key provided."}}""", AssistantFailures.Refused)]
    [InlineData(HttpStatusCode.Forbidden, """{"error":{"message":"Forbidden"}}""", AssistantFailures.Refused)]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"message":"The model `gpt-9` does not exist."}}""", AssistantFailures.ModelUnknown)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"message":"Invalid model: gpt-9"}}""", AssistantFailures.ModelUnknown)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"message":"Model Not Exist","type":"invalid_request_error"}}""", AssistantFailures.ModelUnknown)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"message":"The model `gpt-9.5-sol` does not exist or you do not have access to it.","code":"model_not_found"}}""", AssistantFailures.ModelUnknown)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"message":"Not found the model kimi-x or Permission denied"}}""", AssistantFailures.ModelUnknown)]
    [InlineData(HttpStatusCode.PaymentRequired, """{"error":{"message":"Insufficient Balance"}}""", AssistantFailures.Limited)]
    [InlineData(HttpStatusCode.BadRequest, """[{"error":{"message":"Your credit balance is too low."}}]""", AssistantFailures.Limited)]
    [InlineData(HttpStatusCode.BadRequest, "not json", AssistantFailures.Other)]
    public async Task A_refusal_of_the_provider_is_a_code_with_its_message(HttpStatusCode status, string body, string failure)
    {
        var outcome = await new OpenAiCompatibleConnector(AssistantConnector.OpenAi, new Answering((status, body))).TestAsync(Connection(AssistantConnector.OpenAi), Token);
        Assert.Equal((false, failure), (outcome.Succeeded, outcome.Failure));
        Assert.NotNull(outcome.Detail);
    }

    /// <summary>The key of the probes below: one that is none.</summary>
    private const string ProbeKey = "sk-test-invalid-0000";

    /// <summary>
    /// What the seven addresses of the table answered on 12/11/2026 to a key that is none (the bodies as they were
    /// reported, cut where the report cut them): a key refused is said refused whatever the status and the form of
    /// the body (two answer 400, one a list, one an error that is a text, one a detail alone), and the end of the key
    /// that two of them repeat is not relayed (R5-13).
    /// </summary>
    [Theory]
    [InlineData(AssistantConnector.OpenAi, 401, """{"error":{"message":"Incorrect API key provided: sk-test-********0000. …"}}""")]
    [InlineData(AssistantConnector.Gemini, 400, """[{"error":{"code":400,"message":"Please pass a valid API key","status":"INVALID_ARGUMENT"}}]""")]
    [InlineData(AssistantConnector.Grok, 400, """{"code":"invalid-argument","error":"Incorrect API key provided. You can obtain an API key from https://console.x.ai."}""")]
    [InlineData(AssistantConnector.Mistral, 401, """{"detail":"Invalid API Key"}""")]
    [InlineData(AssistantConnector.DeepSeek, 401, """{"error":{"message":"Authentication Fails, Your api key: ****0000 is invalid (request_id: …)","type":"authentication_error"}}""")]
    [InlineData(AssistantConnector.Kimi, 401, """{"error":{"message":"Invalid Authentication","type":"invalid_authentication_error"}}""")]
    [InlineData(AssistantConnector.Qwen, 401, """{"error":{"message":"Incorrect API key provided. For details, see: …"}}""")]
    [InlineData(AssistantConnector.Kimi, 400, """{"error":{"message":"Invalid Authentication","type":"invalid_authentication_error"}}""")]
    public async Task A_key_refused_is_said_refused_by_each_maker_whatever_the_status_and_the_form_of_its_answer(AssistantConnector kind, int status, string body)
    {
        var connector = new OpenAiCompatibleConnector(kind, new Answering(((HttpStatusCode)status, body)));
        var tested = await connector.TestAsync(Connection(kind, key: ProbeKey), Token);
        var exchanged = await connector.ExchangeAsync(Connection(kind, key: ProbeKey), Prompt(), new Progress(), Token);
        Assert.Equal((AssistantFailures.Refused, AssistantFailures.Refused), (tested.Failure, exchanged.Failure));
        Assert.False(string.IsNullOrWhiteSpace(tested.Detail));
        Assert.Equal(tested.Detail, exchanged.Detail);
        foreach (var part in new[] { "0000", "sk-test" })
            Assert.DoesNotContain(part, tested.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The body of a refusal is read whatever its form, never with an exception: an object whose error is an object,
    /// a text, a list or nothing, a list, a detail, a text that is no JSON, nothing at all; what it says is relayed
    /// when it says something.
    /// </summary>
    [Theory]
    [InlineData("""{"error":{"message":"No key."}}""", "No key.")]
    [InlineData("""{"error":"No key."}""", "No key.")]
    [InlineData("""[{"error":{"message":"No key."}}]""", "No key.")]
    [InlineData("""{"detail":"No key."}""", "No key.")]
    [InlineData("""{"message":"No key."}""", "No key.")]
    [InlineData("""{"detail":[{"loc":["header"],"msg":"No key."}]}""", """[{"loc":["header"],"msg":"No key."}]""")]
    [InlineData("""{"error":{"message":{"reason":"No key."}}}""", """{"reason":"No key."}""")]
    [InlineData("""{"error":1,"error":{"message":"Named twice."}}""", "Named twice.")]
    [InlineData("Unauthorized", "Unauthorized")]
    [InlineData("<html><body>401</body></html>", "<html><body>401</body></html>")]
    [InlineData("""[{"reason":"no error in the list"}]""", null)]
    [InlineData("""{"error":null}""", null)]
    [InlineData("""{"error":{}}""", null)]
    [InlineData("[]", null)]
    [InlineData("[null]", null)]
    [InlineData("[[]]", null)]
    [InlineData("{}", null)]
    [InlineData("null", null)]
    [InlineData("12", null)]
    [InlineData("\"Unauthorized\"", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public async Task The_body_of_a_refusal_is_read_whatever_its_form_never_with_an_exception(string body, string? detail)
    {
        var connector = new OpenAiCompatibleConnector(AssistantConnector.Mistral, new Answering((HttpStatusCode.Unauthorized, body)));
        var tested = await connector.TestAsync(Connection(AssistantConnector.Mistral), Token);
        var exchanged = await connector.ExchangeAsync(Connection(AssistantConnector.Mistral), Prompt(), new Progress(), Token);
        Assert.Equal((AssistantFailures.Refused, detail), (tested.Failure, tested.Detail));
        Assert.Equal((AssistantFailures.Refused, detail), (exchanged.Failure, exchanged.Detail));
        Assert.Equal(detail, OpenAiCompatibleConnector.ErrorOf(body));
    }

    /// <summary>
    /// Another program may hold the port of a model of the machine (SEC-MAJ-01): a body that is not JSON, its page of
    /// error, is never relayed, a fixed sentence is; what Ollama or LM Studio say in JSON still is, and a provider online
    /// keeps its page relayed (above).
    /// </summary>
    [Theory]
    [InlineData(AssistantConnector.Ollama, "<html><body><h1>404 Not Found</h1><p>Visites du musee</p></body></html>", null)]
    [InlineData(AssistantConnector.LmStudio, "<html><body><h1>404 Not Found</h1><p>Visites du musee</p></body></html>", null)]
    [InlineData(AssistantConnector.LmStudio, "Cannot POST /v1/chat/completions", null)]
    [InlineData(AssistantConnector.Ollama, """{"error":"model \"llama9\" not found, try pulling it first"}""", "model \"llama9\" not found, try pulling it first")]
    public async Task A_model_of_this_machine_never_relays_a_body_that_is_not_json(AssistantConnector kind, string body, string? relayed)
    {
        var connector = new OpenAiCompatibleConnector(kind, new Answering((HttpStatusCode.NotFound, body)));
        var tested = await connector.TestAsync(Connection(kind, key: null, port: 8080), Token);
        var exchanged = await connector.ExchangeAsync(Connection(kind, key: null, port: 8080), Prompt(), new Progress(), Token);
        Assert.Equal(relayed ?? OpenAiCompatibleConnector.NotJsonOnThisMachine, tested.Detail);
        Assert.Equal(tested.Detail, exchanged.Detail);
        Assert.DoesNotContain("musee", exchanged.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A request refused (400, 422) for the ceiling of the reply has its own code, on messages of the kind the providers
    /// write; the word `model` alone in a message does not make a model unknown (R5-03).
    /// </summary>
    [Theory]
    [InlineData(400, "max_tokens is too large: 64000. This model supports at most 16384 completion tokens, whereas you provided 64000.", ProviderFailures.CeilingRefused)]
    [InlineData(400, "Invalid max_tokens value, the valid range of max_tokens is [1, 8192]", ProviderFailures.CeilingRefused)]
    [InlineData(400, "The maximum tokens you requested exceeds the model limit of 8192. Try again with a maximum tokens value that is lower than 8192.", ProviderFailures.CeilingRefused)]
    [InlineData(400, "This model's maximum context length is 131072 tokens. However, you requested 140000 tokens (76000 in the messages, 64000 in the completion). Please reduce the length of the messages or completion.", ProviderFailures.CeilingRefused)]
    [InlineData(422, "Input should be less than or equal to 32768 (body.max_tokens)", ProviderFailures.CeilingRefused)]
    [InlineData(400, "This model's maximum context length is 131072 tokens. However, your messages resulted in 150000 tokens. Please reduce the length of the messages.", AssistantFailures.Other)]
    [InlineData(400, "A response_format of type json_schema is not supported by this model.", AssistantFailures.Other)]
    [InlineData(400, "The model produced invalid content. Consider modifying your prompt.", AssistantFailures.Other)]
    public async Task A_ceiling_refused_has_its_own_code_and_the_word_model_alone_does_not_make_a_model_unknown(int status, string message, string failure)
    {
        var body = JsonSerializer.Serialize(new { error = new { message, type = "invalid_request_error" } });
        var outcome = await ExchangeAsync(AssistantConnector.DeepSeek, new Answering(((HttpStatusCode)status, body)));
        Assert.Equal((false, failure, message.Length <= 300 ? message : message[..300]), (outcome.Succeeded, outcome.Failure, outcome.Detail));
    }

    [Fact]
    public async Task A_refusal_of_the_name_of_the_ceiling_is_another_failure_with_its_message_never_a_model_unknown()
    {
        // What OpenAI and Azure OpenAI answer to `max_tokens` for their recent models: the table gives them the other name.
        const string Unsupported = """{"error":{"message":"Unsupported parameter: 'max_tokens' is not supported with this model. Use 'max_completion_tokens' instead.","type":"invalid_request_error","param":"max_tokens","code":"unsupported_parameter"}}""";
        var handler = new Answering((HttpStatusCode.BadRequest, Unsupported), (HttpStatusCode.OK, Completion(Envelope)));
        var outcome = await ExchangeAsync(AssistantConnector.Grok, handler);
        Assert.Equal((false, AssistantFailures.Other), (outcome.Succeeded, outcome.Failure));
        Assert.StartsWith("Unsupported parameter: 'max_tokens' is not supported with this model.", outcome.Detail, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    /// <summary>
    /// The message of a provider is relayed without the key, whole or in part (the start and the end some providers
    /// repeat around a mask), wherever it stands in a message cut at its bound (R5-13).
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"message":"Incorrect API key provided: """ + Key + """. You can find your API key in the settings."}}""")]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"message":"Incorrect API key provided: sk-test-****0123. You can find your API key in the settings."}}""")]
    [InlineData(HttpStatusCode.Forbidden, "Incorrect API key provided: Bearer " + Key)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"Incorrect API key provided: (""" + Key + """)"}""")]
    public async Task The_message_of_a_provider_is_relayed_without_the_key_nor_a_part_of_it(HttpStatusCode status, string body)
    {
        var connector = new OpenAiCompatibleConnector(AssistantConnector.OpenAi, new Answering((status, body)));
        var tested = await connector.TestAsync(Connection(AssistantConnector.OpenAi), Token);
        var exchanged = await connector.ExchangeAsync(Connection(AssistantConnector.OpenAi), Prompt(), new Progress(), Token);
        foreach (var detail in new[] { tested.Detail, exchanged.Detail })
        {
            Assert.StartsWith("Incorrect API key provided: ", detail, StringComparison.Ordinal);
            Assert.True(detail!.Length <= 300);
            foreach (var part in new[] { Key, "sk-test", "chat-0", "-0123" })
                Assert.DoesNotContain(part, detail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_key_astride_the_bound_of_a_message_leaves_no_part_of_it()
    {
        // The key starts at the character 291 of a message cut at 300: cut first, its start would stay.
        var message = "Incorrect API key provided: " + new string('x', 262) + " " + Key + " and more.";
        var body = JsonSerializer.Serialize(new { error = new { message } });
        var outcome = await new OpenAiCompatibleConnector(AssistantConnector.OpenAi, new Answering((HttpStatusCode.Unauthorized, body))).TestAsync(Connection(AssistantConnector.OpenAi), Token);
        Assert.Equal((AssistantFailures.Refused, 300), (outcome.Failure, outcome.Detail!.Length));
        Assert.DoesNotContain("sk-", outcome.Detail, StringComparison.Ordinal);
        Assert.EndsWith(" and", outcome.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A reply of status 200 that is no completion is a failure of the connector as the others, never an exception out
    /// of it (R5-06): no choice, a root or a part of another kind, a body that is no JSON.
    /// </summary>
    [Theory]
    [InlineData("""{"choices":[]}""")]
    [InlineData("[]")]
    [InlineData("""[{"choices":[]}]""")]
    [InlineData("""{"choices":"none"}""")]
    [InlineData("""{"choices":[null]}""")]
    [InlineData("""{"choices":[{"message":"texte"}]}""")]
    [InlineData("""{"choices":[{"index":0}]}""")]
    [InlineData("""{"choices":1,"choices":[]}""")]
    [InlineData("null")]
    [InlineData("\"OK\"")]
    [InlineData("")]
    [InlineData("<html><body>It works</body></html>")]
    public async Task A_reply_that_is_no_completion_is_an_invalid_reply_for_an_exchange_and_for_a_test(string body)
    {
        var connector = new OpenAiCompatibleConnector(AssistantConnector.OpenAi, new Answering((HttpStatusCode.OK, body)));
        var exchanged = await connector.ExchangeAsync(Connection(AssistantConnector.OpenAi), Prompt(), new Progress(), Token);
        Assert.Equal((false, AssistantFailures.InvalidReply, (string?)null), (exchanged.Succeeded, exchanged.Failure, exchanged.Text));
        var tested = await connector.TestAsync(Connection(AssistantConnector.OpenAi), Token);
        Assert.Equal((false, AssistantFailures.InvalidReply), (tested.Succeeded, tested.Failure));
        Assert.False(string.IsNullOrEmpty(tested.Detail));
    }

    /// <summary>The tokens are told as they can be read: a count written otherwise than as a whole number never fails the reply.</summary>
    [Theory]
    [InlineData("""{"prompt_tokens":1200.0,"completion_tokens":"many","prompt_tokens_details":{"cached_tokens":200.4}}""", 1000, 200, 0)]
    [InlineData("""{"prompt_tokens":-5,"completion_tokens":1e40,"prompt_tokens_details":[]}""", 0, 0, 1_000_000_000)]
    [InlineData("\"none\"", 0, 0, 0)]
    public async Task The_tokens_of_a_reply_written_otherwise_are_read_as_they_can_be(string usage, int input, int cached, int output)
    {
        var body = $$"""{"model":5,"choices":[{"message":{"content":[{"type":"text","text":"O"},{"type":"text","text":"K"},7]},"finish_reason":null}],"usage":{{usage}}}""";
        var outcome = await ExchangeAsync(AssistantConnector.OpenAi, new Answering((HttpStatusCode.OK, body)));
        Assert.Equal((true, "OK", (string?)null, new AssistantUsage(input, cached, 0, output)), (outcome.Succeeded, outcome.Text, outcome.Model, outcome.Usage));
    }

    /// <summary>A reply of spaces past the bound of a reply, then a completion; it counts what is read of it.</summary>
    private sealed class Padded(long spaces, byte[] tail) : Stream
    {
        private long position;

        public long Served => position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => spaces + tail.Length;

        public override long Position { get => position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, Length - position);
            for (var index = 0; index < read; index++)
                buffer[offset + index] = position + index < spaces ? (byte)' ' : tail[position + index - spaces];
            position += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class Streaming(Stream body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
    }

    [Fact]
    public async Task A_reply_is_read_up_to_its_bound_and_no_further_and_beyond_it_is_an_invalid_reply()
    {
        // 64 bytes for each token of the highest ceiling of the settings: 8 192 000 bytes.
        Assert.Equal(AssistantRules.MaxOutputTokens * 64, OpenAiCompatibleConnector.ReplyBytesMax);
        var completion = Encoding.UTF8.GetBytes(Completion(Envelope));
        var within = new Padded(OpenAiCompatibleConnector.ReplyBytesMax - completion.Length, completion);
        Assert.True((await ExchangeAsync(AssistantConnector.Ollama, new Streaming(within), Connection(AssistantConnector.Ollama, key: null))).Succeeded);
        var beyond = new Padded(OpenAiCompatibleConnector.ReplyBytesMax + (1 << 20), completion);
        var outcome = await ExchangeAsync(AssistantConnector.Ollama, new Streaming(beyond), Connection(AssistantConnector.Ollama, key: null));
        Assert.Equal((false, AssistantFailures.InvalidReply), (outcome.Succeeded, outcome.Failure));
        Assert.InRange(beyond.Served, OpenAiCompatibleConnector.ReplyBytesMax, OpenAiCompatibleConnector.ReplyBytesMax + (1 << 17));
    }

    [Fact]
    public async Task A_reply_that_starts_with_a_mark_of_byte_order_is_read_as_any_other()
    {
        byte[] marked = [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(Completion(Envelope))];
        var outcome = await ExchangeAsync(AssistantConnector.Mistral, new Streaming(new MemoryStream(marked)));
        Assert.Equal((true, Envelope), (outcome.Succeeded, outcome.Text));
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.PermanentRedirect)]
    public async Task A_redirection_is_said_as_a_provider_that_was_not_reached_with_its_status_after_one_request(HttpStatusCode status)
    {
        var handler = new Answering((status, ""), (HttpStatusCode.OK, Completion(Envelope)));
        var outcome = await ExchangeAsync(AssistantConnector.AzureOpenAi, handler, Connection(AssistantConnector.AzureOpenAi, resource: "contoso"));
        Assert.Equal((false, AssistantFailures.Unreachable), (outcome.Succeeded, outcome.Failure));
        Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), outcome.Detail, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task An_overloaded_service_is_tried_again_for_an_exchange_never_for_a_test_and_an_unreachable_one_is_said()
    {
        var busy = new Answering((HttpStatusCode.TooManyRequests, """{"error":{"message":"Rate limit"}}"""), (HttpStatusCode.OK, Completion(Envelope)));
        Assert.True((await ExchangeAsync(AssistantConnector.Mistral, busy)).Succeeded);
        Assert.Equal(2, busy.Requests.Count);
        var once = new Answering((HttpStatusCode.TooManyRequests, """{"error":{"message":"Rate limit"}}"""));
        Assert.Equal(AssistantFailures.Limited, (await new OpenAiCompatibleConnector(AssistantConnector.Mistral, once).TestAsync(Connection(AssistantConnector.Mistral), Token)).Failure);
        Assert.Single(once.Requests);
        Assert.Equal(AssistantFailures.Unreachable, (await ExchangeAsync(AssistantConnector.Ollama, new Failing(), Connection(AssistantConnector.Ollama, key: null))).Failure);
    }

    [Fact]
    public async Task A_test_asks_one_short_question_without_a_format_with_the_ceiling_of_the_settings_and_says_the_model_that_answered()
    {
        var handler = new Answering((HttpStatusCode.OK, Completion("OK")));
        var outcome = await new OpenAiCompatibleConnector(AssistantConnector.Grok, handler).TestAsync(Connection(AssistantConnector.Grok, model: "grok-4.7", ceiling: 48000), Token);
        Assert.Equal((true, "gpt-5.6-sol", 1000, 50), (outcome.Succeeded, outcome.Model, outcome.InputTokens, outcome.OutputTokens));
        var body = Assert.Single(handler.Requests).Body;
        // The ceiling an exchange will send, so that a provider that refuses it says so at the test (R5-03).
        Assert.Equal(("grok-4.7", 48000), (body["model"]!.ToString(), (int)body["max_tokens"]!));
        Assert.Null(body["response_format"]);
        Assert.Equal("Réponds seulement par le mot OK.", body["messages"]![0]!["content"]!.ToString());
        var azure = new Answering((HttpStatusCode.OK, Completion("OK")));
        await new OpenAiCompatibleConnector(AssistantConnector.AzureOpenAi, azure).TestAsync(Connection(AssistantConnector.AzureOpenAi, resource: "contoso", ceiling: 300), Token);
        Assert.Equal(300, (int)Assert.Single(azure.Requests).Body["max_completion_tokens"]!);
    }

    [Fact]
    public async Task A_test_whose_reply_is_cut_by_the_ceiling_succeeds_and_one_filtered_or_declined_is_a_failure_said_as_such()
    {
        // The model answered, and thought past the ceiling before it wrote: the connection works (R5-11).
        var cut = await new OpenAiCompatibleConnector(AssistantConnector.Gemini, new Answering((HttpStatusCode.OK, Completion(null, "length")))).TestAsync(Connection(AssistantConnector.Gemini), Token);
        Assert.Equal((true, "gpt-5.6-sol", (string?)null, 50), (cut.Succeeded, cut.Model, cut.Failure, cut.OutputTokens));
        var filtered = await new OpenAiCompatibleConnector(AssistantConnector.Gemini, new Answering((HttpStatusCode.OK, Completion(null, "content_filter")))).TestAsync(Connection(AssistantConnector.Gemini), Token);
        Assert.Equal((false, AssistantFailures.Declined), (filtered.Succeeded, filtered.Failure));
    }

    [Fact]
    public void The_table_holds_every_connector_of_the_chat_completions_api_once_its_fixed_addresses_in_https()
    {
        var kinds = Enum.GetValues<AssistantConnector>().Where(AssistantConnectors.SpeaksChatCompletions).ToList();
        Assert.Equal(kinds.Order(), AssistantProviders.All.Select(provider => provider.Kind).Order());
        Assert.All(AssistantProviders.All.Where(provider => provider.Address is not null), provider => Assert.StartsWith("https://", provider.Address, StringComparison.Ordinal));
        Assert.Equal([AssistantConnector.AzureOpenAi, AssistantConnector.Ollama, AssistantConnector.LmStudio],
            AssistantProviders.All.Where(provider => provider.Address is null).Select(provider => provider.Kind));
    }
}
