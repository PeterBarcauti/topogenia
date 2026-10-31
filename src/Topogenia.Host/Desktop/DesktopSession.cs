// DesktopSession.cs
// The launch token of the desktop mode, exchanged once for the session cookie, and the check of that cookie (spec 5.2, ADR 010).

using System.Security.Cryptography;
using System.Text;

namespace Topogenia.Host.Desktop;

/// <summary>
/// The secrets of one run of the desktop mode, new at each start (spec 5.2): the launch token (256 random bits) that the
/// browser receives once, in the address it is opened on, and the session secret (256 other random bits) that the
/// exchange puts in an `HttpOnly`, `SameSite=Strict` cookie. The token is spent by its first exchange: an address read
/// later (history, another user of the machine) opens nothing. Every comparison takes the same time whatever the value.
/// </summary>
public sealed class DesktopSession
{
    /// <summary>The name of the session cookie (no product name, D13).</summary>
    public const string CookieName = "desktop_session";

    /// <summary>The path of the exchange, and the name of its parameter.</summary>
    public const string LaunchPath = "/launch";

    public const string TokenParameter = "token";

    private const int SecretBytes = 32;

    private readonly Lock gate = new();
    private readonly string sessionSecret = NewSecret();
    private string? launchToken = NewSecret();

    /// <summary>The token of the address the browser is opened on, until it is spent; null afterwards.</summary>
    public string? LaunchToken
    {
        get
        {
            lock (gate)
                return launchToken;
        }
    }

    /// <summary>The session secret for a valid launch token, which is spent; null for any other token.</summary>
    public string? Exchange(string? token)
    {
        lock (gate)
        {
            if (launchToken is null || token is null || !SameText(token, launchToken))
                return null;
            launchToken = null;
            return sessionSecret;
        }
    }

    /// <summary>
    /// A new launch token for a second start of the application, which opens the browser of the user on this run (spec
    /// 6.1); the token before it, spent or not, opens nothing any more. The session secret stays: a tab already signed in
    /// keeps working.
    /// </summary>
    public string Renew()
    {
        lock (gate)
            return launchToken = NewSecret();
    }

    /// <summary>True for the cookie of this run.</summary>
    public bool IsSession(string? cookie) => cookie is not null && SameText(cookie, sessionSecret);

    private static bool SameText(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
