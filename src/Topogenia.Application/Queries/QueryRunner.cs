// QueryRunner.cs
// The query template: the declared right, then a projection of the workspace read in one consistent transaction (spec 3.4).

using Topogenia.Application.Authorization;
using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;

namespace Topogenia.Application.Queries;

/// <summary>
/// Runs every read of a project in the same order as the commands, without the lock nor any writing: the user must be
/// authenticated, the declared right is applied to the project of the target (INTROUVABLE for a non-member, ACCES_REFUSE
/// for a weaker role, the administrator as facilitator), the workspace is read in one consistent transaction (snapshot
/// in SQL Server, read transaction in SQLite, ADR 031) and projected by the query, with the history of the reader on a map
/// when the query asks for it. The access, the workspace and the history are separate reads: a member removed between
/// them still gets the state he was allowed to read an instant before.
/// </summary>
public sealed class QueryRunner
{
    private readonly IAccessReader access;
    private readonly IWorkspaceStore store;
    private readonly IOperationLog log;
    private readonly IMapViewBuilder views;
    private readonly ICurrentUser currentUser;

    public QueryRunner(IAccessReader access, IWorkspaceStore store, IOperationLog log, IMapViewBuilder views, ICurrentUser currentUser)
    {
        this.access = access;
        this.store = store;
        this.log = log;
        this.views = views;
        this.currentUser = currentUser;
    }

    public async Task<TResult> RunAsync<TResult>(ProjectQuery<TResult> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var user = currentUser.User ?? throw new DomainException(ErrorCodes.NotAuthenticated, RefusalKind.Forbidden);
        var requirement = AccessPolicy.RequirementOf(query.GetType());
        if (requirement is RequiresSheetFillAttribute && query.SheetStepId is null)
            throw new InvalidOperationException($"The query {query.GetType().Name} requires the right to fill without naming its step.");
        AccessPolicy.DemandUser(requirement, user);
        var found = await access.FindAsync(query.Target, user.Id, cancellationToken);
        var role = AccessPolicy.Demand(requirement, user, found, query.TargetsTrash);
        var content = query.ReadsMapView ? query.Content | views.Content : query.Content;
        var workspace = await store.LoadAsync(found!.ProjectId, content, cancellationToken) ?? throw DomainException.NotFound();
        if (requirement is RequiresSheetFillAttribute)
            AccessPolicy.DemandSheetFill(workspace, query.SheetStepId!.Value, role, user.Id);
        var history = query.HistoryMap is { } mapId ? OperationStack.History(await log.EntriesAsync(mapId, user.Id, cancellationToken)) : null;
        return query.Read(workspace, new QueryContext(user.Id, role, views, history));
    }
}
