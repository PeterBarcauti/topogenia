// SubMapsAndUndoCommandPortedTests.cs
// The 1.x command bar tests (SousCartesEtAnnulationTests.CommandeApiTests, T-SCAN-01 to 05) ported to the whole host on both databases.

using System.Text.Json;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Steps;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Ported;

public sealed class SubMapsAndUndoCommandPortedTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    /// <summary>A project whose root map holds three steps in a row (columns 0 to 2), not linked; their codes.</summary>
    private async Task<(ApiProject Project, IReadOnlyList<CreatedStep> Steps)> ThreeStepsAsync(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        var steps = new List<CreatedStep>();
        for (var column = 0; column < 3; column++)
            steps.Add(await project.AddStepAsync(column, $"Etape {column}"));
        return (project, steps);
    }

    private static Task<ApiResponse> CommandAsync(ApiClient client, ApiProject project, string text) =>
        client.PostAsync($"/api/maps/{project.RootMapId}/command", new { text });

    private static IReadOnlyList<(int Line, string? Code)> Errors(JsonElement details) =>
        [.. details.GetProperty("errors").EnumerateArray().Select(error => (error.GetProperty("line").GetInt32(), error.GetProperty("code").GetString()))];

    /// <summary>T-SCAN-01; the command is undone by the server (spec 4.6).</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_command_takes_full_and_short_codes_and_a_label_after_the_colon_and_is_undone_at_once(EngineKind kind)
    {
        var (project, steps) = await ThreeStepsAsync(kind);
        using var facilitator = project.Facilitator;
        var reply = (await CommandAsync(facilitator, project, $"{steps[0].Code} > e2 : oui\ne2>E3")).Ok();
        Assert.Equal(2, reply.Ids().Count);
        var map = await project.MapAsync();
        Assert.Equal([(steps[0].Id, steps[1].Id), (steps[1].Id, steps[2].Id)], StructureCalls.FlowPairs(map).OrderBy(pair => pair.Source == steps[0].Id ? 0 : 1));
        Assert.Equal("oui", StructureCalls.FlowOf(map, reply.Ids()[0]).GetProperty("label").GetString());

        var undone = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/undo")).Ok();
        Assert.Equal((reply.GetProperty("operation_id").GetGuid(), "commande"), (undone.GetProperty("id").GetGuid(), undone.GetProperty("code").GetString()));
        Assert.Empty(StructureCalls.FlowPairs(undone.GetProperty("map")));
    }

    /// <summary>T-SCAN-02.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_command_with_errors_creates_nothing_and_answers_every_error_by_line_number(EngineKind kind)
    {
        var (project, steps) = await ThreeStepsAsync(kind);
        using var facilitator = project.Facilitator;
        var details = (await CommandAsync(facilitator, project, $"{steps[0].Code} > {steps[1].Code}\n{steps[1].Code} > E99\nn'importe quoi"))
            .Refused(400, "COMMANDE_ERREURS");
        Assert.Equal([(2, "ETAPE_INCONNUE"), (3, "COMMANDE_SYNTAXE")], Errors(details));
        Assert.Contains($"{project.Code}-E099", details.GetProperty("errors")[0].GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Empty(StructureCalls.FlowPairs(await project.MapAsync()));
    }

    /// <summary>T-SCAN-03.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Duplicates_of_an_existing_flow_and_inside_the_command_are_reported_on_their_lines(EngineKind kind)
    {
        var (project, steps) = await ThreeStepsAsync(kind);
        using var facilitator = project.Facilitator;
        (await CommandAsync(facilitator, project, $"{steps[0].Code} > {steps[1].Code}")).Ok();
        var details = (await CommandAsync(facilitator, project, $"{steps[0].Code} > {steps[1].Code}\n{steps[1].Code} > {steps[2].Code}\n{steps[1].Code} > {steps[2].Code}"))
            .Refused(400, "COMMANDE_ERREURS");
        Assert.Equal([(1, "LIEN_DOUBLON"), (3, "LIEN_DOUBLON")], Errors(details));
        Assert.Single(StructureCalls.FlowPairs(await project.MapAsync()));
    }

    /// <summary>T-SCAN-04, with spec 5.6 point 2: the code of a step of another project is unknown, the search stays in the project.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_of_another_map_is_outside_the_map_and_a_step_of_another_project_is_unknown(EngineKind kind)
    {
        var (project, steps) = await ThreeStepsAsync(kind);
        using var facilitator = project.Facilitator;
        var (subMap, subLane) = await project.DetailAsync(steps[1].Id);
        var inside = await project.AddStepAsync(0, "Dedans", subLane, subMap);
        var details = (await CommandAsync(facilitator, project, $"{steps[0].Code} > {inside.Code}")).Refused(400, "COMMANDE_ERREURS");
        Assert.Equal([(1, "ETAPE_HORS_CARTE")], Errors(details));

        var other = await ApiProject.CreateAsync(project.Host);
        var elsewhere = await other.AddStepAsync(0, "Ailleurs");
        details = (await CommandAsync(facilitator, project, $"{steps[0].Code} > {elsewhere.Code}")).Refused(400, "COMMANDE_ERREURS");
        Assert.Equal([(1, "ETAPE_INCONNUE")], Errors(details));
        Assert.Empty(StructureCalls.FlowPairs(await project.MapAsync()));
    }

    /// <summary>T-SCAN-05.</summary>
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task An_empty_command_and_a_command_of_a_reader_are_refused(EngineKind kind)
    {
        var (project, steps) = await ThreeStepsAsync(kind);
        using var facilitator = project.Facilitator;
        using var reader = project.Reader;
        var details = (await CommandAsync(facilitator, project, "  ")).Refused(400, "COMMANDE_ERREURS");
        Assert.Equal("COMMANDE_VIDE", Assert.Single(details.GetProperty("errors").EnumerateArray()).GetProperty("code").GetString());
        (await CommandAsync(reader, project, $"{steps[0].Code} > {steps[1].Code}")).Refused(403, "ACCES_REFUSE");
        Assert.Empty(StructureCalls.FlowPairs(await project.MapAsync()));
    }
}
