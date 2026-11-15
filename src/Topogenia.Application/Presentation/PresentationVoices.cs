// PresentationVoices.cs
// The voice of a project (ADR 103, lot J10): the voice of the browser that reads the subtitles of a presentation in a language, kept by the project for whoever presents it next; one line per project and language, beside the closed core; chosen by a facilitator, read by anyone who may read the project.

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Presentation;

/// <summary>
/// The voice a project keeps for a language, as it is kept: its project (the line goes before it at its purge) and its
/// language, which are its key; the name of the voice as the browser gives it; whether it is a voice of the computer
/// (otherwise a voice read online); its version and its last author. A voice has neither original nor translation: each
/// language has its own.
/// </summary>
public sealed record PresentationVoice(Guid ProjectId, string Language, string Name, bool Local, int Version, DateTime UpdatedAt, Guid UpdatedBy);

/// <summary>The voice of a language as a presentation reads it: its language, its name, whether it is one of the computer, its version for the If-Match of its change.</summary>
public sealed record PresentationVoiceView(string Language, string Name, bool Local, int Version)
{
    public static PresentationVoiceView Of(PresentationVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        return new(voice.Language, voice.Name, voice.Local, voice.Version);
    }

    /// <summary>The voices in a stable order: by language.</summary>
    public static IReadOnlyList<PresentationVoiceView> Of(IEnumerable<PresentationVoice> voices) =>
        [.. voices.OrderBy(voice => voice.Language, StringComparer.Ordinal).Select(Of)];
}

/// <summary>
/// The voice of a language to write: created or replaced by <see cref="Line"/>, or removed when it is null;
/// <see cref="VersionRead"/> is the version of the line the writer read (0 for none), checked by the store.
/// </summary>
public sealed record VoiceWrite(Guid ProjectId, string Language, PresentationVoice? Line, int VersionRead);

/// <summary>
/// The voices beside the projects, as <see cref="IPresentationCommentStore"/> keeps the comments beside the steps: read
/// for a project, written one line at a time in the unit of a request, or all those of an import.
/// </summary>
public interface IPresentationVoiceStore
{
    /// <summary>Every voice the project keeps (none: an empty list), in one consistent read or in the unit of the request.</summary>
    Task<IReadOnlyList<PresentationVoice>> OfProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// In the unit of work of the request: the line written (created or replaced) or removed; the version read of it
    /// checked (0 for none): a line changed meanwhile is CONFLIT_VERSION.
    /// </summary>
    Task WriteAsync(VoiceWrite write, CancellationToken cancellationToken);

    /// <summary>In the unit of work of an import: the voices of the project it made, added after it.</summary>
    Task AddAsync(IReadOnlyList<PresentationVoice> voices, CancellationToken cancellationToken);
}

/// <summary>
/// `GET /api/projects/{id}/presentation-voices` (3.2, ADR 103, reader): the voice the project keeps for each language
/// that has one, by language, which a presentation reads when it starts; INTROUVABLE for a project that is not live.
/// </summary>
[RequiresRole(MemberRole.Reader)]
public sealed class ReadPresentationVoicesQuery(Guid projectId) : UserRequest<IReadOnlyList<PresentationVoiceView>>
{
    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override async Task<IReadOnlyList<PresentationVoiceView>> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return PresentationVoiceView.Of(await scope.Ports.Voices.OfProjectAsync(scope.Project, cancellationToken));
    }
}

/// <summary>
/// `PUT /api/projects/{id}/presentation-voices/{language}` (3.2, ADR 103, facilitator): the voice of a live project for
/// the language given, kept or removed by an empty name (<see cref="PresentationVoiceRules.Chosen"/>); the version of
/// that line read in If-Match (0 when the language had no voice). The project is locked meanwhile, so that neither its
/// purge nor another writer of its voices interleaves. Nothing goes in the history of the maps: a voice is not undone by
/// Ctrl+Z, it is chosen again; no event of audit, as for a comment of presentation. The reply is every voice of the
/// project after the change.
/// </summary>
[RequiresRole(MemberRole.Facilitator)]
public sealed class SetPresentationVoiceRequest(Guid projectId, string? language, string? name, bool? local) : UserRequest<IReadOnlyList<PresentationVoiceView>>
{
    /// <summary>The If-Match of the request, when the client sent one.</summary>
    public int? Version { get; init; }

    public override AccessTarget? Target => AccessTarget.Project(projectId);

    public override bool InUnitOfWork => true;

    public override bool LocksTarget => true;

    public override async Task<IReadOnlyList<PresentationVoiceView>> RunAsync(UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var asked = PresentationVoiceRules.Asked(name, local);
        var spoken = PresentationCommentRules.Language(language, scope.Languages);
        var kept = await scope.Ports.Voices.OfProjectAsync(scope.Project, cancellationToken);
        var line = kept.FirstOrDefault(voice => voice.Language == spoken);
        PresentationVoiceRules.EnsureRead(line, Version);
        if (PresentationVoiceRules.Chosen(scope.Project, spoken, asked, line, scope.Operation) is not { } write)
            return PresentationVoiceView.Of(kept);
        await scope.Ports.Voices.WriteAsync(write, cancellationToken);
        return PresentationVoiceView.Of(PresentationVoiceRules.After(kept, write));
    }

    /// <summary>The If-Match of the request on the voice of the language, when the client sent one.</summary>
    public static SetPresentationVoiceRequest WithVersion(Guid projectId, string? language, string? name, bool? local, int? version) =>
        new(projectId, language, name, local) { Version = version };
}
