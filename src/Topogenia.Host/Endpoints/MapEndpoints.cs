// MapEndpoints.cs
// The HTTP entry points of a map and of its lanes: read, history, undo, redo, delete the detail, reattach, lanes (annex A 1, spec 3.6).

using Microsoft.AspNetCore.Mvc;
using Topogenia.Host.Api.Assistant;
using Topogenia.Host.Api.Lanes;
using Topogenia.Host.Api.Maps;

namespace Topogenia.Host.Endpoints;

/// <summary>`/api/maps/{id}` and `/api/lanes/{id}`: each route reads the request and calls its handler (spec 3.2).</summary>
public static class MapEndpoints
{
    public static void MapMapRoutes(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        var maps = routes.MapGroup("/api/maps/{id:guid}").WithTags("Maps");
        maps.MapGet("", async (Guid id, MapApi api, CancellationToken token) => TypedResults.Ok(await api.GetAsync(id, token)));
        maps.MapGet("/history", async (Guid id, MapApi api, CancellationToken token) => TypedResults.Ok(await api.HistoryAsync(id, token)));
        maps.MapPost("/undo", async (Guid id, MapApi api, CancellationToken token) => TypedResults.Ok(await api.UndoAsync(id, token)));
        maps.MapPost("/redo", async (Guid id, MapApi api, CancellationToken token) => TypedResults.Ok(await api.RedoAsync(id, token)));
        maps.MapDelete("", async (Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, MapApi api, CancellationToken token) =>
            TypedResults.Ok(await api.DeleteDetailAsync(id, ifMatch, token)));
        maps.MapPost("/reattach", async (Guid id, ReattachBody body, [FromHeader(Name = "If-Match")] string? ifMatch, MapApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ReattachAsync(id, body, ifMatch, token)));
        maps.MapPost("/lanes", async (Guid id, AddLaneBody body, LaneApi api, CancellationToken token) => TypedResults.Ok(await api.AddAsync(id, body, token)));
        maps.MapPut("/lane-order", async (Guid id, [FromBody] List<Guid> order, LaneApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ReorderAsync(id, order, token)));

        // The panel of the assistant (3.0): its state, the exchange in a stream of JSON lines, the prompt copied, the reply pasted.
        var limit = new RequestSizeLimitAttribute(AssistantPanelApi.BodyLimit);
        maps.MapGet("/assistant", async (Guid id, AssistantPanelApi api, CancellationToken token) => TypedResults.Ok(await api.StatusAsync(id, token)))
            .WithTags("Assistant");
        maps.MapPost("/assistant/exchanges", async (Guid id, AssistantAskBody body, AssistantPanelApi api, HttpContext http, CancellationToken token) =>
        {
            await api.ExchangeAsync(id, body, http, token);
            return TypedResults.Empty;
        }).WithMetadata(limit).Produces<AssistantEvent>(StatusCodes.Status200OK, AssistantPanelApi.StreamType).WithTags("Assistant");
        maps.MapPost("/assistant/prompt", async (Guid id, AssistantAskBody body, AssistantPanelApi api, CancellationToken token) =>
            TypedResults.Ok(await api.PromptAsync(id, body, token))).WithMetadata(limit).WithTags("Assistant");
        maps.MapPost("/assistant/replies", async (Guid id, AssistantPasteBody body, AssistantPanelApi api, CancellationToken token) =>
            TypedResults.Ok(await api.PastedAsync(id, body, token))).WithMetadata(limit).WithTags("Assistant");
        // A proposal on the map open (3.0, lot 3): its preview, and its application in one operation with If-Match.
        maps.MapPost("/assistant/preview", async (Guid id, ProposalBody body, ProposalApi api, CancellationToken token) =>
            TypedResults.Ok(await api.PreviewAsync(id, body, token))).WithMetadata(limit).WithTags("Assistant");
        maps.MapPost("/assistant/apply", async (Guid id, ProposalBody body, [FromHeader(Name = "If-Match")] string? ifMatch, ProposalApi api,
            CancellationToken token) => TypedResults.Ok(await api.ApplyAsync(id, body, ifMatch, token))).WithMetadata(limit).WithTags("Assistant");

        var lanes = routes.MapGroup("/api/lanes/{id:guid}").WithTags("Lanes");
        lanes.MapPatch("", async (Guid id, UpdateLaneBody body, [FromHeader(Name = "If-Match")] string? ifMatch, LaneApi api, CancellationToken token) =>
            TypedResults.Ok(await api.UpdateAsync(id, body, ifMatch, token)));
        lanes.MapDelete("", async (Guid id, string? steps, Guid? to, [FromHeader(Name = "If-Match")] string? ifMatch, LaneApi api, CancellationToken token) =>
            TypedResults.Ok(await api.DeleteAsync(id, steps, to, ifMatch, token)));
    }
}
