// WorkspaceLoader.cs
// Reads the records of one project into a workspace: the structure and every counter always, the other groups on demand (ADR 015).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;

namespace Topogenia.Persistence.Loading;

/// <summary>
/// The loading of a <see cref="ProjectWorkspace"/> (ADR 015): the project first, then the structure that every operation
/// needs (counters, deletion batches, maps, lanes, steps, flows, RACI roles, frames with their steps and objects), live and
/// deleted, then the groups asked by <see cref="WorkspaceContent"/>. Every counter of the project is loaded
/// (architecture.md 3): a counter left out would be created again by the domain and refused by its key. The caller runs
/// it in one consistent transaction (<see cref="ConsistentRead"/>).
/// </summary>
internal static class WorkspaceLoader
{
    public static async Task<ProjectWorkspace?> LoadAsync(AppDbContext context, Guid projectId, WorkspaceContent content, CancellationToken cancellationToken)
    {
        var scope = new ProjectScope(context, projectId);
        var project = await scope.Project.SingleOrDefaultAsync(cancellationToken);
        if (project is null)
            return null;
        var records = new List<IRecord> { project };
        await AddAsync(records, scope.Counters, cancellationToken);
        await AddAsync(records, scope.Batches, cancellationToken);
        await AddAsync(records, scope.Maps, cancellationToken);
        await AddAsync(records, scope.Lanes, cancellationToken);
        await AddAsync(records, scope.Steps, cancellationToken);
        await AddAsync(records, scope.Flows, cancellationToken);
        await AddAsync(records, scope.Roles, cancellationToken);
        await AddAsync(records, scope.Dissolutions, cancellationToken);
        await AddAsync(records, scope.DissolutionSteps, cancellationToken);
        await AddAsync(records, scope.DissolutionObjects, cancellationToken);
        await AddGroupsAsync(records, scope, content, cancellationToken);
        return new ProjectWorkspace(projectId, records);
    }

    private static async Task AddGroupsAsync(List<IRecord> records, ProjectScope scope, WorkspaceContent content, CancellationToken cancellationToken)
    {
        if (content.HasFlag(WorkspaceContent.Sheets))
        {
            await AddAsync(records, scope.Sheets, cancellationToken);
            await AddAsync(records, scope.SheetOperations, cancellationToken);
            await AddAsync(records, scope.SheetVersions, cancellationToken);
        }
        if (content.HasFlag(WorkspaceContent.Documents))
        {
            await AddAsync(records, scope.Documents, cancellationToken);
            await AddAsync(records, scope.Uses, cancellationToken);
            await AddAsync(records, scope.PainPoints, cancellationToken);
            await AddAsync(records, scope.PainPointHistory, cancellationToken);
        }
        if (content.HasFlag(WorkspaceContent.Translations))
            await AddAsync(records, scope.Translations, cancellationToken);
        if (content.HasFlag(WorkspaceContent.Members))
        {
            await AddAsync(records, scope.Members, cancellationToken);
            await AddAsync(records, scope.MemberUsers, cancellationToken);
        }
    }

    private static async Task AddAsync<T>(List<IRecord> records, IQueryable<T> query, CancellationToken cancellationToken)
        where T : IRecord =>
        records.AddRange((await query.ToListAsync(cancellationToken)).Cast<IRecord>());
}
