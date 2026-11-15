// WritePathGuardTests.cs
// A change set that a rule of the domain would never produce is refused by the guard of the database, through the unit of work.

using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Lanes;
using Topogenia.Domain.Model;
using Topogenia.Domain.Raci;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Guards;

/// <summary>
/// Spec 4.2 and D04: the database keeps the structural guards (partial unique indexes, foreign keys, simple CHECK) as the
/// last line behind the rules of the domain. Each case writes, through <c>IUnitOfWork.RunAsync</c> and the workspace
/// store, a change set that breaks one guard, and expects its code (`CONFLIT_UNICITE` or `CONFLIT_INTEGRITE`, spec
/// 4.3-5) with the name of the guard (SQLite names no foreign key) and nothing written. The raw guards, row by row, are
/// proven by `Model.UniqueIndexTests`, `Model.CheckConstraintTests` and `Model.ForeignKeyTests`.
/// </summary>
public sealed class WritePathGuardTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    public static TheoryData<EngineKind, string> PartialIndexes() => Matrix(
        IndexNames.RootMapOfProject, IndexNames.LiveSubMapOfStep, IndexNames.LiveStepOfCell, IndexNames.SingleApproverOfStep,
        IndexNames.OpenDissolutionOfMap, IndexNames.PainPointOfUse);

    public static TheoryData<EngineKind, string> Checks() => Matrix(
        "CK_process_map_deletion", "CK_lane_deletion", "CK_step_deletion", "CK_flow_deletion", "CK_step_column_index",
        "CK_document_wait_hours", "CK_translation_object_field");

    private static TheoryData<EngineKind, string> Matrix(params string[] names)
    {
        var data = new TheoryData<EngineKind, string>();
        foreach (var kind in Enum.GetValues<EngineKind>())
        {
            foreach (var name in names)
                data.Add(kind, name);
        }
        return data;
    }

    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    private static RecordChange Modified<T>(T before, T after)
        where T : IVersionedRecord => new(ChangeKind.Modified, before, after.Stamp(before.Version + 1, FixedClock.Time, null));

    [Theory]
    [MemberData(nameof(PartialIndexes))]
    public async Task A_change_set_that_takes_a_live_slot_twice_is_refused_by_the_partial_unique_index_naming_it(EngineKind kind, string index)
    {
        var scenario = await ScenarioAsync(kind);
        var changes = await BreakingAsync(scenario, index);
        var before = WorkspaceRecords.Of(await scenario.LoadAsync());

        var refusal = await Assert.ThrowsAsync<DomainException>(() => scenario.WriteAsync(changes));

        Assert.Equal((ErrorCodes.UniqueConflict, RefusalKind.Conflict, (object?)index), (refusal.Code, refusal.Kind, refusal.Details[StoreRefusals.ConstraintDetail]));
        Assert.Equal(before, WorkspaceRecords.Of(await scenario.LoadAsync()));
    }

    [Theory]
    [MemberData(nameof(Checks))]
    public async Task A_change_set_out_of_a_check_constraint_is_refused_as_an_integrity_conflict_naming_it(EngineKind kind, string check)
    {
        var scenario = await ScenarioAsync(kind);
        var changes = await OutOfCheckAsync(scenario, check);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => scenario.WriteAsync(changes));

        Assert.Equal((ErrorCodes.IntegrityConflict, (object?)check), (refusal.Code, refusal.Details[StoreRefusals.ConstraintDetail]));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_put_on_a_lane_of_another_map_is_refused_by_the_composite_foreign_key(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var step = (await scenario.LinkedStepsAsync(2))[1];
        await scenario.RunAsync((workspace, context) => SubMapRules.Detail(workspace, context, step));
        var workspace = await scenario.LoadAsync();
        var foreign = Assert.Single(workspace.LanesOf(workspace.SubMapOf(step)!.Id));
        var moved = workspace.LiveStep(step);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => scenario.WriteAsync(new ChangeSet([Modified(moved, moved with { LaneId = foreign.Id })])));

        Assert.Equal(ErrorCodes.IntegrityConflict, refusal.Code);
        Assert.Equal(kind == EngineKind.SqlServer ? "FK_step_lane" : null, refusal.Details[StoreRefusals.ConstraintDetail]);
        Assert.Equal(moved.LaneId, (await scenario.LoadAsync()).LiveStep(step).LaneId);
    }

    /// <summary>A change set that breaks the partial unique index <paramref name="index"/> on the current state of the scenario.</summary>
    private static async Task<ChangeSet> BreakingAsync(StoreScenario scenario, string index)
    {
        var steps = await scenario.LinkedStepsAsync(3);
        var workspace = await scenario.LoadAsync();
        var now = FixedClock.Time;
        var actor = scenario.Actor.Id;
        switch (index)
        {
            case IndexNames.RootMapOfProject:
                return new ChangeSet([StoreScenario.Created(new ProcessMap(Guid.CreateVersion7(), scenario.ProjectId, null, "Second root", "fr", now, actor, null, null, null, null, 1))]);
            case IndexNames.LiveSubMapOfStep:
                await scenario.RunAsync((current, context) => SubMapRules.Detail(current, context, steps[0]));
                return new ChangeSet([StoreScenario.Created(new ProcessMap(Guid.CreateVersion7(), scenario.ProjectId, steps[0], "Second detail", "fr", now, actor, null, null, null, null, 1))]);
            case IndexNames.LiveStepOfCell:
                var moved = workspace.LiveStep(steps[1]);
                return new ChangeSet([Modified(moved, moved with { Column = workspace.LiveStep(steps[0]).Column })]);
            case IndexNames.SingleApproverOfStep:
                return await SecondApproverAsync(scenario, steps[0]);
            case IndexNames.OpenDissolutionOfMap:
                var batch = new DeletionBatch(Guid.CreateVersion7(), scenario.ProjectId, null, now, actor, null, null);
                return new ChangeSet([StoreScenario.Created(batch),
                    StoreScenario.Created(new Dissolution(Guid.CreateVersion7(), workspace.RootMap.Id, steps[0], batch.Id, now, actor, null, null)),
                    StoreScenario.Created(new Dissolution(Guid.CreateVersion7(), workspace.RootMap.Id, steps[1], batch.Id, now, actor, null, null))]);
            default:
                return await PainPointOfTwoUsesAsync(scenario, steps);
        }
    }

    private static async Task<ChangeSet> SecondApproverAsync(StoreScenario scenario, Guid step)
    {
        var second = Guid.Empty;
        await scenario.RunAsync((workspace, context) =>
        {
            var approver = LaneOperations.Add(workspace, context, new AddLaneRequest(workspace.RootMap.Id, "Approver")).Id;
            second = LaneOperations.Add(workspace, context, new AddLaneRequest(workspace.RootMap.Id, "Second approver")).Id;
            RaciRules.SetRole(workspace, context, new SetRaciRequest(step, approver, "A"));
        });
        return new ChangeSet([StoreScenario.Created(new StepRaci(step, second, RaciRole.A, FixedClock.Time, scenario.Actor.Id))]);
    }

    private static async Task<ChangeSet> PainPointOfTwoUsesAsync(StoreScenario scenario, Guid[] steps)
    {
        var document = await scenario.OutputOfAsync(steps[0]);
        await scenario.RunAsync((workspace, context) =>
        {
            DocumentUses.Use(workspace, context, steps[1], document, new DocumentUseInput(1, PainPoint: "Typed twice"));
            DocumentUses.Use(workspace, context, steps[2], document, new DocumentUseInput(1));
        });
        var uses = (await scenario.LoadAsync()).All<DocumentUse>().ToDictionary(use => use.StepId);
        return new ChangeSet([Modified(uses[steps[2]], uses[steps[2]] with { PainPointId = uses[steps[1]].PainPointId })]);
    }

    /// <summary>A change set that breaks the CHECK <paramref name="check"/>.</summary>
    private static async Task<ChangeSet> OutOfCheckAsync(StoreScenario scenario, string check)
    {
        var steps = await scenario.LinkedStepsAsync(2);
        var document = await scenario.OutputOfAsync(steps[0]);
        var batch = new DeletionBatch(Guid.CreateVersion7(), scenario.ProjectId, null, FixedClock.Time, scenario.Actor.Id, null, null);
        await scenario.WriteAsync(new ChangeSet([StoreScenario.Created(batch)]));
        var workspace = await scenario.LoadAsync();
        var (now, step, flow, lane) = (FixedClock.Time, workspace.LiveStep(steps[1]), workspace.FlowsOf(workspace.RootMap.Id)[0], workspace.Lane(workspace.FirstLane())!);
        return new ChangeSet([check switch
        {
            "CK_process_map_deletion" => Modified(workspace.RootMap, workspace.RootMap with { DeletionBatchId = batch.Id }),
            "CK_lane_deletion" => Modified(lane, lane with { DeletedAt = now }),
            "CK_step_deletion" => Modified(step, step with { DeletedAt = now }),
            "CK_flow_deletion" => Modified(flow, flow with { DeletedAt = now }),
            "CK_step_column_index" => Modified(step, step with { Column = -1 }),
            "CK_document_wait_hours" => Modified(workspace.Document(document)!, workspace.Document(document)! with { WaitHours = 1 }),
            _ => StoreScenario.Created(new Translation(TranslationObject.Lane, lane.Id, TranslationField.Label, "en", "Lane (en)", TranslationSource.Manual, now, null)),
        }]);
    }
}
