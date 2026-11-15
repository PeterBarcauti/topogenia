// WindowsDirectory.cs
// The directory of the server mode: display name and e-mail of a Windows login, from the domain, else from the local accounts (annex A 2.1, A-2.1-25).

using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using Topogenia.Application.Ports;
using Topogenia.Domain.Projects;

namespace Topogenia.Host.Server;

/// <summary>
/// The 1.x `AnnuaireWindows`: the account is looked for in the domain of the machine, then among its local accounts; a
/// machine outside any domain goes straight to the local accounts. An account found without a display name, an account
/// found nowhere, another system than Windows or a directory that fails give what the domain knows, else nothing: the
/// user then takes his short login (`UserRules.NewUser`). A failure is logged by its type only, never with the login
/// (spec 5.4: the logs hold no list of logins) nor the message of the directory.
/// </summary>
public sealed partial class WindowsDirectory(ILogger<WindowsDirectory> logger) : IUserDirectory
{
    public Task<DirectoryEntry?> FindAsync(string login, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        if (!OperatingSystem.IsWindows())
            return Task.FromResult<DirectoryEntry?>(null);
        try
        {
            return Task.FromResult(Find(login));
        }
        catch (Exception failure) when (failure is PrincipalException or InvalidOperationException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            LogUnavailable(logger, failure.GetType().FullName ?? failure.GetType().Name);
            return Task.FromResult<DirectoryEntry?>(null);
        }
    }

    [SupportedOSPlatform("windows")]
    private static DirectoryEntry? Find(string login)
    {
        foreach (var type in new[] { ContextType.Domain, ContextType.Machine })
        {
            try
            {
                using var context = new PrincipalContext(type);
                using var principal = UserPrincipal.FindByIdentity(context, login);
                if (principal is not null)
                    return new DirectoryEntry(string.IsNullOrWhiteSpace(principal.DisplayName) ? null : principal.DisplayName,
                        string.IsNullOrWhiteSpace(principal.EmailAddress) ? null : principal.EmailAddress);
            }
            catch (PrincipalServerDownException)
            {
                // A machine outside any domain: the local accounts are next.
            }
        }
        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Directory unavailable: {Type}; the short login is used.")]
    private static partial void LogUnavailable(ILogger logger, string type);
}
