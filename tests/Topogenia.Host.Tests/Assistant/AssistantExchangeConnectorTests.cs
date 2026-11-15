// AssistantExchangeConnectorTests.cs
// The exchanges of the panel by each connector, on HTTP of the tests: the request in the order of the cache with the schema of the reply, the stream of the API of Anthropic read with its progress, Converse of Bedrock, the refusals and the cut replies; the instructions and the documentation embedded (spec 3.0 sections 5, 7, 8).

using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Assistant;
using Topogenia.Host.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantExchangeConnectorTests
{
    private const string Key = "sk-ant-test-stream-0123";

    private const string Envelope = """{"form":"answer","message":"OK","cited":[],"questions":[],"findings":[],"pmap":null}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A handler that answers with the body and the type given, and keeps the requests.</summary>
    private sealed class Answering(string body, string type, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, type) };
        }
    }

    private sealed class Progress : IProgress<AssistantProgress>
    {
        public List<AssistantProgress> Reports { get; } = [];

        public void Report(AssistantProgress value) => Reports.Add(value);
    }

    private sealed class Factory(HttpMessageHandler handler) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(handler, disposeHandler: false);
        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;
        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => true;
    }

    private static AssistantPrompt Prompt(AssistantIntent intent = AssistantIntent.Ask, IReadOnlyList<AssistantTurn>? history = null, bool proposal = true) =>
        new(intent, AssistantScope.Map, proposal ? [AssistantForm.Answer, AssistantForm.Proposal] : [AssistantForm.Answer], "=== project.json ===\n{}",
            AssistantSchemaFormat.Package, history ?? [], "Facturation", """{"format":"topogenia"}""", ["FAC-E004"], "Combien d'étapes ?", 16000);

    /// <summary>A stream of the API of Anthropic: its start, a thought, the text in two parts, its end with the reason given.</summary>
    private static string Stream(string text, string stop = "end_turn", string? category = null)
    {
        var half = text.Length / 2;
        string Event(string name, object data) => $"event: {name}\ndata: {JsonSerializer.Serialize(data)}\n\n";
        return Event("message_start", new
        {
            type = "message_start",
            message = new
            {
                id = "msg_1", type = "message", role = "assistant", model = "claude-opus-5-5", content = Array.Empty<object>(), stop_reason = (string?)null,
                stop_sequence = (string?)null, usage = new { input_tokens = 900, cache_read_input_tokens = 80000, cache_creation_input_tokens = 1200, output_tokens = 1 },
            },
        }) + Event("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "thinking", thinking = "", signature = "" } })
            + Event("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "thinking_delta", thinking = "Je compte les étapes.\nJe réponds." } })
            + Event("content_block_stop", new { type = "content_block_stop", index = 0 })
            + Event("content_block_start", new { type = "content_block_start", index = 1, content_block = new { type = "text", text = "" } })
            + Event("content_block_delta", new { type = "content_block_delta", index = 1, delta = new { type = "text_delta", text = text[..half] } })
            + Event("content_block_delta", new { type = "content_block_delta", index = 1, delta = new { type = "text_delta", text = text[half..] } })
            + Event("content_block_stop", new { type = "content_block_stop", index = 1 })
            + Event("message_delta", new
            {
                type = "message_delta", delta = new { stop_reason = stop, stop_sequence = (string?)null, stop_details = category is null ? null : new { type = "refusal", category, explanation = "no" } },
                usage = new { output_tokens = 42 },
            })
            + Event("message_stop", new { type = "message_stop" });
    }

    private static Task<AssistantExchangeOutcome> AnthropicAsync(HttpMessageHandler handler, AssistantPrompt prompt, IProgress<AssistantProgress> progress) =>
        new AnthropicConnector(new HttpClient(handler)).ExchangeAsync(
            new AssistantConnection(AssistantSettings.Default with { Connector = AssistantConnector.Anthropic, Model = "claude-opus-5-5" }, Key), prompt, progress, Token);

    [Fact]
    public async Task The_api_of_anthropic_is_called_in_a_stream_in_the_order_of_the_cache_with_the_schema_of_the_reply_and_its_progress_told()
    {
        var handler = new Answering(Stream(Envelope), "text/event-stream");
        var progress = new Progress();
        var history = new[] { new AssistantTurn("Bonjour", Envelope) };
        var outcome = await AnthropicAsync(handler, Prompt(history: history), progress);
        Assert.Equal((true, "claude-opus-5-5", Envelope, new AssistantUsage(900, 80000, 1200, 42)), (outcome.Succeeded, outcome.Model, outcome.Text, outcome.Usage));
        Assert.Contains(progress.Reports, report => report.Phase == AssistantPhase.Thinking);
        Assert.Contains(progress.Reports, report => report.Phase == AssistantPhase.Writing);
        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var root = sent.RootElement;
        Assert.True(root.GetProperty("stream").GetBoolean());
        var system = root.GetProperty("system");
        Assert.Equal(2, system.GetArrayLength());
        Assert.StartsWith(AssistantInstructions.System[..40], system[0].GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.All(system.EnumerateArray(), block => Assert.Equal("ephemeral", block.GetProperty("cache_control").GetProperty("type").GetString()));
        var messages = root.GetProperty("messages");
        Assert.Equal(["user", "assistant", "user"], messages.EnumerateArray().Select(message => message.GetProperty("role").GetString()));
        Assert.Equal("ephemeral", messages[1].GetProperty("content")[0].GetProperty("cache_control").GetProperty("type").GetString());
        Assert.Contains("<request>\nCombien d'étapes ?\n</request>", messages[2].GetProperty("content").GetString(), StringComparison.Ordinal);
        Assert.Equal(("adaptive", "summarized"), (root.GetProperty("thinking").GetProperty("type").GetString(), root.GetProperty("thinking").GetProperty("display").GetString()));
        var output = root.GetProperty("output_config");
        Assert.Equal(("medium", "json_schema"), (output.GetProperty("effort").GetString(), output.GetProperty("format").GetProperty("type").GetString()));
        Assert.Equal(16000, root.GetProperty("max_tokens").GetInt32());
    }

    [Theory]
    [InlineData("refusal", "cyber", AssistantFailures.Declined)]
    [InlineData("max_tokens", null, AssistantFailures.Truncated)]
    public async Task A_refusal_of_the_model_and_a_reply_cut_by_its_ceiling_are_failures_with_their_tokens(string stop, string? category, string failure)
    {
        var outcome = await AnthropicAsync(new Answering(Stream(Envelope, stop, category), "text/event-stream"), Prompt(AssistantIntent.Analyze), new Progress());
        Assert.Equal((false, failure, category, 42), (outcome.Succeeded, outcome.Failure, outcome.Detail, outcome.Usage.OutputTokens));
    }

    [Fact]
    public async Task A_refused_key_is_said_without_the_key()
    {
        // The provider repeats the key it refuses: the message is relayed without it (R5-13).
        var outcome = await AnthropicAsync(new Answering("""{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key (""" + Key + """)"}}""",
            "application/json", HttpStatusCode.Unauthorized), Prompt(), new Progress());
        Assert.Equal((false, AssistantFailures.Refused), (outcome.Succeeded, outcome.Failure));
        Assert.Contains("invalid x-api-key", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, outcome.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The client the connector of Anthropic makes when a test gives it none, the one of the shipped code, on real
    /// connections of the loopback (the address of the API is the only thing the test changes): a service that answers
    /// a redirection is called, the place the redirection names receives nothing, neither the request nor the key.
    /// </summary>
    [Fact]
    public async Task A_redirection_answered_to_the_client_of_anthropic_of_the_shipped_code_is_never_followed_for_a_test_nor_for_an_exchange()
    {
        const string Message = """{"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":[{"type":"text","text":"OK"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":12,"output_tokens":4}}""";
        await using var elsewhere = new LocalChatService(new LocalReply(200, Message));
        await using var service = new LocalChatService(new LocalReply(307, "", elsewhere.Address));
        var connector = new AnthropicConnector(address: new Uri($"http://127.0.0.1:{service.Port}"));
        var connection = new AssistantConnection(AssistantSettings.Default with { Connector = AssistantConnector.Anthropic, Model = "claude-opus-5-5" }, Key);
        var tested = await connector.TestAsync(connection, Token);
        var exchanged = await connector.ExchangeAsync(connection, Prompt(), new Progress(), Token);
        Assert.Empty(elsewhere.Requests);
        Assert.NotEmpty(service.Requests);
        Assert.All(service.Requests, request => Assert.StartsWith("POST /v1/messages", request.Line, StringComparison.Ordinal));
        Assert.Equal((false, AssistantFailures.Unreachable, false, AssistantFailures.Unreachable), (tested.Succeeded, tested.Failure, exchanged.Succeeded, exchanged.Failure));
        Assert.Contains("307", tested.Detail, StringComparison.Ordinal);
        Assert.Contains("307", exchanged.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bedrock_is_called_by_converse_with_points_of_cache_the_structured_output_and_the_effort()
    {
        var reply = $$$"""
            {"output":{"message":{"role":"assistant","content":[{"text":{{{JsonSerializer.Serialize(Envelope)}}}}]}},"stopReason":"end_turn",
             "usage":{"inputTokens":900,"outputTokens":42,"totalTokens":942,"cacheReadInputTokens":80000,"cacheWriteInputTokens":0}}
            """;
        var handler = new Answering(reply, "application/json");
        var connector = new BedrockConnector(new Factory(handler), _ => new BasicAWSCredentials("TESTACCESSKEY", "test-secret-key"));
        var settings = AssistantSettings.Default with { Connector = AssistantConnector.Bedrock, Model = "global.anthropic.claude-sonnet-5-5", Region = "eu-central-1", AwsProfile = "profil" };
        var prompt = Prompt(AssistantIntent.Create);
        var outcome = await connector.ExchangeAsync(new AssistantConnection(settings, null), prompt, new Progress(), Token);
        Assert.Equal((true, Envelope, new AssistantUsage(900, 80000, 0, 42)), (outcome.Succeeded, outcome.Text, outcome.Usage));
        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var root = sent.RootElement;
        Assert.Equal(4, root.GetProperty("system").GetArrayLength());
        Assert.Equal("default", root.GetProperty("system")[1].GetProperty("cachePoint").GetProperty("type").GetString());
        Assert.Equal(("high", "json_schema"), (root.GetProperty("outputConfig").GetProperty("effort").GetString(),
            root.GetProperty("outputConfig").GetProperty("textFormat").GetProperty("type").GetString()));
        Assert.Equal(ReplySchema.For(prompt), root.GetProperty("outputConfig").GetProperty("textFormat").GetProperty("structure").GetProperty("jsonSchema").GetProperty("schema").GetString());
    }

    /// <summary>The prompt of a practice with the forms the application allows it, and a comment of presentation to translate when asked.</summary>
    private static AssistantPrompt PromptOf(AssistantIntent intent, bool comments) => Prompt(intent) with
    {
        Forms = intent switch
        {
            AssistantIntent.Report => [AssistantForm.Report],
            AssistantIntent.Summaries => [AssistantForm.Summaries],
            AssistantIntent.Translate => [AssistantForm.Proposal, AssistantForm.Answer],
            _ => [AssistantForm.Answer, AssistantForm.Questions, AssistantForm.Findings],
        },
        Comments = comments ? [new AssistantCommentTarget(Guid.Empty, "FAC-E004", "La facture est contrôlée.")] : [],
    };

    private static string[] Fields(JsonElement schema) => schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();

    [Theory]
    [InlineData(AssistantIntent.Report, false, "form,message,cited,report")]
    [InlineData(AssistantIntent.Summaries, false, "form,message,cited,summaries")]
    [InlineData(AssistantIntent.Translate, true, "form,message,cited,pmap,comments")]
    [InlineData(AssistantIntent.Translate, false, "form,message,cited,pmap")]
    [InlineData(AssistantIntent.Analyze, false, "form,message,cited,questions,findings")]
    public async Task The_api_of_anthropic_bedrock_and_the_prompt_copied_give_the_schema_of_the_exchange_with_the_fields_of_its_forms_alone(AssistantIntent intent, bool comments,
        string fields)
    {
        var prompt = PromptOf(intent, comments);
        using var expected = JsonDocument.Parse(ReplySchema.For(prompt));
        Assert.Equal(fields.Split(','), Fields(expected.RootElement));

        var anthropic = new Answering(Stream(Envelope), "text/event-stream");
        await AnthropicAsync(anthropic, prompt, new Progress());
        using var first = JsonDocument.Parse(Assert.Single(anthropic.Bodies));
        var schema = first.RootElement.GetProperty("output_config").GetProperty("format").GetProperty("schema");
        Assert.Equal(fields.Split(','), Fields(schema));
        Assert.True(JsonElement.DeepEquals(expected.RootElement, schema));

        var reply = $$$"""{"output":{"message":{"role":"assistant","content":[{"text":{{{JsonSerializer.Serialize(Envelope)}}}}]}},"stopReason":"end_turn","usage":{"inputTokens":1,"outputTokens":1,"totalTokens":2}}""";
        var bedrock = new Answering(reply, "application/json");
        var connector = new BedrockConnector(new Factory(bedrock), _ => new BasicAWSCredentials("TESTACCESSKEY", "test-secret-key"));
        var settings = AssistantSettings.Default with { Connector = AssistantConnector.Bedrock, Model = "global.anthropic.claude-sonnet-5-5", Region = "eu-central-1", AwsProfile = "profil" };
        await connector.ExchangeAsync(new AssistantConnection(settings, null), prompt, new Progress(), Token);
        using var second = JsonDocument.Parse(Assert.Single(bedrock.Bodies));
        var structure = second.RootElement.GetProperty("outputConfig").GetProperty("textFormat").GetProperty("structure");
        Assert.Equal(ReplySchema.For(prompt), structure.GetProperty("jsonSchema").GetProperty("schema").GetString());

        Assert.EndsWith("nothing before or after it:\n\n" + ReplySchema.For(prompt), new AssistantInstructions().Render(prompt), StringComparison.Ordinal);
    }

    private static readonly byte[] PdfBytes = "%PDF-1.7 test"u8.ToArray();
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A prompt whose former turn joined a PDF, and whose request joins a document read by the host and an image (lot 4).</summary>
    private static AssistantPrompt PromptWithFiles() => Prompt(AssistantIntent.Create, [new AssistantTurn("Lis ce plan", Envelope)
    {
        Attachments = [new AssistantAttachment("plan \"achats\".pdf", AttachmentKind.Pdf, "application/pdf", PdfBytes.Length, PdfBytes, null)],
    }]) with
    {
        Attachments =
        [
            new AssistantAttachment("procédure.docx", AttachmentKind.Word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 900, null,
                "# Achats\n- Recevoir la demande"),
            new AssistantAttachment("tableau.png", AttachmentKind.Image, "image/png", PngBytes.Length, PngBytes, null),
        ],
    };

    [Fact]
    public async Task The_files_go_first_in_the_message_of_their_turn_a_pdf_as_a_document_an_image_as_an_image_and_those_of_the_request_cached()
    {
        var handler = new Answering(Stream(Envelope), "text/event-stream");
        await AnthropicAsync(handler, PromptWithFiles(), new Progress());

        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var messages = sent.RootElement.GetProperty("messages");
        var former = messages[0].GetProperty("content");
        Assert.Equal(["text", "document", "text"], former.EnumerateArray().Select(block => block.GetProperty("type").GetString()));
        Assert.Equal("<file name=\"plan &quot;achats&quot;.pdf\" kind=\"pdf\" />", former[0].GetProperty("text").GetString());
        Assert.Equal(("base64", "application/pdf", Convert.ToBase64String(PdfBytes), "plan \"achats\".pdf"), (former[1].GetProperty("source").GetProperty("type").GetString(),
            former[1].GetProperty("source").GetProperty("media_type").GetString(), former[1].GetProperty("source").GetProperty("data").GetString(), former[1].GetProperty("title").GetString()));
        Assert.False(former[1].TryGetProperty("cache_control", out _));
        Assert.Equal("<request>\nLis ce plan\n</request>", former[2].GetProperty("text").GetString());

        var current = messages[2].GetProperty("content");
        Assert.Equal(["text", "text", "image", "text"], current.EnumerateArray().Select(block => block.GetProperty("type").GetString()));
        Assert.Equal("<file name=\"procédure.docx\" kind=\"word\">\n# Achats\n- Recevoir la demande\n</file>", current[0].GetProperty("text").GetString());
        Assert.Equal(("image/png", Convert.ToBase64String(PngBytes)), (current[2].GetProperty("source").GetProperty("media_type").GetString(),
            current[2].GetProperty("source").GetProperty("data").GetString()));
        Assert.Equal("ephemeral", current[2].GetProperty("cache_control").GetProperty("type").GetString());
        Assert.False(current[3].TryGetProperty("cache_control", out _));
        Assert.Contains("<request>\nCombien d'étapes ?\n</request>", current[3].GetProperty("text").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bedrock_takes_the_files_as_documents_and_images_of_converse_named_in_the_request_with_a_point_of_cache_after_those_of_the_request()
    {
        var reply = $$$"""{"output":{"message":{"role":"assistant","content":[{"text":{{{JsonSerializer.Serialize(Envelope)}}}}]}},"stopReason":"end_turn","usage":{"inputTokens":1,"outputTokens":1,"totalTokens":2}}""";
        var handler = new Answering(reply, "application/json");
        var connector = new BedrockConnector(new Factory(handler), _ => new BasicAWSCredentials("TESTACCESSKEY", "test-secret-key"));
        var settings = AssistantSettings.Default with { Connector = AssistantConnector.Bedrock, Model = "global.anthropic.claude-sonnet-5-5", Region = "eu-central-1", AwsProfile = "profil" };
        var prompt = PromptWithFiles();
        await connector.ExchangeAsync(new AssistantConnection(settings, null), prompt with { Attachments = [.. prompt.Attachments, prompt.History[0].Attachments[0]] },
            new Progress(), Token);

        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var messages = sent.RootElement.GetProperty("messages");
        var former = messages[0].GetProperty("content");
        Assert.Equal(("pdf", "document-1", Convert.ToBase64String(PdfBytes)), (former[1].GetProperty("document").GetProperty("format").GetString(),
            former[1].GetProperty("document").GetProperty("name").GetString(), former[1].GetProperty("document").GetProperty("source").GetProperty("bytes").GetString()));
        Assert.Equal(3, former.GetArrayLength());
        var current = messages[2].GetProperty("content");
        Assert.Equal(["text", "text", "image", "text", "document", "cachePoint", "text"], current.EnumerateArray().Select(block => block.EnumerateObject().Single().Name));
        Assert.Equal(("png", Convert.ToBase64String(PngBytes)), (current[2].GetProperty("image").GetProperty("format").GetString(),
            current[2].GetProperty("image").GetProperty("source").GetProperty("bytes").GetString()));
        Assert.Equal("document-2", current[4].GetProperty("document").GetProperty("name").GetString());
    }

    [Fact]
    public void The_prompt_copied_holds_the_text_of_a_document_and_names_a_pdf_or_an_image_for_the_person_to_join()
    {
        var text = new AssistantInstructions().Render(PromptWithFiles());

        var former = text.IndexOf("<file name=\"plan &quot;achats&quot;.pdf\" kind=\"pdf\">joined by the person to this message</file>", StringComparison.Ordinal);
        var request = text.IndexOf("<request>\nLis ce plan\n</request>", StringComparison.Ordinal);
        var document = text.IndexOf("<file name=\"procédure.docx\" kind=\"word\">\n# Achats\n- Recevoir la demande\n</file>", StringComparison.Ordinal);
        var image = text.IndexOf("<file name=\"tableau.png\" kind=\"image\">joined by the person to this message</file>", StringComparison.Ordinal);
        var exchange = text.IndexOf("<exchange practice=\"create\"", StringComparison.Ordinal);
        Assert.True(former > 0 && former < request && request < document && document < image && image < exchange, text);
    }

    [Fact]
    public void The_documentation_embedded_is_the_files_of_the_repository_in_their_order()
    {
        var system = AssistantInstructions.System;
        var at = 0;
        foreach (var file in AssistantInstructions.FormatFiles)
        {
            var text = File.ReadAllText(Path.Combine(RepositoryPaths.Root, file)).ReplaceLineEndings("\n");
            var body = string.Join('\n', text.Split('\n').SkipWhile(line => line.StartsWith("<!--", StringComparison.Ordinal))).Trim();
            var found = system.IndexOf(body, StringComparison.Ordinal);
            Assert.True(found > at, $"{file} is embedded after the files before it.");
            at = found;
        }
        Assert.StartsWith("You are the process assistant", system, StringComparison.Ordinal);
        Assert.DoesNotContain("<!--", system, StringComparison.Ordinal);
    }

    [Fact]
    public void The_message_says_the_practice_the_forms_allowed_the_selection_and_a_reader_is_told_no_proposal()
    {
        var message = AssistantInstructions.MessageOf(Prompt(AssistantIntent.Analyze, proposal: false));
        Assert.Contains("practice=\"analyze\"", message, StringComparison.Ordinal);
        Assert.Contains("allowed_forms=\"answer\"", message, StringComparison.Ordinal);
        Assert.Contains("<selection>FAC-E004</selection>", message, StringComparison.Ordinal);
        Assert.Contains("A proposal is not allowed", message, StringComparison.Ordinal);
        Assert.Contains(AssistantInstructions.PracticeOf(AssistantIntent.Analyze)!, message, StringComparison.Ordinal);
        Assert.Null(AssistantInstructions.PracticeOf(AssistantIntent.Free));
        var prompt = Prompt(history: [new AssistantTurn("Avant", Envelope)]);
        var rendered = new AssistantInstructions().Render(prompt);
        Assert.Contains("<your_reply>\n" + Envelope, rendered, StringComparison.Ordinal);
        Assert.EndsWith(ReplySchema.For(prompt), rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void The_prompt_names_the_versions_the_formats_of_the_exchange_write()
    {
        // The map open and the schemas come from the formats of the exchange: the prompt names their versions (audit 4.8).
        Assert.Equal((Interchange.Packages.PackageFormat.Version, Interchange.Pmap.PmapFormat.Version), (AssistantInstructions.PackageVersion, AssistantInstructions.PmapVersion));
        Assert.Contains($"(format .topogenia {Interchange.Packages.PackageFormat.Version})", AssistantInstructions.SchemaOf(Prompt()), StringComparison.Ordinal);
        Assert.Contains($"one .pmap {Interchange.Pmap.PmapFormat.Version} document", AssistantInstructions.SchemaOf(Prompt() with { SchemaFormat = AssistantSchemaFormat.Pmap }),
            StringComparison.Ordinal);
        Assert.Contains($"format=\"pmap {Interchange.Pmap.PmapFormat.Version}\"", AssistantInstructions.MessageOf(Prompt()), StringComparison.Ordinal);
        // The instructions embedded name no other version of the .pmap, and the common one names every form of the reply (audit 3.1 V2, 4.6.1).
        var instructions = Enum.GetValues<AssistantIntent>().Select(AssistantInstructions.PracticeOf).OfType<string>()
            .Prepend(AssistantInstructions.Resource("Instructions.common.md")).ToList();
        var versions = instructions.SelectMany(text => System.Text.RegularExpressions.Regex.Matches(text, @"\.pmap`? (\d+\.\d+)").Select(match => match.Groups[1].Value)).Distinct().ToList();
        Assert.Equal([Interchange.Pmap.PmapFormat.Version], versions);
        Assert.Contains("`summaries`", instructions[0], StringComparison.Ordinal);
    }

    [Fact]
    public void The_schemas_joined_to_the_instructions_are_those_of_the_versions_the_prompt_names()
    {
        // The prompt says the format of the schema of the project: the schema joined is the one of that version, no other (R5-22).
        var package = $"topogenia-package-{Interchange.Packages.PackageFormat.Version}.schema.json";
        var pmap = $"pmap-{Interchange.Pmap.PmapFormat.Version}.schema.json";
        Assert.Equal([$"schemas/{pmap}", $"schemas/{package}"], AssistantInstructions.FormatFiles.Where(file => file.StartsWith("schemas/", StringComparison.Ordinal)));
        Assert.Contains($"\"$id\": \"{package}\"", AssistantInstructions.System, StringComparison.Ordinal);
        Assert.Contains($"\"$id\": \"{pmap}\"", AssistantInstructions.System, StringComparison.Ordinal);
    }

    [Fact]
    public void The_message_says_the_language_of_origin_and_the_languages_configured_that_a_translation_takes()
    {
        Assert.DoesNotContain("<languages", AssistantInstructions.MessageOf(Prompt()), StringComparison.Ordinal);
        var message = AssistantInstructions.MessageOf(Prompt() with { Languages = new AssistantLanguages("fr", ["fr", "en"]) });
        Assert.Contains("<selection>FAC-E004</selection>\n\n<languages origin=\"fr\" configured=\"fr, en\" />\n\n<exchange", message, StringComparison.Ordinal);
        Assert.Contains("<languages configured=\"fr, en\" />", AssistantInstructions.MessageOf(Prompt() with { Languages = new AssistantLanguages(null, ["fr", "en"]) }),
            StringComparison.Ordinal);
        Assert.Contains("**Translating**", AssistantInstructions.System, StringComparison.Ordinal);
    }

    [Fact]
    public void The_report_has_its_practice_its_one_form_and_the_pain_points_of_the_map_never_told_no_proposal()
    {
        var points = new[] { new AssistantPainPointView("FAC-I001", "FAC-E002", "Facture\nressaisie", 6.5m, "reported"), new AssistantPainPointView("FAC-I002", null, "Relances", null, "in_progress") };
        var message = AssistantInstructions.MessageOf(Prompt(AssistantIntent.Report) with { Forms = [AssistantForm.Report], PainPoints = points });
        Assert.Contains("practice=\"report\"", message, StringComparison.Ordinal);
        Assert.Contains("allowed_forms=\"report\"", message, StringComparison.Ordinal);
        Assert.Contains(AssistantInstructions.PracticeOf(AssistantIntent.Report)!, message, StringComparison.Ordinal);
        Assert.DoesNotContain("A proposal is not allowed", message, StringComparison.Ordinal);
        Assert.Contains("</exchange>\n\n<pain_points>\nFAC-I001 | step FAC-E002 | 6.5 h/month | reported | Facture ressaisie\nFAC-I002 | step none | hours unknown | in_progress | Relances\n</pain_points>\n\n<request>", message, StringComparison.Ordinal);
        Assert.Contains("none: the sheets of the map hold no pain point", AssistantInstructions.PainPointsOf([]), StringComparison.Ordinal);
        Assert.DoesNotContain("<pain_points>", AssistantInstructions.MessageOf(Prompt(AssistantIntent.Analyze) with { PainPoints = points }), StringComparison.Ordinal);
    }
}
