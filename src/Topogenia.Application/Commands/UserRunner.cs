// UserRunner.cs
// The template of the requests that name no existing project: sign-in, right, unit of work or consistent reads, domain, writing (spec 3.4).

using Topogenia.Application.Audit;
using Topogenia.Application.Authorization;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Commands;

/// <summary>
/// Runs every <see cref="UserRequest{TResult}"/> in the same order as <see cref="CommandRunner"/>, so that no handler
/// checks a right itself (spec 3.4):
/// <list type="number">
/// <item>the user must be authenticated (NON_AUTHENTIFIE) and the declared right is read by <see cref="AccessPolicy"/>:
/// an administrator request is refused to anyone else before anything is read (ACCES_REFUSE);</item>
/// <item>a request that names a project gets it from <see cref="IAccessReader"/> in the expected state, live or in the
/// trash (INTROUVABLE otherwise, or for a non-member), with the role it declares;</item>
/// <item>a request that writes runs whole in one unit of work, with a new operation context on each attempt (ADR 030, 033);
/// a read runs each port call in one consistent read (ADR 031);</item>
/// <item>the projects a list may show are decided here once: those of the member, every one for an administrator
/// (`Droits.cs:30`);</item>
/// <item>a request that lacks the version its mode requires is refused once its right is checked, VERSION_REQUISE
/// (<see cref="IVersionRequirement"/>).</item>
/// </list>
/// <see cref="SignInAsync"/> is the step before: it gives the host the user of the request (annex A 2.1).
/// </summary>
public sealed class UserRunner
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAccessReader access;
    private readonly UserPorts ports;
    private readonly ICurrentUser currentUser;
    private readonly IClock clock;
    private readonly AppSettings settings;
    private readonly LanguageSettings languages;
    private readonly IVersionRequirement versions;
    private readonly SignInCache signedIn;

    public UserRunner(IUnitOfWork unitOfWork, IAccessReader access, UserPorts ports, ICurrentUser currentUser, IClock clock,
        AppSettings settings, LanguageSettings languages, IVersionRequirement versions, SignInCache signedIn)
    {
        this.signedIn = signedIn;
        this.versions = versions;
        this.unitOfWork = unitOfWork;
        this.access = access;
        this.ports = ports;
        this.currentUser = currentUser;
        this.clock = clock;
        this.settings = settings;
        this.languages = languages;
    }

    public async Task<TResult> RunAsync<TResult>(UserRequest<TResult> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = currentUser.User ?? throw new DomainException(ErrorCodes.NotAuthenticated, RefusalKind.Forbidden);
        var requirement = AccessPolicy.RequirementOf(request.GetType());
        if (requirement is RequiresSheetFillAttribute || (requirement is RequiresRoleAttribute && request.Target is null))
            throw new InvalidOperationException($"The request {request.GetType().Name} declares a right that needs a target it does not name.");
        if (request.LocksTarget && (request.Target is null || !request.InUnitOfWork))
            throw new InvalidOperationException($"The request {request.GetType().Name} locks a target without a target or a unit of work.");
        AccessPolicy.DemandUser(requirement, user);
        if (!request.InUnitOfWork)
            return await request.RunAsync(await ScopeAsync(request, requirement, user, cancellationToken), cancellationToken);
        try
        {
            return await unitOfWork.RunAsync(async token => await request.RunAsync(await ScopeAsync(request, requirement, user, token), token),
                cancellationToken);
        }
        finally
        {
            // A write may have changed the user (his profile, his last project): his next sign-in reads him again.
            signedIn.Forget(user.Id);
        }
    }

    /// <summary>
    /// The user of an authenticated login (annex A 2.1, `Utilisateurs.cs:99`), for the host to put in <see cref="ICurrentUser"/>
    /// at each request: created at his first request with what the directory says (`UserRules.NewUser`), his administrator
    /// flag synchronised on the configuration both ways (`UserRules.WithAdministratorFlag`, A-2.1-26), and his effective
    /// language (profile, else `Accept-Language`, else the default, AR-51). A user already up to date is only read, and kept
    /// a short time (<see cref="SignInCache"/>, weakness 1.x 5.6-6: no reading at each request); else one unit, run again
    /// whole when a concurrent first request created him since (ADR 033). An invalid login is ACCES_REFUSE.
    /// </summary>
    public async Task<CurrentUser> SignInAsync(string login, string? acceptLanguage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        var now = clock.UtcNow;
        var known = signedIn.Find(login, now) ?? await ports.Users.FindByLoginAsync(login, cancellationToken);
        var user = known is not null && !UserRules.WithAdministratorFlag(Context(known.Id), known, ports.Administrators.Logins).Changed
            ? known
            : await unitOfWork.RunAsync(token => SignInUnitAsync(login, token), cancellationToken);
        signedIn.Keep(user, now);
        return new CurrentUser(user.Id, user.Login, user.DisplayName, languages.EffectiveLanguage(user.Language, acceptLanguage), user.IsAdmin);
    }

    private async Task<AppUser> SignInUnitAsync(string login, CancellationToken cancellationToken)
    {
        var administrators = ports.Administrators.Logins;
        var found = await ports.Users.FindByLoginAsync(login, cancellationToken);
        if (found is null)
        {
            var entry = await ports.Directory.FindAsync(login, cancellationToken);
            var context = Context(Guid.Empty);
            var created = UserRules.NewUser(context, login, entry, administrators);
            var audit = AuditTrail.Event(context, AuditKinds.UserCreated, created.Id, null);
            await ports.Store.SaveAsync(new ChangeSet([new RecordChange(ChangeKind.Created, null, created), new RecordChange(ChangeKind.Created, null, audit)]),
                cancellationToken);
            return created;
        }
        var update = UserRules.WithAdministratorFlag(Context(found.Id), found, administrators);
        if (update.Changed)
            await ports.Store.SaveAsync(update.Changes, cancellationToken);
        return update.Record;
    }

    private async Task<UserScope> ScopeAsync<TResult>(UserRequest<TResult> request, AccessRequirementAttribute requirement, CurrentUser user,
        CancellationToken cancellationToken)
    {
        Guid? projectId = null;
        if (request.Target is { } target)
        {
            var found = await access.FindAsync(target, user.Id, cancellationToken);
            AccessPolicy.Demand(requirement, user, found, request.TargetsTrash);
            projectId = found!.ProjectId;
            if (request.LocksTarget)
                await unitOfWork.LockProjectAsync(projectId.Value, cancellationToken);
        }
        VersionCheck.EnsureRead(versions);
        return new UserScope(user, projectId, new ProjectVisibility(user.Id, user.IsAdmin),
            new OperationContext(user.Id, clock.UtcNow, user.Language, settings), ports, languages, settings);
    }

    /// <summary>The context of a sign-in: the user acts on himself (nobody yet at his creation, which keeps no author).</summary>
    private OperationContext Context(Guid actorId) => new(actorId, clock.UtcNow, languages.DefaultLanguage, settings);
}
