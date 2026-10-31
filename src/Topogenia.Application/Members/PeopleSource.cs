// PeopleSource.cs
// Reads the people a list of members names: the users of the database, the directory for the unknown logins, the administrators (annex A 2.1).

using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Members;

/// <summary>
/// What `MemberRules.Resolve` needs to decide (ADR 033): the known users of the entered logins, read in the unit of the
/// request on each of its attempts (a unit run again after a creation lost to a concurrent one finds the user created
/// since), what the directory says of the logins nobody has yet, and the administrators of the configuration. A login that
/// is not valid is left out: the domain refuses it (MEMBRE_LOGIN_INVALIDE) before it looks anybody up.
/// </summary>
public sealed class PeopleSource
{
    private readonly IUserReader users;
    private readonly IUserDirectory directory;
    private readonly IAdministrators administrators;

    public PeopleSource(IUserReader users, IUserDirectory directory, IAdministrators administrators)
    {
        this.users = users;
        this.directory = directory;
        this.administrators = administrators;
    }

    /// <summary>The source on the ports of a user request.</summary>
    public static PeopleSource Of(UserPorts ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        return new PeopleSource(ports.Users, ports.Directory, ports.Administrators);
    }

    public async Task<PeopleLookup> LookupAsync(IEnumerable<MemberEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var logins = entries.Select(entry => entry.Login?.Trim())
            .Where(login => Login.TryParse(login, out _))
            .Select(login => login!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var known = logins.Count == 0 ? [] : await users.FindByLoginsAsync(logins, cancellationToken);
        var names = known.Select(user => user.LoginNormalized).ToHashSet(StringComparer.Ordinal);
        var found = new Dictionary<string, DirectoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var login in logins.Where(login => !names.Contains(Login.Normalize(login))))
        {
            if (await directory.FindAsync(login, cancellationToken) is { } entry)
                found[login] = entry;
        }
        return new PeopleLookup(known, found, administrators.Logins);
    }
}
