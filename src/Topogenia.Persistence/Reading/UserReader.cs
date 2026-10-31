// UserReader.cs
// Adapter of the port of the users: by identifier, by login, by lists of logins or identifiers, the profile with its roles (spec 3.4, ADR 031, 033).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Reading;

/// <summary>
/// <see cref="IUserReader"/> on a <see cref="StoreSession"/>, by LINQ only: inside a unit of work in its transaction
/// (the unit run again after a creation lost on `UQ_app_user_login_normalized` finds the user, ADR 033), outside in one
/// consistent read per call (<see cref="ConsistentRead"/>). A login is compared by its normalised form, the unique key
/// of the table; a list of logins or of identifiers is sent in packets of 500 (spec 4.3-2).
/// </summary>
public sealed class UserReader : IUserReader
{
    private readonly StoreSession session;

    public UserReader(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    public Task<AppUser?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        ConsistentRead.RunAsync(session, context => Users(context).SingleOrDefaultAsync(user => user.Id == userId, cancellationToken), cancellationToken);

    public Task<AppUser?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        var normalized = Login.Normalize(login);
        return ConsistentRead.RunAsync(session,
            context => Users(context).SingleOrDefaultAsync(user => user.LoginNormalized == normalized, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<AppUser>> FindByLoginsAsync(IReadOnlyCollection<string> logins, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logins);
        var normalized = logins.Select(Login.Normalize).ToList();
        return ConsistentRead.RunAsync<IReadOnlyList<AppUser>>(session, async context =>
        {
            var found = new List<AppUser>();
            foreach (var chunk in Chunks.Of(normalized))
                found.AddRange(await Users(context).Where(user => chunk.Contains(user.LoginNormalized)).ToListAsync(cancellationToken));
            return found;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<AppUser>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        var ids = userIds.Distinct().ToList();
        return ConsistentRead.RunAsync<IReadOnlyList<AppUser>>(session, async context =>
        {
            var found = new List<AppUser>();
            foreach (var chunk in Chunks.Of(ids))
                found.AddRange(await Users(context).Where(user => chunk.Contains(user.Id)).ToListAsync(cancellationToken));
            return found;
        }, cancellationToken);
    }

    public Task<UserProfile?> ProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        ConsistentRead.RunAsync(session, async context =>
        {
            var user = await Users(context).SingleOrDefaultAsync(row => row.Id == userId, cancellationToken);
            if (user is null)
                return null;
            var live = context.Set<Project>().AsNoTracking().Where(project => project.DeletedAt == null);
            var roles = await context.Set<ProjectMember>().AsNoTracking()
                .Where(member => member.UserId == userId && live.Any(project => project.Id == member.ProjectId))
                .Select(member => new ProjectRole(member.ProjectId, member.Role, member.IsSponsor))
                .ToListAsync(cancellationToken);
            return new UserProfile(user, roles);
        }, cancellationToken);

    private static IQueryable<AppUser> Users(AppDbContext context) => context.Set<AppUser>().AsNoTracking();
}
