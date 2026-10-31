// SheetEndpoints.cs
// The HTTP entry points of the sheets and documents of a step (lot C).

using Microsoft.AspNetCore.Mvc;
using Topogenia.Host.Api.Documents;
using Topogenia.Host.Api.Sheets;

namespace Topogenia.Host.Endpoints;

/// <summary>`/api/steps/{id}/sheet`, `/documents` and `/uses`: each route reads the request and calls its handler (spec 3.2).</summary>
public static class SheetEndpoints
{
    public static void MapSheetRoutes(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        var sheets = routes.MapGroup("/api/steps/{id:guid}/sheet").WithTags("Sheets");
        sheets.MapGet("", async (Guid id, SheetApi api, CancellationToken token) => TypedResults.Ok(await api.GetAsync(id, token)));
        sheets.MapPut("", async (Guid id, SaveSheetBody body, [FromHeader(Name = "If-Match")] string? ifMatch, SheetApi api, CancellationToken token) =>
            TypedResults.Ok(await api.SaveAsync(id, body, ifMatch, token)));
        sheets.MapPost("/validate", async (Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, SheetApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ValidateAsync(id, ifMatch, token)));
        sheets.MapPost("/clarification",
            async (Guid id, ClarificationBody body, [FromHeader(Name = "If-Match")] string? ifMatch, SheetApi api, CancellationToken token) =>
                TypedResults.Ok(await api.RequestClarificationAsync(id, body, ifMatch, token)));
        sheets.MapPut("/clarification",
            async (Guid id, ClarificationBody body, [FromHeader(Name = "If-Match")] string? ifMatch, SheetApi api, CancellationToken token) =>
                TypedResults.Ok(await api.ChangeClarificationAsync(id, body, ifMatch, token)));
        sheets.MapDelete("/clarification", async (Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, SheetApi api, CancellationToken token) =>
            TypedResults.Ok(await api.WithdrawClarificationAsync(id, ifMatch, token)));

        var steps = routes.MapGroup("/api/steps/{id:guid}").WithTags("Documents");
        steps.MapPost("/documents", async (Guid id, DocumentBody body, DocumentApi api, CancellationToken token) =>
            TypedResults.Ok(await api.AddAsync(id, body, token)));
        steps.MapPut("/documents/{documentId:guid}",
            async (Guid id, Guid documentId, DocumentBody body, [FromHeader(Name = "If-Match")] string? ifMatch, DocumentApi api, CancellationToken token) =>
                TypedResults.Ok(await api.UpdateAsync(id, documentId, body, ifMatch, token)));
        steps.MapDelete("/documents/{documentId:guid}",
            async (Guid id, Guid documentId, [FromHeader(Name = "If-Match")] string? ifMatch, DocumentApi api, CancellationToken token) =>
                TypedResults.Ok(await api.DeleteAsync(id, documentId, ifMatch, token)));
        steps.MapGet("/documents/available", async (Guid id, DocumentApi api, CancellationToken token) =>
            TypedResults.Ok(await api.AvailableAsync(id, token)));
        steps.MapPut("/uses/{documentId:guid}",
            async (Guid id, Guid documentId, DocumentUseBody body, [FromHeader(Name = "If-Match")] string? ifMatch, DocumentApi api, CancellationToken token) =>
                TypedResults.Ok(await api.UseAsync(id, documentId, body, ifMatch, token)));
        steps.MapDelete("/uses/{documentId:guid}",
            async (Guid id, Guid documentId, [FromHeader(Name = "If-Match")] string? ifMatch, DocumentApi api, CancellationToken token) =>
                TypedResults.Ok(await api.StopUsingAsync(id, documentId, ifMatch, token)));
    }
}
