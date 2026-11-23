// MapEndpoints.cs
// The HTTP entry points of a map and of its lanes: read, history, action plan, comments of presentation, undo, redo, delete the detail, reattach, lanes and their rows (annex A 1, spec 3.6, ADR 096, ADR 099).

using Microsoft.AspNetCore.Mvc;
using Topogenia.Domain.Errors;
using Topogenia.Host.Api.Assistant;
using Topogenia.Host.Api.Exchange;
using Topogenia.Host.Api.Lanes;
using Topogenia.Host.Api.Maps;
using Topogenia.Host.Api.Presentation;

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
        maps.MapGet("/action-plan", async (Guid id, MapApi api, CancellationToken token) => TypedResults.Ok(await api.ActionPlanAsync(id, token)));
        // The comments of presentation of its steps (3.2, ADR 099), read by a presentation when it starts and when it dives.
        maps.MapGet("/presentation-comments", async (Guid id, PresentationApi api, CancellationToken token) => TypedResults.Ok(await api.ReadAsync(id, token)));
        // The comments written by the assistant for its steps and its flows, kept after review (3.3, lot 3, F-130).
        maps.MapPost("/presentation-comments", async (Guid id, CommentDraftsBody body, PresentationApi api, CancellationToken token) =>
            TypedResults.Ok(await api.WriteDraftsAsync(id, body, token)));
        // The comments of presentation of its flows (3.2, ADR 099, lot J10), read with those of its steps.
        maps.MapGet("/flow-presentation-comments", async (Guid id, PresentationApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ReadFlowsAsync(id, token)));
        maps.MapPost("/undo", async (Guid id, MapApi api, CancellationToken token) => TypedResults.Ok(await api.UndoAsync(id, token)));
        maps.MapPost("/redo", async (Guid id, MapApi api, CancellationToken token) => TypedResults.Ok(await api.RedoAsync(id, token)));
        maps.MapDelete("", async (Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, MapApi api, CancellationToken token) =>
            TypedResults.Ok(await api.DeleteDetailAsync(id, ifMatch, token)));
        maps.MapPost("/reattach", async (Guid id, ReattachBody body, [FromHeader(Name = "If-Match")] string? ifMatch, MapApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ReattachAsync(id, body, ifMatch, token)));
        // The copy of the map and of the maps below it that an image carries (plan 3.1, L7), as the copy of a project.
        maps.MapPut("/summaries", async (Guid id, MapSummariesBody body, MapSummaryApi api, CancellationToken token) =>
            TypedResults.Ok(await api.WriteAsync(id, body, token)));
        maps.MapGet("/pmap", async (Guid id, string? purpose, PmapApi api, HttpResponse response, CancellationToken token) =>
        {
            var copy = await api.MapCopyAsync(id, purpose, token);
            if (copy.DepthCut)
                response.Headers[PmapApi.WarningHeader] = WarningCodes.PmapDepthTruncated;
            return TypedResults.File(System.Text.Encoding.UTF8.GetBytes(copy.Content), "text/plain; charset=utf-8", copy.FileName);
        }).Produces(StatusCodes.Status200OK, typeof(Stream), "text/plain");
        // A .pmap file inserted as a detailed step (3.3, F-208): its preview, then its copy in one operation with If-Match.
        var opening = new RequestSizeLimitAttribute(PmapApi.OpeningLimit);
        // The body is read only once the right is granted (independent review of the 3.3, as the opening of a file).
        maps.MapPost("/insert-pmap/preview", async (Guid id, HttpRequest request, PmapInsertionApi api, CancellationToken token) =>
            TypedResults.Ok(await api.PreviewAsync(id, request, token))).WithMetadata(opening).Accepts<InsertPmapPreviewBody>("application/json");
        maps.MapPost("/insert-pmap", async (Guid id, HttpRequest request, [FromHeader(Name = "If-Match")] string? ifMatch, PmapInsertionApi api,
            CancellationToken token) => TypedResults.Ok(await api.InsertAsync(id, request, ifMatch, token))).WithMetadata(opening).Accepts<InsertPmapBody>("application/json");
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
        // Rows inserted into a lane, or an empty row taken out (3.1, ADR 096): the steps and passages under them move, one operation.
        lanes.MapPost("/rows", async (Guid id, InsertRowsBody body, LaneApi api, CancellationToken token) => TypedResults.Ok(await api.InsertRowsAsync(id, body, token)));
        lanes.MapDelete("/rows/{row:int}", async (Guid id, int row, LaneApi api, CancellationToken token) => TypedResults.Ok(await api.RemoveRowAsync(id, row, token)));
    }
}
