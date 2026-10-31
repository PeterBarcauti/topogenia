// ProjectEndpoints.cs
// The HTTP entry points of the profile and of the projects: me, list, create, detail, label of the sharing group, classification, glossary of the translations, export, import, .pmap (annex A 1, spec 3.6, 4.1, 7).

using Microsoft.AspNetCore.Mvc;
using Topogenia.Domain.Errors;
using Topogenia.Host.Api.Exchange;
using Topogenia.Host.Api.Profile;
using Topogenia.Host.Api.Projects;

namespace Topogenia.Host.Endpoints;

/// <summary>`/api/me` and `/api/projects`: each route reads the request and calls its handler (spec 3.2).</summary>
public static class ProjectEndpoints
{
    public static void MapProjectRoutes(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        var me = routes.MapGroup("/api/me").WithTags("Profile");
        me.MapGet("", async (ProfileApi api, CancellationToken token) => TypedResults.Ok(await api.GetAsync(token)));
        me.MapPatch("", async (ChangeProfileBody body, [FromHeader(Name = "If-Match")] string? ifMatch, ProfileApi api, CancellationToken token) =>
            TypedResults.Ok(await api.ChangeAsync(body, ifMatch, token)));
        me.MapPut("/last-project", async (LastProjectBody body, ProfileApi api, CancellationToken token) =>
            TypedResults.Ok(await api.RememberProjectAsync(body, token)));

        var projects = routes.MapGroup("/api/projects").WithTags("Projects");
        projects.MapGet("", async (ProjectApi api, CancellationToken token) => TypedResults.Ok(await api.ListAsync(token)));
        projects.MapPost("", async (CreateProjectBody body, ProjectApi api, CancellationToken token) =>
        {
            var created = await api.CreateAsync(body, token);
            return TypedResults.Created($"/api/projects/{created.Id}", created);
        });
        projects.MapGet("/{id:guid}", async (Guid id, ProjectApi api, CancellationToken token) => TypedResults.Ok(await api.GetAsync(id, token)));
        projects.MapPut("/{id:guid}/sharing-group-label",
            async (Guid id, SharingGroupLabelBody body, [FromHeader(Name = "If-Match")] string? ifMatch, ProjectApi api, CancellationToken token) =>
                TypedResults.Ok(await api.ChangeSharingGroupLabelAsync(id, body, ifMatch, token)));
        projects.MapPut("/{id:guid}/classification",
            async (Guid id, ClassificationBody body, [FromHeader(Name = "If-Match")] string? ifMatch, ProjectApi api, CancellationToken token) =>
                TypedResults.Ok(await api.ChangeClassificationAsync(id, body, ifMatch, token)));
        projects.MapPut("/{id:guid}/translation-glossary",
            async (Guid id, TranslationGlossaryBody body, [FromHeader(Name = "If-Match")] string? ifMatch, ProjectApi api, CancellationToken token) =>
                TypedResults.Ok(await api.ChangeTranslationGlossaryAsync(id, body, ifMatch, token)));
        projects.MapPut("/{id:guid}/work-time",
            async (Guid id, WorkTimeBody body, [FromHeader(Name = "If-Match")] string? ifMatch, ProjectApi api, CancellationToken token) =>
                TypedResults.Ok(await api.ChangeWorkTimeAsync(id, body, ifMatch, token)));
        projects.MapPost("/{id:guid}/export", async (Guid id, ExportPackageBody body, ExchangeApi api, CancellationToken token) =>
        {
            var package = await api.ExportAsync(id, body, token);
            return TypedResults.File(package.Content, ExchangeApi.ExportMediaType, package.FileName);
        }).Produces(StatusCodes.Status200OK, typeof(Stream), ExchangeApi.ExportMediaType);
        MapImportRoutes(projects);
        MapPmapRoutes(projects);
    }

    /// <summary>
    /// The `.pmap` files (ADR 054): the copy as a text file named by the rule of the 1.x (A-1.2-16), the opening of a file
    /// (10 MB at most, refused before it is read) and the duplication, both creating a project (201).
    /// </summary>
    private static void MapPmapRoutes(RouteGroupBuilder projects)
    {
        projects.MapGet("/{id:guid}/pmap", async (Guid id, [FromQuery(Name = "with_sheets")] string? withSheets, PmapApi api, HttpResponse response,
            CancellationToken token) =>
        {
            var copy = await api.CopyAsync(id, withSheets, token);
            if (copy.DepthCut)
                response.Headers[PmapApi.WarningHeader] = WarningCodes.PmapDepthTruncated;
            return TypedResults.File(System.Text.Encoding.UTF8.GetBytes(copy.Content), "text/plain; charset=utf-8", copy.FileName);
        }).Produces(StatusCodes.Status200OK, typeof(Stream), "text/plain");
        projects.MapPost("/import-pmap", async (OpenPmapBody body, PmapApi api, CancellationToken token) =>
        {
            var opened = await api.OpenAsync(body, token);
            return TypedResults.Created($"/api/projects/{opened.Id}", opened);
        }).WithMetadata(new RequestSizeLimitAttribute(PmapApi.OpeningLimit));
        projects.MapPost("/{id:guid}/duplicate", async (Guid id, DuplicateProjectBody body, PmapApi api, HttpResponse response, CancellationToken token) =>
        {
            var duplicated = await api.DuplicateAsync(id, body, token);
            if (duplicated.DepthCut)
                response.Headers[PmapApi.WarningHeader] = WarningCodes.PmapDepthTruncated;
            return TypedResults.Created($"/api/projects/{duplicated.Id}", duplicated);
        });
    }

    /// <summary>The import of a package (spec 7.3, ADR 052): its body is the package, 100 MB at most, refused before it is read.</summary>
    private static void MapImportRoutes(RouteGroupBuilder projects)
    {
        var limit = new RequestSizeLimitAttribute(ExchangeApi.PackageLimit);
        projects.MapPost("/import/preview", async (HttpRequest request, [FromQuery] string? code, [FromQuery(Name = "with_members")] string? withMembers,
            ExchangeApi api, CancellationToken token) => TypedResults.Ok(await api.PreviewAsync(request, code, withMembers, token))).WithMetadata(limit).Accepts<Stream>(ExchangeApi.PackageMediaType);
        projects.MapPost("/import", async (HttpRequest request, [FromQuery] string? code, [FromQuery(Name = "with_members")] string? withMembers,
            ExchangeApi api, CancellationToken token) =>
        {
            var imported = await api.ImportAsync(request, code, withMembers, token);
            return TypedResults.Created($"/api/projects/{imported.Id}", imported);
        }).WithMetadata(limit).Accepts<Stream>(ExchangeApi.PackageMediaType);
        projects.MapPost("/{id:guid}/import/preview", async (Guid id, HttpRequest request, [FromQuery(Name = "with_members")] string? withMembers,
            ExchangeApi api, CancellationToken token) => TypedResults.Ok(await api.PreviewReplacementAsync(id, request, withMembers, token))).WithMetadata(limit).Accepts<Stream>(ExchangeApi.PackageMediaType);
        projects.MapPost("/{id:guid}/import", async (Guid id, HttpRequest request, [FromQuery(Name = "with_members")] string? withMembers,
            [FromHeader(Name = "If-Match")] string? ifMatch, ExchangeApi api, CancellationToken token) =>
        {
            var imported = await api.ReplaceAsync(id, request, withMembers, ifMatch, token);
            return TypedResults.Created($"/api/projects/{imported.Id}", imported);
        }).WithMetadata(limit).Accepts<Stream>(ExchangeApi.PackageMediaType);
    }
}
