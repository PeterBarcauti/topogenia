// DocumentFolderApi.cs
// « Open the folder » of a document, in desktop mode only: the folder that holds the document is shown in the Explorer of Windows, the document itself never opened (ADR 104).

using Microsoft.Extensions.Options;
using Topogenia.Application.Documents;
using Topogenia.Application.Queries;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;
using Topogenia.Host.Composition;
using Topogenia.Host.Desktop;

namespace Topogenia.Host.Api.Documents;

/// <summary>The codes of the refusals of « Open the folder » (ADR 104), with a message in each language of the host.</summary>
public static class DocumentFolderErrors
{
    /// <summary>The document has no location, or a web address: there is no folder to show.</summary>
    public const string NoPath = "DOCUMENT_SANS_CHEMIN";

    /// <summary>The path, or its folder written for the shell, is longer than Windows opens (`max`).</summary>
    public const string PathTooLong = "CHEMIN_TROP_LONG";

    /// <summary>
    /// The path is not written as the folder Windows would open (a device, a pipe, `..`, a name it would cut, a server
    /// that is not written as the name of a machine, a character that is not shown as it is written): nothing was
    /// looked at.
    /// </summary>
    public const string PathNotSupported = "CHEMIN_NON_PRIS_EN_CHARGE";

    /// <summary>Neither the path nor its parent is a folder.</summary>
    public const string FolderNotFound = "DOSSIER_INTROUVABLE";

    /// <summary>Windows refuses the access of the account to the folder.</summary>
    public const string FolderDenied = "DOSSIER_ACCES_REFUSE";

    /// <summary>The server or the drive of the folder does not answer: the bound of the look or of the shell exceeded, and any failure of either that is no answer of Windows.</summary>
    public const string FolderUnreachable = "DOSSIER_INJOIGNABLE";

    /// <summary>Another folder still waits for the answer of Windows: this click was not taken.</summary>
    public const string FolderBusy = "DOSSIER_OUVERTURE_EN_COURS";
}

/// <summary>
/// `POST /api/steps/{id}/documents/{documentId}/folder` (new in 3.2, Lecteur, ADR 104), in this order:
/// <list type="number">
/// <item>the desktop mode, else INTROUVABLE (like the sample project): the mode is decided before anything is read of
/// the document, and the server mode registers no opener at all;</item>
/// <item>the right to read the sheet and the live document that this live step declared, else INTROUVABLE
/// (<see cref="GetStepDocumentQuery"/>);</item>
/// <item>its kept location must be a path, else DOCUMENT_SANS_CHEMIN (no location, or a web address, never followed);</item>
/// <item>the path goes to <see cref="IFolderOpener"/>, which holds it to its rules, looks at it within a bound and has
/// the shell show the folder; each refusal has its code.</item>
/// </list>
/// The request carries identifiers only: the path comes from the base. Nothing is written: no version, no audit event,
/// nothing in the history. This file is the only one of the shipped code that hands the location of a document to
/// something that reaches the disk (the exception to S-5.4-13, held by `SourceRulesTests`).
/// </summary>
public sealed class DocumentFolderApi(QueryRunner queries, IOptions<ProductOptions> options, IFolderOpener? opener = null)
{
    public async Task ShowAsync(Guid stepId, Guid documentId, CancellationToken cancellationToken)
    {
        if (options.Value.Mode != HostMode.Desktop || opener is null)
            throw DomainException.NotFound();
        var document = await queries.RunAsync(new GetStepDocumentQuery(stepId, documentId), cancellationToken);
        if (document.LocationType != LocationType.Path || document.Location is not { } path)
            throw new DomainException(DocumentFolderErrors.NoPath, RefusalKind.Invalid);
        if (Refusal(await opener.ShowAsync(path, cancellationToken)) is { } refusal)
            throw refusal;
    }

    /// <summary>The refusal of an outcome, null once the folder is shown.</summary>
    private static DomainException? Refusal(FolderOutcome outcome) => outcome switch
    {
        FolderOutcome.Shown => null,
        FolderOutcome.TooLong => DomainException.Invalid(DocumentFolderErrors.PathTooLong, limit: FolderPaths.MaxLength),
        FolderOutcome.Unsupported => new DomainException(DocumentFolderErrors.PathNotSupported, RefusalKind.Invalid),
        FolderOutcome.NotFound => new DomainException(DocumentFolderErrors.FolderNotFound, RefusalKind.NotFound),
        FolderOutcome.Denied => new DomainException(DocumentFolderErrors.FolderDenied, RefusalKind.Forbidden),
        FolderOutcome.Busy => DomainException.Conflict(DocumentFolderErrors.FolderBusy),
        _ => DomainException.Conflict(DocumentFolderErrors.FolderUnreachable),
    };
}
