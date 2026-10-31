// ProjectReader.cs
// Adapter of the port of the reads across the projects: the live projects of a user, his trash, the codes in use (spec 3.4, annex A 1).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Reading;

/// <summary>
/// <see cref="IProjectReader"/> on a <see cref="StoreSession"/>, by LINQ only (no SQL written by hand, spec 4.3): each
/// call is one consistent read (ADR 031) of a few queries composed on the visible projects (an `EXISTS` on the members,
/// never a list of identifiers, spec 4.3-2), projected straight to the rows of the port. The counts are those of the 1.x
/// (`ServiceProjets.LireLignes`): live steps on live maps, live maps.
/// </summary>
public sealed class ProjectReader : IProjectReader
{
    private readonly StoreSession session;

    public ProjectReader(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    public Task<IReadOnlyList<ProjectListRow>> LiveProjectsAsync(ProjectVisibility visibility, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visibility);
        return ConsistentRead.RunAsync<IReadOnlyList<ProjectListRow>>(session, async context =>
        {
            var userId = visibility.UserId;
            var projects = Visible(context, visibility, null).Where(project => project.DeletedAt == null);
            var maps = context.Set<ProcessMap>().AsNoTracking();
            var steps = context.Set<Step>().AsNoTracking();
            var members = context.Set<ProjectMember>().AsNoTracking();
            var rows = await projects.Select(project => new
            {
                Project = project,
                RootMapId = maps.Where(map => map.ProjectId == project.Id && map.ParentStepId == null).Select(map => map.Id).FirstOrDefault(),
                Role = members.Where(member => member.ProjectId == project.Id && member.UserId == userId)
                    .Select(member => (MemberRole?)member.Role).FirstOrDefault(),
                Steps = steps.Count(step => step.DeletedAt == null
                    && maps.Any(map => map.Id == step.MapId && map.ProjectId == project.Id && map.DeletedAt == null)),
                Maps = maps.Count(map => map.ProjectId == project.Id && map.DeletedAt == null),
            }).ToListAsync(cancellationToken);
            var names = await context.Set<Translation>().AsNoTracking()
                .Where(translation => translation.ObjectType == TranslationObject.Project && translation.Field == TranslationField.Name
                    && projects.Any(project => project.Id == translation.ObjectId))
                .ToListAsync(cancellationToken);
            var byProject = names.ToLookup(translation => translation.ObjectId);
            return [.. rows.Select(row => new ProjectListRow(row.Project,
                byProject[row.Project.Id].ToDictionary(translation => translation.Language, translation => translation.Text, StringComparer.Ordinal),
                row.RootMapId, row.Role, row.Steps, row.Maps))];
        }, cancellationToken);
    }

    public Task<IReadOnlyList<TrashRow>> TrashAsync(ProjectVisibility visibility, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visibility);
        return ConsistentRead.RunAsync<IReadOnlyList<TrashRow>>(session, async context =>
        {
            var users = context.Set<AppUser>().AsNoTracking();
            var maps = context.Set<ProcessMap>().AsNoTracking();
            var steps = context.Set<Step>().AsNoTracking();
            var rows = await Visible(context, visibility, MemberRole.Facilitator).Where(project => project.DeletedAt != null)
                .Select(project => new
                {
                    Project = project,
                    DeletedBy = users.Where(user => user.Id == project.DeletedBy).Select(user => user.DisplayName).FirstOrDefault(),
                    Steps = steps.Count(step => step.DeletedAt == null
                        && maps.Any(map => map.Id == step.MapId && map.ProjectId == project.Id && map.DeletedAt == null)),
                }).ToListAsync(cancellationToken);
            return [.. rows.Select(row => new TrashRow(row.Project, row.DeletedBy, row.Steps))];
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ExistingProjectCode>> CodesAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        return ConsistentRead.RunAsync<IReadOnlyList<ExistingProjectCode>>(session, async context =>
            await context.Set<Project>().AsNoTracking().Where(project => project.Code == code)
                .Select(project => new ExistingProjectCode(project.Code, project.DeletedAt != null))
                .ToListAsync(cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<string>> CodesStartingWithAsync(string prefix, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        return ConsistentRead.RunAsync<IReadOnlyList<string>>(session, async context =>
            await context.Set<Project>().AsNoTracking().Where(project => project.Code.StartsWith(prefix))
                .Select(project => project.Code)
                .ToListAsync(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// The projects the user may see: every one, or those of which he is a member (with <paramref name="role"/> when one
    /// is given: the trash shows the projects he facilitated, A-2.15-15).
    /// </summary>
    private static IQueryable<Project> Visible(AppDbContext context, ProjectVisibility visibility, MemberRole? role)
    {
        var projects = context.Set<Project>().AsNoTracking();
        if (visibility.Everything)
            return projects;
        var userId = visibility.UserId;
        var members = context.Set<ProjectMember>().AsNoTracking().Where(member => member.UserId == userId);
        if (role is { } required)
            members = members.Where(member => member.Role == required);
        return projects.Where(project => members.Any(member => member.ProjectId == project.Id));
    }
}
