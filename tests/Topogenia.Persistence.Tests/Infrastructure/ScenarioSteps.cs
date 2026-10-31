// ScenarioSteps.cs
// Common starting points of the scenarios: steps linked in a lane, a document and its use, a sheet; codes of new projects.

using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Workspace;

namespace Topogenia.Persistence.Tests.Infrastructure;

/// <summary>The starting points that the scenarios of the persistence share, written by operations of the domain.</summary>
public static class ScenarioSteps
{
    private static int codes;

    /// <summary>The first live lane of the root map.</summary>
    public static Guid FirstLane(this ProjectWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return workspace.LanesOf(workspace.RootMap.Id)[0].Id;
    }

    /// <summary>Steps in the first lane of the root map, at the columns 0 to n - 1, linked in this order.</summary>
    public static async Task<Guid[]> LinkedStepsAsync(this StoreScenario scenario, int count = 3)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var steps = new Guid[count];
        await scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            for (var i = 0; i < count; i++)
                steps[i] = StepOperations.Create(workspace, context, new CreateStepRequest(map, workspace.FirstLane(), i, $"Step {i}")).Id;
            for (var i = 1; i < count; i++)
                FlowRules.Create(workspace, context, new CreateFlowRequest(map, steps[i - 1], steps[i], null));
        });
        return steps;
    }

    /// <summary>An output document of <paramref name="producer"/>, which the other steps of the map can use.</summary>
    public static async Task<Guid> OutputOfAsync(this StoreScenario scenario, Guid producer, string name = "Order form")
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var document = Guid.Empty;
        await scenario.RunAsync((workspace, context) =>
            document = DocumentEdits.Add(workspace, context, producer, new DocumentInput("output", name, null, null, null)));
        return document;
    }

    /// <summary>A sheet input with a purpose and the given procedure rows.</summary>
    public static SheetInput Sheet(string purpose, params string[] rows) => new(
        new Dictionary<string, SheetFieldValue?>
        {
            [SheetContentReader.PurposeKey] = new(purpose, null),
            [SheetContentReader.FrequencyKey] = new("daily", null),
            [SheetContentReader.DurationKey] = new(null, 12.5m),
        },
        rows.Select(row => (ProcedureLine?)new ProcedureLine(row, "ERP", "check")).ToList(),
        null);

    /// <summary>A project code (six capital letters) unique per call and per process, outside the ranges of the other helpers.</summary>
    public static string NewProjectCode()
    {
        var letters = new char[6];
        var value = 250_000_000 + Interlocked.Increment(ref codes) + (Environment.ProcessId % 1000 * 100_000);
        for (var i = 5; i >= 0; i--)
        {
            letters[i] = (char)('A' + (value % 26));
            value /= 26;
        }
        return new string(letters);
    }

    /// <summary>The codes of the projects of the database, read outside any unit, as a handler reads them before a creation.</summary>
    public static async Task<IReadOnlyList<ExistingProjectCode>> ProjectCodesAsync(this TestDatabase database, string code)
    {
        ArgumentNullException.ThrowIfNull(database);
        await using var context = database.Context();
        return await context.Set<Project>().AsNoTracking().Where(project => project.Code == code)
            .Select(project => new ExistingProjectCode(project.Code, project.DeletedAt != null)).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The users of the database with this normalised login, read outside any unit, as a handler resolves the members.</summary>
    public static async Task<IReadOnlyList<AppUser>> UsersAsync(this TestDatabase database, string normalizedLogin)
    {
        ArgumentNullException.ThrowIfNull(database);
        await using var context = database.Context();
        return await context.Set<AppUser>().AsNoTracking().Where(user => user.LoginNormalized == normalizedLogin)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
