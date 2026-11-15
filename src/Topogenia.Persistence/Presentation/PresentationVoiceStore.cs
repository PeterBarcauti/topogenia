// PresentationVoiceStore.cs
// Adapter of the port of the voices of the projects (ADR 103, lot J10): every voice read for a project, a line written or removed in the unit of the request, added after the project of an import.

using Topogenia.Application.Presentation;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Presentation;

/// <summary>
/// <see cref="IPresentationVoiceStore"/> on a <see cref="StoreSession"/>, by LINQ only, as
/// <see cref="PresentationCommentStore"/> keeps the comments of the steps: a voice is a record of the persistence, beside
/// the projects (the closed core does not know it), its key its project and its language, its version the concurrency
/// token; written only inside a unit of work. The work is the one of every table kept by subject and language
/// (<see cref="CommentLineStore{TLine}"/>), the project of a voice being its subject.
/// </summary>
public sealed class PresentationVoiceStore : IPresentationVoiceStore
{
    private readonly CommentLineStore<PresentationVoice> lines;

    public PresentationVoiceStore(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lines = new CommentLineStore<PresentationVoice>(session, TableNames.PresentationVoice, nameof(PresentationVoice.ProjectId),
            voice => new LineStamp(voice.ProjectId, voice.Language, voice.Version, voice.UpdatedBy, voice.UpdatedAt));
    }

    public Task<IReadOnlyList<PresentationVoice>> OfProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        lines.OfAsync([projectId], cancellationToken);

    public Task WriteAsync(VoiceWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        return lines.WriteAsync([new LineWrite<PresentationVoice>(write.ProjectId, write.Language, write.Line, write.VersionRead)], cancellationToken);
    }

    public Task AddAsync(IReadOnlyList<PresentationVoice> voices, CancellationToken cancellationToken) => lines.AddAsync(voices, cancellationToken);
}
