// IUserDirectory.cs
// Port of the directory of the accounts: what the domain, then the local accounts, know of a Windows login (spec 3.4, annex A 2.1).

using Topogenia.Domain.Projects;

namespace Topogenia.Application.Ports;

/// <summary>
/// The directory (the 1.x `IAnnuaire`): the display name and the e-mail of a login, from the domain, else from the local
/// accounts of the machine (annex A 2.1, A-2.1-25). It only reads, outside the database: the user created at the first
/// request and the invited users take what it says, the short login when it knows nothing (`UserRules.NewUser`). The host
/// implements it (P4-T05, P4-T06); a failure of the directory is an unknown login, never a refusal.
/// </summary>
public interface IUserDirectory
{
    /// <summary>What the directory knows of a login, or null when it knows nothing.</summary>
    Task<DirectoryEntry?> FindAsync(string login, CancellationToken cancellationToken);
}
