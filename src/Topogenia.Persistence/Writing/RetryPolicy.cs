// RetryPolicy.cs
// New attempts of a whole unit on a deadlock or a busy database: 3 attempts, after 50 then 100 ms (spec 4.3-5, 1.x `BaseDeDonnees.cs:30`).

using Topogenia.Persistence.Errors;

namespace Topogenia.Persistence.Writing;

/// <summary>One failed attempt about to be retried: its number (from 1), the failure of the engine and the wait before the next.</summary>
public sealed record RetryAttempt(int Attempt, StoreError Error, TimeSpan Delay);

/// <summary>
/// How many times a unit runs and how long it waits between two attempts. The default is the one of spec 4.3-5 and of
/// the 1.x (`Task.Delay(50 * essai)`): 3 attempts, 50 ms after the first failure, 100 ms after the second. The wait and
/// the observer of the attempts can be replaced (tests).
/// </summary>
public sealed class RetryPolicy
{
    private readonly Func<TimeSpan, CancellationToken, Task> wait;
    private readonly Action<RetryAttempt>? retrying;

    public RetryPolicy(int attempts, IReadOnlyList<TimeSpan> delays, Func<TimeSpan, CancellationToken, Task>? wait = null, Action<RetryAttempt>? retrying = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        ArgumentNullException.ThrowIfNull(delays);
        if (attempts > 1 && delays.Count == 0)
            throw new ArgumentException("A policy that retries needs at least one delay.", nameof(delays));
        Attempts = attempts;
        Delays = delays;
        this.wait = wait ?? Task.Delay;
        this.retrying = retrying;
    }

    /// <summary>3 attempts, 50 then 100 ms (spec 4.3-5).</summary>
    public static RetryPolicy Default { get; } = Standard();

    public int Attempts { get; }

    /// <summary>The wait after the first failure, after the second...; the last one repeats if there are more attempts.</summary>
    public IReadOnlyList<TimeSpan> Delays { get; }

    /// <summary>The policy of spec 4.3-5 with another wait or another observer of the attempts.</summary>
    public static RetryPolicy Standard(Func<TimeSpan, CancellationToken, Task>? wait = null, Action<RetryAttempt>? retrying = null) =>
        new(3, [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100)], wait, retrying);

    /// <summary>
    /// Runs <paramref name="attempt"/>; on a transient failure of the engine (deadlock, busy), calls <paramref name="reset"/>,
    /// waits and runs it again, at most <see cref="Attempts"/> times; the last failure, or any other failure, is thrown as it is.
    /// </summary>
    internal async Task<T> RunAsync<T>(Func<Exception, StoreError?> classify, Func<Task<T>> attempt, Func<ValueTask> reset, CancellationToken cancellationToken)
    {
        for (var number = 1; ; number++)
        {
            try
            {
                return await attempt();
            }
            catch (Exception failure) when (number < Attempts && classify(failure) is { IsTransient: true } error)
            {
                await reset();
                var delay = Delays[Math.Min(number - 1, Delays.Count - 1)];
                retrying?.Invoke(new RetryAttempt(number, error, delay));
                await wait(delay, cancellationToken);
            }
        }
    }
}
