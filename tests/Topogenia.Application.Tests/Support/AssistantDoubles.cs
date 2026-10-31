// AssistantDoubles.cs
// Doubles of the ports of the assistant for the tests of the application: the settings kept in memory, a protector that can be told to lose its keys, a connector that answers as told.

using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.Support;

/// <summary>The settings kept in memory, written only in a unit of work, with the event of their audit.</summary>
public sealed class InMemoryAssistantSettingsStore(InMemoryDatabase database) : IAssistantSettingsStore
{
    public StoredAssistantSettings? Stored { get; set; }

    public int Writes { get; private set; }

    public Task<StoredAssistantSettings?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Stored);

    public Task WriteAsync(StoredAssistantSettings stored, AuditEvent audit, CancellationToken cancellationToken)
    {
        if (!database.InTransaction)
            throw new InvalidOperationException("The settings of the assistant are written in a unit of work.");
        Stored = stored;
        Writes++;
        database.Add(audit);
        return Task.CompletedTask;
    }
}

/// <summary>A protector whose protected text shows it is not the secret; told to lose its keys, it reads nothing back.</summary>
public sealed class FakeSecretProtector : ISecretProtector
{
    public const string Prefix = "protected:";

    public bool KeysLost { get; set; }

    public string Protect(string secret) => Prefix + new string([.. secret.Reverse()]);

    public string? Unprotect(string protectedSecret) =>
        KeysLost || !protectedSecret.StartsWith(Prefix, StringComparison.Ordinal) ? null : new string([.. protectedSecret[Prefix.Length..].Reverse()]);
}

/// <summary>A connector of a kind that answers what it is told and keeps the connections it was given.</summary>
public sealed class FakeAssistantConnector(AssistantConnector kind) : IAssistantConnector
{
    public AssistantConnector Kind => kind;

    public AssistantTestOutcome Outcome { get; set; } = new(true, "fake-model", 12, 20, 3, null, null);

    public List<AssistantConnection> Connections { get; } = [];

    public Task<AssistantTestOutcome> TestAsync(AssistantConnection connection, CancellationToken cancellationToken)
    {
        Connections.Add(connection);
        return Task.FromResult(Outcome);
    }

    /// <summary>The outcomes of the next exchanges, in order; an answer when none is left.</summary>
    public Queue<AssistantExchangeOutcome> Replies { get; } = new();

    /// <summary>The prompts of the exchanges, in order.</summary>
    public List<AssistantPrompt> Prompts { get; } = [];

    /// <summary>Thrown by the next exchange in the place of its reply (a cancellation, a failure of the code).</summary>
    public Exception? Throw { get; set; }

    public static AssistantUsage Usage { get; } = new(1000, 80000, 0, 500);

    public Task<AssistantExchangeOutcome> ExchangeAsync(AssistantConnection connection, AssistantPrompt prompt, IProgress<AssistantProgress> progress,
        CancellationToken cancellationToken)
    {
        Connections.Add(connection);
        Prompts.Add(prompt);
        progress.Report(new AssistantProgress(AssistantPhase.Thinking, "Je lis la carte.", 0));
        if (Throw is { } failure)
        {
            Throw = null;
            throw failure;
        }
        return Task.FromResult(Replies.Count > 0 ? Replies.Dequeue() : Reply("""{"form":"answer","message":"OK","cited":[],"questions":[],"findings":[],"pmap":null}"""));
    }

    /// <summary>A reply of the model with the text given.</summary>
    public static AssistantExchangeOutcome Reply(string text) => new(true, "claude-opus-5-5", text, Usage, 900, null, null);
}

/// <summary>The ports of the assistant of a test: the store, the protector and the two connectors that call a model.</summary>
public sealed class AssistantDoubles(InMemoryDatabase database)
{
    public InMemoryAssistantSettingsStore Store { get; } = new(database);

    public FakeSecretProtector Protector { get; } = new();

    public FakeAssistantConnector Anthropic { get; } = new(AssistantConnector.Anthropic);

    public FakeAssistantConnector Bedrock { get; } = new(AssistantConnector.Bedrock);

    public AssistantPorts Ports => new(Store, Protector, [Anthropic, Bedrock]);
}
