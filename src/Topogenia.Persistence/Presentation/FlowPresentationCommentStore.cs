// FlowPresentationCommentStore.cs
// Adapter of the port of the comments of presentation of the flows (ADR 099, lot J10): every line read for some flows, lines written or removed together in the unit of the request, added after the flows of an import.

using Topogenia.Application.Presentation;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Presentation;

/// <summary>
/// <see cref="IFlowPresentationCommentStore"/> on a <see cref="StoreSession"/>, by LINQ only, as
/// <see cref="PresentationCommentStore"/> keeps the comments of the steps: a line is a record of the persistence, beside
/// the flows (the closed core does not know it), its key its flow and its language, its version the concurrency token;
/// written only inside a unit of work, the lines of one request in one save. The work is the one of every table of
/// comments (<see cref="CommentLineStore{TLine}"/>), the flow of a line being its subject.
/// </summary>
public sealed class FlowPresentationCommentStore : IFlowPresentationCommentStore
{
    private readonly CommentLineStore<FlowPresentationComment> lines;

    public FlowPresentationCommentStore(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lines = new CommentLineStore<FlowPresentationComment>(session, TableNames.FlowPresentationComment, nameof(FlowPresentationComment.FlowId),
            comment => LineStamp.Of(comment.AsLine()));
    }

    public Task<IReadOnlyList<FlowPresentationComment>> OfFlowsAsync(IReadOnlyCollection<Guid> flowIds, CancellationToken cancellationToken) =>
        lines.OfAsync(flowIds, cancellationToken);

    public Task WriteAsync(IReadOnlyList<FlowCommentWrite> writes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writes);
        return lines.WriteAsync([.. writes.Select(write => new LineWrite<FlowPresentationComment>(write.FlowId, write.Language, write.Line, write.VersionRead))],
            cancellationToken);
    }

    public Task AddAsync(IReadOnlyList<FlowPresentationComment> comments, CancellationToken cancellationToken) => lines.AddAsync(comments, cancellationToken);
}
