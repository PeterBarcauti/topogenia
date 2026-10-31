// Counted.cs
// Runs an operation, a loading or a writing of a scenario with its commands counted, and gives the bound of the writes of a change set.

using Topogenia.Application.Ports;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Queries;

/// <summary>What an operation sent to the engine and what it left.</summary>
public sealed record CountedRun(CommandLog Log, Outcome Outcome)
{
    /// <summary>
    /// The writes that the operation may send at most: the lock of the project, then one statement per row of its write
    /// plan (a step parked then placed counts twice). SQLite sends each statement alone; SQL Server groups them in batches.
    /// </summary>
    public int WriteBound => 1 + Statements(Outcome.Changes);

    public static int Statements(ChangeSet changes)
    {
        var plan = WritePlan.Of(changes);
        return plan.First.Count + plan.Then.Count;
    }
}

/// <summary>The commands of a gabarit of structure command (lock, loading, domain, writing) or of a loading alone.</summary>
public static class Counted
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task<CountedRun> RunAsync(StoreScenario scenario, Action<ProjectWorkspace, OperationContext> operation)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var log = new CommandLog();
        await using var session = scenario.Database.Session(null, log);
        var outcome = await scenario.RunAsync(operation, session: session);
        return new CountedRun(log, outcome);
    }

    public static async Task<(CommandLog Log, ProjectWorkspace Workspace)> LoadAsync(StoreScenario scenario, WorkspaceContent content)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var log = new CommandLog();
        await using var session = scenario.Database.Session(null, log);
        var workspace = await new WorkspaceStore(session).LoadAsync(scenario.ProjectId, content, Token);
        return (log, workspace!);
    }

    /// <summary>The reads and the writes of a run are those of a run of another size: the same reads, writes bounded by the rows.</summary>
    public static void AssertIndependentReads(CountedRun small, CountedRun large)
    {
        ArgumentNullException.ThrowIfNull(small);
        ArgumentNullException.ThrowIfNull(large);
        Assert.Equal(small.Log.Reads, large.Log.Reads);
        Assert.InRange(small.Log.Writes, 1, small.WriteBound);
        Assert.InRange(large.Log.Writes, 1, large.WriteBound);
        Assert.Equal(0, small.Log.Migrations + large.Log.Migrations);
    }
}
