// HttpsOnly.cs
// The server mode answers over HTTPS only: a request that reaches it in plain HTTP is refused HTTPS_EXIGE (spec 5.3).

using Topogenia.Domain.Errors;

namespace Topogenia.Host.Security;

/// <summary>
/// After the redirection to HTTPS: ASP.NET Core redirects only when it knows the HTTPS port, and lets the request through
/// otherwise (a server configured with an HTTP address only). The server mode never serves such a request, the anonymous
/// health included: 403 HTTPS_EXIGE, before the authentication, so that no credentials nor data cross in clear (spec 5.1:
/// secure by default, 5.3: HTTPS only). Behind IIS, HTTPS is the binding of the site: the request arrives as HTTPS.
/// </summary>
public sealed class HttpsOnly(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Request.IsHttps)
            throw new DomainException(ErrorCodes.HttpsRequired, RefusalKind.Forbidden);
        return next(context);
    }
}
