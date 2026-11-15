// ModelLock.cs
// The creations of test bases of every process of the session, one at a time: a named mutex held by a thread of its own (ADR 040).

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>
/// A `CREATE DATABASE` takes the exclusive lock of `model`: several runs of the suites at once (the agents of a gate, the
/// CI) made each other fail with 1807 beyond the budget of the retries. Every process of the session now creates its
/// bases one at a time, through a named mutex (`Local\`): a process killed while it holds it leaves it abandoned, and the
/// next one takes it (a semaphore would stay taken). A mutex belongs to the thread that took it, and a creation awaits:
/// a thread of its own takes it, holds it until the creation ends, then gives it back.
/// </summary>
internal static class ModelLock
{
    private const string Name = @"Local\Topogenia-Test-CreateDatabase";

    /// <summary>Takes the lock, waiting as long as another creation of the session runs; dispose the result to give it back.</summary>
    public static Task<IDisposable> TakeAsync(CancellationToken cancellationToken)
    {
        var taken = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => Hold(taken, cancellationToken)) { IsBackground = true, Name = "Model lock of the tests" };
        thread.Start();
        return taken.Task;
    }

    private static void Hold(TaskCompletionSource<IDisposable> taken, CancellationToken cancellationToken)
    {
        using var mutex = new Mutex(false, Name);
        using var released = new ManualResetEventSlim();
        try
        {
            while (!mutex.WaitOne(TimeSpan.FromMilliseconds(100)))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    taken.TrySetCanceled(cancellationToken);
                    return;
                }
            }
        }
        catch (AbandonedMutexException)
        {
            // Taken: the process that held it ended without giving it back.
        }
        taken.TrySetResult(new Release(released));
        released.Wait(CancellationToken.None);
        mutex.ReleaseMutex();
    }

    private sealed class Release(ManualResetEventSlim released) : IDisposable
    {
        public void Dispose() => released.Set();
    }
}
