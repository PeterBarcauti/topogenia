// SearchFixture.cs
// Helpers of the search tests: the search run as the current user, its results read flat, and the steps, lanes, documents, pain points, translations and sheets of a fictional project.

using Topogenia.Application.Search;
using Topogenia.Application.Sheets;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Search;

internal static class SearchFixture
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public static Task<ProjectSearchView> SearchAsync(TestProject project, string? text, string? language = null, Guid? mapId = null) =>
        project.Queries.RunAsync(new ProjectSearchQuery(project.ProjectId, text, language, mapId), Token);

    /// <summary>Every result in its order: the level of its map, its kind and the text it was found in.</summary>
    public static IReadOnlyList<(int Depth, SearchKind Kind, string Text)> Found(ProjectSearchView view) =>
        [.. view.Maps.SelectMany(map => map.Hits.Select(hit => (map.Depth, hit.Kind, hit.Text)))];

    public static IReadOnlyList<SearchHitView> Hits(ProjectSearchView view) => [.. view.Maps.SelectMany(map => map.Hits)];

    /// <summary>Gives a step its label and, when given, its code.</summary>
    public static Guid Named(TestProject project, Guid stepId, string label, string? code = null)
    {
        var step = project.Database.Get<Step>(Step.KeyOf(stepId));
        project.Database.Add(step with { Label = label, Code = code ?? step.Code });
        return stepId;
    }

    public static void RenameLane(TestProject project, Guid laneId, string name) =>
        project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(laneId)) with { Name = name });

    public static void RenameMap(TestProject project, Guid mapId, string name) =>
        project.Database.Add(project.Database.Get<ProcessMap>(ProcessMap.KeyOf(mapId)) with { Name = name });

    /// <summary>Adds a live lane to a map, after the first one by default.</summary>
    public static Guid AddLane(TestProject project, Guid mapId, string name, int sortOrder = 1, Guid? actor = null)
    {
        var id = project.NextId();
        project.Database.Add(new Lane(id, mapId, $"PRJ-C{sortOrder + 10:D2}", name, "fr", LaneType.Person, actor, "#E3F2FD", sortOrder, null,
            TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        return id;
    }

    /// <summary>Adds a document held by a step, entered at a given minute so that the entry order is known.</summary>
    public static Guid AddDocument(TestProject project, Guid stepId, string name, int minute = 0)
    {
        var id = project.NextId();
        project.Database.Add(new Document(id, stepId, DocumentDirection.Output, name, null, null, null, false, "fr",
            TestProject.Now.AddMinutes(minute), project.Facilitator.Id, null, null, 1));
        return id;
    }

    public static Guid AddPainPoint(TestProject project, Guid? stepId, string code, string description,
        PainPointStatus status = PainPointStatus.Reported, string? action = null, string? owner = null)
    {
        var id = project.NextId();
        project.Database.Add(new PainPoint(id, project.ProjectId, stepId, code, description, "fr", null, status, TestProject.Now,
            project.Facilitator.Id, null, null, 1) { Action = action, Owner = owner });
        return id;
    }

    public static void Translate(TestProject project, TranslationObject type, Guid id, string language, string text) =>
        project.Database.Add(new Translation(type, id, type is TranslationObject.Step or TranslationObject.Flow or TranslationObject.Summary
            ? TranslationField.Label : TranslationField.Name, language, text, TranslationSource.Manual, TestProject.Now, project.Facilitator.Id));

    /// <summary>Saves the draft of a sheet as the facilitator: its field « Outils » and one line of the procedure for each tool given.</summary>
    public static async Task DraftAsync(TestProject project, Guid stepId, string? tools, params string[] procedureTools)
    {
        var user = project.CurrentUser.User;
        project.ActingAs(project.Facilitator);
        var fields = new Dictionary<string, SheetFieldValue?> { [SheetContentReader.PurposeKey] = new("Traiter", null) };
        if (tools is not null)
            fields[SheetContentReader.ToolsKey] = new(tools, null);
        var lines = procedureTools.Select(tool => (ProcedureLine?)new ProcedureLine("Saisir", tool, null)).ToList();
        await project.Runner.RunAsync(new SaveSheetCommand(stepId, new SheetInput(fields, lines, null)), Token);
        project.CurrentUser.User = user;
    }

    /// <summary>Validates the draft of a sheet as the facilitator.</summary>
    public static async Task ValidateAsync(TestProject project, Guid stepId)
    {
        var user = project.CurrentUser.User;
        project.ActingAs(project.Facilitator);
        await project.Runner.RunAsync(new ValidateSheetCommand(stepId), Token);
        project.CurrentUser.User = user;
    }

    /// <summary>A sheet validated with some tools, then changed into a draft with others.</summary>
    public static async Task ValidatedThenDraftAsync(TestProject project, Guid stepId, string validated, string draft)
    {
        await DraftAsync(project, stepId, validated);
        await ValidateAsync(project, stepId);
        await DraftAsync(project, stepId, draft);
    }

    public static void Retype(TestProject project, Guid stepId, StepType type) =>
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(stepId)) with { StepType = type });

    public static void DeleteStep(TestProject project, Guid stepId) =>
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(stepId)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });

    public static void DeleteMap(TestProject project, Guid mapId) =>
        project.Database.Add(project.Database.Get<ProcessMap>(ProcessMap.KeyOf(mapId)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });
}
