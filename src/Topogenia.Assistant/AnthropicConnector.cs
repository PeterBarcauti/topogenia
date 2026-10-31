// AnthropicConnector.cs
// The connector « Claude par l'API Anthropic »: the official SDK of Anthropic, the key of the settings given for the call only; the test of the connection and the exchanges of the panel in a stream (spec 3.0 sections 4, 5).

using System.Diagnostics;
using System.Text;
using Anthropic;
using Anthropic.Models.Messages;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;

namespace Topogenia.Assistant;

/// <summary>
/// Claude by the API of Anthropic (`api.anthropic.com`), the connector of the tests of the owner of the product. A client
/// is made for each call with the key unprotected for it, never kept; the requests go out by the proxy of the system.
/// The tests give their own HTTP client, so that no test reaches the real service.
/// </summary>
public sealed class AnthropicConnector(HttpClient? http = null) : IAssistantConnector
{
    /// <summary>The longest exchange: a large proposal written at a high effort takes minutes.</summary>
    public static readonly TimeSpan ExchangeLimit = TimeSpan.FromMinutes(15);

    /// <summary>The time between two reports of progress while the model writes.</summary>
    private static readonly TimeSpan ReportEvery = TimeSpan.FromMilliseconds(400);

    public AssistantConnector Kind => AssistantConnector.Anthropic;

    public Task<AssistantTestOutcome> TestAsync(AssistantConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var model = connection.Settings.Model ?? AssistantRules.DefaultAnthropicModel;
        return ConnectionTest.RunAsync(token => Client(connection.Secret, ConnectionTest.TimeLimit, 0).Messages.Create(ConnectionTest.Question(model), token),
            cancellationToken);
    }

