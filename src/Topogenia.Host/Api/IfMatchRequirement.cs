// IfMatchRequirement.cs
// The If-Match required in server mode on every route that reads it: a request without it is refused 428 VERSION_REQUISE (spec 5.3, A-2.16-04).

using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Topogenia.Application.Ports;
using Topogenia.Host.Composition;

namespace Topogenia.Host.Api;

/// <summary>
/// A filter of the routes: a route whose handler reads `If-Match` changes a versioned record (A-2.16-05), and in server
/// mode a request without the header is marked as lacking it (<see cref="RequestVersions"/>); the template of the
/// command refuses it 428 VERSION_REQUISE once its rights are checked (spec 5.3, weakness 1.x 5.6-3). In desktop mode
/// the header stays optional, as in the 1.x (AR-07). The routes are found by their parameter: a route of the API that
/// reads the header needs no other declaration. That a write route reads it is not deduced: a test requires every write
/// route to read it or to be exempted with its reason (ADR 045, revised). A client that read no record (a sheet not saved
/// yet, a document not used yet) sends `0` (ADR 045).
/// </summary>
public static class IfMatchRequirement
{
    /// <summary>The name of the header, as the routes read it.</summary>
    public const string Header = "If-Match";

    /// <summary>True when the handler reads `If-Match`.</summary>
    public static bool ReadsIfMatch(MethodInfo handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return handler.GetParameters().Any(parameter => parameter.GetCustomAttribute<FromHeaderAttribute>()?.Name == Header);
    }

    public static EndpointFilterDelegate Filter(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!ReadsIfMatch(context.MethodInfo)
            || context.ApplicationServices.GetRequiredService<IOptions<ProductOptions>>().Value.Mode != HostMode.Server)
            return next;
        return invocation =>
        {
            var http = invocation.HttpContext;
            http.RequestServices.GetRequiredService<RequestVersions>().IsMissing = string.IsNullOrWhiteSpace(http.Request.Headers.IfMatch);
            return next(invocation);
        };
    }
}

/// <summary>Whether the request lacks the If-Match its route requires in server mode (scoped, set by <see cref="IfMatchRequirement"/>).</summary>
public sealed class RequestVersions : IVersionRequirement
{
    public bool IsMissing { get; set; }
}
