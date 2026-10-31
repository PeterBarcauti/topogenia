// StepEndpoints.cs
// The HTTP entry points of the steps, flows (their reversal too, AR-155), layout, command bar, RACI, detail, insertion, translations of steps and flows (lot A).

using Microsoft.AspNetCore.Mvc;
using Topogenia.Host.Api.Flows;
using Topogenia.Host.Api.Layout;
using Topogenia.Host.Api.Raci;
using Topogenia.Host.Api.Steps;

namespace Topogenia.Host.Endpoints;

/// <summary>`/api/maps/{id}/...` of the structure, `/api/steps/{id}` and `/api/flows/{id}`: each route reads the request and calls its handler (spec 3.2).</summary>
public static class StepEndpoints
{
    public static void MapStepRoutes(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        MapMapStructureRoutes(routes);
        MapStepItemRoutes(routes);
        MapFlowItemRoutes(routes);
    }

    private static void MapMapStructureRoutes(IEndpointRouteBuilder routes)
    {
        var maps = routes.MapGroup("/api/maps/{id:guid}").WithTags("Steps");
        maps.MapPost("/steps", async (Guid id, CreateStepBody body, StepApi api, CancellationToken token) => TypedResults.Ok(await api.CreateAsync(id, body, token)));
        maps.MapPost("/flows", async (Guid id, CreateFlowBody body, FlowApi api, CancellationToken token) => TypedResults.Ok(await api.CreateAsync(id, body, token)));
        maps.MapPost("/command", async (Guid id, CommandBarBody body, FlowApi api, CancellationToken token) =>
            TypedResults.Ok(await api.CommandAsync(id, body, token)));
        maps.MapPut("/layout", async (Guid id, LayoutBody body, LayoutApi api, CancellationToken token) => TypedResults.Ok(await api.LayOutAsync(id, body, token)));
        maps.MapPost("/move", async (Guid id, MoveStepsBody body, LayoutApi api, CancellationToken token) => TypedResults.Ok(await api.MoveAsync(id, body, token)));
    }

    private static void MapStepItemRoutes(IEndpointRouteBuilder routes)
    {
        var steps = routes.MapGroup("/api/steps/{id:guid}").WithTags("Steps");
        steps.MapPatch("", async (Guid id, UpdateStepBody body, [FromHeader(Name = "If-Match")] string? ifMatch, StepApi api, CancellationToken token) =>
            TypedResults.Ok(await api.UpdateAsync(id, body, ifMatch, token)));
        steps.MapDelete("", async (Guid id, [FromQuery(Name = "sub_map")] string? subMap, Guid? to, bool? stitch, [FromHeader(Name = "If-Match")] string? ifMatch, StepApi api,
                CancellationToken token) =>
            TypedResults.Ok(await api.DeleteAsync(id, subMap, to, stitch, ifMatch, token)));
        steps.MapPost("/insert", async (Guid id, InsertStepBody body, StepApi api, CancellationToken token) => TypedResults.Ok(await api.InsertAsync(id, body, token)));
        steps.MapPost("/detail", async (Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, StepApi api, CancellationToken token) =>
            TypedResults.Ok(await api.DetailAsync(id, ifMatch, token)));
        steps.MapPut("/raci", async (Guid id, SetRaciBody body, [FromHeader(Name = "If-Match")] string? ifMatch, RaciApi api, CancellationToken token) =>
            TypedResults.Ok(await api.SetAsync(id, body, ifMatch, token)));
        steps.MapPut("/translations", async (Guid id, StepTranslationsBody body, [FromHeader(Name = "If-Match")] string? ifMatch, StepApi api, CancellationToken token) =>
            TypedResults.Ok(await api.TranslateAsync(id, body, ifMatch, token)));
    }

    private static void MapFlowItemRoutes(IEndpointRouteBuilder routes)
    {
        var flows = routes.MapGroup("/api/flows/{id:guid}").WithTags("Flows");
        flows.MapPatch("", async (Guid id, RelabelFlowBody body, [FromHeader(Name = "If-Match")] string? ifMatch, FlowApi api, CancellationToken token) =>
            TypedResults.Ok(await api.RelabelAsync(id, body, ifMatch, token)));
        flows.MapPut("/share", async (Guid id, ShareFlowBody body, [FromHeader(Name = "If-Match")] string? ifMatch, FlowApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ShareAsync(id, body, ifMatch, token)));
        flows.MapPost("/reverse", async (Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, FlowApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ReverseAsync(id, ifMatch, token)));
        flows.MapDelete("", async (Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, FlowApi api, CancellationToken token) =>
            TypedResults.Ok(await api.DeleteAsync(id, ifMatch, token)));
        flows.MapPut("/translations", async (Guid id, FlowTranslationsBody body, [FromHeader(Name = "If-Match")] string? ifMatch, FlowApi api, CancellationToken token) =>
            TypedResults.Ok(await api.TranslateAsync(id, body, ifMatch, token)));
    }
}
