// Concurrently.cs
// Operations really run at the same time, each in its own task and unit of work, and the meeting point of their loadings.

using Topogenia.Domain.Errors;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>What one of the concurrent operations gave: its result, or the refusal of the domain or of a guard.</summary>
public sealed record Attempted<T>(T? Result, DomainException? Refusal)
{
    public bool Succeeded => Refusal is null;
}

/// <summary>
/// Starts every operation on its own thread of the pool at the same instant (a gate opened once all are scheduled) and
/// waits for all of them. A refusal (<see cref="DomainException"/>) is kept as the outcome of its operation; any other
/// failure fails the test.
/// </summary>
public static class Concurrently
{
    public static async Task<IReadOnlyList<Attempted<T>>> RunAsync<T>(int count, Func<int, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = Enumerable.Range(0, count).Select(index => Task.Run(async () =>
        {
            await gate.Task;
            try
            {
                return new Attempted<T>(await operation(index), null);
            }
            catch (DomainException refusal)
            {
                return new Attempted<T>(default, refusal);
            }
        }, TestContext.Current.CancellationToken)).ToList();
        gate.SetResult();
        return await Task.WhenAll(runs);
    }
}

/// <summary>
/// The point where concurrent units meet after their loading, before any of them writes: each waits until all have
/// arrived, so that every unit works on the state that none of the others has changed yet. On SQL Server the units always
/// meet (read committed lets them all read before any writes): the wait lasts until the last one arrives, at most
/// <see cref="ServerPatience"/>, long enough for an instance loaded by other runs, and a test that needs the race checks
/// <see cref="AllMet"/> with <see cref="Missed"/> as its message. SQLite serialises the write transactions from their
/// start (immediate transactions) and never lets the units meet: the wait then ends after <see cref="SqlitePatience"/>
/// and the units run one after the other, as the engine decides.
/// </summary>
public sealed class Rendezvous(EngineKind kind, int parties)
{
    public static readonly TimeSpan ServerPatience = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan SqlitePatience = TimeSpan.FromSeconds(2);

    private readonly TaskCompletionSource all = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrived;
    private int met;

    /// <summary>How long a unit waits for the others on this engine.</summary>
    public TimeSpan Patience { get; } = kind == EngineKind.Sqlite ? SqlitePatience : ServerPatience;

    /// <summary>True when every unit met all the others before writing: the units really raced.</summary>
    public bool AllMet => Volatile.Read(ref met) == parties;

    /// <summary>What a test that needed the race reports when the units did not meet.</summary>
    public string Missed => $"The units of {kind} did not meet within {Patience.TotalSeconds} s: {Volatile.Read(ref arrived)} of {parties} arrived, {Volatile.Read(ref met)} met.";

    /// <summary>Arrives at the meeting point (only the first attempt of a unit counts) and waits for the others.</summary>
    public async Task ArriveAsync(int attempt)
    {
        if (attempt > 1)
            return;
        if (Interlocked.Increment(ref arrived) == parties)
            all.TrySetResult();
        try
        {
            await all.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
            Interlocked.Increment(ref met);
        }
        catch (TimeoutException)
        {
            // The engine serialises the units: this one runs alone.
        }
    }
}
