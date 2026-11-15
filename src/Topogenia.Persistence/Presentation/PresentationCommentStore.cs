// PresentationCommentStore.cs
// Adapter of the port of the comments of presentation of the steps (ADR 099 revised): every line read for some steps, lines written or removed together in the unit of the request, added after the steps of an import.

using Topogenia.Application.Presentation;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Presentation;

/// <summary>
/// <see cref="IPresentationCommentStore"/> on a <see cref="StoreSession"/>, by LINQ only. A line of a comment is a record
/// of the persistence, beside the steps (the closed core does not know it): its key is its step and its language, its
/// version the concurrency token, so that two writers of the same line do not overwrite each other silently: the second
/// is refused `CONFLIT_VERSION`. It is written only inside a unit of work, the lines of one request in one save: all or
/// none; the reads go by packets of identifiers. The work is the one of every table of comments
/// (<see cref="CommentLineStore{TLine}"/>), the step of a line being its subject.
/// </summary>
public sealed class PresentationCommentStore : IPresentationCommentStore
{
    private readonly CommentLineStore<PresentationComment> lines;

    public PresentationCommentStore(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lines = new CommentLineStore<PresentationComment>(session, TableNames.PresentationComment, nameof(PresentationComment.StepId),
            comment => LineStamp.Of(comment.AsLine()));
    }

    public Task<IReadOnlyList<PresentationComment>> OfStepsAsync(IReadOnlyCollection<Guid> stepIds, CancellationToken cancellationToken) =>
        lines.OfAsync(stepIds, cancellationToken);

    public Task WriteAsync(IReadOnlyList<CommentWrite> writes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writes);
        return lines.WriteAsync([.. writes.Select(write => new LineWrite<PresentationComment>(write.StepId, write.Language, write.Line, write.VersionRead))],
            cancellationToken);
    }

    public Task AddAsync(IReadOnlyList<PresentationComment> comments, CancellationToken cancellationToken) => lines.AddAsync(comments, cancellationToken);
}
