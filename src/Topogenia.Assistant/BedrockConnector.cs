// BedrockConnector.cs
// The connector « Claude par AWS Bedrock »: Bedrock Runtime by the AWS SDK, with the credentials of a named AWS profile (SSO) or of the identity of the host, read fresh for each call (spec 3.0 sections 4, 5).

using System.Diagnostics;
using System.Text;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime.Credentials;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;

namespace Topogenia.Assistant;

/// <summary>
/// Claude by AWS Bedrock Runtime, in the region of the settings. The credentials come from the named AWS profile of the
/// settings, passed explicitly so that the keys a launcher may have left in the environment are ignored; without a
/// profile, from the identity of the host (the default chain of AWS: the role of a server). They last about an hour and
/// are read again for each call, the AWS SDK renewing them while the SSO session is valid; no key is kept by the application.
/// The model is the identifier of an inference profile (`global.anthropic.claude-sonnet-5-5`): a base identifier is
/// refused on demand. The SDK of Anthropic is not used here: its Bedrock client of this endpoint takes no session token,
/// which the credentials of an SSO profile carry. The tests give their own HTTP factory and credentials, so that no test
/// reaches the real service nor reads the profiles of the machine.
/// </summary>
public sealed class BedrockConnector(HttpClientFactory? http = null, Func<string?, AWSCredentials?>? credentials = null) : IAssistantConnector
{
    private readonly Func<string?, AWSCredentials?> credentialsOf = credentials ?? Credentials;

    public AssistantConnector Kind => AssistantConnector.Bedrock;

