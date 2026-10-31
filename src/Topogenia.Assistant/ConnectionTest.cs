// ConnectionTest.cs
// The minimal exchange of a test of the connection, and the failures of the SDK of Anthropic turned into the codes the interface names (spec 3.0 section 4).

using System.Diagnostics;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
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

    /// <summary>Runs the exchange of a client, timed, every failure of the provider turned into an outcome.</summary>
    public static async Task<AssistantTestOutcome> RunAsync(Func<CancellationToken, Task<Message>> exchange, CancellationToken cancellationToken)
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
            return AssistantTestOutcome.Failed(code, Elapsed(clock), Detail(failure));
        }
    }

    /// <summary>The code of a failure of the SDK of Anthropic or of the network; null for a failure of the code itself.</summary>
    public static string? Failure(Exception failure) => failure switch
    {
        AnthropicUnauthorizedException or AnthropicForbiddenException => AssistantFailures.Refused,
        AnthropicNotFoundException => AssistantFailures.ModelUnknown,
        AnthropicRateLimitException => AssistantFailures.Limited,
        AnthropicBadRequestException bad when Mentions(bad, "model") => AssistantFailures.ModelUnknown,
        AnthropicBadRequestException bad when Mentions(bad, "credit") => AssistantFailures.Limited,
        AnthropicIOException or HttpRequestException or TimeoutException => AssistantFailures.Unreachable,
        AnthropicApiException or AnthropicException => AssistantFailures.Other,
        _ => null,
    };

    /// <summary>
    /// The message of the provider, which names what went wrong (never the key, which the SDK does not repeat), bounded
    /// to the length the interface shows.
    /// </summary>
    public static string Detail(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var text = failure.Message.ReplaceLineEndings(" ").Trim();
        return text.Length <= 300 ? text : text[..300];
    }

    public static int Elapsed(Stopwatch clock) => (int)Math.Min(int.MaxValue, clock.ElapsedMilliseconds);

    private static bool Mentions(Exception failure, string word) => failure.Message.Contains(word, StringComparison.OrdinalIgnoreCase);
}
