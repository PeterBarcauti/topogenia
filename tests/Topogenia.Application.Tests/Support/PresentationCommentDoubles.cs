// PresentationCommentDoubles.cs
// The comments of presentation in memory, those of the steps and those of the flows (ADR 099 revised): every line read for some steps or flows, lines written together in a unit only, the version read of each checked as the persistence does.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;

namespace Topogenia.Application.Tests.Support;

/// <summary>
/// <see cref="IPresentationCommentStore"/> on the <see cref="InMemoryDatabase"/>: rolled back with its unit; a version read
/// that is no longer the one kept is CONFLIT_VERSION, as the concurrency token of the table refuses it, and none of the
/// lines of the request is then written; an import adds only lines that are not there.
/// </summary>
public sealed class InMemoryPresentationCommentStore(InMemoryDatabase database) : IPresentationCommentStore
{
    /// <summary>The number of calls that wrote something.</summary>
    public int Writes { get; private set; }

    /// <summary>The number of lines written or removed.</summary>
    public int Lines { get; private set; }

    public Task<IReadOnlyList<PresentationComment>> OfStepsAsync(IReadOnlyCollection<Guid> stepIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stepIds);
        var asked = stepIds.ToHashSet();
        IReadOnlyList<PresentationComment> found = [.. database.Comments.Values.Where(comment => asked.Contains(comment.StepId))];
        return Task.FromResult(found);
    }

    public Task WriteAsync(IReadOnlyList<CommentWrite> writes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writes);
        if (!database.InTransaction)
            throw new InvalidOperationException("A comment of presentation is written in a unit of work.");
        if (writes.Select(write => (write.StepId, write.Language)).Distinct().Count() != writes.Count)
            throw new ArgumentException("A line of a comment is written once in a request.", nameof(writes));
        foreach (var write in writes)
        {
            var kept = database.Comments.GetValueOrDefault((write.StepId, write.Language));
            if ((kept?.Version ?? 0) != write.VersionRead)
            {
                throw DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
                {
                    ["object"] = "presentation_comment", ["updated_by"] = kept?.UpdatedBy, ["updated_at"] = kept?.UpdatedAt,
                });
            }
        }
        foreach (var write in writes)
            database.PutComment(write.StepId, write.Language, write.Line);
        if (writes.Count > 0)
            Writes++;
        Lines += writes.Count;
        return Task.CompletedTask;
    }

    public Task AddAsync(IReadOnlyList<PresentationComment> comments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comments);
        if (!database.InTransaction)
            throw new InvalidOperationException("The comments of an import are added in its unit of work.");
        foreach (var comment in comments)
        {
            if (database.Comments.ContainsKey((comment.StepId, comment.Language)))
                throw new InvalidOperationException("A step has one line of comment of presentation per language at most.");
            database.PutComment(comment.StepId, comment.Language, comment);
        }
        Writes++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// <see cref="IFlowPresentationCommentStore"/> on the <see cref="InMemoryDatabase"/>, as
/// <see cref="InMemoryPresentationCommentStore"/> keeps the comments of the steps: rolled back with its unit; a version
/// read that is no longer the one kept is CONFLIT_VERSION and none of the lines of the request is then written; an import
/// adds only lines that are not there.
/// </summary>
public sealed class InMemoryFlowPresentationCommentStore(InMemoryDatabase database) : IFlowPresentationCommentStore
{
    /// <summary>The number of calls that wrote something.</summary>
    public int Writes { get; private set; }

    /// <summary>The number of lines written or removed.</summary>
    public int Lines { get; private set; }

    public Task<IReadOnlyList<FlowPresentationComment>> OfFlowsAsync(IReadOnlyCollection<Guid> flowIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flowIds);
        var asked = flowIds.ToHashSet();
        IReadOnlyList<FlowPresentationComment> found = [.. database.FlowComments.Values.Where(comment => asked.Contains(comment.FlowId))];
        return Task.FromResult(found);
    }

    public Task WriteAsync(IReadOnlyList<FlowCommentWrite> writes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writes);
        if (!database.InTransaction)
            throw new InvalidOperationException("A comment of presentation is written in a unit of work.");
        if (writes.Select(write => (write.FlowId, write.Language)).Distinct().Count() != writes.Count)
            throw new ArgumentException("A line of a comment is written once in a request.", nameof(writes));
        foreach (var write in writes)
        {
            var kept = database.FlowComments.GetValueOrDefault((write.FlowId, write.Language));
            if ((kept?.Version ?? 0) != write.VersionRead)
            {
                throw DomainException.Conflict(ErrorCodes.VersionConflict, new Dictionary<string, object?>
                {
                    ["object"] = "flow_presentation_comment", ["updated_by"] = kept?.UpdatedBy, ["updated_at"] = kept?.UpdatedAt,
                });
            }
        }
        foreach (var write in writes)
            database.PutFlowComment(write.FlowId, write.Language, write.Line);
        if (writes.Count > 0)
            Writes++;
        Lines += writes.Count;
        return Task.CompletedTask;
    }

    public Task AddAsync(IReadOnlyList<FlowPresentationComment> comments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comments);
        if (!database.InTransaction)
            throw new InvalidOperationException("The comments of an import are added in its unit of work.");
        foreach (var comment in comments)
        {
            if (database.FlowComments.ContainsKey((comment.FlowId, comment.Language)))
                throw new InvalidOperationException("A flow has one line of comment of presentation per language at most.");
            database.PutFlowComment(comment.FlowId, comment.Language, comment);
        }
        Writes++;
        return Task.CompletedTask;
    }
}
