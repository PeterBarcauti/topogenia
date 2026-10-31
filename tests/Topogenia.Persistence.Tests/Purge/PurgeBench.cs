// PurgeBench.cs
// What the purge tests need: a complete project written, an operation at a chosen time, the plan, the purge, the rows left.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Trash;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Purge;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Purge;

internal static class PurgeBench
{
    /// <summary>Records of a project that the purge leaves, whatever it purges: the users and the audit (spec 5.5).</summary>
    public static readonly Type[] Survivors = [typeof(AppUser), typeof(AuditEvent)];

    /// <summary>A day after the retention of the default settings, from <paramref name="moment"/>.</summary>
    public static DateTime AfterRetention(DateTime moment) => moment.AddDays(TrashRules.DefaultRetentionDays + 1);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A complete fictitious project written in one save (<see cref="ProjectGraph"/>).</summary>
    public static async Task<ProjectGraph> GraphAsync(TestDatabase database)
    {
        var graph = ProjectGraph.New();
        await using var context = database.Context();
        context.AddRange(graph.Records);
        await context.SaveChangesAsync(Token);
        return graph;
    }

    public static async Task<ProjectWorkspace?> LoadAsync(TestDatabase database, Guid projectId)
    {
        await using var session = database.Session();
        return await new WorkspaceStore(session).LoadAsync(projectId, WorkspaceContent.All, Token);
    }

    /// <summary>An operation of the domain on the project, done at the time of <paramref name="context"/>, as a command would.</summary>
    public static async Task RunAsync(TestDatabase database, Guid projectId, OperationContext context, Action<ProjectWorkspace> operation)
    {
        await using var session = database.Session();
        var unit = new UnitOfWork(session);
        var store = new WorkspaceStore(session);
        await unit.RunAsync(async token =>
        {
            await unit.LockProjectAsync(projectId, token);
            var workspace = (await store.LoadAsync(projectId, WorkspaceContent.All, token))!;
            operation(workspace);
            await store.SaveAsync(workspace.Commit(context), token);
            return true;
        }, Token);
    }

    public static OperationContext At(Guid actorId, DateTime time) => new(actorId, time, "fr", AppSettings.Default);

    /// <summary>The project moved to the trash at <paramref name="time"/> by its owner.</summary>
    public static Task TrashAsync(TestDatabase database, ProjectGraph graph, DateTime time) =>
        RunAsync(database, graph.Project.Id, At(graph.Owner.Id, time), workspace => TrashRules.MoveToTrash(workspace, At(graph.Owner.Id, time), graph.Code));

    /// <summary>The plan of the daily purge of the project at <paramref name="now"/>, on the project read now.</summary>
    public static async Task<PurgePlan> PlanAsync(TestDatabase database, Guid projectId, DateTime now) =>
        PurgePlanner.Expired((await LoadAsync(database, projectId))!, now, AppSettings.Default);

    public static async Task<PurgeOutcome> PurgeAsync(TestDatabase database, Guid projectId, PurgePlan plan, int size, params IInterceptor[] interceptors)
    {
        await using var session = database.Session(null, interceptors);
        return await new TrashPurge(session, size).PurgeAsync(projectId, plan, Token);
    }

    /// <summary>Whether the row of a record still exists, found by its primary key.</summary>
    public static async Task<bool> ExistsAsync(TestDatabase database, object record)
    {
        await using var context = database.Context();
        var key = context.Model.FindEntityType(record.GetType())!.FindPrimaryKey()!;
        var values = key.Properties.Select(property => property.GetGetter().GetClrValue(record)).ToArray();
        var found = await context.FindAsync(record.GetType(), values);
        return found is not null;
    }

    public static async Task<int> LogEntriesAsync(TestDatabase database, Guid projectId)
    {
        await using var context = database.Context();
        return await context.Set<OperationLogEntry>().CountAsync(entry => entry.ProjectId == projectId, Token);
    }
}
