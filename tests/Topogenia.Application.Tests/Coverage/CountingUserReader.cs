// CountingUserReader.cs
// A reader of the users that counts its calls by kind before it hands them to the reader it wraps (A-6-24).

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.Coverage;

/// <summary>Counts the reads of the users, one counter per method, so that a test proves how many reads a handler makes.</summary>
public sealed class CountingUserReader(IUserReader inner) : IUserReader
{
    public int ById { get; private set; }

    public int ByLogin { get; private set; }

    public int ByLogins { get; private set; }

    public int Profiles { get; private set; }

    public int ByIds { get; private set; }

    public Task<AppUser?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        ById++;
        return inner.FindAsync(userId, cancellationToken);
    }

    public Task<AppUser?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        ByLogin++;
        return inner.FindByLoginAsync(login, cancellationToken);
    }

    public Task<IReadOnlyList<AppUser>> FindByLoginsAsync(IReadOnlyCollection<string> logins, CancellationToken cancellationToken)
    {
        ByLogins++;
        return inner.FindByLoginsAsync(logins, cancellationToken);
    }

    public Task<IReadOnlyList<AppUser>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        ByIds++;
        return inner.FindManyAsync(userIds, cancellationToken);
    }

    public Task<UserProfile?> ProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        Profiles++;
        return inner.ProfileAsync(userId, cancellationToken);
    }
}
