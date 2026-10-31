// UndoBatchPurgeTests.cs
// The batch written by an undo is purged with the operation undone, on both databases: nothing is left to redo and the rest of the stack stays (spec 4.6, S-4.6-11, ADR 042).

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Maintenance;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Maintenance;

/// <summary>A class of its own, so that its base holds no project of another test: the purge takes every expired batch.</summary>
public sealed class UndoBatchPurgeTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>The clock of the host moved by some days.</summary>
    private sealed class LaterClock(int days) : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow.AddDays(days);
    }

    private static async Task PurgeAsync(ApiHost host, int days)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var purge = new ExpiredPurge(new LaterClock(days), services.GetRequiredService<ITrashPurge>(), services.GetRequiredService<IWorkspaceStore>(),
            services.GetRequiredService<AppSettings>(), services.GetRequiredService<IAuditStore>());
        var report = await purge.RunAsync(TestContext.Current.CancellationToken);
        Assert.Empty(report.Failures);
    }

    private static List<Guid> Operations(JsonElement history, string stack) =>
        [.. history.GetProperty(stack).EnumerateArray().Select(item => item.GetProperty("operation_id").GetGuid())];

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_batch_of_an_undo_is_purged_with_the_operation_undone_and_the_rest_of_the_stack_stays(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        using var facilitator = project.Facilitator;
        var history = $"/api/maps/{project.RootMapId}/history";
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Ventes" })).Ok();
        var lane = Operations((await facilitator.GetAsync(history)).Ok(), "undo")[0];
        var step = await project.AddStepAsync(0, "Tableau");
        var creation = Operations((await facilitator.GetAsync(history)).Ok(), "undo")[0];
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        var undone = (await facilitator.GetAsync(history)).Ok();
        Assert.Equal([lane], Operations(undone, "undo"));
        Assert.Equal([creation], Operations(undone, "redo"));

        await PurgeAsync(host, 29);
        Assert.Equal([creation], Operations((await facilitator.GetAsync(history)).Ok(), "redo"));

        await PurgeAsync(host, 31);
        var purged = (await facilitator.GetAsync(history)).Ok();
        Assert.Empty(Operations(purged, "redo"));
        Assert.Equal([lane], Operations(purged, "undo"));
        Assert.DoesNotContain(step.Id, (await project.MapAsync()).GetProperty("steps").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.DoesNotContain("Ventes", (await project.MapAsync()).GetProperty("lanes").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
    }
}
