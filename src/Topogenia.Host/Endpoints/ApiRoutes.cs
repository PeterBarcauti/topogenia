// ApiRoutes.cs
// Every HTTP entry point of the API, mapped once, the unknown routes under /api answered INTROUVABLE, any other route the page of the application (annex A 1, spec 3.6).

using Topogenia.Host.Api;

namespace Topogenia.Host.Endpoints;

/// <summary>
/// The table of the routes (`docs/couverture.md`, routes 1.x -> 2.0), one feature after the other; the routes that read
/// `If-Match` require it in server mode (<see cref="IfMatchRequirement"/>).
/// </summary>
public static class ApiRoutes
{
    public static void MapApi(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        var api = routes.MapGroup("");
        api.AddEndpointFilterFactory(IfMatchRequirement.Filter);
        api.MapProjectRoutes();
        api.MapMapRoutes();
        api.MapStepRoutes();
        api.MapSelectionRoutes();
        api.MapSheetRoutes();
        api.MapAdministrationRoutes();
        api.MapSystemRoutes();
        routes.MapOpenApi();
        routes.Map("/api/{**rest}", UnknownRoute.Refuse).ExcludeFromDescription();
        // A-1.5-22, as the 1.x: an address of the interface reloaded in the browser gets the page of the application; a
        // path that names a file (with an extension) is not taken, and the fallback policy keeps it for an authenticated user.
        routes.MapFallbackToFile("index.html");
    }
}
