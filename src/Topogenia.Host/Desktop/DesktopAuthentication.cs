// DesktopAuthentication.cs
// The user of the desktop mode: the Windows account of the process, authenticated by the session cookie only (spec 5.2).

using System.Security.Claims;
using System.Security.Principal;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;
using Topogenia.Host.Users;

namespace Topogenia.Host.Desktop;

/// <summary>The account the process runs under (spec 5.2: `WindowsIdentity.GetCurrent()`), the only user of the desktop mode.</summary>
public interface IProcessAccount
{
    /// <summary>The login `DOMAIN\account` of the process.</summary>
    string Login { get; }
}

/// <summary>The Windows account of the process; elsewhere, the user and domain names of the environment.</summary>
public sealed class ProcessAccount : IProcessAccount
{
    public string Login { get; } = OperatingSystem.IsWindows()
        ? WindowsIdentity.GetCurrent().Name
        : $@"{Environment.UserDomainName}\{Environment.UserName}";
}

/// <summary>
/// The scheme of the desktop mode: a request that carries the session cookie of this run is the account of the process;
/// any other request is anonymous and the fallback policy answers 401, the health included (AR-46). Nothing else
/// authenticates: no header, no parameter (AR-17).
/// </summary>
public sealed class DesktopAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, DesktopSession session, IProcessAccount account)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Name = "Desktop";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!session.IsSession(Request.Cookies[DesktopSession.CookieName]))
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, account.Login)], Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
    }
}

/// <summary>
/// Exchanges the launch token of `GET /launch?token=` for the session cookie, then sends the browser to the application
/// without the token in its address (spec 5.2). A token that is wrong or already spent is a 401, and no cookie.
/// </summary>
public sealed class LaunchMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, DesktopSession session)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);
        if (!HttpMethods.IsGet(context.Request.Method) || !context.Request.Path.Equals(DesktopSession.LaunchPath, StringComparison.Ordinal))
        {
            await next(context);
            return;
        }
        var secret = session.Exchange(context.Request.Query[DesktopSession.TokenParameter].ToString());
        if (secret is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        context.Response.Cookies.Append(DesktopSession.CookieName, secret, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
        context.Response.Redirect("/");
    }
}

/// <summary>
/// The administrators of the desktop mode: the account of the process, administrator of his file (spec 5.2), plus those of
/// the configuration, read again at each change of the file.
/// </summary>
public sealed class DesktopAdministrators(IOptionsMonitor<ProductOptions> options, IProcessAccount account) : IAdministrators
{
    private readonly ConfiguredAdministrators configured = new(options);

    public IReadOnlyCollection<string> Logins => [account.Login, .. configured.Logins];
}
