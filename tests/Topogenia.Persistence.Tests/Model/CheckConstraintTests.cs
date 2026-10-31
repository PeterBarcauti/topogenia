// CheckConstraintTests.cs
// Every CHECK constraint of the model refuses its rows on both engines, and the bounds themselves are accepted (spec 4.2, S-4.2-01).

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Persistence.Model;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Persistence.Tests.Model;

public sealed partial class CheckConstraintTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    /// <summary>
    /// A value outside a closed list: an enumeration value that does not exist, whose code is its number (`99`, or `7`
    /// for a list of one-letter codes, whose column is one character wide).
    /// </summary>
    private static T Unknown<T>(int value = 99)
        where T : struct, Enum => (T)Enum.ToObject(typeof(T), value);

    public static TheoryData<EngineKind, string> EveryCheck()
    {
        var data = new TheoryData<EngineKind, string>();
        foreach (var kind in Enum.GetValues<EngineKind>())
        {
            foreach (var name in Violations.Keys.Order(StringComparer.Ordinal))
                data.Add(kind, name);
        }
        return data;
    }

    public static TheoryData<EngineKind, string> EveryBound()
    {
        var data = new TheoryData<EngineKind, string>();
        foreach (var kind in Enum.GetValues<EngineKind>())
        {
            foreach (var name in Bounds.Keys.Order(StringComparer.Ordinal))
                data.Add(kind, name);
        }
        return data;
    }

    private async Task<(TestDatabase Database, ProjectGraph Graph)> WrittenAsync(EngineKind kind)
    {
        var database = await databases.OfAsync(kind);
        var graph = ProjectGraph.New();
        await Refusals.SaveAsync(database, [.. graph.Records]);
        return (database, graph);
    }

    [Theory]
    [MemberData(nameof(EveryCheck))]
    public async Task A_row_that_breaks_a_check_constraint_is_refused_by_it(EngineKind kind, string check)
    {
        var (database, graph) = await WrittenAsync(kind);
        await Refusals.AssertRefusedAsync(database, GuardKind.Check, check, Violations[check](graph));
    }

    [Theory]
    [MemberData(nameof(EveryBound))]
    public async Task A_row_on_the_bound_of_a_check_constraint_is_accepted(EngineKind kind, string bound)
    {
        var (database, graph) = await WrittenAsync(kind);
        await Refusals.SaveAsync(database, Bounds[bound](graph));
    }

    [Fact]
    public void Every_check_constraint_of_the_model_has_a_refused_row() =>
        Assert.Equal(ModelGuards.Checks.Select(guard => guard.Name).Order(StringComparer.Ordinal), Violations.Keys.Order(StringComparer.Ordinal));

    /// <summary>A new project of the graph, with its own code (the code is unique).</summary>
    private static Project OtherProject(ProjectGraph graph) => graph.Project with { Id = Guid.CreateVersion7(), Code = ProjectGraph.New().Code };

    private static AppUser OtherUser(ProjectGraph graph) =>
        graph.Reader with { Id = Guid.CreateVersion7(), Login = graph.Reader.Login + "x", LoginNormalized = graph.Reader.LoginNormalized + "X" };

    private static Lane OtherLane(ProjectGraph graph) => graph.Lanes[0] with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('C', 60), SortOrder = 5 };

    /// <summary>A new live step in a free cell of the first lane.</summary>
    private static Step OtherStep(ProjectGraph graph) => graph.Steps[0] with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('E', 60), Column = 70 };

    /// <summary>A new start step, which may carry an incoming volume (spec 14.2, S1).</summary>
    private static Step OtherStart(ProjectGraph graph) => OtherStep(graph) with { StepType = StepType.Start };

    private static Flow OtherFlow(ProjectGraph graph) => graph.LiveFlow with { Id = Guid.CreateVersion7(), SourceStepId = graph.Steps[3].Id, TargetStepId = graph.Steps[4].Id };

    private static StepSheet OtherSheet(ProjectGraph graph) => graph.Sheets[0] with { Id = Guid.CreateVersion7(), StepId = graph.SubStep.Id };

    private static StepSheetVersion OtherVersion(ProjectGraph graph, int number) =>
        graph.Of<StepSheetVersion>()[0] with { Id = Guid.CreateVersion7(), VersionNumber = number };

    private static Document OtherDocument(ProjectGraph graph) => graph.Documents[0] with { Id = Guid.CreateVersion7() };

    private static PainPoint OtherPainPoint(ProjectGraph graph) => graph.MapPainPoint with { Id = Guid.CreateVersion7(), Code = graph.ReadableCode('I', 60) };

    private static DocumentUse OtherUse(ProjectGraph graph) => graph.Use with { StepId = graph.Steps[4].Id, PainPointId = null };

    private static Translation OtherTranslation(ProjectGraph graph) =>
        new(TranslationObject.Step, graph.Steps[1].Id, TranslationField.Label, "en", "Translated", TranslationSource.Manual, graph.At(20), null);

    private static Dissolution ClosedDissolution(ProjectGraph graph) =>
        graph.Dissolution with { Id = Guid.CreateVersion7(), ClosedAt = graph.At(20), ClosedBy = graph.Owner.Id };

    private static StepRaci OtherRaci(ProjectGraph graph) => new(graph.Steps[3].Id, graph.Lanes[1].Id, RaciRole.C, graph.At(20), graph.Owner.Id);

    private static SheetOperation OtherOperation(ProjectGraph graph, int position) => new(graph.Sheets[1].Id, position, "Row", null, null);

    private static OperationLogEntry OtherEntry(ProjectGraph graph) => graph.LogEntry with { Id = Guid.CreateVersion7(), Sequence = 30 };

    private static PainPointStatusChange OtherChange(ProjectGraph graph) => graph.Of<PainPointStatusChange>()[1] with { Id = Guid.CreateVersion7() };
}
