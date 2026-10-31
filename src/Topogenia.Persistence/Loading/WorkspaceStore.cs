// WorkspaceStore.cs
// Adapter of the workspace port: loads a project into a workspace and writes the change set of an operation (ADR 015, 016).

using Topogenia.Application.Ports;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Loading;

/// <summary>
/// <see cref="IWorkspaceStore"/> on a <see cref="StoreSession"/>: the loading in one consistent transaction
/// (<see cref="ConsistentRead"/>), the writing in the transaction of the open unit of work (<see cref="ChangeSetWriter"/>).
/// </summary>
public sealed class WorkspaceStore : IWorkspaceStore
{
    private readonly StoreSession session;

    public WorkspaceStore(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    public Task<ProjectWorkspace?> LoadAsync(Guid projectId, WorkspaceContent content, CancellationToken cancellationToken) =>
        ConsistentRead.RunAsync(session, context => WorkspaceLoader.LoadAsync(context, projectId, content, cancellationToken), cancellationToken);

    public Task SaveAsync(ChangeSet changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (!session.InUnit)
            throw new InvalidOperationException("A change set is written inside a unit of work.");
        return ChangeSetWriter.WriteAsync(session.Context, session.Dialect, changes, session.LockedProjects, cancellationToken);
    }
}
