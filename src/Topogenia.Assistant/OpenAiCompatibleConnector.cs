// OpenAiCompatibleConnector.cs
// The connectors that speak the Chat Completions API of OpenAI (ADR 098): OpenAI, Google Gemini, xAI Grok, Mistral AI, DeepSeek, Moonshot Kimi, Alibaba Qwen, Microsoft Azure OpenAI, Ollama and LM Studio on the machine; one class read in the table of the providers, the key given for the call only, the reply constrained to the schema of the reply, read up to a bound, no redirection followed, an error of a model of the machine relayed only when it is JSON.

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;

namespace Topogenia.Assistant;

/// <summary>
/// A connector of the Chat Completions API. A client is made for each call, the key in its header for that call only,
/// never kept. Its handler follows no redirection: the project and the key go to the address of the table and nowhere
/// else, a reply 3xx is a failure. The requests to a provider on line go out by the proxy of the system; those to a
/// model of this machine never do. The whole reply is read at once (no stream), up to a bound: the panel says that the
/// model works. Whatever the provider answers is turned into an outcome, never thrown. The tests give their own
/// handler, so that no test reaches a real service.
/// </summary>
public sealed class OpenAiCompatibleConnector(AssistantConnector kind, HttpMessageHandler? handler = null) : IAssistantConnector
{
    /// <summary>The time before a new attempt after a refusal of an overloaded service, doubled at each attempt.</summary>
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The most bytes read of a reply: 64 for each token of the highest ceiling of the settings (128 000 tokens, so
    /// 8 192 000 bytes). A token is four characters on average, and the heaviest ways to write one stay far below 64
    /// bytes: two characters outside ASCII each escaped on six bytes (12 bytes), a quote of the `.pmap` of a proposal
    /// escaped twice (4 bytes for one character). Beyond, the reply is not the one of a model within its ceiling, and
    /// nothing more of it is read.
    /// </summary>
    public const int ReplyBytesMax = AssistantRules.MaxOutputTokens * 64;

    /// <summary>The most bytes read of a refusal, of which a message of a few lines is kept.</summary>
    public const int ErrorBytesMax = 64 * 1024;

    private const string NoCompletion = "The reply is not a chat completion: no choice with a message.";

    /// <summary>
    /// What is said of an error of a model of this machine that is not JSON (SEC-MAJ-01): another program may hold its port,
    /// and its page is never relayed to the person who asked, nor its text read for a code.
    /// </summary>
    public const string NotJsonOnThisMachine = "The program on this port did not answer in JSON as Ollama and LM Studio do: another program may hold the port, and its reply is not shown.";

    public AssistantConnector Kind => kind;

    private AssistantProvider Provider { get; } = AssistantProviders.Of(kind);

    /// <summary>
    /// The handler of the calls to a provider: no redirection is ever followed (a provider, or the program that listens
    /// on the port of this machine, would send the project where it wants); the proxy of the system for a provider on
    /// line, which a company needs, and none for a model of this machine, whose requests stay on the loopback.
    /// </summary>
    public static SocketsHttpHandler HandlerOf(AssistantConnector kind) => new()
    {
        AllowAutoRedirect = false,
        UseProxy = !AssistantConnectors.OnThisMachine(kind),
    };

    /// <summary>
    /// One short question with the ceiling of the settings, the one an exchange sends: a provider that refuses it says
    /// so at the test. A reply cut by its ceiling is still an answer (the model was reached and wrote).
    /// </summary>
    public async Task<AssistantTestOutcome> TestAsync(AssistantConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var body = new JsonObject
        {
            ["model"] = connection.Settings.Model,
            [Provider.CeilingParameter] = connection.Settings.MaxOutputTokens,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = ConnectionTest.QuestionText }),
        };
        var clock = Stopwatch.StartNew();
        var reply = await CallAsync(connection, body, ConnectionTest.TimeLimit, 0, cancellationToken);
        return reply.Failure is { } failure && failure != AssistantFailures.Truncated
            ? AssistantTestOutcome.Failed(failure, ConnectionTest.Elapsed(clock), reply.Detail)
            : new AssistantTestOutcome(true, reply.Model, ConnectionTest.Elapsed(clock), reply.Usage.InputTokens + reply.Usage.CacheReadTokens, reply.Usage.OutputTokens, null, null);
    }

    /// <summary>
    /// One exchange of the panel: the instructions with the documentation of the formats and the schema of the project, the
    /// former turns, then the message; the reply constrained to the schema of the reply (by the schema, or by a JSON object
    /// and the schema told). A refusal and a reply cut by its ceiling are failures said in clear.
    /// </summary>
    public async Task<AssistantExchangeOutcome> ExchangeAsync(AssistantConnection connection, AssistantPrompt prompt, IProgress<AssistantProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(progress);
        var clock = Stopwatch.StartNew();
        progress.Report(new AssistantProgress(AssistantPhase.Waiting, null, 0));
        var reply = await CallAsync(connection, Body(connection.Settings.Model!, prompt), AnthropicConnector.ExchangeLimit, 2, cancellationToken);
        if (reply.Failure is { } failure)
            return AssistantExchangeOutcome.Failed(failure, ConnectionTest.Elapsed(clock), reply.Usage, reply.Detail) with { Model = reply.Model };
        progress.Report(new AssistantProgress(AssistantPhase.Writing, null, reply.Text?.Length ?? 0));
        return new AssistantExchangeOutcome(true, reply.Model, reply.Text, reply.Usage, ConnectionTest.Elapsed(clock), null, null);
    }

    /// <summary>The body of an exchange: one message of instructions, the former turns, the message with its files; the format of the reply.</summary>
    public JsonObject Body(string model, AssistantPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var system = new StringBuilder(AssistantInstructions.System);
        if (AssistantInstructions.SchemaOf(prompt) is { } schema)
            system.Append("\n\n").Append(schema);
        if (Provider.Format == ReplyFormat.JsonObject)
            system.Append("\n\n# The schema of your reply\n\nReply with one JSON object that follows this JSON Schema, and nothing before or after it:\n\n").Append(ReplySchema.For(prompt));
        var messages = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = system.ToString() });
        foreach (var turn in prompt.History)
        {
            messages.Add(User(turn.Attachments, AssistantInstructions.TurnRequest(turn)));
            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = turn.Reply });
        }
        messages.Add(User(prompt.Attachments, AssistantInstructions.MessageOf(prompt)));
        return new JsonObject
        {
            ["model"] = model,
            [Provider.CeilingParameter] = prompt.MaxOutputTokens,
            ["messages"] = messages,
            ["response_format"] = Provider.Format == ReplyFormat.JsonSchema
                ? new JsonObject { ["type"] = "json_schema", ["json_schema"] = new JsonObject { ["name"] = "reply", ["strict"] = true, ["schema"] = JsonNode.Parse(ReplySchema.For(prompt)) } }
                : new JsonObject { ["type"] = "json_object" },
        };
    }

    /// <summary>A message of the person: its text alone, or its parts when files are joined (an image as an image, a PDF as a file, a document read by the host as its text).</summary>
    private static JsonObject User(IReadOnlyList<AssistantAttachment> files, string text)
    {
        if (files.Count == 0)
            return new JsonObject { ["role"] = "user", ["content"] = text };
        var parts = new JsonArray();
        foreach (var file in files)
        {
            switch (file.Kind)
            {
                case AttachmentKind.Image:
                    parts.Add(Text(AssistantInstructions.FileHeading(file)));
                    parts.Add(new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = $"data:{file.MediaType};base64,{Convert.ToBase64String(file.Content!)}" } });
                    break;
                case AttachmentKind.Pdf:
                    parts.Add(Text(AssistantInstructions.FileHeading(file)));
                    parts.Add(new JsonObject
                    {
                        ["type"] = "file",
                        ["file"] = new JsonObject { ["filename"] = file.Name, ["file_data"] = $"data:application/pdf;base64,{Convert.ToBase64String(file.Content!)}" },
                    });
                    break;
                default:
                    parts.Add(Text(AssistantInstructions.FileText(file)));
                    break;
            }
        }
        parts.Add(Text(text));
        return new JsonObject { ["role"] = "user", ["content"] = parts };
    }

    private static JsonObject Text(string text) => new() { ["type"] = "text", ["text"] = text };

    /// <summary>
    /// One call, timed, with its new attempts on an overloaded service (429, 5xx); every answer of the provider and every
    /// failure of the network turned into an outcome; a cancellation of the caller thrown as it is.
    /// </summary>
    private async Task<Reply> CallAsync(AssistantConnection connection, JsonObject body, TimeSpan timeout, int retries, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        using var client = handler is null ? new HttpClient(HandlerOf(kind)) : new HttpClient(handler, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;
        var address = AssistantProviders.ChatAddress(kind, connection.Settings);
        var content = body.ToJsonString();
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                using var request = Request(address, content, connection.Secret);
                // The headers first: the body is read by this connector, up to its bound, never whole by the client.
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
                var status = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                    return await ReadAsync(response, connection.Secret, limit.Token);
                if (status is >= 300 and < 400)
                    return Reply.Failed(AssistantFailures.Unreachable, ProviderErrors.Redirection(status));
                if (attempt < retries && (status == 429 || status >= 500))
                {
                    await Task.Delay(RetryAfter * (1 << attempt), limit.Token);
                    continue;
                }
                var text = (await TextAsync(response.Content, ErrorBytesMax, limit.Token)).Text;
                var error = ErrorOf(text, AssistantConnectors.OnThisMachine(kind) ? NotJsonOnThisMachine : text);
                return Reply.Failed(ProviderErrors.Of(status, error), ProviderMessages.Relayed(error, connection.Secret));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Reply.Failed(AssistantFailures.Unreachable, null);
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException)
        {
            // The network failed, before the reply or while it was read.
            return Reply.Failed(AssistantFailures.Unreachable, ConnectionTest.Detail(failure, connection.Secret));
        }
    }

    /// <summary>The request of a call: its body, and the key in the header of the provider when it takes one.</summary>
    private HttpRequestMessage Request(Uri address, string content, string? secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, address) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
        if (string.IsNullOrEmpty(secret))
            return request;
        if (Provider.BearerKey)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        else
            request.Headers.Add("api-key", secret);
        return request;
    }

    /// <summary>The reply of a call that succeeded, read up to its bound: beyond it, or when it is no completion, an invalid reply.</summary>
    private static async Task<Reply> ReadAsync(HttpResponseMessage response, string? secret, CancellationToken cancellationToken)
    {
        var (text, whole) = await TextAsync(response.Content, ReplyBytesMax, cancellationToken);
        return whole
            ? Reply.Read(text, secret)
            : Reply.Failed(AssistantFailures.InvalidReply, $"The reply is longer than {ReplyBytesMax} bytes: it was not read further.");
    }

    /// <summary>The text of a content read up to a bound in bytes, and whether it was read whole: one byte past the bound at most is asked for.</summary>
    private static async Task<(string Text, bool Whole)> TextAsync(HttpContent content, int max, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var kept = new MemoryStream();
        var buffer = new byte[81920];
        var whole = true;
        while (whole)
        {
            var asked = (int)Math.Min(buffer.Length, max - kept.Length + 1);
            var read = await stream.ReadAsync(buffer.AsMemory(0, asked), cancellationToken);
            if (read == 0)
                break;
            whole = kept.Length + read <= max;
            kept.Write(buffer, 0, whole ? read : (int)(max - kept.Length));
        }
        return (Decoded(kept.GetBuffer().AsSpan(0, (int)kept.Length)), whole);
    }

    /// <summary>The text of the bytes of a body, in UTF-8 as JSON is written; a mark of byte order at its start is no part of it.</summary>
    private static string Decoded(ReadOnlySpan<byte> bytes) =>
        Encoding.UTF8.GetString(bytes.StartsWith(Encoding.UTF8.Preamble) ? bytes[Encoding.UTF8.Preamble.Length..] : bytes);

    /// <summary>
    /// The message of an error of the provider, whatever its form: `{"error": {"message": ...}}`, `{"error": "..."}`, a
    /// list whose first item is one of them, `{"message": ...}`, `{"detail": ...}`; the text itself when it is not
    /// JSON; nothing for an empty body or a JSON that says nothing of these.
    /// </summary>
    public static string? ErrorOf(string text) => ErrorOf(text, text);

    /// <summary><see cref="ErrorOf(string)"/>, a text that is not JSON said by <paramref name="notJson"/>.</summary>
    private static string? ErrorOf(string text, string notJson)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            JsonElement? root = document.RootElement;
            if (root is { ValueKind: JsonValueKind.Array } list)
                root = list.GetArrayLength() > 0 ? list[0] : null;
            var error = Child(root, "error");
            return Said(error is { ValueKind: JsonValueKind.Object } ? Child(error, "message") : error) ?? Said(Child(root, "message")) ?? Said(Child(root, "detail"));
        }
        catch (JsonException)
        {
            return notJson;
        }
    }

    /// <summary>The member of an object by its name (the last one when it is written twice); nothing for a node that is no object or has no such member.</summary>
    private static JsonElement? Child(JsonElement? parent, string name) =>
        parent is { ValueKind: JsonValueKind.Object } node && node.TryGetProperty(name, out var child) ? child : null;

    /// <summary>What a node of an error says: its text, or its JSON when it is not a text (the details of a refusal); nothing for none.</summary>
    private static string? Said(JsonElement? node) => node switch
    {
        null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        { ValueKind: JsonValueKind.String } text => text.GetString(),
        { } other => other.GetRawText(),
    };

    /// <summary>What a call gave: the text of the reply and the model, the tokens, or the code of its failure.</summary>
    private sealed record Reply(string? Model, string? Text, AssistantUsage Usage, string? Failure, string? Detail)
    {
        public static Reply Failed(string failure, string? detail) => new(null, null, AssistantUsage.None, failure, detail);

        /// <summary>
        /// The reply of the API: its first choice, the reason it stopped (`length`: the ceiling; `content_filter`, or a refusal
        /// written: declined), the tokens (those read from the cache of the provider apart). A body that is no JSON, or
        /// that has no choice with a message, is an invalid reply; every other part is read as it can be, never trusted.
        /// </summary>
        public static Reply Read(string text, string? secret)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                if (Child(root, "choices") is not { ValueKind: JsonValueKind.Array } choices || choices.GetArrayLength() == 0
                    || Child(choices[0], "message") is not { ValueKind: JsonValueKind.Object } message)
                    return Failed(AssistantFailures.InvalidReply, NoCompletion);
                var model = Child(root, "model") is { ValueKind: JsonValueKind.String } named ? named.GetString() : null;
                var usage = Child(root, "usage");
                var prompt = Tokens(Child(usage, "prompt_tokens"));
                var cached = Tokens(Child(Child(usage, "prompt_tokens_details"), "cached_tokens"));
                var tokens = new AssistantUsage(Math.Max(0, prompt - cached), cached, 0, Tokens(Child(usage, "completion_tokens")));
                var stop = Child(choices[0], "finish_reason") is { ValueKind: JsonValueKind.String } reason ? reason.GetString() : null;
                if (Child(message, "refusal") is { ValueKind: JsonValueKind.String } refusal && refusal.GetString() is { Length: > 0 } said)
                    return new Reply(model, null, tokens, AssistantFailures.Declined, ProviderMessages.Relayed(said, secret));
                if (stop == "content_filter")
                    return new Reply(model, null, tokens, AssistantFailures.Declined, null);
                if (stop == "length")
                    return new Reply(model, null, tokens, AssistantFailures.Truncated, null);
                return new Reply(model, ContentOf(Child(message, "content")), tokens, null, null);
            }
            catch (JsonException)
            {
                return Failed(AssistantFailures.InvalidReply, NoCompletion);
            }
        }

        /// <summary>A count of tokens as it can be read: a number, whole or not, from zero to a billion; zero for anything else.</summary>
        private static int Tokens(JsonElement? node) =>
            node is { ValueKind: JsonValueKind.Number } number && number.TryGetDouble(out var count) && count > 0 ? (int)Math.Min(count, 1_000_000_000) : 0;

        /// <summary>The text of a message: a string, or the text parts of a list joined.</summary>
        private static string? ContentOf(JsonElement? content) => content switch
        {
            { ValueKind: JsonValueKind.String } text => text.GetString(),
            { ValueKind: JsonValueKind.Array } parts => string.Concat(parts.EnumerateArray()
                .Select(part => Child(part, "text") is { ValueKind: JsonValueKind.String } written ? written.GetString() : null)),
            _ => null,
        };
    }
}
