// AssistantExchangeConnectorTests.cs
// The exchanges of the panel by each connector, on HTTP of the tests: the request in the order of the cache with the schema of the reply, the stream of the API of Anthropic read with its progress, Converse of Bedrock, the refusals and the cut replies; the instructions and the documentation embedded (spec 3.0 sections 5, 7, 8).

using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Assistant;
using Topogenia.Domain.Values;
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
        var outcome = await AnthropicAsync(new Answering("""{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""", "application/json",
            HttpStatusCode.Unauthorized), Prompt(), new Progress());
        Assert.Equal((false, AssistantFailures.Refused), (outcome.Succeeded, outcome.Failure));
        Assert.DoesNotContain(Key, outcome.Detail ?? "", StringComparison.Ordinal);
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
        var settings = AssistantSettings.Default with { Connector = AssistantConnector.Bedrock, Model = "global.anthropic.claude-sonnet-5-5", Region = "eu-west-3", AwsProfile = "profil" };
        var outcome = await connector.ExchangeAsync(new AssistantConnection(settings, null), Prompt(AssistantIntent.Create), new Progress(), Token);
        Assert.Equal((true, Envelope, new AssistantUsage(900, 80000, 0, 42)), (outcome.Succeeded, outcome.Text, outcome.Usage));
        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var root = sent.RootElement;
        Assert.Equal(4, root.GetProperty("system").GetArrayLength());
        Assert.Equal("default", root.GetProperty("system")[1].GetProperty("cachePoint").GetProperty("type").GetString());
        Assert.Equal(("high", "json_schema"), (root.GetProperty("outputConfig").GetProperty("effort").GetString(),
            root.GetProperty("outputConfig").GetProperty("textFormat").GetProperty("type").GetString()));
        Assert.Equal(ReplySchema.Text, root.GetProperty("outputConfig").GetProperty("textFormat").GetProperty("structure").GetProperty("jsonSchema").GetProperty("schema").GetString());
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
        var settings = AssistantSettings.Default with { Connector = AssistantConnector.Bedrock, Model = "global.anthropic.claude-sonnet-5-5", Region = "eu-west-3", AwsProfile = "profil" };
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
        var rendered = new AssistantInstructions().Render(Prompt(history: [new AssistantTurn("Avant", Envelope)]));
        Assert.Contains("<your_reply>\n" + Envelope, rendered, StringComparison.Ordinal);
        Assert.EndsWith(ReplySchema.Text, rendered, StringComparison.Ordinal);
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
    public void The_schema_of_the_reply_has_no_recursion_and_closes_every_object_with_all_its_properties_required()
    {
        using var schema = JsonDocument.Parse(ReplySchema.Text);
        static void Check(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                Assert.False(node.TryGetProperty("$ref", out _));
                if (node.TryGetProperty("type", out var type) && type.GetString() == "object")
                {
                    Assert.False(node.GetProperty("additionalProperties").GetBoolean());
                    Assert.Equal(node.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order(),
                        node.GetProperty("required").EnumerateArray().Select(name => name.GetString()!).Order());
                }
                foreach (var property in node.EnumerateObject())
                    Check(property.Value);
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                    Check(item);
            }
        }
        Check(schema.RootElement);
        Assert.Equal(Codes.All<FindingKind>(), ReplySchema.FindingKinds);
        Assert.Equal(Codes.All<AssistantForm>(), ReplySchema.Forms);
    }
}
