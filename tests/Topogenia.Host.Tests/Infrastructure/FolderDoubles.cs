// FolderDoubles.cs
// The doubles of « Open the folder » for the tests: a shell that records the folders it is asked to show and never opens an Explorer, a look that answers what it is told, a look that waits like a server that does not answer, a shell that waits like Windows kept waiting, a look and a shell that fail.

using Topogenia.Host.Desktop;

namespace Topogenia.Host.Tests.Infrastructure;

/// <summary>
/// Given to every host of the tests that runs the desktop mode, in the place of the shell of Windows (ADR 104): no test
/// opens a real Explorer. It answers <see cref="Reply"/>, shown by default.
/// </summary>
public sealed class FolderRecorder : IFolderShell
{
    private readonly List<string> shown = [];

    public FolderOutcome Reply { get; init; } = FolderOutcome.Shown;

    /// <summary>The folders asked, in their order, as they were given.</summary>
    public IReadOnlyList<string> Shown
    {
        get
        {
            lock (shown)
                return [.. shown];
        }
    }

    public FolderOutcome Show(string folder)
    {
        lock (shown)
            shown.Add(folder);
        return Reply;
    }
}

/// <summary>A look that finds the same thing under every path, without reaching the disk.</summary>
public sealed class FixedLook(FolderLook look) : IFolderDisk
{
    public FolderLook Look(string path) => look;
}

/// <summary>A look that fails otherwise than by an answer of Windows.</summary>
public sealed class FailingLook : IFolderDisk
{
    public FolderLook Look(string path) => throw new InvalidOperationException("The look failed.");
}

/// <summary>A shell that fails otherwise than by an answer of Windows.</summary>
public sealed class FailingShell : IFolderShell
{
    public FolderOutcome Show(string folder) => throw new InvalidOperationException("The shell failed.");
}

/// <summary>
/// A shell that does not answer: each call waits until the test lets it go, twenty seconds at most so that no test
/// hangs, then says the folder shown.
/// </summary>
public sealed class HeldShell : IFolderShell, IDisposable
{
    private readonly ManualResetEventSlim release = new(false);
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int asked;

    /// <summary>Waits until the shell is asked; a shell never asked fails the test instead of holding it.</summary>
    public Task WaitingAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

    /// <summary>The folders the shell was asked to show.</summary>
    public int Asked => Volatile.Read(ref asked);

    public FolderOutcome Show(string folder)
    {
        Interlocked.Increment(ref asked);
        entered.TrySetResult();
        release.Wait(TimeSpan.FromSeconds(20));
        return FolderOutcome.Shown;
    }

    /// <summary>Windows answers at last: the call waiting, and every later one, says the folder shown.</summary>
    public void Release() => release.Set();

    public void Dispose()
    {
        release.Set();
        release.Dispose();
    }
}

/// <summary>A server that does not answer: each look waits until the test lets it go, then finds a folder.</summary>
public sealed class HeldLook : IFolderDisk, IDisposable
{
    private readonly ManualResetEventSlim release = new(false);
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int looks;

    /// <summary>Waits until a look waits; a look that never starts fails the test instead of holding it.</summary>
    public Task WaitingAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

    /// <summary>The looks started.</summary>
    public int Looks => Volatile.Read(ref looks);

    public FolderLook Look(string path)
    {
        Interlocked.Increment(ref looks);
        entered.TrySetResult();
        release.Wait();
        return FolderLook.Folder;
    }

    /// <summary>The server answers at last: the looks waiting, and every later one, find a folder.</summary>
    public void Release() => release.Set();

    public void Dispose()
    {
        release.Set();
        release.Dispose();
    }
}
