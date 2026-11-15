// AccessReader.cs
// Adapter of the access port: the project of the object named by a request and the membership of the user (annex A 1, 1.x `Droits.RoleDans`).

using Microsoft.EntityFrameworkCore;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Loading;

/// <summary>
/// <see cref="IAccessReader"/> on a <see cref="StoreSession"/>: inside a unit of work, in its transaction, before the
/// lock (the right is decided on the state that the unit then writes); outside, in a consistent read (ADR 031). The
/// object is found whether it is deleted logically or not; the project is found whether it is in the trash or not.
/// </summary>
public sealed class AccessReader : IAccessReader
{
    private readonly StoreSession session;

    public AccessReader(StoreSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    public Task<ProjectAccess?> FindAsync(AccessTarget target, Guid userId, CancellationToken cancellationToken) =>
        ConsistentRead.RunAsync(session, context => FindAsync(context, target, userId, cancellationToken), cancellationToken);

    private static async Task<ProjectAccess?> FindAsync(AppDbContext context, AccessTarget target, Guid userId, CancellationToken cancellationToken)
    {
        var projectId = await ProjectOf(context, target).FirstOrDefaultAsync(cancellationToken);
        if (projectId is not { } id)
            return null;
        var project = await context.Set<Project>().AsNoTracking().Where(row => row.Id == id)
            .Select(row => new { row.DeletedAt }).SingleOrDefaultAsync(cancellationToken);
        if (project is null)
            return null;
        var role = await context.Set<ProjectMember>().AsNoTracking().Where(member => member.ProjectId == id && member.UserId == userId)
            .Select(member => (MemberRole?)member.Role).SingleOrDefaultAsync(cancellationToken);
        return new ProjectAccess(id, project.DeletedAt is not null, role);
    }

    /// <summary>The project of the target, by the map that holds it: one query, whatever the kind.</summary>
    private static IQueryable<Guid?> ProjectOf(AppDbContext context, AccessTarget target)
    {
        var maps = context.Set<ProcessMap>().AsNoTracking();
        var id = target.Id;
        return target.Kind switch
        {
            TargetKind.Project => context.Set<Project>().AsNoTracking().Where(project => project.Id == id).Select(project => (Guid?)project.Id),
            TargetKind.Map => maps.Where(map => map.Id == id).Select(map => (Guid?)map.ProjectId),
            TargetKind.Lane => context.Set<Lane>().AsNoTracking().Where(lane => lane.Id == id)
                .Join(maps, lane => lane.MapId, map => map.Id, (_, map) => (Guid?)map.ProjectId),
            TargetKind.Step => context.Set<Step>().AsNoTracking().Where(step => step.Id == id)
                .Join(maps, step => step.MapId, map => map.Id, (_, map) => (Guid?)map.ProjectId),
            TargetKind.Flow => context.Set<Flow>().AsNoTracking().Where(flow => flow.Id == id)
                .Join(maps, flow => flow.MapId, map => map.Id, (_, map) => (Guid?)map.ProjectId),
            TargetKind.Dissolution => context.Set<Dissolution>().AsNoTracking().Where(dissolution => dissolution.Id == id)
                .Join(maps, dissolution => dissolution.MapId, map => map.Id, (_, map) => (Guid?)map.ProjectId),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target.Kind, "Unknown kind of target."),
        };
    }
}
