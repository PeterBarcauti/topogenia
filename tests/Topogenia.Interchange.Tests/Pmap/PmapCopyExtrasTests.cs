// PmapCopyExtrasTests.cs
// The .pmap 1.5: the comments of presentation of the steps and the flows, the glossary of the project, the code and the status of a pain point; written by a whole copy only (the view in BPMN 2.0 keeps the code and the status alone), read, opened and refused as the other properties, left out of a proposal.

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapCopyExtrasTests
{
    private static readonly DateTime At = new(2026, 11, 16, 20, 0, 0, DateTimeKind.Utc);

    private const string StepComment = """{ "text": "On reçoit la demande.", "translations": { "en": "The request comes in." } }""";

    /// <summary>
    /// A file of two steps: the first with a comment and two outputs, the second using them, one pain point still
    /// reported and one dropped; a comment on the flow; a glossary.
    /// </summary>
    private static string Whole(string version = "1.5", string stepComment = StepComment, string status = "dropped", string extraUse = "") => $$"""
        {
          "format": "topogenia",
          "version": "{{version}}",
          "project": { "name": "Commentaires", "code": "COM", "glossary": "demande = request\nacheteur = buyer" },
          "map": {
            "lanes": [{{TwoLanes}}],
            "steps": [
              { "id": "e1", "lane": "c1", "label": "Recevoir", "comment": {{stepComment}},
                "sheet": { "outputs": [ { "id": "d1", "name": "Demande" }, { "id": "d2", "name": "Devis" } ] } },
              { "id": "e2", "lane": "c2", "label": "Traiter",
                "sheet": { "uses": [
                  { "document": "d1", "pain_point": "Demande incomplète", "lost_hours_per_month": 3, "pain_point_code": "COM-I001", "pain_point_status": "reported" },
                  { "document": "d2", "pain_point": "Devis illisible", "lost_hours_per_month": 5, "pain_point_code": "COM-I002", "pain_point_status": "{{status}}"{{extraUse}} } ] } }
            ],
            "flows": [ { "source": "e1", "target": "e2", "comment": { "text": "La demande passe à l'acheteur." } } ]
          }
        }
        """;

    private static PmapExtras ExtrasOf(PmapBuilt built) => new(built.Comments, built.FlowComments);

    private static string Copy(ProjectWorkspace workspace, PmapExtras? extras) =>
        PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(true, _ => new SheetViewer(false, true)) { Extras = extras }).Document, "Test", At);

    [Fact]
    public void A_file_1_5_reads_its_comments_its_glossary_and_the_code_and_the_status_of_its_pain_points()
    {
        var document = Read(Whole());

        Assert.Equal("demande = request\nacheteur = buyer", document.Project.Glossary);
        var comment = document.Map.Steps[0].Comment!;
        Assert.Equal(("On reçoit la demande.", "The request comes in."), (comment.Text, comment.Translations["en"]));
        Assert.Equal("La demande passe à l'acheteur.", document.Map.Flows[0].Comment!.Text);
        var uses = document.Map.Steps[1].Sheet!.Uses;
        Assert.Equal([("COM-I001", "reported"), ("COM-I002", "dropped")], uses.Select(use => (use.PainPointCode, use.PainPointStatus)));
    }

    [Fact]
    public void Opened_the_file_gives_its_glossary_to_the_project_its_comments_to_the_new_steps_and_flows_and_its_status_to_each_pain_point()
    {
        var (built, workspace) = Build(Read(Whole()));

        Assert.Equal("demande = request\nacheteur = buyer", workspace.Project.TranslationGlossary);
        var receive = workspace.All<Step>().Single(step => step.Label == "Recevoir");
        Assert.Equal([("en", "The request comes in."), ("fr", "On reçoit la demande.")],
            built.Comments.Where(line => line.StepId == receive.Id).OrderBy(line => line.Language, StringComparer.Ordinal).Select(line => (line.Language, line.Text)));
        var flow = Assert.Single(workspace.All<Flow>());
        var said = Assert.Single(built.FlowComments);
        Assert.Equal((flow.Id, "fr", "La demande passe à l'acheteur."), (said.FlowId, said.Language, said.Text));
        // ADR 116 point 10: the pain point dropped is raised and keeps its status, it is not reopened.
        Assert.Equal([("Demande incomplète", 3m, receive.Id, PainPointStatus.Reported), ("Devis illisible", 5m, receive.Id, PainPointStatus.Dropped)],
            workspace.All<PainPoint>().OrderBy(point => point.Code, StringComparer.Ordinal)
                .Select(point => (point.Description, point.LostHoursPerMonth!.Value, point.StepId!.Value, point.Status)));
        Assert.Equal(2, workspace.All<DocumentUse>().Count());
    }

    [Fact]
    public void A_whole_copy_writes_1_5_and_opens_again_as_it_was_and_a_copy_without_the_extras_stays_older()
    {
        var (built, workspace) = Build(Read(Whole()));

        var copy = Copy(workspace, ExtrasOf(built));
        Assert.Contains("\"version\": \"1.5\"", copy, StringComparison.Ordinal);
        var again = Read(copy);
        Assert.Equal("demande = request\nacheteur = buyer", again.Project.Glossary);
        Assert.Equal(("On reçoit la demande.", "The request comes in."), (again.Map.Steps[0].Comment!.Text, again.Map.Steps[0].Comment!.Translations["en"]));
        Assert.Equal("La demande passe à l'acheteur.", again.Map.Flows[0].Comment!.Text);
        Assert.Equal([("COM-I001", "reported"), ("COM-I002", "dropped")], again.Map.Steps[1].Sheet!.Uses.Select(use => (use.PainPointCode, use.PainPointStatus)));

        var plain = Copy(workspace, null);
        Assert.DoesNotContain("\"version\": \"1.5\"", plain, StringComparison.Ordinal);
        Assert.All(new[] { "comment", "glossary", "pain_point_code", "pain_point_status" }, name => Assert.DoesNotContain($"\"{name}\"", plain, StringComparison.Ordinal));
    }

    [Fact]
    public void The_additions_are_unknown_in_a_file_1_4_and_checked_by_their_rules_in_1_5()
    {
        var older = Faults(Whole("1.4")).Select(fault => fault.Path).ToList();
        Assert.Contains("project.glossary", older);
        Assert.Contains("map.steps[0].comment", older);
        Assert.Contains("map.flows[0].comment", older);
        Assert.Contains("map.steps[1].sheet.uses[0].pain_point_code", older);

        Assert.Contains(Faults(Whole(stepComment: $$"""{ "text": "{{new string('a', 501)}}" }""")), fault => fault.Path == "map.steps[0].comment.text");
        Assert.Contains(Faults(Whole(stepComment: """{ "translations": { "xx": "?" } }""")), fault => fault.Path == "map.steps[0].comment.translations.xx");
        Assert.Contains(Faults(Whole(stepComment: """{ "text": "<b>x</b>" }""")), fault => fault.Path == "map.steps[0].comment.text");
        Assert.Contains(Faults(Whole(status: "lost")), fault => fault.Path == "map.steps[1].sheet.uses[1].pain_point_status");
        Assert.Contains(Faults(Whole().Replace("\"pain_point\": \"Demande incomplète\", \"lost_hours_per_month\": 3, ", "", StringComparison.Ordinal)),
            fault => fault.Path == "map.steps[1].sheet.uses[0].pain_point_code");
    }

    [Fact]
    public void A_proposal_reads_a_file_1_5_without_keeping_its_additions()
    {
        var read = PmapReader.Read(Whole(), new PmapReadSettings(Languages, "ACI", Proposal: true));

        var document = read.Document!;
        Assert.Null(document.Project.Glossary);
        Assert.Null(document.Map.Steps[0].Comment);
        Assert.Null(document.Map.Flows[0].Comment);
        Assert.All(document.Map.Steps[1].Sheet!.Uses, use => Assert.Equal((null, null), (use.PainPointCode, use.PainPointStatus)));
    }

    /// <summary>
    /// A RACI letter the installation does not offer (S where only A, C and I are offered, the example project) is kept by
    /// a copy and by the project it opens, as the package keeps it (decision of the owner of the project, 16/11/2026); a
    /// letter offered goes through the rule of the domain as before.
    /// </summary>
    [Fact]
    public void A_raci_letter_the_installation_does_not_offer_is_kept_by_the_project_opened()
    {
        var file = Whole().Replace("\"label\": \"Traiter\",", "\"label\": \"Traiter\", \"raci\": { \"c1\": \"S\" },", StringComparison.Ordinal);

        var (_, workspace) = Build(Read(file, Domain.Raci.RaciRules.KnownLetters));

        var handle = workspace.All<Step>().Single(step => step.Label == "Traiter");
        var role = Assert.Single(workspace.All<StepRaci>(), raci => raci.StepId == handle.Id);
        Assert.Equal(RaciRole.S, role.Role);
        var lane = workspace.All<Lane>().Single(candidate => candidate.Id == role.LaneId).Code;
        Assert.Contains($"\"{lane}\": \"S\"", PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(false)).Document, "Test", At), StringComparison.Ordinal);
    }

    [Fact]
    public void A_proposal_opened_as_a_new_project_keeps_the_status_of_the_pain_points_its_map_of_origin_dealt_with()
    {
        // Audit of the copy 64, P2-05: the sheets carried from the map of origin (ADR 082) are read with the status of their
        // pain points, as a whole copy is, so that one dropped there is not raised again as reported in the new project; it
        // keeps its status there (ADR 116 point 10).
        var (_, origin) = Build(Read(Whole()));
        var point = origin.All<PainPoint>().Single(candidate => candidate.Status == PainPointStatus.Reported);
        origin.Put(point with { Status = PainPointStatus.Dropped });
        var lanes = origin.All<Lane>().OrderBy(lane => lane.SortOrder).Select(lane => lane.Code).ToList();
        var steps = origin.All<Step>().OrderBy(step => step.Column).Select(step => step.Code).ToList();
        var proposal = $$"""
            {
              "format": "topogenia", "version": "1.1", "project": { "name": "Commentaires", "code": "COM" },
              "map": {
                "lanes": [{ "id": "{{lanes[0]}}", "name": "Demandeur" }, { "id": "{{lanes[1]}}", "name": "Acheteur" }],
                "steps": [{ "id": "{{steps[0]}}", "lane": "{{lanes[0]}}", "label": "Recevoir", "column": 0 }, { "id": "{{steps[1]}}", "lane": "{{lanes[1]}}", "label": "Traiter", "column": 1 }],
                "flows": [{ "source": "{{steps[0]}}", "target": "{{steps[1]}}" }]
              }
            }
            """;
        var format = new PmapFiles();
        var carried = format.Carry(format.ReadProposal(proposal, Languages, "ACI"), origin, origin.RootMap.Id, _ => new SheetViewer(false, true));

        var opened = format.Build(Context(), carried, "NEW", "Nouveau", Inputs());

        var records = opened.Changes.Changes.Select(change => change.After).ToList();
        Assert.Equal(2, records.OfType<DocumentUse>().Count());
        Assert.Equal([PainPointStatus.Dropped, PainPointStatus.Dropped], records.OfType<PainPoint>().Select(created => created.Status));
    }

    [Fact]
    public void The_copy_the_view_in_bpmn_reads_holds_the_map_alone_without_the_places_of_its_documents()
    {
        // Audit of the copy 64, P2-03: read without any event, the copy of the view holds no more than the map shows; the
        // copy of the branch, an export with its event, keeps the levels below and the places.
        var file = Whole()
            .Replace("{ \"id\": \"d1\", \"name\": \"Demande\" }", "{ \"id\": \"d1\", \"name\": \"Demande\", \"location\": \"C:\\\\Partage\\\\Demandes\" }", StringComparison.Ordinal)
            .Replace("\"label\": \"Traiter\",", "\"label\": \"Traiter\", \"sub_map\": { \"lanes\": [{ \"id\": \"c9\", \"name\": \"Acheteur\" }], \"steps\": [{ \"id\": \"e9\", \"lane\": \"c9\", \"label\": \"Lire\", \"column\": 0 }] },",
                StringComparison.Ordinal);
        var (built, workspace) = Build(Read(file));
        var format = new PmapFiles();
        SheetViewer Viewer(Step step) => new(false, true);

        var view = format.OutlineView(workspace, workspace.RootMap.Id, Viewer);
        var branch = format.OutlineBranch(workspace, workspace.RootMap.Id, Viewer, ExtrasOf(built));

        var viewed = format.Write(view.Document, "Test", At);
        Assert.Contains("\"Demande\"", viewed, StringComparison.Ordinal);
        Assert.All(new[] { "Partage", "\"sub_map\"", "\"comment\"", "\"glossary\"", "Lire" }, text => Assert.DoesNotContain(text, viewed, StringComparison.Ordinal));
        var copied = format.Write(branch.Document, "Test", At);
        Assert.All(new[] { "Partage", "\"sub_map\"", "\"comment\"", "Lire" }, text => Assert.Contains(text, copied, StringComparison.Ordinal));
    }

    [Fact]
    public void The_copy_the_view_in_bpmn_reads_keeps_the_code_and_the_status_of_each_pain_point_and_neither_comment_nor_glossary()
    {
        // Audit of the copy 65, NEW-P2-01: without them the view drew a pain point dealt with as open, and without its code;
        // the comments and the glossary of a whole copy stay out of it (P2-03).
        var (_, workspace) = Build(Read(Whole()));
        var point = workspace.All<PainPoint>().Single(candidate => candidate.Status == PainPointStatus.Reported);
        workspace.Put(point with { Status = PainPointStatus.Dropped });
        var format = new PmapFiles();

        var view = format.OutlineView(workspace, workspace.RootMap.Id, _ => new SheetViewer(false, true));

        var viewed = format.Write(view.Document, "Test", At);
        var use = Read(viewed).Map.Steps.SelectMany(step => step.Sheet?.Uses ?? []).Single(candidate => candidate.PainPoint == point.Description);
        Assert.Equal((point.Code, "dropped"), (use.PainPointCode, use.PainPointStatus));
        Assert.All(new[] { "\"comment\"", "\"glossary\"" }, text => Assert.DoesNotContain(text, viewed, StringComparison.Ordinal));
    }
}
