// StructureConcurrencyTests.cs
// Operations run at the same time on one project, each in its own unit of work, on both engines: no duplicate, one expected result.

using Topogenia.Application.Ports;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Loading;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Concurrency;

/// <summary>
/// Real concurrency (several tasks started at once, one unit of work each, spec 4.7, A-2.16-08): under the lock of the
/// project the operations run one after the other and all succeed, each seeing the commits before it; without the lock,
/// an operation computed on a state that a concurrent commit changed is refused by a guard of the database (unique index,
/// version, counter) and never leaves a duplicate.
/// </summary>
public sealed class StructureConcurrencyTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    /// <summary>How long a unit may wait for another under the load of other runs on the instance.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_creations_of_one_project_code_at_the_same_time_leave_one_project_and_refuse_the_other(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var code = ScenarioSteps.NewProjectCode();
        var creators = new[] { scenario.Actor, scenario.Other };
        var meeting = new Rendezvous(kind, 2);

        var attempts = await Concurrently.RunAsync(2, async index =>
        {
            await using var session = scenario.Database.Session();
            var unit = new UnitOfWork(session);
            var store = new WorkspaceStore(session);
            var attempt = 0;
            return await unit.RunAsync(async token =>
            {
                // The handler reads the codes in use, then creates: both read before either writes.
                var existing = await scenario.Database.ProjectCodesAsync(code);
                await meeting.ArriveAsync(++attempt);
                var created = ProjectCreation.Create(scenario.Context(creators[index]), new CreateProjectRequest(code, $"Race {index}"),
                    new ProjectCreationInputs(existing, StoreScenario.Languages, "fr", new PeopleLookup([creators[index]], new Dictionary<string, DirectoryEntry>(), [])));
                await store.SaveAsync(new ChangeSet(created.Records.Select(StoreScenario.Created).ToList()), token);
                return created.Project.Id;
            }, Token);
        });

        Assert.Single(attempts, attempt => attempt.Succeeded);
        var refusal = Assert.Single(attempts, attempt => !attempt.Succeeded).Refusal!;
        Assert.True(kind == EngineKind.Sqlite || meeting.AllMet, meeting.Missed);
        if (meeting.AllMet)
            Assert.Equal((ErrorCodes.UniqueConflict, (object?)"UQ_project_code"), (refusal.Code, refusal.Details["constraint"]));
        else
            Assert.Equal(ErrorCodes.ProjectCodeTaken, refusal.Code);
        Assert.Single(await scenario.Database.ProjectCodesAsync(code));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Steps_lanes_and_pain_points_created_at_the_same_time_get_distinct_codes_and_their_counters_move_once_each(EngineKind kind)
    {
        const int operations = 5;
        var scenario = await ScenarioAsync(kind);
        var producer = (await scenario.LinkedStepsAsync(1))[0];
        var document = await scenario.OutputOfAsync(producer);

        var attempts = await Concurrently.RunAsync(operations, index => scenario.RunAsync((workspace, context) =>
        {
            var map = workspace.RootMap.Id;
            var step = StepOperations.Create(workspace, context, new CreateStepRequest(map, workspace.FirstLane(), 10 + index, $"Concurrent {index}"));
            LaneOperations.Add(workspace, context, new AddLaneRequest(map, $"Lane {index}"));
            DocumentUses.Use(workspace, context, step.Id, document, new DocumentUseInput(1, PainPoint: $"Pain {index}"));
        }));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        var stored = await scenario.LoadAsync();
        Assert.Equal(operations + 1, stored.All<Step>().Select(step => step.Code).Distinct().Count());
        Assert.Equal(operations + 1, stored.All<Lane>().Select(lane => lane.Code).Distinct().Count());
        Assert.Equal(operations, stored.All<PainPoint>().Select(painPoint => painPoint.Code).Distinct().Count());
        var counters = stored.All<ProjectCounter>().ToDictionary(counter => counter.Counter, counter => counter.LastValue);
        Assert.Equal((operations + 1, operations + 1, operations), (counters[CounterKind.Step], counters[CounterKind.Lane], counters[CounterKind.PainPoint]));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_creations_computed_on_one_state_never_give_a_code_twice(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var prepared = new List<Outcome>();
        for (var i = 0; i < 2; i++)
        {
            var label = $"Stale {i}";
            prepared.Add(await scenario.PrepareAsync((workspace, context) =>
                StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.FirstLane(), i, label))));
        }

        // The counter has no version: its change sets are written under the lock (ADR 030), the second one after the
        // commit of the first, and the counter read back is no longer the one the second started from.
        var attempts = await Concurrently.RunAsync(2, async index =>
        {
            await scenario.WriteAsync(prepared[index].Changes, locked: true);
            return true;
        });

        Assert.Single(attempts, attempt => attempt.Succeeded);
        Assert.Contains(Assert.Single(attempts, attempt => !attempt.Succeeded).Refusal!.Code, new[] { ErrorCodes.VersionConflict, ErrorCodes.UniqueConflict });
        var stored = await scenario.LoadAsync();
        Assert.Equal(1, Assert.Single(stored.All<ProjectCounter>(), counter => counter.Counter == CounterKind.Step).LastValue);
        Assert.Single(stored.All<Step>());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_creations_of_one_flow_under_the_lock_leave_one_flow_and_the_second_sees_the_duplicate(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);

        var attempts = await Concurrently.RunAsync(2, index => scenario.RunAsync((workspace, context) =>
            FlowRules.Create(workspace, context, new CreateFlowRequest(workspace.RootMap.Id, steps[1], steps[0], $"back {index}"))));

        Assert.Single(attempts, attempt => attempt.Succeeded);
        Assert.Equal(ErrorCodes.DuplicateFlow, Assert.Single(attempts, attempt => !attempt.Succeeded).Refusal!.Code);
        var stored = await scenario.LoadAsync();
        Assert.Equal(2, stored.FlowsOf(stored.RootMap.Id).Count);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Two_creations_of_one_flow_computed_on_one_state_are_refused_by_the_partial_unique_index(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var steps = await scenario.LinkedStepsAsync(2);
        var prepared = new List<Outcome>();
        for (var i = 0; i < 2; i++)
            prepared.Add(await scenario.PrepareAsync((workspace, context) =>
                FlowRules.Create(workspace, context, new CreateFlowRequest(workspace.RootMap.Id, steps[1], steps[0], null))));

        var attempts = await Concurrently.RunAsync(2, async index =>
        {
            await scenario.WriteAsync(prepared[index].Changes);
            return true;
        });

        Assert.Single(attempts, attempt => attempt.Succeeded);
        var refusal = Assert.Single(attempts, attempt => !attempt.Succeeded).Refusal!;
        Assert.Equal((ErrorCodes.UniqueConflict, (object?)IndexNames.LiveFlowOfPair), (refusal.Code, refusal.Details["constraint"]));
        var stored = await scenario.LoadAsync();
        Assert.Single(stored.FlowsOf(stored.RootMap.Id), flow => flow.SourceStepId == steps[1]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Validations_of_one_sheet_at_the_same_time_under_the_lock_give_versions_without_duplicate(EngineKind kind)
    {
        const int operations = 3;
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];

        var attempts = await Concurrently.RunAsync(operations, index => scenario.RunAsync((workspace, context) =>
        {
            SheetWorkflow.Save(workspace, context, step, ScenarioSteps.Sheet($"Purpose {index}"));
            SheetWorkflow.Validate(workspace, context, step);
        }));

        Assert.All(attempts, attempt => Assert.True(attempt.Succeeded));
        var versions = (await scenario.LoadAsync()).All<StepSheetVersion>().Select(version => version.VersionNumber).Order();
        Assert.Equal(Enumerable.Range(1, operations), versions);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Validations_of_one_sheet_computed_on_one_state_never_give_a_version_number_twice(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(1))[0];
        await scenario.RunAsync((workspace, context) => SheetWorkflow.Save(workspace, context, step, ScenarioSteps.Sheet("First")));
        var prepared = new List<Outcome>();
        for (var i = 0; i < 2; i++)
        {
            var purpose = $"Stale {i}";
            prepared.Add(await scenario.PrepareAsync((workspace, context) =>
            {
                SheetWorkflow.Save(workspace, context, step, ScenarioSteps.Sheet(purpose));
                SheetWorkflow.Validate(workspace, context, step);
            }));
        }

        var attempts = await Concurrently.RunAsync(2, async index =>
        {
            await scenario.WriteAsync(prepared[index].Changes);
            return true;
        });

        Assert.Single(attempts, attempt => attempt.Succeeded);
        Assert.Contains(Assert.Single(attempts, attempt => !attempt.Succeeded).Refusal!.Code, new[] { ErrorCodes.VersionConflict, ErrorCodes.UniqueConflict });
        Assert.Equal(1, Assert.Single((await scenario.LoadAsync()).All<StepSheetVersion>()).VersionNumber);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_placement_that_waits_for_the_lock_while_its_lane_is_deleted_is_refused_as_lane_deleted(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var lane = Guid.Empty;
        await scenario.RunAsync((workspace, context) => lane = LaneOperations.Add(workspace, context, new AddLaneRequest(workspace.RootMap.Id, "Doomed")).Id);
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var watch = new LockWatch(kind);
        var heldDuringTheDeletion = true;

        await using var session = scenario.Database.Session();
        var unit = new UnitOfWork(session);
        var store = new WorkspaceStore(session);
        var deletion = unit.RunAsync(async token =>
        {
            await unit.LockProjectAsync(scenario.ProjectId, token);
            locked.TrySetResult();
            // The placement asks for the lock while this unit holds it, then waits.
            await watch.Asked.Task.WaitAsync(Patience, token);
            var workspace = (await store.LoadAsync(scenario.ProjectId, WorkspaceContent.None, token))!;
            await Task.Delay(200, token);
            var context = scenario.Context();
            LaneOperations.Delete(workspace, context, new DeleteLaneRequest(lane));
            await store.SaveAsync(workspace.Commit(context), token);
            heldDuringTheDeletion = watch.Held.Task.IsCompleted;
            return true;
        }, Token);
        await locked.Task;
        await using var placementSession = scenario.Database.Session(null, watch);
        var placement = scenario.RunAsync((workspace, context) =>
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, lane, 0, "Too late")),
            new RunOptions { Session = placementSession });

        await deletion.WaitAsync(Patience, Token);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => placement.WaitAsync(Patience, Token));
        // The placement asked for the lock before the deletion committed and got it only after.
        Assert.False(heldDuringTheDeletion, "The placement held the lock while the deletion held it.");
        Assert.True(watch.Held.Task.IsCompleted, "The placement never held the lock.");
        Assert.Equal((ErrorCodes.LaneDeleted, RefusalKind.Conflict), (refusal.Code, refusal.Kind));
        Assert.Empty((await scenario.LoadAsync()).All<Step>());
    }
}
