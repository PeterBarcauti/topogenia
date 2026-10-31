// CommandRunner.cs
// The command template: right, unit of work and lock, workspace, If-Match, domain, writing, log, view (spec 3.4).

using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Commands;

/// <summary>
/// Runs every write command in the same order (spec 3.4), so that no handler can skip or reorder a step:
/// <list type="number">
/// <item>the user must be authenticated (NON_AUTHENTIFIE) and the declared right is applied to the project of the target,
/// read inside the transaction (INTROUVABLE for a non-member, ACCES_REFUSE for a weaker role);</item>
/// <item>the unit of work runs the whole request and the project is locked (`structure_version + 1`, spec 4.7);</item>
/// <item>the workspace of the project is loaded with what the command and the view need;</item>
/// <item>the right to fill a sheet and the versions read by the client (If-Match) are checked on it; a request that lacks
/// the version its mode requires is refused there, VERSION_REQUISE (<see cref="IVersionRequirement"/>);</item>
/// <item>the domain runs, the workspace is committed and its change set written with the audit event of the command
/// (spec 5.5), and the command writes the operation log (spec 4.6: a new entry for an operation that can be undone; undo
/// and redo their own entries);</item>
/// <item>the reply carries the result, the operation, the warnings, the view of the map built from the same workspace and
/// the history of the user on the map.</item>
/// </list>
/// A refusal of the domain may be turned by the command into the refusal its route owes, reading in the unit the access of
/// the objects it names besides its target (AR-71).
/// The unit may run the work again after a deadlock or a busy database (ADR 030): the work does nothing outside the
/// database and builds a new operation context, with new identifiers, on each attempt. A refusal that the command must
/// still log (AR-05, a refused undo or redo) is logged in a second unit under the lock, after the first is rolled back.
/// </summary>
public sealed class CommandRunner
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAccessReader access;
    private readonly IWorkspaceStore store;
    private readonly IOperationLog log;
    private readonly IMapViewBuilder views;
    private readonly ICurrentUser currentUser;
    private readonly IClock clock;
    private readonly AppSettings settings;
    private readonly IVersionRequirement versions;

    public CommandRunner(
        IUnitOfWork unitOfWork, IAccessReader access, IWorkspaceStore store, IOperationLog log, IMapViewBuilder views,
        ICurrentUser currentUser, IClock clock, AppSettings settings, IVersionRequirement versions)
    {
        this.versions = versions;
        this.unitOfWork = unitOfWork;
        this.access = access;
        this.store = store;
        this.log = log;
        this.views = views;
        this.currentUser = currentUser;
        this.clock = clock;
        this.settings = settings;
    }

    public async Task<CommandOutcome<TResult>> RunAsync<TResult>(ProjectCommand<TResult> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = currentUser.User ?? throw new DomainException(ErrorCodes.NotAuthenticated, RefusalKind.Forbidden);
        var requirement = AccessPolicy.RequirementOf(command.GetType());
        if (requirement is RequiresSheetFillAttribute && command.SheetStepId is null)
            throw new InvalidOperationException($"The command {command.GetType().Name} requires the right to fill without naming its step.");
        AccessPolicy.DemandUser(requirement, user);
        Guid? lockedProject = null;
        try
        {
            return await unitOfWork.RunAsync(async token =>
            {
                var found = await access.FindAsync(command.Target, user.Id, token);
                var role = AccessPolicy.Demand(requirement, user, found, command.TargetsTrash);
                lockedProject = found!.ProjectId;
                return await RunUnitAsync(command, user, requirement, role, found.ProjectId, token);
            }, cancellationToken);
        }
        catch (DomainException refusal) when (lockedProject is { } projectId && command.LogsRefusal(refusal))
        {
            // AR-05: the refused unit is rolled back; what the log keeps of the refusal is written in a unit of its own.
            await unitOfWork.RunAsync(async token =>
            {
                await unitOfWork.LockProjectAsync(projectId, token);
                await command.LogRefusalAsync(log, user.Id, token);
                return true;
            }, cancellationToken);
            throw;
        }
    }

    private async Task<CommandOutcome<TResult>> RunUnitAsync<TResult>(ProjectCommand<TResult> command, CurrentUser user,
        AccessRequirementAttribute requirement, MemberRole role, Guid projectId, CancellationToken cancellationToken)
    {
        var structureVersion = await unitOfWork.LockProjectAsync(projectId, cancellationToken);
        await command.PrepareAsync(log, user.Id, cancellationToken);
        var workspace = await store.LoadAsync(projectId, command.Content | views.Content, cancellationToken)
            ?? throw DomainException.NotFound();
        if (requirement is RequiresSheetFillAttribute)
            AccessPolicy.DemandSheetFill(workspace, command.SheetStepId!.Value, role, user.Id);
        VersionCheck.EnsureRead(versions);
        foreach (var check in command.Versions)
            check.Ensure(workspace);
        var mapId = command.MapOf(workspace);
        var context = new OperationContext(user.Id, clock.UtcNow, user.Language, settings);
        TResult result;
        try
        {
            result = command.Execute(workspace, context);
        }
        catch (DomainException refusal)
        {
            throw await command.TranslateRefusalAsync(refusal, new RefusalContext(access, user, projectId), cancellationToken);
        }
        var changes = workspace.Commit(context);
        if (!changes.IsEmpty)
            await store.SaveAsync(changes, cancellationToken);
        if (command.AuditKind is { } kind && AuditTrail.OfChange(context, kind, projectId, changes, command.AuditDetails(changes)) is { } audit)
            await store.SaveAsync(new ChangeSet([new RecordChange(ChangeKind.Created, null, audit)]), cancellationToken);
        var operationId = mapId is { } loggedMap
            ? await command.LogAsync(log, context, projectId, loggedMap, changes, cancellationToken)
            : null;
        if (mapId is not { } viewed)
            return new CommandOutcome<TResult>(result, operationId, structureVersion, command.WarningsOf(result), null, null);
        var view = views.Build(workspace, viewed, new MapViewer(user.Id, role));
        var history = OperationStack.History(await log.EntriesAsync(viewed, user.Id, cancellationToken));
        return new CommandOutcome<TResult>(result, operationId, structureVersion, command.WarningsOf(result), view, history);
    }
}
