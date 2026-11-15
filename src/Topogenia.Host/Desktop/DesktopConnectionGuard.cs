// DesktopConnectionGuard.cs
// The connections of the desktop mode kept for the processes of the account of the application, any other closed before its first byte is read (spec 5.2, ADR 075).

using System.Net;
using System.Security.Principal;
using Microsoft.AspNetCore.Connections;

namespace Topogenia.Host.Desktop;

/// <summary>The process at the client end of a connection, and its account: the SID of its token (`S-1-5-21-...`).</summary>
public sealed record ConnectionOwner(int ProcessId, string Account);

/// <summary>Tells which process opened a connection received on the loopback (<see cref="WindowsConnectionOwners"/>).</summary>
public interface IConnectionOwners
{
    /// <summary>
    /// The owner of the client end <paramref name="client"/> of the connection received at <paramref name="server"/>; null
    /// when it cannot be told (no such connection, a process gone, or one this account may not query): never a guess.
    /// </summary>
    ConnectionOwner? OwnerOf(IPEndPoint server, IPEndPoint client);
}

/// <summary>
/// The first middleware of each connection of the desktop mode, before Kestrel reads a byte of HTTP: a connection whose
/// client process runs under the account of the application goes on; any other (a process of another account of the
/// machine, such as another user of a shared remote desktop server, or one whose account cannot be told) is closed
/// without an answer and logged as a warning, with no login, account or process number. The session cookie alone is no
/// longer enough: a cookie is not tied to a port (RFC 6265, 8.5), so the browser also sends it to any other server of
/// `127.0.0.1`, and another account that replays it reaches nothing (security M-1). The check is made once per
/// connection: what a connection carries afterwards comes from the process that opened it.
/// </summary>
public sealed partial class DesktopConnectionGuard(IConnectionOwners owners, ILogger<DesktopConnectionGuard> logger)
{
    private const string Refused = "The client process of the connection does not run under the account of the application.";

    /// <summary>The account of the application, the SID of the token of the process; none elsewhere than on Windows.</summary>
    private readonly string? account = AccountOfProcess();

    /// <summary>The middleware of the connections, for `ListenOptions.Use`.</summary>
    public ConnectionDelegate Use(ConnectionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return connection =>
        {
            if (Admits(connection.LocalEndPoint as IPEndPoint, connection.RemoteEndPoint as IPEndPoint))
                return next(connection);
            LogRefused(logger);
            connection.Abort(new ConnectionAbortedException(Refused));
            return Task.CompletedTask;
        };
    }

    /// <summary>True only for a connection whose client process is known and runs under the account of the application.</summary>
    public bool Admits(IPEndPoint? server, IPEndPoint? client) =>
        server is not null && client is not null && account is not null
        && owners.OwnerOf(server, client) is { } owner && string.Equals(owner.Account, account, StringComparison.Ordinal);

    private static string? AccountOfProcess()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A connection is closed unread: its client process runs under another account, or its account cannot be read.")]
    private static partial void LogRefused(ILogger logger);
}
