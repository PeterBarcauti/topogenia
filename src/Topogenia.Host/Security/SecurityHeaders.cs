// SecurityHeaders.cs
// The security headers of every reply of both modes: CSP, nosniff, no referrer, permissions, isolation of the window, no framing, no cache of the API (spec 5.4).

namespace Topogenia.Host.Security;

/// <summary>
/// Adds the headers of spec 5.4 to every reply, errors and redirections included (they are added when the reply starts,
/// after the error middleware has rewritten it):
/// <list type="bullet">
/// <item>`Content-Security-Policy` (<see cref="Policy"/>): everything from the application only, no inline script nor
/// `eval`, images also from `data:` and `blob:` for the PNG and PDF exports, no plugin, no base, forms to the application
/// only, never framed (A-4-15, A-4-16);</item>
/// <item>`X-Frame-Options: DENY` for the browsers that ignore `frame-ancestors` (the 1.x header, A-4-15);</item>
/// <item>`X-Content-Type-Options: nosniff` (A-4-14), `Referrer-Policy: no-referrer` (A-4-18);</item>
/// <item>`Permissions-Policy`: no camera, microphone, location, payment or device access (<see cref="Permissions"/>);</item>
/// <item>`Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Resource-Policy: same-origin`;</item>
/// <item>`Cache-Control: no-store` on a reply of the API that sets no rule of its own: no browser nor proxy keeps the data
/// of a project (ASVS V8.2.1, F-36); the files of the interface keep the rules of their static assets.</item>
/// </list>
/// </summary>
public sealed class SecurityHeaders(RequestDelegate next)
{
    public const string Policy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; "
        + "object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>The value of `X-Frame-Options`: no page may frame the application.</summary>
    public const string FrameOptions = "DENY";

    /// <summary>The cache rule of a reply of the API: kept nowhere.</summary>
    public const string NoStore = "no-store";

    public const string Permissions =
        "accelerometer=(), bluetooth=(), camera=(), display-capture=(), geolocation=(), gyroscope=(), hid=(), magnetometer=(), "
        + "microphone=(), midi=(), payment=(), serial=(), usb=()";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = Policy;
            headers.XFrameOptions = FrameOptions;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = Permissions;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(headers.CacheControl))
                headers.CacheControl = NoStore;
            return Task.CompletedTask;
        });
        return next(context);
    }
}
