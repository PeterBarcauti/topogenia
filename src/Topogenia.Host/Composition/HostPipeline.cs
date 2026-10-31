// HostPipeline.cs
// The order of the middlewares of a request: log, security headers, errors, checks of the request, size of the body, transport, launch, authentication, sign-in, authorisation, static files, routes.

using Microsoft.Extensions.Options;
using Topogenia.Host.Desktop;
using Topogenia.Host.Endpoints;
using Topogenia.Host.Errors;
using Topogenia.Host.Logging;
using Topogenia.Host.Security;
using Topogenia.Host.Users;

namespace Topogenia.Host.Composition;

/// <summary>
/// The log of the request comes first, so that it sees the final status (<see cref="RequestLog"/>, A-4-26); the security
/// headers next, added to every reply (<see cref="SecurityHeaders"/>, spec 5.4). The error middleware follows, so that
/// every failure, the checks and the sign-in included, becomes the JSON error of the API (A-1.1-04). `Host`, the
/// anti-CSRF header, `Origin` and `Sec-Fetch-Site` are checked before anything reads the request (<see cref="RequestGuard"/>,
/// spec 5.2, 5.3), then the size of the body is limited before anything reads it (<see cref="BodySizeLimit"/>, spec 5.4).
/// The server mode then sends HSTS, redirects HTTP to HTTPS and refuses a plain request it could not redirect (spec 5.3,
/// <see cref="HttpsOnly"/>); the desktop mode exchanges the launch token for
/// the session cookie (spec 5.2). The user is signed in once the authentication of the mode has named him. The static
/// files come after the authorisation, whose fallback policy applies to a request that no route takes: they are served
/// to an authenticated user only (spec 5.3, A-4-04).
/// </summary>
public static class HostPipeline
{
    public static WebApplication UseTopogenia(this WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var desktop = application.Services.GetRequiredService<IOptions<ProductOptions>>().Value.Mode == HostMode.Desktop;
        application.UseMiddleware<RequestLog>();
        application.UseMiddleware<SecurityHeaders>();
        // HSTS before the error middleware and the guard of the requests: their refusals carry it too (F-05).
        if (!desktop)
            application.UseHsts();
        application.UseMiddleware<ErrorMiddleware>();
        application.UseMiddleware<RequestGuard>();
        application.UseMiddleware<BodySizeLimit>();
        if (desktop)
        {
            application.UseMiddleware<LaunchMiddleware>();
        }
        else
        {
            application.UseHttpsRedirection();
            application.UseMiddleware<HttpsOnly>();
        }
        application.UseAuthentication();
        application.UseMiddleware<SignInMiddleware>();
        application.UseAuthorization();
        application.UseStaticFiles();
        application.MapApi();
        return application;
    }
}