    /// <summary>
    /// One exchange of the panel, in a stream: the common instruction with the documentation of the formats, then the
    /// schema of the project, each cached (prompt caching, five minutes renewed at each read), then the former turns
    /// (cached up to the last), then the message; the reply constrained to the schema of the reply; adaptive thinking whose
    /// summary is told as it comes, the effort set by the practice. A refusal of the model and a reply cut by its ceiling
    /// are failures said in clear.
    /// </summary>
    public async Task<AssistantExchangeOutcome> ExchangeAsync(AssistantConnection connection, AssistantPrompt prompt, IProgress<AssistantProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(progress);
        var parameters = Parameters(connection.Settings.Model ?? AssistantRules.DefaultAnthropicModel, prompt);
        var clock = Stopwatch.StartNew();
        var reporter = new Reporter(progress);
        var text = new StringBuilder();
        string? model = null;
        int input = 0, cacheRead = 0, cacheWrite = 0, output = 0;
        string? stop = null;
        string? category = null;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(ExchangeLimit);
        reporter.Phase(AssistantPhase.Waiting);
        try
        {
            await foreach (var streamEvent in Client(connection.Secret, ExchangeLimit, 2).Messages.CreateStreaming(parameters, limit.Token))
            {
                if (streamEvent.TryPickStart(out var start))
                {
                    model = start.Message.Model;
                    input = (int)start.Message.Usage.InputTokens;
                    cacheRead = (int)(start.Message.Usage.CacheReadInputTokens ?? 0);
                    cacheWrite = (int)(start.Message.Usage.CacheCreationInputTokens ?? 0);
                }
                else if (streamEvent.TryPickContentBlockStart(out var block))
                {
                    if (block.ContentBlock.TryPickThinking(out _))
                        reporter.Phase(AssistantPhase.Thinking);
                    else if (block.ContentBlock.TryPickText(out _))
                        reporter.Phase(AssistantPhase.Writing);
                }
                else if (streamEvent.TryPickContentBlockDelta(out var delta))
                {
                    if (delta.Delta.TryPickThinking(out var thinking))
                        reporter.Thought(thinking.Thinking);
                    else if (delta.Delta.TryPickText(out var written))
                    {
                        text.Append(written.Text);
                        reporter.Written(text.Length);
                    }
                }
                else if (streamEvent.TryPickDelta(out var ending))
                {
                    // The raw codes of the API (`refusal`, `max_tokens`, `cyber`): the enumerations of the SDK name them otherwise.
                    stop = ending.Delta.StopReason?.Raw();
                    category = ending.Delta.StopDetails?.Category?.Raw();
                    output = (int)ending.Usage.OutputTokens;
                    if (ending.Usage.InputTokens is { } counted)
                        input = (int)counted;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AssistantExchangeOutcome.Failed(AssistantFailures.Unreachable, ConnectionTest.Elapsed(clock), Usage());
        }
        catch (Exception failure) when (ConnectionTest.Failure(failure) is { } code)
        {
            return AssistantExchangeOutcome.Failed(code, ConnectionTest.Elapsed(clock), Usage(), ConnectionTest.Detail(failure));
        }
        return stop switch
        {
            "refusal" => AssistantExchangeOutcome.Failed(AssistantFailures.Declined, ConnectionTest.Elapsed(clock), Usage(), category),
            "max_tokens" => AssistantExchangeOutcome.Failed(AssistantFailures.Truncated, ConnectionTest.Elapsed(clock), Usage()) with { Model = model },
            _ => new AssistantExchangeOutcome(true, model, text.ToString(), Usage(), ConnectionTest.Elapsed(clock), null, null),
        };

        AssistantUsage Usage() => new(input, cacheRead, cacheWrite, output);
    }

    /// <summary>The request of an exchange, in the order of the cache.</summary>
    public static MessageCreateParams Parameters(string model, AssistantPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var system = new List<TextBlockParam> { new() { Text = AssistantInstructions.System, CacheControl = new CacheControlEphemeral() } };
        if (AssistantInstructions.SchemaOf(prompt) is { } schema)
            system.Add(new TextBlockParam { Text = schema, CacheControl = new CacheControlEphemeral() });
        var messages = new List<MessageParam>();
        for (var index = 0; index < prompt.History.Count; index++)
        {
            var turn = prompt.History[index];
            messages.Add(User(turn.Attachments, AssistantInstructions.TurnRequest(turn), cached: false));
            var reply = new TextBlockParam { Text = turn.Reply };
            if (index == prompt.History.Count - 1)
                reply = reply with { CacheControl = new CacheControlEphemeral() };
            messages.Add(new AssistantMessageParam { Content = new List<ContentBlockParam> { reply } });
        }
        messages.Add(User(prompt.Attachments, AssistantInstructions.MessageOf(prompt), cached: true));
        return new MessageCreateParams
        {
            Model = model,
            MaxTokens = prompt.MaxOutputTokens,
            System = system,
            Messages = messages,
            Thinking = new ThinkingConfigAdaptive { Display = Display.Summarized },
            OutputConfig = new OutputConfig
            {
                Effort = AssistantInstructions.EffortOf(prompt.Intent) == "medium" ? Effort.Medium : Effort.High,
                Format = new JsonOutputFormat { Schema = ReplySchema.Properties() },
            },
        };
    }

    /// <summary>A message of the person: its text alone, or its blocks when files are joined to it.</summary>
    private static UserMessageParam User(IReadOnlyList<AssistantAttachment> files, string text, bool cached) =>
        files.Count == 0 ? new UserMessageParam { Content = text } : new UserMessageParam { Content = Blocks(files, text, cached) };

    /// <summary>
    /// The content of a message of the person: the files joined first, each named by its line (a PDF as a document, an
    /// image as an image, a document read by the host as its text), then the text. The files of the request of the
    /// exchange end with a point of cache: given again with the next turn at the same place, they are read from the cache.
    /// </summary>
    private static List<ContentBlockParam> Blocks(IReadOnlyList<AssistantAttachment> files, string text, bool cached)
    {
        var blocks = new List<ContentBlockParam>();
        foreach (var file in files)
        {
            switch (file.Kind)
            {
                case AttachmentKind.Pdf:
                    blocks.Add(new TextBlockParam { Text = AssistantInstructions.FileHeading(file) });
                    blocks.Add(new DocumentBlockParam { Source = new Base64PdfSource { Data = Convert.ToBase64String(file.Content!) }, Title = file.Name });
                    break;
                case AttachmentKind.Image:
                    blocks.Add(new TextBlockParam { Text = AssistantInstructions.FileHeading(file) });
                    blocks.Add(new ImageBlockParam { Source = new Base64ImageSource { Data = Convert.ToBase64String(file.Content!), MediaType = MediaTypeOf(file.MediaType) } });
                    break;
                default:
                    blocks.Add(new TextBlockParam { Text = AssistantInstructions.FileText(file) });
                    break;
            }
        }
        if (cached && blocks.Count > 0)
            blocks[^1] = Cached(blocks[^1]);
        blocks.Add(new TextBlockParam { Text = text });
        return blocks;
    }

    private static ContentBlockParam Cached(ContentBlockParam block) =>
        block.TryPickDocument(out var document) ? document with { CacheControl = new CacheControlEphemeral() }
        : block.TryPickImage(out var image) ? image with { CacheControl = new CacheControlEphemeral() }
        : block.TryPickText(out var text) ? text with { CacheControl = new CacheControlEphemeral() }
        : block;

    private static MediaType MediaTypeOf(string mediaType) => mediaType switch
    {
        "image/png" => MediaType.ImagePng,
        "image/gif" => MediaType.ImageGif,
        "image/webp" => MediaType.ImageWebP,
        _ => MediaType.ImageJpeg,
    };

    /// <summary>
    /// The client of one call: the time it may take, its new attempts (none for a test, whose first failure is said; two for
    /// an exchange, which an overloaded service may refuse once); no span of the SDK (OpenTelemetry, which its variables of
    /// environment could set to carry the messages).
    /// </summary>
    private AnthropicClient Client(string? key, TimeSpan timeout, int retries) =>
        http is null
            ? new AnthropicClient { ApiKey = key ?? string.Empty, MaxRetries = retries, Timeout = timeout, OpenTelemetry = false }
            : new AnthropicClient { ApiKey = key ?? string.Empty, HttpClient = http, MaxRetries = retries, Timeout = timeout, OpenTelemetry = false };

    /// <summary>Tells the progress: each change of phase at once, the thought and the characters written at most every <see cref="ReportEvery"/>.</summary>
    private sealed class Reporter(IProgress<AssistantProgress> progress)
    {
        private readonly Stopwatch since = Stopwatch.StartNew();
        private readonly StringBuilder thought = new();
        private AssistantPhase phase = AssistantPhase.Waiting;
        private int characters;

        public void Phase(AssistantPhase next)
        {
            phase = next;
            Report();
        }

        public void Thought(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            thought.Append(text);
            if (thought.Length > 4000)
                thought.Remove(0, thought.Length - 2000);
            Maybe();
        }

        public void Written(int count)
        {
            characters = count;
            Maybe();
        }

        private void Maybe()
        {
            if (since.Elapsed >= ReportEvery)
                Report();
        }

        private void Report()
        {
            since.Restart();
            progress.Report(new AssistantProgress(phase, LastLine(thought.ToString()), characters));
        }

        /// <summary>
        /// The last line of the summary of the thinking, bounded: what the panel shows of what the model weighs. The line
        /// still being written is shown only while it is the first: a line cut in the middle of a word reads badly.
        /// </summary>
        private static string? LastLine(string text)
        {
            var lines = text.ReplaceLineEndings("\n").Split('\n');
            var complete = lines.Length > 1 ? lines[..^1] : lines;
            var line = complete.Select(candidate => candidate.Trim()).LastOrDefault(candidate => candidate.Length > 0);
            if (string.IsNullOrEmpty(line))
                return null;
            line = line.Trim('*', '#', ' ');
            return line.Length <= 200 ? line : string.Concat(line.AsSpan(0, 199), "…");
        }
    }
}
