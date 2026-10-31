// CommandBarRoutesTests.cs
// The command bar of a map through the whole host on both databases: all or nothing, the errors detailed by line (annex A 1.3, A 2.6).

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Steps;

public sealed class CommandBarRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_command_bar_draws_its_flows_in_one_operation_and_answers_them_in_the_order_of_the_lines(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (first, second, third) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Valider"), await project.AddStepAsync(2, "Payer"));
        var reply = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/command", new { text = $"{first.Code} > {second.Code} : oui\n{second.Code} > {third.Code}" })).Ok();
        var ids = reply.Ids();
        Assert.Equal(2, ids.Count);
        var map = await project.MapAsync();
        var flows = ids.Select(id => StructureCalls.FlowOf(map, id)).ToList();
        Assert.Equal((first.Id, second.Id, "oui"), (flows[0].GetProperty("source_step_id").GetGuid(), flows[0].GetProperty("target_step_id").GetGuid(), flows[0].GetProperty("label").GetString()));
        Assert.Equal((second.Id, third.Id), (flows[1].GetProperty("source_step_id").GetGuid(), flows[1].GetProperty("target_step_id").GetGuid()));
        Assert.Equal("commande", reply.GetProperty("history").GetProperty("undo")[0].GetProperty("kind").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_command_with_a_wrong_line_draws_nothing_and_details_the_errors_by_line(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var (first, second) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Valider"));
        var unknown = $"{project.Code}-E999";
        var details = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/command", new { text = $"{first.Code} > {second.Code}\n{second.Code} > {unknown}" }))
            .Refused(400, "COMMANDE_ERREURS");
        var error = Assert.Single(details.GetProperty("errors").EnumerateArray());
        Assert.Equal((2, "ETAPE_INCONNUE", unknown), (error.GetProperty("line").GetInt32(), error.GetProperty("code").GetString(), error.GetProperty("detail").GetString()));
        Assert.Equal($"L2 : Étape inconnue : {unknown}.", error.GetProperty("message").GetString());
        Assert.Empty(StructureCalls.FlowPairs(await project.MapAsync()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_contributor_runs_no_command_and_a_stranger_is_told_not_found(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var stranger = project.Stranger;
        var (first, second) = (await project.AddStepAsync(0, "Commander"), await project.AddStepAsync(1, "Valider"));
        var body = new { text = $"{first.Code} > {second.Code}" };
        (await contributor.PostAsync($"/api/maps/{project.RootMapId}/command", body)).Refused(403, "ACCES_REFUSE");
        (await stranger.PostAsync($"/api/maps/{project.RootMapId}/command", body)).Refused(404, "INTROUVABLE");
        Assert.Empty(StructureCalls.FlowPairs(await project.MapAsync()));
    }
}
