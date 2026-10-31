// SampleProjectApi.cs
// The fictitious sample project offered at the first launch of the desktop mode: the package of the samples, embedded in the host and imported by the chain of every import (spec 6.1, 7.3).

using Microsoft.Extensions.Options;
using Topogenia.Application.Exchange;
using Topogenia.Domain.Errors;
using Topogenia.Host.Composition;

namespace Topogenia.Host.Api.Exchange;

/// <summary>
/// `POST /api/desktop/sample-project`: the desktop mode only (INTROUVABLE in server mode, like the command « Quit »), then
/// the import of a new project from the package of `samples/` (one source: the project file embeds the file itself), by
/// <see cref="PackageImport"/> as a package sent by the user: right of the administrator, checks of the package, its code
/// free or refused (PROJET_CODE_EXISTANT, PROJET_CODE_CORBEILLE), the audit event of an import. The package holds no
/// people (exported without them, AR-34), so none is taken: the user is its only facilitator.
/// </summary>
public sealed class SampleProjectApi(PackageImport import, IOptions<ProductOptions> options)
{
    /// <summary>The name of the package in the resources of the host (its project file names it).</summary>
    public const string ResourceName = "Samples/sample-project";

    public Task<ImportedProject> CreateAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Mode != HostMode.Desktop)
            throw DomainException.NotFound();
        return import.ImportAsync(Package(), null, false, cancellationToken);
    }

    /// <summary>The bytes of the sample package, as the repository holds it.</summary>
    public static byte[] Package()
    {
        using var stream = typeof(SampleProjectApi).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The sample package is not embedded in the host.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return content.ToArray();
    }
}
