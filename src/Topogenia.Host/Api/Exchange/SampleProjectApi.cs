// SampleProjectApi.cs
// The fictitious sample project offered in desktop mode: a package of the samples, embedded in the host and imported by the chain of every import (spec 6.1, 7.3), written in the language of the interface, under the first free code once its own is taken (ADR 111).

using Microsoft.Extensions.Options;
using Topogenia.Application.Commands;
using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Projects;
using Topogenia.Domain.Errors;
using Topogenia.Host.Composition;

namespace Topogenia.Host.Api.Exchange;

/// <summary>
/// `POST /api/desktop/sample-project`: the desktop mode only (INTROUVABLE in server mode, like the command « Quit »), then
/// the import of a new project from a package of `samples/` (one source: the project file embeds the file itself), by
/// <see cref="PackageImport"/> as a package sent by the user: right of the administrator, checks of the package, the audit
/// event of an import. The sample stays offered once projects exist (ADR 111 point 6): the preview of the import says
/// whether the code of the package is free; when it is not (a live project or one in the trash holds it), the sample takes
/// the first free code of the rule of the trash (<see cref="FreeProjectCodeQuery"/>: CONGES, then CONGEA, CONGEB…), and the
/// reply names its root map, which the interface opens. The package holds no people (exported without them, AR-34), so
/// none is taken: the user is its only facilitator. An interface in French gets the sample written in French (CONGES), any
/// other the one written in English (LEAVE): a sample shown through its translation would be locked (ADR 083) and keep
/// its sheets in the language it was written in.
/// </summary>
public sealed class SampleProjectApi(PackageImport import, UserRunner users, ICurrentUser currentUser, IOptions<ProductOptions> options)
{
    /// <summary>The name of the package written in French in the resources of the host (its project file names it).</summary>
    public const string ResourceName = "Samples/sample-project";

    /// <summary>The name of the package written in English.</summary>
    public const string EnglishResourceName = "Samples/sample-project-en";

    public async Task<ImportedProject> CreateAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Mode != HostMode.Desktop)
            throw DomainException.NotFound();
        var package = Package(currentUser.User?.Language == "fr" ? ResourceName : EnglishResourceName);
        var preview = await import.PreviewAsync(package, null, false, cancellationToken);
        var code = preview.CodeState == CodeState.Free ? null : await users.RunAsync(new FreeProjectCodeQuery(preview.PackageCode), cancellationToken);
        return await import.ImportAsync(package, code, false, cancellationToken);
    }

    /// <summary>The bytes of a sample package, as the repository holds it.</summary>
    public static byte[] Package(string resource)
    {
        using var stream = typeof(SampleProjectApi).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("The sample package is not embedded in the host.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return content.ToArray();
    }
}
