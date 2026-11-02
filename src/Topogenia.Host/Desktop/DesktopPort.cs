// DesktopPort.cs
// The port of the desktop mode: the one configured, else a port stable for the account and its data file, else a free one chosen by the system (spec 5.2, ADR 089).

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Topogenia.Host.Composition;

namespace Topogenia.Host.Desktop;

/// <summary>
/// The port the desktop mode listens on. A port configured is kept as it is (a port taken stops the start). Otherwise a
/// port computed from the account and the data file, the same at each start: the browser keeps what it stores for an
/// origin (scheme, host, port), the display preferences and the width of the side panels, which a port changing at each
/// start forgot (audit 3.2). Two data files of an account, or two accounts of a shared machine, get two ports. When that
/// port is taken by another program, or refused by Windows (a range it keeps, for Hyper-V for instance), a free port
/// chosen by the system, as before: the run works, only that origin is new.
/// </summary>
public static class DesktopPort
{
    /// <summary>The first port of the range: under the ports Windows gives to the connections it opens (49152 and up).</summary>
    public const int First = 41_000;

    /// <summary>The number of ports of the range.</summary>
    public const int Range = 4_000;

    /// <summary>The port to listen on: the one configured, else the stable one when it is free, else 0 (a free port chosen at the start).</summary>
    public static int Of(DesktopOptions options, string dataFile, string account, Func<int, bool>? isFree = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Port != 0)
            return options.Port;
        var stable = StableFor(dataFile, account);
        return (isFree ?? IsFree)(stable) ? stable : 0;
    }

    /// <summary>The stable port of an account and a data file: a hash of both, in the range; the case of the path ignored, as Windows does.</summary>
    public static int StableFor(string dataFile, string account)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFile);
        ArgumentNullException.ThrowIfNull(account);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{account}\n{dataFile.ToUpperInvariant()}"));
        return First + (int)(BitConverter.ToUInt32(hash, 0) % Range);
    }

    /// <summary>The account of the process: the SID of its token on Windows, else the name of the user.</summary>
    public static string AccountOfProcess()
    {
        if (!OperatingSystem.IsWindows())
            return Environment.UserName;
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? Environment.UserName;
    }

    /// <summary>
    /// Whether no program listens on the port of the loopback and Windows lets it be taken: a listener of the loopback, as
    /// the host is, exclusive, stopped at once (it connects to nothing, D14).
    /// </summary>
    private static bool IsFree(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port) { ExclusiveAddressUse = true };
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener.Stop();
        }
    }
}
