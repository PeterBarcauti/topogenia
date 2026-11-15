// ConnectionTest.cs
// The minimal exchange of a test of the connection, and the failures of the SDK of Anthropic turned into the codes the interface names (spec 3.0 section 4).

using System.Diagnostics;
using System.Net;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;

namespace Topogenia.Assistant;

/// <summary>
/// One short question whose answer does not matter, the same for every connector: what is checked is that the model
/// answers. The effort is low and the reply bounded, so that a test costs a few tokens; a reply cut by its bound is still
/// an answer.
/// </summary>
internal static class ConnectionTest
{
    /// <summary>The bound of the reply of a test (the model may think before it answers).</summary>
    public const int ReplyTokens = 1024;

    /// <summary>The time a test may take before it is said unreachable.</summary>
    public static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(90);

    /// <summary>The question of a test, the same for every connector.</summary>
    public const string QuestionText = "Réponds seulement par le mot OK.";

    public static MessageCreateParams Question(string model) => new()
    {
        Model = model,
        MaxTokens = ReplyTokens,
        OutputConfig = new OutputConfig { Effort = Effort.Low },
        Messages = [new UserMessageParam { Content = QuestionText }],
    };

    /// <summary>Runs the exchange of a client, timed, every failure of the provider turned into an outcome, its message without the key given.</summary>
    public static async Task<AssistantTestOutcome> RunAsync(Func<CancellationToken, Task<Message>> exchange, string? secret, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeLimit);
        try
        {
            var reply = await exchange(limit.Token);
            return new AssistantTestOutcome(true, reply.Model, Elapsed(clock), (int)reply.Usage.InputTokens, (int)reply.Usage.OutputTokens, null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AssistantTestOutcome.Failed(AssistantFailures.Unreachable, Elapsed(clock));
        }
        catch (Exception failure) when (Failure(failure) is { } code)
        {
            return AssistantTestOutcome.Failed(code, Elapsed(clock), Detail(failure, secret));
        }
    }

    /// <summary>The code of a failure of the SDK of Anthropic or of the network; null for a failure of the code itself.</summary>
    public static string? Failure(Exception failure) => failure switch
    {
        AnthropicUnauthorizedException or AnthropicForbiddenException => AssistantFailures.Refused,
        AnthropicNotFoundException => AssistantFailures.ModelUnknown,
        AnthropicRateLimitException => AssistantFailures.Limited,
        // A request refused says why in its message only: the ceiling, the model, the credit (the word `model` alone decides nothing).
        AnthropicBadRequestException bad => ProviderErrors.OfBadRequest(bad.Message),
        AnthropicIOException or HttpRequestException or TimeoutException => AssistantFailures.Unreachable,
        // A redirection is never followed: the provider was not reached at its address.
        AnthropicApiException { StatusCode: >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest } => AssistantFailures.Unreachable,
        AnthropicApiException or AnthropicException => AssistantFailures.Other,
        _ => null,
    };

    /// <summary>
    /// The message of the provider, which names what went wrong, without the key nor a part of it (the message of the
    /// SDK holds the body of the refusal, and a provider may repeat there the key it refuses), bounded to the length the
    /// interface shows; a redirection, never followed, is said with its status.
    /// </summary>
    public static string? Detail(Exception failure, string? secret)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure is AnthropicApiException { StatusCode: >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest } redirected
            ? ProviderErrors.Redirection((int)redirected.StatusCode)
            : ProviderMessages.Relayed(failure.Message, secret);
    }

    public static int Elapsed(Stopwatch clock) => (int)Math.Min(int.MaxValue, clock.ElapsedMilliseconds);
}
