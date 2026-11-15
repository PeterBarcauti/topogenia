// RequestUser.cs
// The user of the current request, signed in once the authentication has named his login (annex A 2.1, spec 3.4).

using Microsoft.Extensions.Options;
using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Projects;
using Topogenia.Host.Composition;

namespace Topogenia.Host.Users;

/// <summary>The user of one request (scoped), null until <see cref="SignInMiddleware"/> signs him in.</summary>
public sealed class RequestUser : ICurrentUser
{
    public CurrentUser? User { get; set; }
}

/// <summary>
/// Signs in the login given by the authentication of the mode (Negotiate in the Server mode, the session of the Desktop
/// mode): the user is created at his first request, his administrator flag follows the configuration and his effective
/// language is read from the profile, else `Accept-Language` (<see cref="UserRunner.SignInAsync"/>). An anonymous request
/// keeps no user: the handlers refuse it (NON_AUTHENTIFIE, 401).
/// </summary>
public sealed class SignInMiddleware
{
    private readonly RequestDelegate next;

    public SignInMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context, RequestUser user, UserRunner runner)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(runner);
        if (context.User.Identity is { IsAuthenticated: true, Name: { Length: > 0 } login })
            user.User = await runner.SignInAsync(login, context.Request.Headers.AcceptLanguage.ToString(), context.RequestAborted);
        await next(context);
    }
}

/// <summary>The administrators of the configuration as it is now: the file is read again at each change (spec 5.3, A-2.1-26).</summary>
public sealed class ConfiguredAdministrators : IAdministrators
{
    private readonly IOptionsMonitor<ProductOptions> options;

    public ConfiguredAdministrators(IOptionsMonitor<ProductOptions> options)
    {
        this.options = options;
    }

    public IReadOnlyCollection<string> Logins => options.CurrentValue.Administrators;
}

/// <summary>
/// The directory that knows nobody: a new user takes his short login as display name (`UserRules.NewUser`). The modes
/// replace it by the directory of their accounts (the domain in the Server mode, the account of the process in the Desktop
/// mode, P4-T05 and P4-T06).
/// </summary>
public sealed class EmptyDirectory : IUserDirectory
{
    public Task<DirectoryEntry?> FindAsync(string login, CancellationToken cancellationToken) => Task.FromResult<DirectoryEntry?>(null);
}
