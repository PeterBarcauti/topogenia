// TransientFailures.cs
// The passing failures of a SQL Server instance under the load of other runs, which only the setup of the tests retries, bounded.

using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Topogenia.Application.Ports;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// What the load of several runs on one instance makes fail for a moment, never a fault of the code under test: the
/// exclusive lock of `model` that every creation of a base takes (1807, reported with 1802 « the creation failed »),
/// a base not open yet right after its creation (4060), a statement or a connection that waits beyond its delay (-2),
/// a creation or a removal chosen as the victim of a deadlock on the system tables (1205), a base still in use at its
/// removal (3702), the Query Store of a base that fails to write while the removal takes the base for itself (12408),
/// and a connection cut by the server (233, 10053, 10054, 64, 121). These numbers are read in every error of every SQL
/// Server exception of the chain (the provider wraps them, EF Core too), and in the refusal that the migrations make of
/// them: <see cref="Topogenia.Persistence.Schema.SchemaMigrations"/> turns a contention into
/// <c>SchemaRefusedException(BASE_OCCUPEE)</c> whose inner <see cref="EngineFailureException"/> keeps only the numbers.
/// Those numbers are read for <c>BASE_OCCUPEE</c> only, never under another refusal (`VERROU_MIGRATIONS`, a refused
/// right, an unreachable database): the code says what the failure is. 1802 alone is not passing: without 1807 it
/// stands for a real refusal (rights, disk, path) and is never retried. Only the creation, the opening and the removal
/// of the test bases are retried, never a statement of a test (<see cref="TestServer"/>). The retries nest (a
/// preparation creates, opens and removes its base): they all share the <see cref="Budget"/> of the outermost one, and
/// none starts once it is spent (ADR 040).
/// </summary>
public static class TransientFailures
{
    public const int Timeout = -2;
    public const int ModelLocked = 1807;
    public const int CreationFailed = 1802;
    public const int CannotOpen = 4060;
    public const int Deadlock = 1205;
    public const int InUse = 3702;
    public const int QueryStoreFailed = 12408;
    public const int SingleUser = 924;

    /// <summary>Attempts of one step of the setup at most: the first one and seven more.</summary>
    public const int Attempts = 8;

    private static readonly int[] Cut = [233, 10053, 10054, 64, 121];

    /// <summary>
    /// The time from the start of the outermost retried step after which no attempt starts any more, however nested the
    /// retries: the delay of the migrations (ADR 041). A preparation lasts at most this budget and its last attempt.
    /// </summary>
    public static readonly TimeSpan Budget = TimeSpan.FromMinutes(10);

    private static readonly AsyncLocal<Deadline?> deadline = new();

    /// <summary>The wait before the attempt after <paramref name="attempt"/>: 250 ms doubled each time, 8 s at most (23.75 s in all).</summary>
    public static TimeSpan DelayAfter(int attempt) => TimeSpan.FromMilliseconds(Math.Min(250 * (1 << Math.Min(attempt - 1, 10)), 8000));

    /// <summary>A creation of a base that failed for a moment (the lock of `model`, a delay, a deadlock, a cut connection).</summary>
    public static bool OfCreation(Exception failure) => OfCreation(Numbers(failure));

    public static bool OfCreation(IEnumerable<int> numbers) => numbers.Any(number => number is ModelLocked or Timeout or Deadlock || Cut.Contains(number));

    /// <summary>An opening of a base just created that failed for a moment (not open yet, a delay, a cut connection).</summary>
    public static bool OfOpening(Exception failure) => OfOpening(Numbers(failure));

    public static bool OfOpening(IEnumerable<int> numbers) => numbers.Any(number => number is CannotOpen or Timeout || Cut.Contains(number));

    /// <summary>A removal of a base that failed for a moment (still in use, the lock of `model`, a delay, a deadlock, the Query Store, a cut connection).</summary>
    public static bool OfRemoval(Exception failure) => OfRemoval(Numbers(failure));

    public static bool OfRemoval(IEnumerable<int> numbers) =>
        numbers.Any(number => number is InUse or ModelLocked or Timeout or Deadlock or QueryStoreFailed || Cut.Contains(number));

