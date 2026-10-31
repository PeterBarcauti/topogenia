// MemberApi.cs
// The routes of the members of a project turned into their requests: replace the whole list, read the participants (annex A 1, A 2.1).

using Topogenia.Application.Commands;
using Topogenia.Application.Members;
using Topogenia.Application.Ports;
using Topogenia.Application.Queries;
using Topogenia.Host.Api.Projects;

namespace Topogenia.Host.Api.Members;

/// <summary>
/// Runs the requests of the member routes. The body of `PUT /api/projects/{id}/members` is the complete list, a raw JSON
/// array of <see cref="MemberBody"/> as in the 1.x (`List&lt;MembreDemande&gt;`); the people it names are read through the
/// users of the database, the directory and the administrators of the configuration.
/// </summary>
public sealed class MemberApi
{
    private readonly CommandRunner commands;
    private readonly QueryRunner queries;
    private readonly PeopleSource people;

    public MemberApi(CommandRunner commands, QueryRunner queries, IUserReader users, IUserDirectory directory, IAdministrators administrators)
    {
        this.commands = commands;
        this.queries = queries;
        people = new PeopleSource(users, directory, administrators);
    }

    /// <summary>
    /// Replaces the members and answers the list as written, with the users invited and the people removed. The detail of
    /// the project is not read again: a facilitator who removes himself is no longer a member and still gets his success
    /// reply (A-2.1-21).
    /// </summary>
    public async Task<MembersReply> ReplaceAsync(Guid projectId, IReadOnlyList<MemberBody>? members, string? ifMatch, CancellationToken cancellationToken)
    {
        var entries = ApiInput.Required(ProjectApi.Members(members), "members");
        var command = ReplaceMembersCommand.WithVersion(projectId, entries, people, ApiInput.Version(ifMatch));
        var outcome = await commands.RunAsync(command, cancellationToken);
        return outcome.Result;
    }

    public Task<IReadOnlyList<MemberView>> ParticipantsAsync(Guid projectId, CancellationToken cancellationToken) =>
        queries.RunAsync(new GetParticipantsQuery(projectId), cancellationToken);
}
