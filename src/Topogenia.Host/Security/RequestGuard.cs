// RequestGuard.cs
// The checks of each request before anything else reads it: Host, then on a write the anti-CSRF header, Origin and Sec-Fetch-Site (spec 5.2, 5.3).

using Topogenia.Domain.Errors;

namespace Topogenia.Host.Security;

/// <summary>What a mode answers to: the names it accepts in `Host`, and the origin of the application in the browser.</summary>
public interface IRequestAuthority
{
    /// <summary>True when the request names the application: `127.0.0.1:port` in desktop mode, a name of the list in server mode.</summary>
    bool Accepts(HostString host);

    /// <summary>The origin a page of the application sends with its writes.</summary>
    string OriginOf(HttpRequest request);
}

/// <summary>
/// The checks common to both modes, before the authentication and before anything reads the body:
/// <list type="bullet">
/// <item>`Host` must name the application (<see cref="IRequestAuthority"/>), else REQUETE_INVALIDE (A-4-19);</item>
/// <item>a write (any method but GET, HEAD, OPTIONS) must carry the custom header <see cref="CsrfHeader"/>, else
/// EN_TETE_MANQUANT (A-4-10): a page of another site cannot add it without a preflight, which no CORS allows (A-4-12);
/// the reads are not concerned (A-4-13);</item>
/// <item>on a write, an `Origin` other than the one of the application, or a `Sec-Fetch-Site` other than `same-origin`
/// or `none`, is refused, ACCES_REFUSE (A-4-11). A write without these two headers comes from no browser page (a
/// browser sends them on every write): it still needs the custom header and the authentication.</item>
/// </list>
/// </summary>
public sealed class RequestGuard(RequestDelegate next)
{
    /// <summary>The anti-CSRF header of the API (1.x `X-Ariane`, AR-13); any value.</summary>
    public const string CsrfHeader = "X-Requested-With";

    private static readonly string[] SameSite = ["same-origin", "none"];

    public Task InvokeAsync(HttpContext context, IRequestAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authority);
        var request = context.Request;
        if (!authority.Accepts(request.Host))
            throw new DomainException(ErrorCodes.InvalidRequest, RefusalKind.Invalid);
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method))
        {
            if (string.IsNullOrWhiteSpace(request.Headers[CsrfHeader]))
                throw new DomainException(ErrorCodes.MissingHeader, RefusalKind.Invalid);
            var origin = request.Headers.Origin.ToString();
            var site = request.Headers["Sec-Fetch-Site"].ToString();
            if ((origin.Length > 0 && !string.Equals(origin, authority.OriginOf(request), StringComparison.OrdinalIgnoreCase))
                || (site.Length > 0 && !SameSite.Contains(site, StringComparer.OrdinalIgnoreCase)))
                throw DomainException.Forbidden();
        }
        return next(context);
    }
}
