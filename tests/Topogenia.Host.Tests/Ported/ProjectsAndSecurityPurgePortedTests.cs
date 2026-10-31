// ProjectsAndSecurityPurgePortedTests.cs
// The 1.x purge past the retention ported to the whole host on both databases: the purge of the host run at a later time, then read through the API (T-CORB-04).

using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Maintenance;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// A class of its own, so that its base holds no project of another test: the purge takes every project and every batch
/// past the retention of the base.
/// </summary>
public sealed class ProjectsAndSecurityPurgePortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>The clock of the host moved by some days.</summary>
    private sealed class LaterClock(int days) : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow.AddDays(days);
    }

    /// <summary>The daily purge of the host (the services of its scope) run as if <paramref name="days"/> days had passed.</summary>
    private static async Task PurgeAsync(ApiHost host, int days)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var purge = new ExpiredPurge(new LaterClock(days), services.GetRequiredService<ITrashPurge>(), services.GetRequiredService<IWorkspaceStore>(),
            services.GetRequiredService<AppSettings>(), services.GetRequiredService<IAuditStore>());
        var report = await purge.RunAsync(TestContext.Current.CancellationToken);
        Assert.Empty(report.Failures);
    }

    private static async Task<IReadOnlyList<Guid>> TrashIdsAsync(ApiClient client) =>
        [.. (await client.GetAsync("/api/trash")).Ok().GetProperty("projects").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private static List<Guid> StepIds(System.Text.Json.JsonElement map) => [.. map.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetGuid())];

    // T-CORB-04
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_purge_takes_the_projects_of_the_trash_and_the_deleted_objects_past_thirty_days_only_and_the_undo_no_longer_brings_them_back(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var trashed = await ApiProject.CreateAsync(host);
        await trashed.AddStepAsync(0, "Extraction");
        var live = await ApiProject.CreateAsync(host);
        var (kept, deleted) = (await live.AddStepAsync(0, "Tableau"), await live.AddStepAsync(1, "Extraction"));
        await live.AddFlowAsync(kept.Id, deleted.Id);
        await live.DetailAsync(deleted.Id);
        using var facilitator = live.Facilitator;
        using var admin = live.Admin;
        using var trashedFacilitator = trashed.Facilitator;
        (await trashedFacilitator.DeleteAsync($"/api/projects/{trashed.Id}?confirmation={trashed.Code}", await trashed.ProjectVersionAsync())).Ok(204);
        var deletion = (await facilitator.DeleteAsync($"/api/steps/{deleted.Id}?sub_map=delete", await live.StepVersionAsync(deleted.Id))).Ok()
            .GetProperty("operation_id").GetGuid();

        await PurgeAsync(host, 29);
        Assert.Contains(trashed.Id, await TrashIdsAsync(admin));
        var history = (await facilitator.GetAsync($"/api/maps/{live.RootMapId}/history")).Ok();
        Assert.Contains(deletion, history.GetProperty("undo").EnumerateArray().Select(item => item.GetProperty("operation_id").GetGuid()));

        await PurgeAsync(host, 31);
        Assert.DoesNotContain(trashed.Id, await TrashIdsAsync(admin));
        (await admin.PostAsync($"/api/trash/{trashed.Id}/restore")).Refused(404, "INTROUVABLE");
        Assert.Equal([kept.Id], StepIds(await live.MapAsync()));
        history = (await facilitator.GetAsync($"/api/maps/{live.RootMapId}/history")).Ok();
        Assert.DoesNotContain(deletion, history.GetProperty("undo").EnumerateArray().Select(item => item.GetProperty("operation_id").GetGuid()));
        (await facilitator.PostAsync($"/api/maps/{live.RootMapId}/undo")).Refused(409, "OPERATION_DEPASSEE");
        Assert.Equal([kept.Id], StepIds(await live.MapAsync()));
    }
}
