// ActionPlanFixture.cs
// Helpers of the action plan tests: the plan read as the current user, pain points raised through a use or alone, and fictional projects drawn from a seed with their translations and decoys.

using Topogenia.Application.Documents;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Documents;

internal static class ActionPlanFixture
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public static Task<ActionPlanView> PlanAsync(TestProject project, Guid? mapId = null) =>
        project.Queries.RunAsync(new GetActionPlanQuery(mapId ?? project.RootMapId), Token);

    /// <summary>The lines of a plan by the code of their pain point.</summary>
    public static IReadOnlyDictionary<string, ActionPlanLine> ByCode(ActionPlanView plan) =>
        plan.Lines.ToDictionary(line => line.Code, StringComparer.Ordinal);

    /// <summary>
    /// A pain point raised on the producing step by the use of one of its outputs by the receiving step, written as the
    /// use command leaves them (the output, the use that names the pain point, the pain point on the producer), with a
    /// status and hours that only a package can give; returns the pain point and the document.
    /// </summary>
    public static (Guid PainPoint, Guid Document) Raise(TestProject project, Guid producer, Guid receiver, string code,
        decimal? hours = null, PainPointStatus status = PainPointStatus.Reported)
    {
        var document = project.NextId();
        project.Database.Add(new Document(document, producer, DocumentDirection.Output, $"Document {code}", null, null, null, false, "fr",
            TestProject.Now, project.Facilitator.Id, null, null, 1));
        var point = Alone(project, producer, code, hours, status);
        project.Database.Add(new DocumentUse(document, receiver, null, null, point, TestProject.Now, project.Facilitator.Id, null, null));
        return (point, document);
    }

    /// <summary>A pain point without the use that carries it (a package written by hand), on a step or on the map.</summary>
    public static Guid Alone(TestProject project, Guid? stepId, string code, decimal? hours = null, PainPointStatus status = PainPointStatus.Reported)
    {
        var id = project.NextId();
        project.Database.Add(new PainPoint(id, project.ProjectId, stepId, code, $"Irritant {code}", "fr", hours, status, TestProject.Now,
            project.Facilitator.Id, null, null, 1));
        return id;
    }

    /// <summary>Adds a live lane to a map, after the first one, held by an actor or not.</summary>
    public static Guid AddLane(TestProject project, Guid mapId, string name, int sortOrder = 1, Guid? actor = null)
    {
        var id = project.NextId();
        project.Database.Add(new Lane(id, mapId, $"PRJ-C{sortOrder + 10:D2}", name, "fr", LaneType.Person, actor, "#E3F2FD", sortOrder, null,
            TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        return id;
    }

    public static void Retype(TestProject project, Guid stepId, StepType type) =>
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(stepId)) with { StepType = type });

    public static void DeleteStep(TestProject project, Guid stepId) =>
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(stepId)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });

    public static void DeleteMap(TestProject project, Guid mapId) =>
        project.Database.Add(project.Database.Get<ProcessMap>(ProcessMap.KeyOf(mapId)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });

    public static void Translate(TestProject project, TranslationObject type, Guid id, TranslationField field, string language, string text) =>
        project.Database.Add(new Translation(type, id, field, language, text, TranslationSource.Manual, TestProject.Now, project.Facilitator.Id));

    /// <summary>The field a translation of a type is read on (the label of steps, flows and summaries, the name otherwise), written here, not read from the rule.</summary>
    public static TranslationField FieldOf(TranslationObject type) =>
        type is TranslationObject.Step or TranslationObject.Flow or TranslationObject.Summary ? TranslationField.Label : TranslationField.Name;

    /// <summary>
    /// A project drawn at random: two lanes at the root (one held by the contributor now and then), steps of every type
    /// assigned to the contributor now and then, zero to two detailed maps (in English now and then, one deleted now and
    /// then), pain points raised by uses within a map in every status, some without use or without step, steps deleted;
    /// translations of the maps, steps and lanes in two languages, and decoys: translations of another field, of another
    /// type of object, or of an object that does not exist.
    /// </summary>
    public static TestProject Draw(Random random)
    {
        var project = new TestProject();
        var lanes = new List<Guid> { project.LaneId, AddLane(project, project.RootMapId, "Comptabilité") };
        if (random.Next(2) == 0)
            project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(lanes[random.Next(2)])) with { ActorUserId = project.Contributor.Id });
        var maps = new List<(Guid Map, List<Guid> Lanes, List<Guid> Steps)> { (project.RootMapId, lanes, Steps(project, random, project.RootMapId, lanes, random.Next(3, 8))) };
        foreach (var parent in maps[0].Steps.OrderBy(_ => random.Next()).Take(random.Next(3)).ToList())
        {
            var (mapId, laneId) = project.SubMap(parent, random.Next(3) == 0 ? "en" : "fr");
            if (random.Next(3) == 0)
                project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(laneId)) with { ActorUserId = project.Contributor.Id });
            maps.Add((mapId, [laneId], Steps(project, random, mapId, [laneId], random.Next(2, 6))));
        }
        var number = 0;
        foreach (var (_, _, steps) in maps)
            PainPoints(project, random, steps, ref number);
        foreach (var step in maps.SelectMany(map => map.Steps).Where(_ => random.Next(10) == 0).ToList())
            DeleteStep(project, step);
        if (maps.Count > 1 && random.Next(4) == 0)
            DeleteMap(project, maps[^1].Map);
        Translations(project, random, maps);
        return project;
    }

    private static readonly StepType[] Types = [StepType.Task, StepType.Task, StepType.Task, StepType.Decision, StepType.Wait, StepType.Start, StepType.Parallel];

    private static readonly string[] Languages = ["en", "de", "fr"];

    private static List<Guid> Steps(TestProject project, Random random, Guid mapId, IReadOnlyList<Guid> lanes, int count)
    {
        var steps = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var step = project.Step(random.Next(6), random.Next(3) == 0 ? project.Contributor.Id : null, lanes[random.Next(lanes.Count)], mapId);
            Retype(project, step, Types[random.Next(Types.Length)]);
            steps.Add(step);
        }
        return steps;
    }

    private static void PainPoints(TestProject project, Random random, List<Guid> steps, ref int number)
    {
        for (var count = random.Next(5); count > 0; count--)
        {
            var code = $"PRJ-I{++number:D3}";
            var hours = random.Next(4) == 0 ? (decimal?)null : random.Next(0, 1000) / 10m;
            var status = (PainPointStatus)random.Next(7);
            var producer = steps[random.Next(steps.Count)];
            switch (random.Next(6))
            {
                case 0:
                    Alone(project, producer, code, hours, status);
                    break;
                case 1:
                    Alone(project, null, code, hours, status);
                    break;
                default:
                    Raise(project, producer, steps[random.Next(steps.Count)], code, hours, status);
                    break;
            }
        }
    }

    private static void Translations(TestProject project, Random random, List<(Guid Map, List<Guid> Lanes, List<Guid> Steps)> maps)
    {
        var objects = maps.SelectMany(map => map.Steps.Select(step => (TranslationObject.Step, step))
            .Concat(map.Lanes.Select(lane => (TranslationObject.Lane, lane))).Prepend((TranslationObject.Map, map.Map))).ToList();
        foreach (var (type, id) in objects)
        {
            foreach (var language in Languages.Where(_ => random.Next(2) == 0))
                Translate(project, type, id, FieldOf(type), language, $"{type} {id.ToString()[^4..]} {language}");
        }
        for (var count = random.Next(6); count > 0; count--)
        {
            var (_, id) = random.Next(5) == 0 ? (TranslationObject.Step, project.NextId()) : objects[random.Next(objects.Count)];
            var type = (TranslationObject)random.Next(6);
            var field = random.Next(2) == 0 ? TranslationField.Name : TranslationField.Label;
            var language = Languages[random.Next(Languages.Length)];
            Translate(project, type, id, field, language, $"Leurre {type} {field} {language}");
        }
    }
}
