// ErrorTranslationTests.cs
// Refusals of the guards of both engines translated into the codes of the domain, with the name of the guard (spec 4.3-5, A-4-53).

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Persistence.Errors;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Sqlite;
using Topogenia.Persistence.Tests.Infrastructure;
using Topogenia.Persistence.Writing;

namespace Topogenia.Persistence.Tests.Writing;

/// <summary>
/// A-5-07, A-5-08, A-2.16-11, A-4-53 (AR-80): a unique index or key refused by the engine gives `CONFLIT_UNICITE`, a
/// foreign key or a CHECK gives `CONFLIT_INTEGRITE`, never an internal error; the details name the guard as
/// `docs/modele-donnees.md` does (SQLite names no foreign key). Each refusal is produced by the real engine; the change
/// sets that break a guard are written by hand, since the domain never produces them.
/// </summary>
public sealed class ErrorTranslationTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<StoreScenario> ScenarioAsync(EngineKind kind) => await StoreScenario.CreateAsync(await databases.OfAsync(kind));

    private static async Task<DomainException> RefusedAsync(StoreScenario scenario, ChangeSet changes)
    {
        var refusal = await Assert.ThrowsAsync<DomainException>(() => scenario.WriteAsync(changes));
        Assert.Equal(RefusalKind.Conflict, refusal.Kind);
        return refusal;
    }

    /// <summary>A step of the root map at a cell of the first lane, with a code of its own, as the domain would build it.</summary>
    private static Step StepAt(ProjectWorkspace workspace, StoreScenario scenario, int column, int row, int number) =>
        new(Guid.CreateVersion7(), workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, $"{workspace.Project.Code}-E{number}",
            "Written by hand", "fr", StepType.Task, column, row, null, AutomationStatus.Manual, null, FixedClock.Time, scenario.Actor.Id,
            null, null, null, null, 1);

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_step_written_in_a_cell_taken_since_is_refused_as_a_unique_conflict_naming_the_index(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var stale = await scenario.LoadAsync();
        await scenario.RunAsync((workspace, context) =>
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 3, "First")));

        var refusal = await RefusedAsync(scenario, new ChangeSet([StoreScenario.Created(StepAt(stale, scenario, 3, 0, 900))]));

        Assert.Equal(ErrorCodes.UniqueConflict, refusal.Code);
        Assert.Equal(IndexNames.LiveStepOfCell, refusal.Details[StoreRefusals.ConstraintDetail]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_counter_left_out_of_the_workspace_is_refused_by_its_key_and_never_gives_a_code_twice(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var loaded = await scenario.LoadAsync();
        var forgotten = new ProjectWorkspace(scenario.ProjectId,
            WorkspaceRecords.Of(loaded).Values.Where(record => record is not ProjectCounter { Counter: CounterKind.Step }));
        var context = scenario.Context();
        StepOperations.Create(forgotten, context, new CreateStepRequest(forgotten.RootMap.Id, forgotten.LanesOf(forgotten.RootMap.Id)[0].Id, 0, "Coded"));

        var refusal = await RefusedAsync(scenario, forgotten.Commit(context));

        Assert.Equal(ErrorCodes.UniqueConflict, refusal.Code);
        Assert.Equal("PK_project_counter", refusal.Details[StoreRefusals.ConstraintDetail]);
        Assert.Empty((await scenario.LoadAsync()).All<Step>());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_flow_to_a_step_that_does_not_exist_is_refused_as_an_integrity_conflict(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var loaded = await scenario.LoadAsync();
        var source = StepAt(loaded, scenario, 0, 0, 901);
        var flow = new Flow(Guid.CreateVersion7(), loaded.RootMap.Id, source.Id, Guid.CreateVersion7(), null, FixedClock.Time, scenario.Actor.Id,
            null, null, null, null, 1);

        var refusal = await RefusedAsync(scenario, new ChangeSet([StoreScenario.Created(source), StoreScenario.Created(flow)]));

        Assert.Equal(ErrorCodes.IntegrityConflict, refusal.Code);
        Assert.Equal(kind == EngineKind.SqlServer ? "FK_flow_target_step" : null, refusal.Details[StoreRefusals.ConstraintDetail]);
        Assert.Empty((await scenario.LoadAsync()).All<Step>());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_row_out_of_a_check_constraint_is_refused_as_an_integrity_conflict_naming_the_constraint(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var loaded = await scenario.LoadAsync();

        var refusal = await RefusedAsync(scenario, new ChangeSet([StoreScenario.Created(StepAt(loaded, scenario, 0, Cell.MaxRow + 1, 902))]));

        Assert.Equal(ErrorCodes.IntegrityConflict, refusal.Code);
        Assert.Equal("CK_step_row_index", refusal.Details[StoreRefusals.ConstraintDetail]);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_text_longer_than_its_column_is_refused_as_an_integrity_conflict_naming_its_text_guard(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var loaded = await scenario.LoadAsync();
        var step = StepAt(loaded, scenario, 0, 0, 905) with { Label = new string('l', StructureEdits.StepLabelMax + 1) };

        var refusal = await RefusedAsync(scenario, new ChangeSet([StoreScenario.Created(step)]));

        // SQLite refuses it by the CHECK, SQL Server by the type of the column (2628), read as the same guard.
        Assert.Equal(ErrorCodes.IntegrityConflict, refusal.Code);
        Assert.Equal("CK_step_label_text", refusal.Details[StoreRefusals.ConstraintDetail]);
        Assert.Empty((await scenario.LoadAsync()).All<Step>());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_structure_version_at_the_end_of_an_int_refuses_the_lock_as_an_integrity_conflict_and_stays_readable(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        await using (var context = scenario.Database.Context())
        {
            await context.Set<Project>().Where(project => project.Id == scenario.ProjectId)
                .ExecuteUpdateAsync(row => row.SetProperty(project => project.StructureVersion, int.MaxValue), Token);
        }

        var refusal = await Assert.ThrowsAsync<DomainException>(() => scenario.RunAsync((workspace, context) =>
            StepOperations.Create(workspace, context, new CreateStepRequest(workspace.RootMap.Id, workspace.LanesOf(workspace.RootMap.Id)[0].Id, 0, "Locked"))));

        // SQLite refuses `structure_version + 1` by the CHECK, SQL Server by the type of the column (8115), which names nothing.
        Assert.Equal(ErrorCodes.IntegrityConflict, refusal.Code);
        Assert.Equal(kind == EngineKind.Sqlite ? "CK_project_structure_version" : null, refusal.Details[StoreRefusals.ConstraintDetail]);
        Assert.Equal(int.MaxValue, (await scenario.LoadAsync()).Project.StructureVersion);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_failure_of_a_foreign_key_and_of_a_check_is_read_as_its_own_kind(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        var loaded = await scenario.LoadAsync();
        var source = StepAt(loaded, scenario, 0, 0, 903);
        var flow = new Flow(Guid.CreateVersion7(), loaded.RootMap.Id, source.Id, Guid.CreateVersion7(), null, FixedClock.Time, scenario.Actor.Id,
            null, null, null, null, 1);

        var foreignKey = await FailureAsync(scenario.Database, source, flow);
        var check = await FailureAsync(scenario.Database, StepAt(loaded, scenario, 0, Cell.MaxRow + 1, 904));

        Assert.Equal(StoreErrorKind.ForeignKey, foreignKey?.Kind);
        Assert.Equal(new StoreError(StoreErrorKind.Check, "CK_step_row_index"), check);
    }

    /// <summary>The failure of the engine when the records are saved directly, as the dialect reads it.</summary>
    private static async Task<StoreError?> FailureAsync(TestDatabase database, params object[] records)
    {
        await using var context = database.Context();
        context.AddRange(records);
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync(Token));
        return database.Dialect.Classify(failure, context.Model);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_failure_that_is_no_refusal_of_a_guard_is_left_as_it_is(EngineKind kind)
    {
        var scenario = await ScenarioAsync(kind);
        await using var session = scenario.Database.Session();
        var unit = new UnitOfWork(session);

        // Not a guard of the database: no code of the domain hides it.
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => unit.RunAsync<bool>(_ => throw new InvalidOperationException("outside"), Token));

        Assert.Equal("outside", failure.Message);
        await using var context = scenario.Database.Context();
        Assert.Null(scenario.Database.Dialect.Classify(failure, context.Model));
    }

    [Fact]
    public async Task Sqlite_names_a_unique_index_or_key_by_its_columns_and_a_check_by_its_name()
    {
        await using var database = TestDatabase.Create(EngineKind.Sqlite);
        await using var context = database.Context();
        var model = context.Model;
        var dialect = new SqliteDialect();

        var cell = dialect.Classify(new SqliteException("SQLite Error 19: 'UNIQUE constraint failed: step.lane_id, step.column_index, step.row_index'.", 19, 2067), model);
        var key = dialect.Classify(new SqliteException("SQLite Error 19: 'UNIQUE constraint failed: step_raci.step_id, step_raci.lane_id'.", 19, 1555), model);
        var check = dialect.Classify(new SqliteException("SQLite Error 19: 'CHECK constraint failed: CK_step_row_index'.", 19, 275), model);
        var foreignKey = dialect.Classify(new SqliteException("SQLite Error 19: 'FOREIGN KEY constraint failed'.", 19, 787), model);
        var busy = dialect.Classify(new InvalidOperationException("wrapped", new SqliteException("SQLite Error 5: 'database is locked'.", 5, 5)), model);
        var locked = dialect.Classify(new SqliteException("SQLite Error 6: 'database table is locked'.", 6, 6), model);

        Assert.Equal(new StoreError(StoreErrorKind.Unique, IndexNames.LiveStepOfCell), cell);
        Assert.Equal(new StoreError(StoreErrorKind.Unique, "PK_step_raci"), key);
        Assert.Equal(new StoreError(StoreErrorKind.Check, "CK_step_row_index"), check);
        Assert.Equal(new StoreError(StoreErrorKind.ForeignKey, null), foreignKey);
        Assert.Equal((true, true), (busy!.IsTransient, locked!.IsTransient));
        Assert.Null(dialect.Classify(new SqliteException("SQLite Error 1: 'no such table'.", 1, 1), model));
    }
}
