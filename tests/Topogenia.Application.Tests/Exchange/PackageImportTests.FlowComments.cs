// PackageImportTests.FlowComments.cs
// The import of an exchange package that carries comments of presentation on its flows (ADR 099, lot J10): written beside the flows of the new project, refused whole when invalid, kept by a replaced project.

using Topogenia.Application.Presentation;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.Exchange;

public sealed partial class PackageImportTests
{
    /// <summary>The comments of the flows of a package of the format 2.4 (ADR 099, lot J10) are written beside the flows of the new project, as those of its steps.</summary>
    [Fact]
    public async Task An_import_writes_the_comments_of_its_flows_the_original_as_a_text_typed_and_leaves_out_those_in_a_language_not_configured_here()
    {
        var (fixture, _, import) = Arrange(new FakeImporter
        {
            Comments = [(0, "Le client demande un devis.", "fr")],
            FlowComments = [("Le devis part au client.", "fr", CommentSource.Workbook), ("The quote goes to the customer.", "en", CommentSource.Assistant),
                ("Das Angebot geht an den Kunden.", "de", CommentSource.Manual)],
        });
        fixture.ActingAs(fixture.Project.Admin);

        var imported = await import.ImportAsync([1], null, withMembers: false, Token);

        var flow = Assert.Single(fixture.Database.All<Flow>(), made => fixture.Database.Get<ProcessMap>(ProcessMap.KeyOf(made.MapId)).ProjectId == imported.Id);
        // The line in the language of origin of the source step of the flow is the original, always a text typed.
        Assert.Equal([("en", "The quote goes to the customer.", CommentSource.Assistant), ("fr", "Le devis part au client.", CommentSource.Manual)],
            fixture.Database.FlowCommentsOf(flow.Id).Values.Select(comment => (comment.Language, comment.Text, comment.Source)).Order());
        Assert.All(fixture.Database.FlowComments.Values, comment => Assert.Equal((flow.Id, 1, fixture.Project.Admin.Id), (comment.FlowId, comment.Version, comment.UpdatedBy)));
        Assert.Single(fixture.Database.Comments);
    }

    [Theory]
    [InlineData("<b>Gras</b>", "fr")]
    [InlineData(" Rogné ", "fr")]
    [InlineData("Deux fois dans une langue.", "en")]
    public async Task A_comment_of_a_flow_that_its_rule_refuses_or_written_twice_in_a_language_refuses_the_package_and_nothing_is_written(string text, string language)
    {
        var (fixture, _, import) = Arrange(new FakeImporter
        {
            Comments = [(0, "Le client demande un devis.", "fr")],
            FlowComments = [("The quote goes to the customer.", "en", CommentSource.Manual), (text, language, CommentSource.Manual)],
        });
        fixture.ActingAs(fixture.Project.Admin);

        var refusal = await Assert.ThrowsAsync<DomainException>(() => import.ImportAsync([1], null, withMembers: false, Token));

        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"values"), (refusal.Code, refusal.Details["reason"]));
        Assert.Equal(["presentationComment"], (IEnumerable<string>)refusal.Details["fields"]!);
        Assert.DoesNotContain(fixture.Database.All<Project>(), project => project.Code == "VIS");
        Assert.Empty(fixture.Database.FlowComments);
        Assert.Empty(fixture.Database.Comments);
    }

    [Fact]
    public async Task A_replacement_writes_the_comments_of_the_flows_of_the_new_project_and_the_replaced_one_keeps_its_own_in_the_trash()
    {
        var (fixture, _, import) = Arrange(new FakeImporter("NEUF") { FlowComments = [("Nouvelle phrase.", "fr", CommentSource.Manual)] });
        var old = fixture.Project.ProjectId;
        var oldFlow = fixture.Project.Flow(fixture.Project.Step(0), fixture.Project.Step(1));
        fixture.Database.AddFlowComment(new FlowPresentationComment(oldFlow, "Ancienne phrase.", "fr", CommentSource.Manual, 2, TestProject.Now, fixture.Project.Facilitator.Id));
        fixture.ActingAs(fixture.Project.Facilitator);

        var imported = await import.ReplaceAsync(old, [1], withMembers: false, version: null, Token);

        Assert.Equal(("Ancienne phrase.", 2), (fixture.Database.FlowComments[(oldFlow, "fr")].Text, fixture.Database.FlowComments[(oldFlow, "fr")].Version));
        var added = Assert.Single(fixture.Database.FlowComments.Values, comment => comment.FlowId != oldFlow);
        Assert.Equal((imported.Id, "Nouvelle phrase."),
            (fixture.Database.Get<ProcessMap>(ProcessMap.KeyOf(fixture.Database.Get<Flow>(Flow.KeyOf(added.FlowId)).MapId)).ProjectId, added.Text));
    }
}
