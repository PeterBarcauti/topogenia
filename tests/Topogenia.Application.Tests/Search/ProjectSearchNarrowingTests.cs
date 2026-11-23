// ProjectSearchNarrowingTests.cs
// The choice of the candidate sheets never changes the reply of a search (ADR 110 point 4): a hundred projects drawn from a fixed seed, each role, against the rule of the sheets called for every step; an older validated version never makes a candidate.

using System.Text.Json;
using Topogenia.Application.Ports;
using Topogenia.Application.Search;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using static Topogenia.Application.Tests.Search.SearchFixture;

namespace Topogenia.Application.Tests.Search;

public sealed class ProjectSearchNarrowingTests
{
    /// <summary>A tool drawn only for a validated version that is not the latest one.</summary>
    private const string OlderTool = "Registre ancien";

    /// <summary>The fewest sheets, over the hundred projects, whose older versions alone hold the word drawn.</summary>
    private const int OlderOnlyMin = 50;

    private static readonly string?[] Tools = ["Tableur", "Messagerie", "Logiciel", "Tableur partagé", "Scanner", null];

    private static readonly string[] Words = ["tab", "messa", "logi", "partag", "scan", "regis", "e"];

    private static readonly StepType[] Types = [StepType.Task, StepType.Task, StepType.Task, StepType.Decision, StepType.Wait, StepType.Start, StepType.Parallel];

    [Fact]
    public async Task The_choice_of_candidates_never_changes_what_is_found_for_any_role()
    {
        var random = new Random(110);
        var (tools, differing, olderOnly) = (0, 0, 0);
        for (var round = 0; round < 100; round++)
        {
            var project = Draw(random);
            var word = Words[random.Next(Words.Length)];
            var workspace = (await new InMemoryWorkspaceStore(project.Database).LoadAsync(project.ProjectId,
                WorkspaceContent.Sheets | WorkspaceContent.Documents | WorkspaceContent.Translations, Token))!;
            var replies = new List<string>();
            var older = OlderOnly(workspace, SearchWord.Of(word)!);
            olderOnly += older.Count;
            var asked = new List<Guid>();
            ProjectSearchWalk.Run(workspace, new SearchRequest(SearchTerms.Of(word)!, null, null), step =>
            {
                asked.Add(step.Id);
                return SheetRights.ViewerOf(workspace, step, MemberRole.Reader, project.Reader.Id);
            });
            Assert.DoesNotContain(asked, older.Contains);
            foreach (var (user, role) in new[] { (project.Facilitator, MemberRole.Facilitator), (project.Contributor, MemberRole.Contributor),
                (project.Reader, MemberRole.Reader), (project.Admin, MemberRole.Facilitator) })
            {
                var found = await SearchAsync(project.ActingAs(user), word);
                var everyStep = ProjectSearchWalk.Run(workspace, new SearchRequest(SearchTerms.Of(word)!, null, null),
                    step => SheetRights.ViewerOf(workspace, step, role, user.Id), narrowed: false);
                Assert.Equal(JsonSerializer.Serialize(everyStep), JsonSerializer.Serialize(found));
                tools += Hits(found).Count(hit => hit.Kind == SearchKind.Tool);
                replies.Add(JsonSerializer.Serialize(found));
            }
            differing += replies.Distinct(StringComparer.Ordinal).Count() > 1 ? 1 : 0;
        }
        // The property is not empty: tools are found, the roles see different sheets in many projects, and many sheets
        // hold the word in an older version alone.
        Assert.InRange(tools, 300, int.MaxValue);
        Assert.InRange(differing, 30, 100);
        Assert.InRange(olderOnly, OlderOnlyMin, int.MaxValue);
    }

    [Fact]
    public async Task The_choice_of_candidates_never_changes_what_several_words_find_for_any_role()
    {
        var random = new Random(9);
        var tools = 0;
        string[] queries = ["tab partag", "partag tab", "logi tab", "scan e", "regis ancien"];
        for (var round = 0; round < 40; round++)
        {
            var project = Draw(random);
            var query = queries[round % queries.Length];
            var workspace = (await new InMemoryWorkspaceStore(project.Database).LoadAsync(project.ProjectId,
                WorkspaceContent.Sheets | WorkspaceContent.Documents | WorkspaceContent.Translations, Token))!;
            foreach (var (user, role) in new[] { (project.Facilitator, MemberRole.Facilitator), (project.Contributor, MemberRole.Contributor),
                (project.Reader, MemberRole.Reader) })
            {
                var found = await SearchAsync(project.ActingAs(user), query);
                var everyStep = ProjectSearchWalk.Run(workspace, new SearchRequest(SearchTerms.Of(query)!, null, null),
                    step => SheetRights.ViewerOf(workspace, step, role, user.Id), narrowed: false);
                Assert.Equal(JsonSerializer.Serialize(everyStep), JsonSerializer.Serialize(found));
                tools += Hits(found).Count(hit => hit.Kind == SearchKind.Tool);
                Assert.DoesNotContain(Hits(found), hit => query == "logi tab" && hit.Kind == SearchKind.Tool);
            }
        }
        // Not empty: the shared spreadsheet tool holds both words of the first two queries, « Scanner » the word kept of the fourth.
        Assert.InRange(tools, 20, int.MaxValue);
    }

