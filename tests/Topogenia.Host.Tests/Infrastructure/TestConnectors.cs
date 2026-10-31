// TestConnectors.cs
// The connectors of the assistant in the test hosts: they answer at once and keep what they were given, so that no test of the host reaches a real model (spec 3.0 section 8).

using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;

namespace Topogenia.Host.Tests.Infrastructure;

/// <summary>
/// A connector of the test hosts, in the place of the real one of its kind: the model `refused-model` is refused as a key
/// would be, any other answers with the model it was given; the connections are kept for the tests to read.
/// </summary>
public sealed class TestConnector(AssistantConnector kind) : IAssistantConnector
{
    /// <summary>The model a test names to be refused by the provider.</summary>
    public const string RefusedModel = "refused-model";

    private readonly List<AssistantConnection> connections = [];

    public AssistantConnector Kind => kind;

    public IReadOnlyList<AssistantConnection> Connections
    {
        get
        {
            lock (connections)
                return [.. connections];
        }
    }

    public Task<AssistantTestOutcome> TestAsync(AssistantConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        lock (connections)
            connections.Add(connection);
        return Task.FromResult(connection.Settings.Model == RefusedModel
            ? AssistantTestOutcome.Failed(AssistantFailures.Refused, 5, "invalid x-api-key")
            : new AssistantTestOutcome(true, connection.Settings.Model, 5, 20, 3, null, null));
    }

    private readonly Queue<string> replies = new();

    private readonly List<AssistantPrompt> prompts = [];

    /// <summary>The prompts of the exchanges, in order.</summary>
    public IReadOnlyList<AssistantPrompt> Prompts
    {
        get
        {
            lock (prompts)
                return [.. prompts];
        }
    }

    /// <summary>The time the next exchanges wait before their reply (to see the heartbeats of the stream, or to stop one).</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>Gives the text of the next reply of the model.</summary>
    public void Next(string reply)
    {
        lock (replies)
            replies.Enqueue(reply);
    }

    public async Task<AssistantExchangeOutcome> ExchangeAsync(AssistantConnection connection, AssistantPrompt prompt, IProgress<AssistantProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(progress);
        lock (connections)
            connections.Add(connection);
        lock (prompts)
            prompts.Add(prompt);
        progress.Report(new AssistantProgress(AssistantPhase.Thinking, "Je lis la carte.", 0));
        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, cancellationToken);
        if (connection.Settings.Model == RefusedModel)
            return AssistantExchangeOutcome.Failed(AssistantFailures.Refused, 5, null, "invalid x-api-key");
        string text;
        lock (replies)
            text = replies.Count > 0 ? replies.Dequeue() : """{"form":"answer","message":"OK","cited":[],"questions":[],"findings":[],"pmap":null}""";
        return new AssistantExchangeOutcome(true, connection.Settings.Model, text, new AssistantUsage(1000, 0, 80000, 500), 5, null, null);
    }
}
