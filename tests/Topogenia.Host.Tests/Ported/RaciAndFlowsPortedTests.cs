// RaciAndFlowsPortedTests.cs
// The 1.x integration tests of the RACI (T-RACI) that the RACI routes tests did not prove, through the whole host on both databases; the flow scenarios are in FlowScenariosPortedTests.

using Microsoft.Extensions.DependencyInjection;
using Topogenia.Host.Composition;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

/// <summary>
/// Each test carries the intention of one 1.x test of `RaciTests` (T-RACI-nn of `docs/portage-tests.md`) on the map of the
/// 1.x: the step « Cloture » on the first lane (its R), and the lanes « Direction » and « Logistique ».
/// </summary>
public sealed class RaciAndFlowsPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private sealed record RaciPlan(ApiProject Project, Guid Performs, Guid Direction, Guid Logistics, Guid Step);

    private async Task<RaciPlan> PlanAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var direction = await project.AddLaneAsync("Direction");
        var logistics = await project.AddLaneAsync("Logistique");
        var step = (await project.AddStepAsync(0, "Clôture")).Id;
        return new RaciPlan(project, project.FirstLaneId, direction, logistics, step);
    }

    /// <summary>Sets (or removes, with a null role) the role of a lane with the version of the step the client reads on the map.</summary>
    private static async Task<ApiResponse> SetAsync(ApiClient client, RaciPlan plan, Guid laneId, string? role)
    {
        var map = (await client.GetAsync($"/api/maps/{plan.Project.RootMapId}")).Ok().GetProperty("map");
        var version = StructureCalls.StepOf(map, plan.Step).GetProperty("version").GetInt32();
        return await client.PutAsync($"/api/steps/{plan.Step}/raci", new { lane_id = laneId, role }, version);
    }

    /// <summary>The visible roles of the map read by the client, as "role:lane name", in order.</summary>
    private static async Task<IReadOnlyList<string>> RolesAsync(ApiClient client, ApiProject project)
    {
        var map = (await client.GetAsync($"/api/maps/{project.RootMapId}")).Ok().GetProperty("map");
        var names = map.GetProperty("lanes").EnumerateArray().ToDictionary(lane => lane.GetProperty("id").GetGuid(), lane => lane.GetProperty("name").GetString());
        return [.. map.GetProperty("raci").EnumerateArray()
            .Select(role => $"{role.GetProperty("role").GetString()}:{names[role.GetProperty("lane_id").GetGuid()]}").Order(StringComparer.Ordinal)];
    }

    /// <summary>T-RACI-01: the default letters are ACI; a role is set, replaced by another letter and removed by null.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_default_letters_are_aci_and_a_role_is_set_replaced_and_removed(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        Assert.Equal("ACI", (await plan.Project.MapAsync()).GetProperty("raci_letters").GetString());
        (await SetAsync(facilitator, plan, plan.Direction, "A")).Ok();
        (await SetAsync(facilitator, plan, plan.Logistics, "I")).Ok();
        Assert.Equal(["A:Direction", "I:Logistique"], await RolesAsync(facilitator, plan.Project));
        (await SetAsync(facilitator, plan, plan.Logistics, "C")).Ok();
        Assert.Equal(["A:Direction", "C:Logistique"], await RolesAsync(facilitator, plan.Project));
        (await SetAsync(facilitator, plan, plan.Logistics, null)).Ok();
        Assert.Equal(["A:Direction"], await RolesAsync(facilitator, plan.Project));
    }

    /// <summary>T-RACI-02: there is one approver; setting A on another lane moves it.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Setting_a_on_another_lane_moves_the_single_approver(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        (await SetAsync(facilitator, plan, plan.Direction, "A")).Ok();
        (await SetAsync(facilitator, plan, plan.Logistics, "A")).Ok();
        Assert.Equal(["A:Logistique"], await RolesAsync(facilitator, plan.Project));
    }

    /// <summary>T-RACI-04: a step moved into a lane that holds a role hides the role behind its R; the role comes back when the step leaves.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_moved_into_a_lane_with_a_role_hides_it_behind_its_r_and_the_role_comes_back_when_it_leaves(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        (await SetAsync(facilitator, plan, plan.Logistics, "C")).Ok();
        (await facilitator.PatchAsync($"/api/steps/{plan.Step}", new { lane_id = plan.Logistics, column = 0 }, await plan.Project.StepVersionAsync(plan.Step))).Ok();
        Assert.Empty(await RolesAsync(facilitator, plan.Project));
        (await facilitator.PatchAsync($"/api/steps/{plan.Step}", new { lane_id = plan.Performs, column = 0 }, await plan.Project.StepVersionAsync(plan.Step))).Ok();
        Assert.Equal(["C:Logistique"], await RolesAsync(facilitator, plan.Project));
    }

    /// <summary>
    /// T-RACI-05: S is refused while the setting offers ACI, and so are R and several letters; with the letters ASCI of the
    /// configuration (`RaciLetters`, in place of the SQL of the 1.x), S is offered and set.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_letter_s_is_set_only_when_the_configuration_offers_it_and_r_or_several_letters_never(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        foreach (var role in new[] { "S", "R", "AC" })
            Assert.Equal("role", (await SetAsync(facilitator, plan, plan.Logistics, role)).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Empty(await RolesAsync(facilitator, plan.Project));

        await plan.Project.Host.WithServicesAsync(services => services.PostConfigure<ProductOptions>(options => options.RaciLetters = "ASCI"), Logins.Facilitator,
            async configured =>
            {
                Assert.Equal("ASCI", (await configured.GetAsync($"/api/maps/{plan.Project.RootMapId}")).Ok().GetProperty("map").GetProperty("raci_letters").GetString());
                (await SetAsync(configured, plan, plan.Logistics, "S")).Ok();
                Assert.Equal(["S:Logistique"], await RolesAsync(configured, plan.Project));
                Assert.Equal("role", (await SetAsync(configured, plan, plan.Logistics, "R")).Refused(400, "VALEUR_INVALIDE").Field());
            });
    }

    /// <summary>T-RACI-06: the roles of a deleted lane disappear and come back with the lane when the deletion is undone.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_roles_of_a_deleted_lane_disappear_and_come_back_when_the_deletion_is_undone(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        using var facilitator = plan.Project.Facilitator;
        (await SetAsync(facilitator, plan, plan.Logistics, "I")).Ok();
        (await facilitator.DeleteAsync($"/api/lanes/{plan.Logistics}", await plan.Project.LaneVersionAsync(plan.Logistics))).Ok();
        Assert.Empty(await RolesAsync(facilitator, plan.Project));
        (await facilitator.PostAsync($"/api/maps/{plan.Project.RootMapId}/undo")).Ok();
        Assert.Equal(["I:Logistique"], await RolesAsync(facilitator, plan.Project));
    }

    /// <summary>T-RACI-07: a lane of another map is refused, a contributor and a reader set no role, and the reader sees the roles.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_lane_of_another_map_is_refused_contributor_and_reader_set_no_role_and_the_reader_sees_the_roles(EngineKind kind)
    {
        var plan = await PlanAsync(kind);
        var other = await ApiProject.CreateAsync(plan.Project.Host);
        var macro = (await plan.Project.AddStepAsync(1, "Achats")).Id;
        var (_, subLane) = await plan.Project.DetailAsync(macro);
        using var facilitator = plan.Project.Facilitator;
        using var contributor = plan.Project.Contributor;
        using var reader = plan.Project.Reader;
        Assert.Equal("lane_id", (await SetAsync(facilitator, plan, other.FirstLaneId, "C")).Refused(400, "VALEUR_INVALIDE").Field());
        Assert.Equal("lane_id", (await SetAsync(facilitator, plan, subLane, "C")).Refused(400, "VALEUR_INVALIDE").Field());
        (await SetAsync(contributor, plan, plan.Logistics, "C")).Refused(403, "ACCES_REFUSE");
        (await SetAsync(reader, plan, plan.Logistics, "C")).Refused(403, "ACCES_REFUSE");
        Assert.Empty(await RolesAsync(reader, plan.Project));

        (await SetAsync(facilitator, plan, plan.Logistics, "C")).Ok();
        Assert.Equal(["C:Logistique"], await RolesAsync(reader, plan.Project));
    }
}
