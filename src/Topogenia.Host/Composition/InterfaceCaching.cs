// InterfaceCaching.cs
// The rule of the browser cache for the files of the interface (F-116): the page, the help and every file whose name stays the same from a version to the next are asked again at each opening; the files of the build, whose names carry the print of their content, are kept.

using Microsoft.AspNetCore.StaticFiles;

namespace Topogenia.Host.Composition;

/// <summary>
/// The desktop mode listens on the same port from a version to the next (ADR 089): a page kept by the browser would show
/// the interface of the version before an update, talking to the new server, until the browser decides to ask again. The
/// page of the application (`index.html`, also sent for any address of the interface), the help (`/aide/`) and the other
/// files whose names stay the same are sent with `Cache-Control: no-cache`: the browser asks again at each opening and
/// gets a 304 without the body while the file has not changed (ETag, Last-Modified), so the opening stays fast. The files
/// of `/assets/`, built by Vite with the print of their content in their names, never change under a name: kept a year,
/// and private, since they are served to an authenticated user only and no shared cache may keep them.
/// </summary>
public static class InterfaceCaching
{
    /// <summary>The rule of a file whose name stays the same: asked again at each use.</summary>
    public const string Revalidate = "no-cache";

    /// <summary>The rule of a file of the build, its name printed from its content.</summary>
    public const string Kept = "private, max-age=31536000, immutable";

    /// <summary>The options of the static files and of the page sent for any address of the interface.</summary>
    public static StaticFileOptions Options() => new() { OnPrepareResponse = Prepare };

    private static void Prepare(StaticFileResponseContext context) =>
        context.Context.Response.Headers.CacheControl =
            context.Context.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase) ? Kept : Revalidate;
}