    public async Task<AssistantTestOutcome> TestAsync(AssistantConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var settings = connection.Settings;
        if (settings.Region is not { } region || settings.Model is not { } model)
            return AssistantTestOutcome.Failed(AssistantFailures.ModelUnknown, 0);
        var clock = Stopwatch.StartNew();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(ConnectionTest.TimeLimit);
        try
        {
            if (credentialsOf(settings.AwsProfile) is not { } found)
                return AssistantTestOutcome.Failed(AssistantFailures.ProfileMissing, 0);
            using var client = new AmazonBedrockRuntimeClient(found, Configuration(region, ConnectionTest.TimeLimit, 0));
            var reply = await client.ConverseAsync(new ConverseRequest
            {
                ModelId = model,
                Messages = [new Message { Role = ConversationRole.User, Content = [new ContentBlock { Text = ConnectionTest.QuestionText }] }],
                InferenceConfig = new InferenceConfiguration { MaxTokens = ConnectionTest.ReplyTokens },
            }, limit.Token);
            return new AssistantTestOutcome(true, model, ConnectionTest.Elapsed(clock), reply.Usage?.InputTokens, reply.Usage?.OutputTokens, null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AssistantTestOutcome.Failed(AssistantFailures.Unreachable, ConnectionTest.Elapsed(clock));
        }
        catch (Exception failure) when (Failure(failure) is { } code)
        {
            return AssistantTestOutcome.Failed(code, ConnectionTest.Elapsed(clock), ConnectionTest.Detail(failure));
        }
    }

    /// <summary>
    /// One exchange of the panel by `Converse`, without a stream (the host tells that the model works while it waits): the
    /// common instruction with the documentation of the formats, then the schema of the project, each followed by a point
    /// of cache, the former turns, the message; the reply constrained to the schema of the reply (structured output of
    /// Bedrock), the effort set by the practice. A reply cut by its ceiling, or filtered, is a failure said in clear.
    /// </summary>
    public async Task<AssistantExchangeOutcome> ExchangeAsync(AssistantConnection connection, AssistantPrompt prompt, IProgress<AssistantProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(progress);
        var settings = connection.Settings;
        if (settings.Region is not { } region || settings.Model is not { } model)
            return AssistantExchangeOutcome.Failed(AssistantFailures.ModelUnknown, 0);
        var clock = Stopwatch.StartNew();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(AnthropicConnector.ExchangeLimit);
        progress.Report(new AssistantProgress(AssistantPhase.Waiting, null, 0));
        try
        {
            if (credentialsOf(settings.AwsProfile) is not { } found)
                return AssistantExchangeOutcome.Failed(AssistantFailures.ProfileMissing, 0);
            using var client = new AmazonBedrockRuntimeClient(found, Configuration(region, AnthropicConnector.ExchangeLimit, 2));
            var reply = await client.ConverseAsync(Request(model, prompt), limit.Token);
            var usage = new AssistantUsage(reply.Usage?.InputTokens ?? 0, reply.Usage?.CacheReadInputTokens ?? 0, reply.Usage?.CacheWriteInputTokens ?? 0,
                reply.Usage?.OutputTokens ?? 0);
            var stop = reply.StopReason?.Value;
            if (stop == StopReason.Max_tokens.Value)
                return AssistantExchangeOutcome.Failed(AssistantFailures.Truncated, ConnectionTest.Elapsed(clock), usage) with { Model = model };
            if (stop == StopReason.Content_filtered.Value || stop == StopReason.Guardrail_intervened.Value)
                return AssistantExchangeOutcome.Failed(AssistantFailures.Declined, ConnectionTest.Elapsed(clock), usage, stop);
            var text = new StringBuilder();
            foreach (var block in reply.Output?.Message?.Content ?? [])
                if (block.Text is { } written)
                    text.Append(written);
            return new AssistantExchangeOutcome(true, model, text.ToString(), usage, ConnectionTest.Elapsed(clock), null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AssistantExchangeOutcome.Failed(AssistantFailures.Unreachable, ConnectionTest.Elapsed(clock));
        }
        catch (Exception failure) when (Failure(failure) is { } code)
        {
            return AssistantExchangeOutcome.Failed(code, ConnectionTest.Elapsed(clock), null, ConnectionTest.Detail(failure));
        }
    }

    /// <summary>The request of an exchange, in the order of the cache.</summary>
    public static ConverseRequest Request(string model, AssistantPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var system = new List<SystemContentBlock>
        {
            new() { Text = AssistantInstructions.System }, new() { CachePoint = new CachePointBlock { Type = CachePointType.Default } },
        };
        if (AssistantInstructions.SchemaOf(prompt) is { } schema)
        {
            system.Add(new SystemContentBlock { Text = schema });
            system.Add(new SystemContentBlock { CachePoint = new CachePointBlock { Type = CachePointType.Default } });
        }
        var messages = new List<Message>();
        var documents = 0;
        for (var index = 0; index < prompt.History.Count; index++)
        {
            var turn = prompt.History[index];
            messages.Add(new Message { Role = ConversationRole.User, Content = Blocks(turn.Attachments, AssistantInstructions.TurnRequest(turn), false, ref documents) });
            var reply = new List<ContentBlock> { new() { Text = turn.Reply } };
            if (index == prompt.History.Count - 1)
                reply.Add(new ContentBlock { CachePoint = new CachePointBlock { Type = CachePointType.Default } });
            messages.Add(new Message { Role = ConversationRole.Assistant, Content = reply });
        }
        messages.Add(new Message { Role = ConversationRole.User, Content = Blocks(prompt.Attachments, AssistantInstructions.MessageOf(prompt), true, ref documents) });
        return new ConverseRequest
        {
            ModelId = model,
            System = system,
            Messages = messages,
            InferenceConfig = new InferenceConfiguration { MaxTokens = prompt.MaxOutputTokens },
            OutputConfig = new OutputConfig
            {
                Effort = AssistantInstructions.EffortOf(prompt.Intent),
                TextFormat = new OutputFormat
                {
                    Type = OutputFormatType.Json_schema,
                    Structure = new OutputFormatStructure { JsonSchema = new JsonSchemaDefinition { Name = "reply", Schema = ReplySchema.Text } },
                },
            },
        };
    }

    /// <summary>
    /// The content of a message of the person: the files joined first, each named by its line (a PDF as a document of
    /// `Converse`, which Bedrock reads as its text without citations; an image; a document read by the host as its text),
    /// a point of cache after those of the request of the exchange, then the text. A document of `Converse` has a name of
    /// letters, digits and hyphens, unique in the request: `document-1`, `document-2`, the line before it giving its own.
    /// </summary>
    private static List<ContentBlock> Blocks(IReadOnlyList<AssistantAttachment> files, string text, bool cached, ref int documents)
    {
        var blocks = new List<ContentBlock>();
        foreach (var file in files)
        {
            if (!file.Binary)
            {
                blocks.Add(new ContentBlock { Text = AssistantInstructions.FileText(file) });
                continue;
            }
            blocks.Add(new ContentBlock { Text = AssistantInstructions.FileHeading(file) });
            if (file.Kind == AttachmentKind.Pdf)
            {
                documents++;
                blocks.Add(new ContentBlock
                {
                    Document = new DocumentBlock
                    {
                        Format = DocumentFormat.Pdf, Name = $"document-{documents}", Source = new DocumentSource { Bytes = new MemoryStream(file.Content!, writable: false) },
                    },
                });
            }
            else
            {
                blocks.Add(new ContentBlock
                {
                    Image = new ImageBlock { Format = ImageFormatOf(file.MediaType), Source = new ImageSource { Bytes = new MemoryStream(file.Content!, writable: false) } },
                });
            }
        }
        if (cached && blocks.Count > 0)
            blocks.Add(new ContentBlock { CachePoint = new CachePointBlock { Type = CachePointType.Default } });
        blocks.Add(new ContentBlock { Text = text });
        return blocks;
    }

    private static ImageFormat ImageFormatOf(string mediaType) => mediaType switch
    {
        "image/png" => ImageFormat.Png,
        "image/gif" => ImageFormat.Gif,
        "image/webp" => ImageFormat.Webp,
        _ => ImageFormat.Jpeg,
    };

    /// <summary>
    /// The client of one call, in the region given, with the time it may take and its new attempts (none for a test, whose
    /// first failure is said).
    /// </summary>
    private AmazonBedrockRuntimeConfig Configuration(string region, TimeSpan timeout, int retries)
    {
        var configuration = new AmazonBedrockRuntimeConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(region), MaxErrorRetry = retries, Timeout = timeout };
        if (http is not null)
            configuration.HttpClientFactory = http;
        return configuration;
    }

    /// <summary>The credentials of the named profile, null when the machine has none of that name; the identity of the host without a profile.</summary>
    private static AWSCredentials? Credentials(string? profile)
    {
        if (profile is null)
            return DefaultAWSCredentialsIdentityResolver.GetCredentials();
        return new CredentialProfileStoreChain().TryGetAWSCredentials(profile, out var credentials) ? credentials : null;
    }

    /// <summary>The code of a failure of Bedrock or of the credentials of AWS; null for a failure of the code itself.</summary>
    private static string? Failure(Exception failure) => failure switch
    {
        AccessDeniedException => AssistantFailures.Refused,
        ValidationException or ResourceNotFoundException => AssistantFailures.ModelUnknown,
        ThrottlingException or ServiceQuotaExceededException => AssistantFailures.Limited,
        AmazonServiceException service when service.ErrorCode is "ExpiredTokenException" or "UnrecognizedClientException" => AssistantFailures.SessionExpired,
        // A quota of the account: the SDK does not know this error for Converse and gives it as a service exception.
        AmazonServiceException { ErrorCode: "ServiceQuotaExceededException" } => AssistantFailures.Limited,
        AmazonServiceException => AssistantFailures.Other,
        // The credentials of a profile that cannot be had: its SSO session expired, or it was never signed in.
        AmazonClientException => AssistantFailures.SessionExpired,
        HttpRequestException or TimeoutException => AssistantFailures.Unreachable,
        _ => null,
    };
}
