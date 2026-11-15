// AuditRoutesTests.cs
// The audit trail through the whole host: `GET /api/admin/audit` for the administrator alone, repeated refusals, removal past the retention (spec 5.5).

using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Audit;
using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Host.Maintenance;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Api;

public sealed class AuditRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_administrator_reads_the_events_of_a_project_newest_first_and_anyone_else_is_refused(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var admin = project.Admin;
        using var facilitator = project.Facilitator;
        (await facilitator.DeleteAsync($"/api/projects/{project.Id}?confirmation={project.Code}", await project.ProjectVersionAsync())).Ok(204);
        var events = (await admin.GetAsync($"/api/admin/audit?project_id={project.Id}")).Ok();
        Assert.Equal([AuditKinds.ProjectTrashed, AuditKinds.ProjectCreated], events.EnumerateArray().Select(audit => audit.GetProperty("kind").GetString()));
        var trashed = events[0];
        Assert.Equal(["id", "occurred_at", "user_id", "kind", "project_id", "details"], trashed.EnumerateObject().Select(property => property.Name));
        var facilitatorId = (await facilitator.GetAsync("/api/me")).Ok().GetProperty("id").GetGuid();
        Assert.Equal((facilitatorId, project.Id), (trashed.GetProperty("user_id").GetGuid(), trashed.GetProperty("project_id").GetGuid()));
        Assert.Single((await admin.GetAsync($"/api/admin/audit?project_id={project.Id}&kind={AuditKinds.ProjectCreated}&limit=5")).Ok().EnumerateArray());
        (await facilitator.GetAsync("/api/admin/audit")).Refused(403, "ACCES_REFUSE");
        Assert.Equal("limit", (await admin.GetAsync("/api/admin/audit?limit=0")).Refused(400, "VALEUR_INVALIDE").GetProperty("field").GetString());
        (await admin.GetAsync("/api/admin/audit?from=nope")).Refused(400, "REQUETE_INVALIDE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Ten_refusals_of_a_user_within_ten_minutes_make_one_event_and_the_count_starts_again(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var reader = project.Reader;
        using var admin = project.Admin;
        var readerId = (await reader.GetAsync("/api/me")).Ok().GetProperty("id").GetGuid();
        async Task<int> SeriesAsync() =>
            (await admin.GetAsync($"/api/admin/audit?user_id={readerId}&kind={AuditKinds.RepeatedRefusals}")).Ok().GetArrayLength();
        var before = await SeriesAsync();
        for (var refusal = 1; refusal <= AuditTrail.RefusalThreshold - 1; refusal++)
            (await reader.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Refused(403, "ACCES_REFUSE");
        Assert.Equal(before, await SeriesAsync());
        (await reader.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Refused(403, "ACCES_REFUSE");
        Assert.Equal(before + 1, await SeriesAsync());
        (await reader.PostAsync($"/api/maps/{project.RootMapId}/lanes", new { name = "Achats" })).Refused(403, "ACCES_REFUSE");
        Assert.Equal(before + 1, await SeriesAsync());
    }

    /// <summary>
    /// AR-103 (C1), ADR 046: the INTROUVABLE that a non-member meets on a project is the reply of an object that does not
    /// exist, which ordinary navigation meets, and is never counted; the ACCES_REFUSE of the same user still are.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_not_found_refusals_of_a_non_member_are_never_counted_as_repeated_refusals_and_his_access_refusals_are(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var stranger = project.Stranger;
        using var admin = project.Admin;
        var strangerId = (await stranger.GetAsync("/api/me")).Ok().GetProperty("id").GetGuid();
        async Task<int> SeriesAsync() =>
            (await admin.GetAsync($"/api/admin/audit?user_id={strangerId}&kind={AuditKinds.RepeatedRefusals}")).Ok().GetArrayLength();
        var before = await SeriesAsync();
        for (var refusal = 1; refusal <= AuditTrail.RefusalThreshold + 2; refusal++)
            (await stranger.GetAsync($"/api/maps/{project.RootMapId}")).Refused(404, "INTROUVABLE");
        Assert.Equal(before, await SeriesAsync());
        for (var refusal = 1; refusal <= AuditTrail.RefusalThreshold - 1; refusal++)
            (await stranger.GetAsync("/api/admin/audit")).Refused(403, "ACCES_REFUSE");
        Assert.Equal(before, await SeriesAsync());
        (await stranger.GetAsync("/api/admin/audit")).Refused(403, "ACCES_REFUSE");
        Assert.Equal(before + 1, await SeriesAsync());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_daily_job_removes_the_events_past_the_retention_and_keeps_the_others(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var kindOf = $"test.{Guid.CreateVersion7():N}";
        var now = DateTime.UtcNow;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IAuditStore>();
            await store.AppendAsync(new AuditEvent(Guid.CreateVersion7(), now.AddDays(-AuditTrail.DefaultRetentionDays - 1), null, kindOf, null, null), Token);
            await store.AppendAsync(new AuditEvent(Guid.CreateVersion7(), now.AddDays(-AuditTrail.DefaultRetentionDays + 1), null, kindOf, null, null), Token);
        }
        await host.Services.GetRequiredService<MaintenanceService>().RunOnceAsync(Token);
        await using var reading = host.Services.CreateAsyncScope();
        var left = await reading.ServiceProvider.GetRequiredService<IAuditStore>().ReadAsync(new AuditQuery(null, null, null, null, kindOf, 10), Token);
        Assert.Equal([now.AddDays(-AuditTrail.DefaultRetentionDays + 1).Date], left.Select(audit => audit.At.Date));
    }
}