    [Fact]
    public async Task The_rule_is_asked_for_a_draft_or_a_latest_version_that_holds_the_word_never_for_an_older_version()
    {
        var project = new TestProject();
        var older = project.Step(0);
        await DraftAsync(project, older, "Registre");
        await ValidateAsync(project, older);
        await DraftAsync(project, older, "Classeur");
        await ValidateAsync(project, older);
        await DraftAsync(project, older, "Tableur");
        var draft = project.Step(1);
        await DraftAsync(project, draft, "Registre neuf");
        var latest = project.Step(2);
        await DraftAsync(project, latest, "Classeur");
        await ValidateAsync(project, latest);
        await DraftAsync(project, latest, "Registre");
        await ValidateAsync(project, latest);
        await DraftAsync(project, latest, "Tableur");
        var workspace = (await new InMemoryWorkspaceStore(project.Database).LoadAsync(project.ProjectId,
            WorkspaceContent.Sheets | WorkspaceContent.Documents | WorkspaceContent.Translations, Token))!;

        var asked = new List<Guid>();
        ProjectSearchWalk.Run(workspace, new SearchRequest(SearchTerms.Of("registre")!, null, null), step =>
        {
            asked.Add(step.Id);
            return SheetRights.ViewerOf(workspace, step, MemberRole.Reader, project.Reader.Id);
        });
        Assert.Equal([draft, latest], asked);
        project.ActingAs(project.Reader);
        Assert.Equal([latest], Hits(await SearchAsync(project, "registre")).Select(hit => hit.StepId!.Value));
        Assert.Equal([older], Hits(await SearchAsync(project, "classeur")).Select(hit => hit.StepId!.Value));
        project.ActingAs(project.Facilitator);
        Assert.Equal([draft], Hits(await SearchAsync(project, "registre")).Select(hit => hit.StepId!.Value));
    }

    /// <summary>A project of one to three maps, two lanes at the root, steps of every kind, sheets with drafts and versions drawn at random.</summary>
    private static TestProject Draw(Random random)
    {
        var project = new TestProject();
        var lanes = new List<Guid> { project.LaneId, AddLane(project, project.RootMapId, "Comptabilité") };
        if (random.Next(2) == 0)
            project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(lanes[random.Next(2)])) with { ActorUserId = project.Contributor.Id });
        var steps = Steps(project, random, project.RootMapId, lanes, random.Next(3, 9));
        foreach (var parent in steps.OrderBy(_ => random.Next()).Take(random.Next(3)).ToList())
        {
            var (mapId, laneId) = project.SubMap(parent);
            if (random.Next(3) == 0)
                project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(laneId)) with { ActorUserId = project.Contributor.Id });
            steps.AddRange(Steps(project, random, mapId, [laneId], random.Next(2, 6)));
        }
        foreach (var step in steps)
        {
            if (random.Next(10) < 7)
                Sheet(project, random, step);
            if (random.Next(10) == 0)
                DeleteStep(project, step);
        }
        return project;
    }

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

    /// <summary>
    /// A sheet: its draft (tools and procedure), and zero to five validated versions written in a random order, the
    /// versions before the latest one holding now and then a tool of their own.
    /// </summary>
    private static void Sheet(TestProject project, Random random, Guid stepId)
    {
        var sheetId = project.NextId();
        project.Database.Add(new StepSheet(sheetId, stepId, (SheetStatus)random.Next(4), null, "Traiter", Tool(random), null, null, null, "fr", null,
            TestProject.Now, project.Facilitator.Id, null, null, 1));
        foreach (var (task, position) in Procedure(random).Select((task, position) => (task, position)))
            project.Database.Add(new SheetOperation(sheetId, position, task.Text, task.Tool, null));
        var count = random.Next(6);
        foreach (var number in Enumerable.Range(1, count).OrderBy(_ => random.Next()).ToList())
        {
            var tool = number < count && random.Next(3) == 0 ? OlderTool : Tool(random);
            var content = new SheetContent(null, "Traiter", tool, null, null, Procedure(random), null);
            project.Database.Add(new StepSheetVersion(project.NextId(), sheetId, number, SheetJson.Freeze(content), null, null,
                project.Facilitator.Id, TestProject.Now));
        }
    }

    private static List<ProcedureTask> Procedure(Random random) =>
        [.. Enumerable.Range(0, random.Next(4)).Select(_ => new ProcedureTask("Saisir", Tool(random), null))];

    private static string? Tool(Random random) => Tools[random.Next(Tools.Length)];

    /// <summary>The steps whose sheet holds the word in a tool of a version older than the latest one, and nowhere else.</summary>
    private static HashSet<Guid> OlderOnly(ProjectWorkspace workspace, SearchWord word)
    {
        bool Holds(IEnumerable<string?> tools) => tools.Any(tool => word.In(tool) is not null);
        IEnumerable<string?> ToolsOf(StepSheetVersion version)
        {
            var content = SheetJson.Thaw(version.ContentJson).Content;
            return content.Procedure.Select(task => task.Tool).Prepend(content.Tools);
        }
        return [.. workspace.All<StepSheet>().Where(sheet =>
        {
            var versions = workspace.All<StepSheetVersion>().Where(version => version.SheetId == sheet.Id).OrderBy(version => version.VersionNumber).ToList();
            var draft = workspace.All<SheetOperation>().Where(operation => operation.SheetId == sheet.Id).Select(operation => operation.Tool).Prepend(sheet.Tools);
            return versions.Count > 1 && !Holds(draft) && !Holds(ToolsOf(versions[^1])) && versions.SkipLast(1).Any(version => Holds(ToolsOf(version)));
        }).Select(sheet => sheet.StepId)];
    }
}
