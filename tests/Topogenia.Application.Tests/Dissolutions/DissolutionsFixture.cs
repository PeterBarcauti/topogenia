// DissolutionsFixture.cs
// A detailed step ready to be dissolved, and a runner whose access reader also finds the project of a dissolution.

using Topogenia.Application.Commands;
using Topogenia.Application.Maps;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using static Topogenia.Application.Tests.Selection.SelectionFixture;

namespace Topogenia.Application.Tests.Dissolutions;

/// <summary>
/// Finds the project of a dissolution from its map, as the persistence does (`AccessReader`), and every other target as
/// the access reader of the support.
/// </summary>
internal sealed class FrameAccessReader(InMemoryDatabase database) : IAccessReader
{
    private readonly InMemoryAccessReader others = new(database);

    public Task<ProjectAccess?> FindAsync(AccessTarget target, Guid userId, CancellationToken cancellationToken)
    {
        if (target.Kind != TargetKind.Dissolution)
            return others.FindAsync(target, userId, cancellationToken);
        var dissolution = database.All<Dissolution>().SingleOrDefault(record => record.Id == target.Id);
        return dissolution is null
            ? Task.FromResult<ProjectAccess?>(null)
            : others.FindAsync(AccessTarget.Map(dissolution.MapId), userId, cancellationToken);
    }
}

/// <summary>
/// On the root map: Before (column 0), Macro (1) and After (2) linked in a row; the sub-map of Macro holds First and Second
/// linked. With a remainder, Macro produces a document and holds a reported pain point.
/// </summary>
internal sealed class DetailedFlow
{
    public DetailedFlow(bool remainder = true, AppSettings? settings = null)
    {
        Before = Project.Step(0);
        Macro = Project.Step(1);
        After = Project.Step(2);
        Flow(Project, Before, Macro);
        Flow(Project, Macro, After);
        (SubMap, SubLane) = Project.SubMap(Macro);
        First = Project.Step(0, laneId: SubLane, mapId: SubMap);
        Second = Project.Step(1, laneId: SubLane, mapId: SubMap);
        Flow(Project, First, Second, SubMap);
        if (remainder)
        {
            Project.Database.Add(new Document(Document, Macro, DocumentDirection.Output, "Bon de commande", null, null, null, false, "fr",
                TestProject.Now, Project.Facilitator.Id, null, null, 1));
            Project.Database.Add(new PainPoint(PainPoint, Project.ProjectId, Macro, "PRJ-I001", "Saisie en double", "fr", null,
                PainPointStatus.Reported, TestProject.Now, Project.Facilitator.Id, null, null, 1));
        }
        var chosen = settings ?? AppSettings.Default;
        Runner = new CommandRunner(Project.Unit, new FrameAccessReader(Project.Database), new InMemoryWorkspaceStore(Project.Database),
            new InMemoryOperationLog(Project.Database), new MapViewBuilder(chosen), Project.CurrentUser, Project.Clock, chosen, Project.Versions);
    }

    public TestProject Project { get; } = new();

    public CommandRunner Runner { get; }

    public Guid Before { get; }

    public Guid Macro { get; }

    public Guid After { get; }

    public Guid SubMap { get; }

    public Guid SubLane { get; }

    public Guid First { get; }

    public Guid Second { get; }

    public Guid Document { get; } = Guid.CreateVersion7();

    public Guid PainPoint { get; } = Guid.CreateVersion7();

    public Task<CommandOutcome<TResult>> RunAsync<TResult>(ProjectCommand<TResult> command) => Runner.RunAsync(command, Token);

    public Dissolution? OpenFrame => Project.Database.All<Dissolution>().SingleOrDefault(dissolution => dissolution.IsOpen);

    public Guid? HolderOfDocument => Project.Database.All<Document>().SingleOrDefault(document => document.Id == Document)?.StepId;
}
