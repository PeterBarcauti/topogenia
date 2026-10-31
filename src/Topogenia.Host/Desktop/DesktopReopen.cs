// DesktopReopen.cs
// A second start of the desktop mode on a data file already in use: it signals the running application, by two named events of the session of the user, to open the browser on itself (spec 6.1).

using Microsoft.Extensions.Options;
using Topogenia.Host.Composition;
using Topogenia.Host.Startup;

namespace Topogenia.Host.Desktop;

/// <summary>
/// The request of a second start, and its answer: two named events of the session of the user (`Local\`, the names of
/// <see cref="DesktopInstance.NameOf"/> for the data file, no path in them), « open » set by the second start and
/// « opened » set back by the running application. Nothing passes but the two signals, and an event never leaves the
/// machine: the running application opens the browser on its own address, with a new launch token, and nothing else can
/// be asked of it.
/// </summary>
public static class DesktopReopen
{
    /// <summary>The longest a second start waits for the running application before it refuses (INSTANCE_DEJA_LANCEE).</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    /// <summary>The name of the event of the request for a data file.</summary>
    public static string RequestName(string dataFile) => DesktopInstance.NameOf(dataFile) + "-open";

    /// <summary>The name of the event of the answer for a data file.</summary>
    public static string AnswerName(string dataFile) => DesktopInstance.NameOf(dataFile) + "-opened";

    /// <summary>Asks the application running on the data file to open itself; false when none listens or answers in time.</summary>
    public static bool Ask(string dataFile, CancellationToken cancellationToken)
    {
        // The desktop mode runs on Windows (spec 6.1); elsewhere no second start is answered.
        if (!OperatingSystem.IsWindows() || !EventWaitHandle.TryOpenExisting(RequestName(dataFile), out var request))
            return false;
        using (request)
        {
            if (!EventWaitHandle.TryOpenExisting(AnswerName(dataFile), out var answer))
                return false;
            using (answer)
            {
                answer.Reset();
                request.Set();
                return WaitHandle.WaitAny([answer, cancellationToken.WaitHandle], Wait) == 0;
            }
        }
    }
}

/// <summary>
/// In desktop mode, once the application listens: it answers each second start on its data file with a new launch token
/// and the browser opened on it, unless the configuration says not to open the browser (then the token is only renewed),
/// and sets the answer back.
/// </summary>
public sealed class DesktopReopenService(IHostApplicationLifetime lifetime, IOptions<ProductOptions> options, DesktopPaths paths,
    DesktopAddress address, DesktopSession session, IBrowserLauncher browser, StartupConsole console) : IHostedService, IDisposable
{
    private readonly Lock gate = new();
    private EventWaitHandle? request;
    private EventWaitHandle? answer;
    private RegisteredWaitHandle? listening;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(Listen);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (gate)
        {
            listening?.Unregister(null);
            listening = null;
            request?.Dispose();
            answer?.Dispose();
            request = answer = null;
        }
    }

    private void Listen()
    {
        lock (gate)
        {
            request = new EventWaitHandle(false, EventResetMode.AutoReset, DesktopReopen.RequestName(paths.DataFile));
            answer = new EventWaitHandle(false, EventResetMode.ManualReset, DesktopReopen.AnswerName(paths.DataFile));
            listening = ThreadPool.RegisterWaitForSingleObject(request, (_, _) => Answer(), null, Timeout.Infinite, executeOnlyOnce: false);
        }
    }

    private void Answer()
    {
        var token = session.Renew();
        if (options.Value.Desktop.OpenBrowser)
            browser.Open(address.LaunchAddress(token));
        console.Write("REOUVERTE");
        lock (gate)
            answer?.Set();
    }
}