    /// <summary>A read of the bases of the instance (their list, the opening that tells whether one exists) that failed for a moment: a delay, a cut connection.</summary>
    public static bool OfReading(Exception failure) => OfReading(Numbers(failure));

    public static bool OfReading(IEnumerable<int> numbers) => numbers.Any(number => number == Timeout || Cut.Contains(number));

    /// <summary>
    /// A failure without any answer of the server: every number is a connection cut by the server, none is an error that
    /// the server gave. A test that proves the answer of the server to an opening may open again on it (ADR 040, revision).
    /// </summary>
    public static bool Unanswered(Exception failure) => Numbers(failure) is { Count: > 0 } numbers && numbers.All(Cut.Contains);

    /// <summary>A preparation of a new base (its creation, its first opening, its migration) that failed for a moment.</summary>
    public static bool OfSetup(Exception failure) => OfCreation(failure) || OfOpening(failure);

    /// <summary>
    /// Every number of every SQL Server error carried by the exception, its inner exceptions and its aggregated ones, and
    /// those that a refusal <c>BASE_OCCUPEE</c> of the migrations keeps (<see cref="EngineFailureException"/>); nothing
    /// under any other refusal of the schema.
    /// </summary>
    public static IReadOnlyList<int> Numbers(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var numbers = new List<int>();
        var pending = new Stack<Exception>([failure]);
        while (pending.TryPop(out var current))
        {
            if (current is SqlException sql)
                numbers.AddRange(sql.Errors.Cast<SqlError>().Select(error => error.Number).Append(sql.Number));
            if (current is SchemaRefusedException refusal)
            {
                if (refusal.Code != SchemaCodes.DatabaseBusy)
                    continue;
                if (refusal.InnerException is EngineFailureException engine)
                    numbers.AddRange(engine.Numbers);
            }
            if (current is AggregateException aggregate)
                aggregate.InnerExceptions.ToList().ForEach(pending.Push);
            else if (current.InnerException is { } inner)
                pending.Push(inner);
        }
        return numbers;
    }

    /// <summary>
    /// Runs <paramref name="step"/> (given the number of its attempt, from 1) until it succeeds; a failure that
    /// <paramref name="passing"/> accepts is tried again after <see cref="DelayAfter"/>, <see cref="Attempts"/> times at most
    /// and within the <see cref="Budget"/>; any other failure, and the last one, is thrown as it is.
    /// </summary>
    public static Task RetriedAsync(Func<Exception, bool> passing, Func<int, Task> step, CancellationToken cancellationToken) =>
        RetriedAsync(passing, step, Budget, cancellationToken);

    /// <summary>The same with the given budget when no retried step around it has opened one (the tests of the budget).</summary>
    internal static Task RetriedAsync(Func<Exception, bool> passing, Func<int, Task> step, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(passing);
        ArgumentNullException.ThrowIfNull(step);
        return WithinBudgetAsync(budget, async () =>
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await step(attempt);
                    return true;
                }
                catch (Exception failure) when (MayRetry(attempt) && passing(failure))
                {
                    await Task.Delay(DelayAfter(attempt), cancellationToken);
                }
            }
        });
    }

    /// <summary>
    /// Runs <paramref name="run"/> within the budget of the retried step around it, or within <paramref name="budget"/>
    /// from now when there is none: every retry inside shares it.
    /// </summary>
    public static async Task<T> WithinBudgetAsync<T>(TimeSpan budget, Func<Task<T>> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        // Set in this method, the deadline flows to what it calls and never back to its caller.
        deadline.Value ??= new Deadline(Stopwatch.GetTimestamp(), budget);
        return await run();
    }

    /// <summary>True when another attempt may start after <paramref name="attempt"/>: fewer than <see cref="Attempts"/>, and its wait ends within the budget.</summary>
    public static bool MayRetry(int attempt) => attempt < Attempts && !(deadline.Value?.Spent(DelayAfter(attempt)) ?? false);

    /// <summary>The start of the outermost retried step and its budget.</summary>
    private sealed record Deadline(long Start, TimeSpan Budget)
    {
        public bool Spent(TimeSpan wait) => Stopwatch.GetElapsedTime(Start) + wait > Budget;
    }
}
