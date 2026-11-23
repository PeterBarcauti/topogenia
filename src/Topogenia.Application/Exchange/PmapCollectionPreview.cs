// PmapCollectionPreview.cs
// The preview of a template collection (ADR 114 points 4 and 6): each process with what it holds and a free code proposed, the codes proposed higher in the list counted; read only, the administrator alone.

using System.Text.Json.Serialization;
using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Application.Projects;
using Topogenia.Domain.Raci;

namespace Topogenia.Application.Exchange;

/// <summary>
/// A fault of a process of a collection, as the opening of a file reports it (FICHIER_INVALIDE, A-1.2-19): its place in
/// the `.pmap` of the process and its code; the host writes the message in the language of the person from its values.
/// </summary>
public sealed record CollectionFault(string Path, string Code, string Message, [property: JsonIgnore] IReadOnlyDictionary<string, object?> Values);

/// <summary>
/// A process of a collection previewed: its rank, its name (as the file writes it for a process in fault, when it is a readable text of 1 to 200 characters, never interpreted), the language of its texts, the code of its file, the free
/// code proposed, its description, what it holds, its points to check and how many addresses of its documents reach
/// another machine (web, network path: pre-audit 68, P3-02); its faults instead, without code proposed
/// nor counts, when its `.pmap` is refused; and its text as the host read it, which the interface sends as it is to the
/// opening of a file (`POST /api/projects/import-pmap`), never reading the collection again.
/// </summary>
public sealed record CollectionProcessPreview(int Index, string? Name, string? Language, string? FileCode, string? ProposedCode, string? Description,
    PmapCounts? Counts, IReadOnlyList<string> Uncertainties, int RemoteLocations, IReadOnlyList<CollectionFault> Faults, string Content);

/// <summary>A collection previewed: its name, its description and its processes in the order of the file.</summary>
public sealed record CollectionPreview(string Name, string? Description, IReadOnlyList<CollectionProcessPreview> Processes);

/// <summary>
/// `POST /api/projects/import-collection/preview` (administrator, ADR 114 point 4): the text bounded as the opening of a
/// file bounds it (<see cref="OpenPmapCommand.Bounded"/>), then read by the format with the settings of the opening (the
/// languages of the installation, every letter of the RACI), FICHIER_INVALIDE when its envelope breaks a rule. For each
/// process accepted, the code proposed (ADR 114 point 6): the code of its file, else the one its name proposes
/// (<see cref="ProjectCodeProposal.FromName"/>, none for a name that gives none); then the first free one by the rule of
/// the trash (<see cref="FreeProjectCodeQuery"/>), the codes proposed higher in the list counted as taken. Read only:
/// no unit of work, nothing written, neither project, operation log nor audit.
/// </summary>
[RequiresAdmin]
public sealed class PreviewCollectionQuery(IPmapFormat format, string? content) : UserRequest<CollectionPreview>
{
    public override async Task<CollectionPreview> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var collection = format.ReadCollection(OpenPmapCommand.Bounded(content), scope.Languages, RaciRules.KnownLetters);
        var proposed = new List<string>();
        var processes = new List<CollectionProcessPreview>();
        foreach (var process in collection.Processes)
        {
            var faults = process.Faults.Select(fault => new CollectionFault(fault.Path, fault.Code, "", fault.Values)).ToList();
            if (process.Document is not { } document)
            {
                processes.Add(new CollectionProcessPreview(process.Index, process.Name, null, null, null, null, null, [], 0, faults, process.Content));
                continue;
            }
            var code = await ProposedAsync(scope, document, proposed, cancellationToken);
            if (code is not null)
                proposed.Add(code);
            processes.Add(new CollectionProcessPreview(process.Index, document.ProjectName, process.Language, document.ProjectCode, code, process.Description,
                document.Counts, document.Uncertainties, document.RemoteLocations, faults, process.Content));
        }
        return new CollectionPreview(collection.Name, collection.Description, processes);
    }

    /// <summary>The code of the file or of the name, made free among the projects and the codes already proposed; null when there is none.</summary>
    private static async Task<string?> ProposedAsync(UserScope scope, IPmapDocument document, IReadOnlyCollection<string> proposed, CancellationToken cancellationToken)
    {
        var start = document.ProjectCode ?? ProjectCodeProposal.FromName(document.ProjectName);
        return start is null ? null : await FreeProjectCodeQuery.FirstFreeAsync(scope, start, proposed, cancellationToken);
    }
}
