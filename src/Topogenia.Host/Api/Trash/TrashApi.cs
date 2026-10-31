// TrashApi.cs
// The routes of the trash turned into their requests: move a project to the trash, list, restore, purge at once (annex A 1, A 2.15).

using Topogenia.Application.Commands;
using Topogenia.Application.Trash;

namespace Topogenia.Host.Api.Trash;

/// <summary>Runs the requests of the trash routes; the writes answer nothing (204, as the 1.x).</summary>
public sealed class TrashApi
{
    private readonly CommandRunner commands;
    private readonly UserRunner users;

    public TrashApi(CommandRunner commands, UserRunner users)
    {
        this.commands = commands;
        this.users = users;
    }

    /// <summary>`DELETE /api/projects/{id}?confirmation=CODE`: the code typed by the user, checked by the domain.</summary>
    public Task MoveToTrashAsync(Guid projectId, string? confirmation, string? ifMatch, CancellationToken cancellationToken) =>
        commands.RunAsync(MoveToTrashCommand.WithVersion(projectId, confirmation, ApiInput.Version(ifMatch)), cancellationToken);

    public Task<TrashView> ListAsync(CancellationToken cancellationToken) => users.RunAsync(new ListTrashQuery(), cancellationToken);

    public Task RestoreAsync(Guid projectId, CancellationToken cancellationToken) =>
        commands.RunAsync(new RestoreProjectCommand(projectId), cancellationToken);

    public Task PurgeAsync(Guid projectId, CancellationToken cancellationToken) =>
        users.RunAsync(new PurgeProjectCommand(projectId), cancellationToken);
}
