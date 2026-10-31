// RoundTripTests.cs
// Every type of record of the domain written then read back identical on both engines (spec 4.1, ADR 006, 007, 008, AR-81).

using Microsoft.EntityFrameworkCore;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Assistant;
using Topogenia.Persistence.Schema;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Model;

/// <summary>
/// The undo compares the records read back with the records the journal kept (AR-81): a record read back must be equal
/// to the record written, every field, every code of every closed list, every date to the tick and of kind UTC.
/// </summary>
public sealed class RoundTripTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Every concrete record of the domain model, with how to read its table back.</summary>
    private static readonly Dictionary<Type, Func<AppDbContext, Task<List<IRecord>>>> Readers = new()
    {
        [typeof(AppUser)] = Read<AppUser>, [typeof(Project)] = Read<Project>, [typeof(ProjectCounter)] = Read<ProjectCounter>,
        [typeof(ProjectMember)] = Read<ProjectMember>, [typeof(DeletionBatch)] = Read<DeletionBatch>, [typeof(ProcessMap)] = Read<ProcessMap>,
        [typeof(Lane)] = Read<Lane>, [typeof(Step)] = Read<Step>, [typeof(Flow)] = Read<Flow>, [typeof(StepRaci)] = Read<StepRaci>,
        [typeof(Dissolution)] = Read<Dissolution>, [typeof(DissolutionStep)] = Read<DissolutionStep>, [typeof(DissolutionObject)] = Read<DissolutionObject>,
        [typeof(StepSheet)] = Read<StepSheet>, [typeof(SheetOperation)] = Read<SheetOperation>, [typeof(StepSheetVersion)] = Read<StepSheetVersion>,
        [typeof(Document)] = Read<Document>, [typeof(DocumentUse)] = Read<DocumentUse>, [typeof(PainPoint)] = Read<PainPoint>,
        [typeof(PainPointStatusChange)] = Read<PainPointStatusChange>, [typeof(Translation)] = Read<Translation>,
        [typeof(OperationLogEntry)] = Read<OperationLogEntry>, [typeof(AuditEvent)] = Read<AuditEvent>,
    };

    public static TheoryData<EngineKind, string> EveryRecordType()
    {
        var data = new TheoryData<EngineKind, string>();
        foreach (var kind in Enum.GetValues<EngineKind>())
        {
            foreach (var type in Readers.Keys.OrderBy(type => type.Name, StringComparer.Ordinal))
                data.Add(kind, type.Name);
        }
        return data;
    }

    private static async Task<List<IRecord>> Read<T>(AppDbContext context)
        where T : class, IRecord =>
        (await context.Set<T>().AsNoTracking().ToListAsync(Token)).Cast<IRecord>().ToList();

    private async Task<TestDatabase> WrittenAsync(EngineKind kind, ProjectGraph graph)
    {
        var database = await databases.OfAsync(kind);
        await using var context = database.Context();
        context.AddRange(graph.Records);
        await context.SaveChangesAsync(Token);
        return database;
    }

    [Theory]
    [MemberData(nameof(EveryRecordType))]
    public async Task Every_record_is_read_back_equal_to_the_record_written(EngineKind kind, string recordType)
    {
        var graph = ProjectGraph.New();
        var database = await WrittenAsync(kind, graph);
        var type = Readers.Keys.Single(candidate => candidate.Name == recordType);
        var written = graph.Records.OfType<IRecord>().Where(record => record.GetType() == type).ToList();
        Assert.NotEmpty(written);
        await using var context = database.Context();
        var read = (await Readers[type](context)).ToDictionary(record => record.Key);
        Assert.All(written, record => Assert.Equal(record, read[record.Key]));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_date_comes_back_utc_to_the_tick(EngineKind kind)
    {
        var graph = ProjectGraph.New();
        var database = await WrittenAsync(kind, graph);
        await using var context = database.Context();
        var step = await context.Set<Step>().AsNoTracking().SingleAsync(item => item.Id == graph.Steps[0].Id, Token);
        Assert.Equal(DateTimeKind.Utc, step.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, step.UpdatedAt!.Value.Kind);
        Assert.Equal(graph.Steps[0].CreatedAt.Ticks, step.CreatedAt.Ticks);
        Assert.NotEqual(0, step.CreatedAt.Ticks % TimeSpan.TicksPerMillisecond);
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Every_classification_of_a_project_is_read_back(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var projects = Enum.GetValues<Classification>().Select(classification => ProjectGraph.New(classification)).ToList();
        await using (var context = database.Context())
        {
            context.AddRange(projects.SelectMany(graph => graph.Records));
            await context.SaveChangesAsync(Token);
        }
        await using var reader = database.Context();
        foreach (var graph in projects)
            Assert.Equal(graph.Project, await reader.Set<Project>().AsNoTracking().SingleAsync(project => project.Id == graph.Project.Id, Token));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_model_maps_every_record_of_the_domain_the_history_of_the_schema_and_the_settings_of_the_assistant(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        await using var context = database.Context();
        var domain = typeof(IRecord).Assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IRecord).IsAssignableFrom(type));
        Assert.Equal(domain.Append(typeof(SchemaHistoryEntry)).Append(typeof(AssistantSettingsRow)).Select(type => type.Name).Order(StringComparer.Ordinal),
            context.Model.GetEntityTypes().Select(entity => entity.ClrType.Name).Order(StringComparer.Ordinal));
        Assert.Equal(domain.Select(type => type.Name).Order(StringComparer.Ordinal), Readers.Keys.Select(type => type.Name).Order(StringComparer.Ordinal));
    }
}
