// PmapApi.cs
// The routes of the .pmap files turned into their requests: the copy of a project, the opening of a file, the duplication (ADR 054).

using Topogenia.Application.Commands;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;

namespace Topogenia.Host.Api.Exchange;

/// <summary>
/// `POST /api/projects/import-pmap`: the file as a text in the JSON body (A-4-54), the code and the name entered at the
/// opening, and, for a proposal of the assistant, its map of origin, whose steps carry their sheets and translations (ADR 082).
/// </summary>
public sealed record OpenPmapBody(string? Content, string? Code, string? Name, Guid? OriginMapId = null);

/// <summary>`POST /api/projects/{id}/duplicate`: the code and the name of the copy, those of the source when absent (A-1.2-21).</summary>
public sealed record DuplicateProjectBody(string? Code, string? Name);

/// <summary>
/// Reads the bodies of the `.pmap` routes and runs their requests. The opening takes a JSON body of 10 MB at most, refused
/// before it is read (A-4-22: the 1.x read 8 million characters before it checked them).
/// </summary>
public sealed class PmapApi(PmapCopyExport copy, UserRunner runner, IPmapFormat format)
{
    /// <summary>The limit of the body of the opening of a file (A-4-22, spec 5.4).</summary>
    public const long OpeningLimit = 10L * 1024 * 1024;

    /// <summary>The header that reports sub-maps left out of a copy beyond 12 levels, or of a duplication beyond the walks of the domain (A-2.17-47).</summary>
    public const string WarningHeader = "X-Pmap-Warning";

    /// <summary>The copy, with its sheets when <c>with_sheets</c> is `true` (AR-93: off by default); another value is VALEUR_INVALIDE.</summary>
    public Task<PmapCopy> CopyAsync(Guid projectId, string? withSheets, CancellationToken cancellationToken) =>
        copy.RunAsync(projectId, withSheets switch
        {
            null or "" or "false" => false,
            "true" => true,
            _ => throw DomainException.InvalidValue("with_sheets"),
        }, cancellationToken);

    public Task<PmapOpened> OpenAsync(OpenPmapBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return runner.RunAsync(new OpenPmapCommand(format, body.Content, body.Code, body.Name, body.OriginMapId), cancellationToken);
    }

    public Task<PmapOpened> DuplicateAsync(Guid projectId, DuplicateProjectBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return runner.RunAsync(new DuplicateProjectCommand(format, projectId, body.Code, body.Name), cancellationToken);
    }
}
