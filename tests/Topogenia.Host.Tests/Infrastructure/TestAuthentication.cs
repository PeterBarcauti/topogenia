// TestAuthentication.cs
// The authentication of the tests of the API: the login named by a header, or nobody (the modes bring their own, P4-T05 and P4-T06).

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Topogenia.Host.Tests.Infrastructure;

/// <summary>
/// Authenticates the login written in <see cref="Header"/>, as Negotiate or the session of the Desktop mode would; a
/// request without the header stays anonymous, so that the API refuses it itself.
/// </summary>
public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Name = "Test";

    public const string Header = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var login) || string.IsNullOrEmpty(login))
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, login.ToString())], Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
    }
}
