// ExchangeApi.cs
// The routes of the exchange package turned into their requests: the export of a project, the preview and the import (spec 7, 3.6).

using Topogenia.Application.Exchange;
using Topogenia.Domain.Errors;

namespace Topogenia.Host.Api.Exchange;

/// <summary>
/// `POST /api/projects/{id}/export`: <c>without_people</c> is required (spec 5.4: an absent field is an error, never a
/// silent default); the interface checks it by default (AR-34).
/// </summary>
public sealed record ExportPackageBody(bool? WithoutPeople);

/// <summary>
/// Reads the bodies of the exchange routes and runs their requests. An import carries the package itself as its body
/// (`application/octet-stream`, 100 MB at most, refused before it is read, spec 5.4) and its options in the query string,
/// all required: <c>with_members</c> (`true` or `false`) and, for a new project, <c>code</c> when the code of the package is
/// taken (ADR 052).
/// </summary>
public sealed class ExchangeApi(PackageExport export, PackageImport import)
{
    /// <summary>The limit of the body of an import of a package (spec 5.4).</summary>
    public const long PackageLimit = 100L * 1024 * 1024;

    /// <summary>The media type of a package sent to the import routes, as the contract describes it (ADR 052, D09).</summary>
    public const string PackageMediaType = "application/octet-stream";

    /// <summary>The media type of an exported package.</summary>
    public const string ExportMediaType = "application/zip";

    public Task<ExportedPackage> ExportAsync(Guid projectId, ExportPackageBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return export.RunAsync(projectId, ApiInput.Required(body.WithoutPeople, "without_people"), cancellationToken);
    }

    public async Task<ImportPreview> PreviewAsync(HttpRequest request, string? code, string? withMembers, CancellationToken cancellationToken) =>
        await import.PreviewAsync(await BodyAsync(request, cancellationToken), Code(code), Flag(withMembers, "with_members"), cancellationToken);

    public async Task<ImportedProject> ImportAsync(HttpRequest request, string? code, string? withMembers, CancellationToken cancellationToken) =>
        await import.ImportAsync(await BodyAsync(request, cancellationToken), Code(code), Flag(withMembers, "with_members"), cancellationToken);

    public async Task<ImportPreview> PreviewReplacementAsync(Guid projectId, HttpRequest request, string? withMembers, CancellationToken cancellationToken) =>
        await import.PreviewReplacementAsync(projectId, await BodyAsync(request, cancellationToken), Flag(withMembers, "with_members"), cancellationToken);

    /// <summary>The replacement reads the If-Match of the replaced project (spec 5.3), as its move to the trash does.</summary>
    public async Task<ImportedProject> ReplaceAsync(Guid projectId, HttpRequest request, string? withMembers, string? ifMatch,
        CancellationToken cancellationToken) =>
        await import.ReplaceAsync(projectId, await BodyAsync(request, cancellationToken), Flag(withMembers, "with_members"), ApiInput.Version(ifMatch),
            cancellationToken);

    /// <summary>The package, read whole: the limit of the route has already refused a longer body (`BodySizeLimit`).</summary>
    private static async Task<byte[]> BodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var content = new MemoryStream();
        await request.Body.CopyToAsync(content, cancellationToken);
        return content.ToArray();
    }

    /// <summary>A flag of the query string: `true` or `false`, CHAMP_OBLIGATOIRE when absent, VALEUR_INVALIDE otherwise.</summary>
    private static bool Flag(string? text, string field) => text switch
    {
        null or "" => throw DomainException.Invalid(ErrorCodes.RequiredField, field),
        "true" => true,
        "false" => false,
        _ => throw DomainException.InvalidValue(field),
    };

    private static string? Code(string? code) => string.IsNullOrEmpty(code) ? null : code;
}
