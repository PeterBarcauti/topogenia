// OriginLanguageFlowCommentsTests.cs
// The comments of presentation of the flows at the change of the language of origin of a project (ADR 100, ADR 099 lot J10): one that has an original and no line in the language asked lacks its translation, named after the texts of the core and the comments of the steps, as the workbook names a flow.

using Topogenia.Application.Presentation;
using Topogenia.Application.Projects;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Projects;

public sealed class OriginLanguageFlowCommentsTests
{
    private static readonly LanguageSettings Languages = new(["fr", "en", "de"], "fr", new Dictionary<string, string> { ["fr"] = "Acteur 1" });

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FlowPresentationComment Line(TestProject project, Guid flow, string language, string text) =>
        new(flow, text, language, CommentSource.Manual, 1, TestProject.Now, project.Facilitator.Id);

    private static async Task<DomainException> RefusedAsync(TestProject project, string language) =>
        await Assert.ThrowsAsync<DomainException>(() => project.Runner.RunAsync(new ChangeOriginLanguageCommand(project.ProjectId, language, Languages), Token));

    private static List<MissingText> MissingOf(DomainException refusal) => [.. ((IEnumerable<MissingText>)refusal.Details["missing"]!)];

    [Fact]
    public async Task A_comment_of_a_flow_without_its_line_in_the_language_asked_is_a_text_that_lacks_named_last_as_the_workbook_names_a_flow()
    {
        var project = new TestProject();
        var (a, b, c) = (project.Step(0), project.Step(1), project.Step(2));
        var lacking = project.Flow(a, b);
        var translated = project.Flow(b, c);
        var orphan = project.Flow(a, c);
        project.Database.AddFlowComment(Line(project, lacking, "fr", "La demande part chez le responsable."));
        project.Database.AddFlowComment(Line(project, lacking, "de", "Der Antrag geht an die Leitung."));
        project.Database.AddFlowComment(Line(project, translated, "fr", "Le dossier part au paiement."));
        project.Database.AddFlowComment(Line(project, translated, "en", "The file goes to payment."));
        project.Database.AddFlowComment(Line(project, orphan, "de", "Ohne Original."));
        project.Database.AddComment(new PresentationComment(a, "Le salarié saisit.", "fr", CommentSource.Manual, 1, TestProject.Now, project.Facilitator.Id));

        var refusal = await RefusedAsync(project, "en");

        Assert.Equal(ErrorCodes.IncompleteTranslation, refusal.Code);
        var missing = MissingOf(refusal);
        Assert.Equal(((object?)"en", (object?)missing.Count), (refusal.Details["language"], refusal.Details["count"]));
        // The texts of the core first, then the comment of the step, then the one of the flow.
        Assert.Equal([new MissingText("presentation_comment", a, "PRJ-E001"), new MissingText("flow_presentation_comment", lacking, "PRJ-E001 > PRJ-E002")], missing[^2..]);
        Assert.DoesNotContain(missing, text => text.Id == translated || text.Id == orphan);
        Assert.Equal(0, project.Database.Commits);
    }

    [Fact]
    public async Task The_comment_of_a_flow_alone_refuses_the_change_and_once_translated_no_longer_counts()
    {
        var project = new TestProject();
        var flow = project.Flow(project.Step(0), project.Step(1));
        project.Database.AddFlowComment(Line(project, flow, "fr", "La demande part chez le responsable."));

        var before = MissingOf(await RefusedAsync(project, "en"));
        Assert.Equal(new MissingText("flow_presentation_comment", flow, "PRJ-E001 > PRJ-E002"), before[^1]);

        // Translated, the comment no longer lacks: what still refuses the change is the texts of the core, none of them a comment.
        project.Database.AddFlowComment(Line(project, flow, "en", "The request goes to the manager."));
        var after = await RefusedAsync(project, "en");
        Assert.Equal((ErrorCodes.IncompleteTranslation, (object?)(before.Count - 1)), (after.Code, after.Details["count"]));
        // Into German, the comment lacks again.
        Assert.Contains(MissingOf(await RefusedAsync(project, "de")), text => text.Type == "flow_presentation_comment" && text.Id == flow);
    }
}
