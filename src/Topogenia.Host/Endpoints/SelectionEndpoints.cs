// SelectionEndpoints.cs
// The HTTP entry points of the selection (deletion, paste, transfer, group) and dissolutions (lot B).

using Microsoft.AspNetCore.Mvc;
using Topogenia.Host.Api.Dissolutions;
using Topogenia.Host.Api.Selection;

namespace Topogenia.Host.Endpoints;

/// <summary>Each route reads the request and calls its handler (spec 3.2).</summary>
public static class SelectionEndpoints
{
    public static void MapSelectionRoutes(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        var maps = routes.MapGroup("/api/maps/{id:guid}").WithTags("Selection");
        maps.MapPost("/deletion", async (Guid id, DeleteSelectionBody body, SelectionApi api, CancellationToken token) =>
            TypedResults.Ok(await api.DeleteAsync(id, body, token)));
        maps.MapPost("/paste", async (Guid id, PasteBody body, SelectionApi api, CancellationToken token) => TypedResults.Ok(await api.PasteAsync(id, body, token)));
        maps.MapPost("/transfer", async (Guid id, TransferBody body, SelectionApi api, CancellationToken token) =>
            TypedResults.Ok(await api.TransferAsync(id, body, token)));
        maps.MapPost("/group", async (Guid id, GroupBody body, SelectionApi api, CancellationToken token) => TypedResults.Ok(await api.GroupAsync(id, body, token)));

        var steps = routes.MapGroup("/api/steps/{id:guid}").WithTags("Dissolutions");
        steps.MapPost("/dissolve", async (Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, DissolutionApi api, CancellationToken token) =>
            TypedResults.Ok(await api.DissolveAsync(id, ifMatch, token)));

        var dissolutions = routes.MapGroup("/api/dissolutions/{id:guid}").WithTags("Dissolutions");
        dissolutions.MapPost("/assign", async (Guid id, AssignBody body, DissolutionApi api, CancellationToken token) =>
            TypedResults.Ok(await api.AssignAsync(id, body, token)));
        dissolutions.MapPost("/remove", async (Guid id, RemoveBody body, DissolutionApi api, CancellationToken token) =>
            TypedResults.Ok(await api.RemoveAsync(id, body, token)));
        dissolutions.MapPost("/finish", async (Guid id, DissolutionApi api, CancellationToken token) => TypedResults.Ok(await api.FinishAsync(id, token)));
    }
}
