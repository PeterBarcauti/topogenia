// AssistantReportTests.cs
// The report of a map read from the reply of the model (plan 3.1, L4): its parts read, its texts bounded, its scores brought within 1 to 5, its waves and sources among those known, the problems an action treats checked; then its codes checked against the project, a problem noted only with a pain point of the map.

using Topogenia.Application.Assistant;
using Topogenia.Domain.Values;

namespace Topogenia.Application.Tests.Assistant;

public sealed class AssistantReportTests
{
    private const string Report = """
        {"form":"report","message":"Le rapport de la carte Facturation.","cited":["FAC-E001"],"questions":[],"findings":[],"pmap":null,
         "report":{
           "summary":"La facture part de la commande.","actors":"La comptabilité fait l'essentiel.",
           "chapters":[{"title":"Recevoir la commande","short":"Recevoir","steps":["FAC-E001","FAC-E999","FAC-C01"],"story":"Tout commence."},
                       {"title":"","short":"","steps":[],"story":"sans titre"}],
           "journey":[{"step":"FAC-E001","short":"Saisir","score":9,"why":"Fluide."},{"step":"FAC-E002","short":"","score":0.4,"why":"Ressaisie."},
                      {"step":"FAC-E999","short":"x","score":3,"why":""},{"step":"FAC-E003","short":"y","score":"haut","why":""}],
           "problems":[{"source":"noted","pain_point":"FAC-I001","steps":["FAC-E002"],"title":"Ressaisie de la facture","short":"","detail":"6 h par mois.","kind":"re_entry"},
                       {"source":"noted","pain_point":"FAC-I777","steps":["FAC-E003"],"title":"Relances","short":"Relances","detail":"","kind":"invented"},
                       {"source":"invented","pain_point":null,"steps":[],"title":"Attente","short":"Attente","detail":"","kind":"wait"}],
           "actions":[{"title":"Importer les commandes","short":"Importer","detail":"Un import.","problems":[1,1,5,0],"owner":"Comptabilité","effort":2,"impact":5,"wave":"now"},
                      {"title":"Relancer moins","short":"Relancer","detail":"","problems":[2],"owner":"","effort":7,"impact":-1,"wave":"soon"}],
           "conclusion":"Commencer par l'import."}}
        """;

    private static AssistantReply Read(string text) => AssistantReplyReader.Read(text, out var reason) ?? throw new InvalidOperationException(reason);

    [Fact]
    public void A_report_is_read_its_scores_brought_within_one_to_five_its_waves_and_sources_known_its_actions_on_its_problems()
    {
        var report = Read(Report).Report!;
        Assert.Equal(("La facture part de la commande.", "La comptabilité fait l'essentiel.", "Commencer par l'import."), (report.Summary, report.Actors, report.Conclusion));
        Assert.Equal([("Recevoir la commande", "Recevoir")], report.Chapters.Select(chapter => (chapter.Title, chapter.Short)));
        Assert.Equal([5, 1, 3, 3], report.Journey.Select(step => step.Score));
        Assert.Equal("FAC-E002", report.Journey[1].Short);
        Assert.Equal([("noted", "re_entry", "Ressaisie de la facture"), ("noted", "other", "Relances"), ("spotted", "wait", "Attente")],
            report.Problems.Select(problem => (problem.Source, problem.Kind, problem.Short)));
        Assert.Equal([([1], 2, 5, "now"), ([2], 5, 1, "later")], report.Actions.Select(action => (action.Problems.ToArray(), action.Effort, action.Impact, action.Wave)));
        Assert.Empty(report.Noted);
    }

    [Fact]
    public void An_action_treats_the_problems_by_the_numbers_the_model_wrote_a_problem_left_out_shifting_none()
    {
        var text = Report.Replace("\"title\":\"Ressaisie de la facture\"", "\"title\":\" \"", StringComparison.Ordinal)
            .Replace("\"problems\":[1,1,5,0]", "\"problems\":[2,2,3,1]", StringComparison.Ordinal);
        var report = Read(text).Report!;
        Assert.Equal(["Relances", "Attente"], report.Problems.Select(problem => problem.Title));
        // Written 2 and 3 are the kept 1 and 2; written 1, left out, treats nothing.
        Assert.Equal([[1, 2], [1]], report.Actions.Select(action => action.Problems.ToArray()));
    }

    [Fact]
    public void A_short_title_is_cut_at_its_bound_and_a_report_form_without_its_report_is_refused()
    {
        var report = Read(Report.Replace("\"short\":\"Recevoir\"", "\"short\":\"Recevoir la commande du client par courriel\"", StringComparison.Ordinal)).Report!;
        Assert.InRange(report.Chapters[0].Short.Length, 2, AssistantBounds.ShortMax);
        Assert.EndsWith("…", report.Chapters[0].Short, StringComparison.Ordinal);
        Assert.Null(AssistantReplyReader.Read("""{"form":"report","message":"x","cited":[],"questions":[],"findings":[],"pmap":null,"report":null}""", out var reason));
        Assert.Equal("report without report", reason);
        Assert.Null(Read("""{"form":"answer","message":"x","cited":[],"questions":[],"findings":[],"pmap":null,"report":{"summary":"y"}}""").Report);
    }

    [Fact]
    public void The_codes_of_a_report_are_those_of_the_map_and_a_problem_is_noted_only_with_a_pain_point_of_its_sheets()
    {
        var map = Guid.NewGuid();
        AssistantObjectView Step(string code, Guid on) => new(code, "step", on, Guid.NewGuid(), code);
        var objects = new Dictionary<string, AssistantObjectView>(StringComparer.Ordinal)
        {
            ["FAC-E001"] = Step("FAC-E001", map), ["FAC-E002"] = Step("FAC-E002", map), ["FAC-E003"] = Step("FAC-E003", Guid.NewGuid()),
            ["FAC-C01"] = new("FAC-C01", "lane", map, Guid.NewGuid(), "Comptabilité"),
        };
        var noted = new AssistantPainPointView("FAC-I001", "FAC-E002", "Ressaisie", 6m, "reported");
        var context = new AssistantContext(Guid.NewGuid(), "FAC", "Facturation", "fr", Classification.Internal, map, "Facturation", false, false, MemberRole.Reader,
            null, AssistantSchemaFormat.None, null, [], objects) { PainPoints = [noted] };

        var report = AssistantReportCodes.Checked(Read(Report).Report!, context);
        Assert.Equal(["FAC-E001"], report.Chapters[0].Steps);
        Assert.Equal(["FAC-E001", "FAC-E002"], report.Journey.Select(step => step.Step));
        Assert.Equal([("noted", "FAC-I001"), ("spotted", null), ("spotted", null)], report.Problems.Select(problem => (problem.Source, problem.PainPoint)));
        // A step of another map is not of the report.
        Assert.Equal([["FAC-E002"], [], []], report.Problems.Select(problem => problem.Steps.ToArray()));
        Assert.Equal([noted], report.Noted);
        Assert.Equal(["FAC-E001", "FAC-E001", "FAC-E002", "FAC-E002"], AssistantReportCodes.Named(report));
    }
}
