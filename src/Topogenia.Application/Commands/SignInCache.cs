// SignInCache.cs
// The users signed in, kept a short time by login: no reading of the database at each request (weakness 1.x 5.6-6, A-4-47).

using System.Collections.Concurrent;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Commands;

/// <summary>
/// The user of a login as the last sign-in read or wrote him, for <see cref="Duration"/> (spec 5.6-6: a short cache per
/// user, the 1.x read and wrote him at each request). A request of the user that writes forgets him at once
/// (<see cref="UserRunner"/>): his next request reads his profile again. A change of the administrators of the
/// configuration is seen at once, the flag being compared with the configuration on every sign-in. Another process
/// (a second instance behind the same database) sees a change of the profile within <see cref="Duration"/>.
/// </summary>
public sealed class SignInCache
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, (AppUser User, DateTime Until)> users = new(StringComparer.Ordinal);

    /// <summary>The user of <paramref name="login"/> kept until after <paramref name="now"/>, or null.</summary>
    public AppUser? Find(string login, DateTime now) =>
        users.TryGetValue(Login.Normalize(login), out var kept) && kept.Until > now ? kept.User : null;

    public void Keep(AppUser user, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(user);
        users[user.LoginNormalized] = (user, now + Duration);
    }

    /// <summary>Forgets the user: his next sign-in reads him again.</summary>
    public void Forget(Guid userId)
    {
        foreach (var kept in users.Where(pair => pair.Value.User.Id == userId).ToList())
            users.TryRemove(kept);
    }
}
