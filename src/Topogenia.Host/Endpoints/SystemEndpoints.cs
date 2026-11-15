// SystemEndpoints.cs
// The HTTP entry points of the host itself: health, anonymous and minimal in server mode, the command « Quit » and the sample project of the desktop mode (annex A 1, spec 5.2, 5.3, 6.1).

using Microsoft.Extensions.Options;
using Topogenia.Host.Api.Exchange;
using Topogenia.Host.Api.Health;
using Topogenia.Host.Composition;
using Topogenia.Host.Startup;

namespace Topogenia.Host.Endpoints;

/// <summary>
/// Each route reads the request and calls its handler (spec 3.2). The health is the only anonymous route, in server mode
/// only (spec 5.3, A-4-03): an anonymous caller gets `{status}` alone, an authenticated one the version, the state of the
/// database and the time; in desktop mode it needs the session cookie like any route (AR-46).
/// </summary>
public static class SystemEndpoints
{
    public static void MapSystemRoutes(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        var health = routes.MapGet("/api/health", async Task<IResult> (HttpContext context, HealthApi api, CancellationToken token) =>
        {
            var health = await api.ReadAsync(token);
            var status = health.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
            return context.User.Identity?.IsAuthenticated == true
                ? TypedResults.Json(health, statusCode: status)
                : TypedResults.Json(new HealthStatusReply(health.Status), statusCode: status);
        }).WithTags("System").Produces<HealthReply>().Produces<HealthStatusReply>(StatusCodes.Status503ServiceUnavailable);
        if (routes.ServiceProvider.GetRequiredService<IOptions<ProductOptions>>().Value.Mode == HostMode.Server)
            health.AllowAnonymous();
        routes.MapPost("/api/desktop/quit", (IHostApplicationLifetime lifetime, IOptions<ProductOptions> options) =>
        {
            DesktopQuit.Stop(lifetime, options);
            return TypedResults.Accepted((string?)null);
        }).WithTags("System");
        routes.MapPost("/api/desktop/sample-project", async (SampleProjectApi api, CancellationToken token) =>
        {
            var imported = await api.CreateAsync(token);
            return TypedResults.Created($"/api/projects/{imported.Id}", imported);
        }).WithTags("System");
    }
}
