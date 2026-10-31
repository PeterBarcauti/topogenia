// AdministrationEndpoints.cs
// The HTTP entry points of the members, trash, deletion of a project, translations of a project (lot D), of the audit trail (spec 5.5) and of the settings of the assistant (3.0).

using Microsoft.AspNetCore.Mvc;
using Topogenia.Host.Api.Assistant;
using Topogenia.Host.Api.Audit;
using Topogenia.Host.Api.Members;
using Topogenia.Host.Api.Projects;
using Topogenia.Host.Api.Translations;
using Topogenia.Host.Api.Trash;

namespace Topogenia.Host.Endpoints;

/// <summary>`/api/projects/{id}/...`, `/api/trash`, `/api/admin/audit` and `/api/admin/assistant`: each route reads the request and calls its handler (spec 3.2).</summary>
public static class AdministrationEndpoints
{
    public static void MapAdministrationRoutes(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        var project = routes.MapGroup("/api/projects/{id:guid}").WithTags("Projects");
        project.MapPut("/members",
            async (Guid id, [FromBody] List<MemberBody>? members, [FromHeader(Name = "If-Match")] string? ifMatch, MemberApi api, CancellationToken token) =>
                TypedResults.Ok(await api.ReplaceAsync(id, members, ifMatch, token)));
        project.MapGet("/participants", async (Guid id, MemberApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ParticipantsAsync(id, token)));
        project.MapDelete("", async (Guid id, string? confirmation, [FromHeader(Name = "If-Match")] string? ifMatch, TrashApi api, CancellationToken token) =>
        {
            await api.MoveToTrashAsync(id, confirmation, ifMatch, token);
            return TypedResults.NoContent();
        });
        project.MapGet("/translations", async (Guid id, string? scope, string? languages, ProjectTranslationApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ListAsync(id, scope, languages, token)));
        project.MapPut("/translations", async (Guid id, ImportTranslationsBody body, ProjectTranslationApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ImportAsync(id, body, token)));

        var trash = routes.MapGroup("/api/trash").WithTags("Trash");
        trash.MapGet("", async (TrashApi api, CancellationToken token) => TypedResults.Ok(await api.ListAsync(token)));
        trash.MapPost("/{id:guid}/restore", async (Guid id, TrashApi api, CancellationToken token) =>
        {
            await api.RestoreAsync(id, token);
            return TypedResults.NoContent();
        });
        trash.MapDelete("/{id:guid}", async (Guid id, TrashApi api, CancellationToken token) =>
        {
            await api.PurgeAsync(id, token);
            return TypedResults.NoContent();
        });

        routes.MapGet("/api/admin/audit", async ([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery(Name = "project_id")] Guid? projectId,
                [FromQuery(Name = "user_id")] Guid? userId, [FromQuery] string? kind, [FromQuery] int? limit, AuditApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ReadAsync(from, to, projectId, userId, kind, limit, token))).WithTags("Administration");

        var assistant = routes.MapGroup("/api/admin/assistant").WithTags("Administration");
        assistant.MapGet("", async (AssistantApi api, CancellationToken token) => TypedResults.Ok(await api.ReadAsync(token)));
        assistant.MapPut("", async (AssistantSettingsBody body, [FromHeader(Name = "If-Match")] string? ifMatch, AssistantApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ChangeAsync(body, ifMatch, token)));
        assistant.MapPost("/test", async (AssistantSettingsBody body, AssistantApi api, CancellationToken token) =>
            TypedResults.Ok(await api.TestAsync(body, token)));

        // A project described from the home page (3.0, spec section 3.2, administrator): the panel without a map.
        var describe = routes.MapGroup("/api/assistant").WithTags("Assistant");
        var limit = new RequestSizeLimitAttribute(AssistantPanelApi.BodyLimit);
        describe.MapPost("/exchanges", async (AssistantAskBody body, AssistantPanelApi api, HttpContext http, CancellationToken token) =>
        {
            await api.ExchangeAsync(null, body, http, token);
            return TypedResults.Empty;
        }).WithMetadata(limit).Produces<AssistantEvent>(StatusCodes.Status200OK, AssistantPanelApi.StreamType);
        describe.MapPost("/prompt", async (AssistantAskBody body, AssistantPanelApi api, CancellationToken token) =>
            TypedResults.Ok(await api.PromptAsync(null, body, token))).WithMetadata(limit);
        describe.MapPost("/replies", async (AssistantPasteBody body, AssistantPanelApi api, CancellationToken token) =>
            TypedResults.Ok(await api.PastedAsync(null, body, token))).WithMetadata(limit);
    }
}
