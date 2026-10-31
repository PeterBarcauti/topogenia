// DesktopInstance.cs
// The named lock of a data file of the desktop mode: one process of the application at a time on it (ADR 029, ADR 050).

using System.Security.Cryptography;
using System.Text;

namespace Topogenia.Host.Desktop;

/// <summary>
/// A named semaphore of one token per data file, in the session of the user (`Local\`), named after the full path of the
/// file in lower case: the running application takes it at its start and keeps it until its stop; `--check` takes it
/// before opening the file and gives it back after. A process that ends gives it back with its handles, even killed. Unlike
/// the test of the file held open, it does not depend on the connections the application keeps: an idle application
/// releases its connections, not its lock (two processes on one file are a limit of ADR 029). Not tied to a thread, so
/// that an asynchronous start may take it and its stop give it back.
/// </summary>
public sealed class DesktopInstance : IDisposable
{
    private readonly Semaphore semaphore;
    private bool held;

    public DesktopInstance(DesktopPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        semaphore = new Semaphore(1, 1, NameOf(paths.DataFile));
    }

    /// <summary>The name of the lock of a data file: a hash of its full path, so that no path appears in the name.</summary>
    public static string NameOf(string dataFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFile);
        var path = Path.GetFullPath(dataFile).ToLowerInvariant();
        return @"Local\" + Application.ProductIdentity.Name + "-data-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..32];
    }

    /// <summary>Takes the lock without waiting; false when another process, or another run of this one, holds it.</summary>
    public bool TryAcquire()
    {
        if (!held)
            held = semaphore.WaitOne(0);
        return held;
    }

    /// <summary>Gives the lock back if it is held.</summary>
    public void Release()
    {
        if (!held)
            return;
        semaphore.Release();
        held = false;
    }

    public void Dispose()
    {
        Release();
        semaphore.Dispose();
    }
}
