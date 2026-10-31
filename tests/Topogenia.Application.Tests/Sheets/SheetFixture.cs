// SheetFixture.cs
// Helpers of the sheet, document and translation tests: fictional inputs, steps of a given type, flows and the languages.

using Topogenia.Application.Commands;
using Topogenia.Application.OperationLog;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Sheets;

internal static class SheetFixture
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>French and English, as the configuration of the host gives them (AR-29).</summary>
    public static LanguageSettings Languages { get; } = new(["fr", "en"], "fr",
        new Dictionary<string, string> { ["fr"] = "Acteur 1", ["en"] = "Actor 1" });

    /// <summary>A sheet with its purpose only (one rubric out of five filled).</summary>
    public static SheetInput Purpose(string text) =>
        new(new Dictionary<string, SheetFieldValue?> { [SheetContentReader.PurposeKey] = new(text, null) }, null, null);

    /// <summary>A sheet with its purpose and its frequency.</summary>
    public static SheetInput PurposeAndFrequency(string text, string frequency) =>
        new(new Dictionary<string, SheetFieldValue?>
        {
            [SheetContentReader.PurposeKey] = new(text, null),
            [SheetContentReader.FrequencyKey] = new(frequency, null),
            [SheetContentReader.ToolsKey] = new("Tableur, Messagerie", null),
        }, null, null);

    public static DocumentInput Output(string name) => new("output", name, null, null, false);

    public static DocumentInput ExternalInput(string name) => new("input", name, "https://exemple.invalid/doc", 4m, true);

    public static StepSheet SheetOf(TestProject project, Guid stepId) =>
        project.Database.All<StepSheet>().Single(sheet => sheet.StepId == stepId);

    /// <summary>Adds a step of a given type in a cell of the first lane.</summary>
    public static Guid StepOfType(TestProject project, int column, StepType type)
    {
        var id = project.Step(column);
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(id)) with { StepType = type });
        return id;
    }

    /// <summary>Marks a step deleted, as a deletion batch leaves it.</summary>
    public static void Delete(TestProject project, Guid stepId) =>
        project.Database.Add(project.Database.Get<Step>(Step.KeyOf(stepId)) with { DeletedAt = TestProject.Now, DeletionBatchId = project.NextId() });

    /// <summary>Adds a live flow between two steps of the root map, with an optional label.</summary>
    public static Guid Flow(TestProject project, Guid source, Guid target, string? label = null)
    {
        var id = project.NextId();
        project.Database.Add(new Flow(id, project.RootMapId, source, target, label, TestProject.Now, project.Facilitator.Id, null, null, null, null, 1));
        return id;
    }

    /// <summary>Makes a lane held by a user (the actor of the lane, AR-01).</summary>
    public static void HoldLane(TestProject project, Guid userId) =>
        project.Database.Add(project.Database.Get<Lane>(Lane.KeyOf(project.LaneId)) with { ActorUserId = userId });

    public static async Task<DomainException> Refused<T>(TestProject project, ProjectCommand<T> command) =>
        await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(command, Token));

    /// <summary>Nothing can be undone on the root map: the history is empty and an undo finds nothing (annex B 2.6-37 to 43).</summary>
    public static async Task AssertNothingToUndo(TestProject project)
    {
        Assert.Empty(project.Database.Entries);
        project.ActingAs(project.Facilitator);
        var refusal = await Refused(project, new UndoCommand(project.RootMapId));
        Assert.Equal(ErrorCodes.NotFound, refusal.Code);
    }
}
