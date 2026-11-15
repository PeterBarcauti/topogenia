// PmapMakigamiTests.cs
// The value class of a step and the medium of a document in a .pmap 1.4 (Makigami M1, M3, ADR 095): read, built, copied in 1.4 and only in 1.4; unknown in 1.3, refused on a step without content, on a step whose detail has steps, or out of their lists; the class asleep on a detailed step never written; the class of a proposal in its state wanted (F-103).

using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapMakigamiTests
{
    private static readonly DateTime At = new(2026, 11, 7, 2, 0, 0, DateTimeKind.Utc);

    /// <summary>A start, a step classified whose sheet receives a document on paper and produces one by email, of the version given.</summary>
    private static string Classified(string version = "1.4", string type = "task", string valueClass = "waste", string medium = "email") => $$"""
        {
          "format": "topogenia",
          "version": "{{version}}",
          "project": { "name": "Makigami", "code": "MAK" },
          "map": {
            "lanes": [{{TwoLanes}}],
            "steps": [
              { "id": "e0", "lane": "c1", "label": "Début", "type": "start" },
              { "id": "e1", "lane": "c1", "label": "Ressaisir", "type": "{{type}}", "value_class": "{{valueClass}}",
                "sheet": { "inputs": [{ "name": "Bon papier", "medium": "paper" }], "outputs": [{ "name": "Courriel", "medium": "{{medium}}" }] } }
            ],
            "flows": [ { "source": "e0", "target": "e1" } ]
          }
        }
        """;

    /// <summary>A start and a step whose detail holds the steps given, with the class given (audit 3.1 V2, 4.5).</summary>
    private static string Detailed(string valueClass = "\"value_class\": \"waste\", ", string inner = "{ \"id\": \"d1\", \"lane\": \"s1\", \"label\": \"Saisir\" }") => $$"""
        {
          "format": "topogenia",
          "version": "1.4",
          "project": { "name": "Makigami", "code": "MAK" },
          "map": {
            "lanes": [{{TwoLanes}}],
            "steps": [
              { "id": "e0", "lane": "c1", "label": "Début", "type": "start" },
              { "id": "e1", "lane": "c1", "label": "Traiter", {{valueClass}}"sub_map": { "lanes": [{ "id": "s1", "name": "Interne" }], "steps": [{{inner}}] } }
            ],
            "flows": [ { "source": "e0", "target": "e1" } ]
          }
        }
        """;

    [Fact]
    public void A_class_on_a_step_whose_detail_has_steps_is_refused_as_the_api_refuses_it_and_an_empty_detail_keeps_it()
    {
        Assert.Contains(Faults(Detailed()), fault => fault.Path == "map.steps[1].value_class");
        Assert.Equal("waste", Read(Detailed(inner: "")).Map.Steps.Single(step => step.Id == "e1").ValueClass);
        Assert.Null(Read(Detailed(valueClass: "")).Map.Steps.Single(step => step.Id == "e1").ValueClass);
    }

    [Fact]
    public void The_class_asleep_on_a_step_whose_detail_has_steps_is_never_written_and_the_copy_opens()
    {
        var (_, workspace) = Build(Read(Detailed(valueClass: "")));
        var detailed = workspace.All<Step>().Single(step => step.Label == "Traiter");
        workspace.Put(detailed with { ValueClass = ValueClass.Waste });
        var copy = PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(true)).Document, "Test", At);
        Assert.DoesNotContain("value_class", copy, StringComparison.Ordinal);
        Assert.Null(Read(copy).Map.Steps.Single(step => step.Id == "MAK-E002").ValueClass);
    }

    [Fact]
    public void A_class_and_the_media_of_a_file_1_4_are_read_and_built()
    {
        var document = Read(Classified());
        var step = document.Map.Steps.Single(candidate => candidate.Id == "e1");
        Assert.Equal("waste", step.ValueClass);
        Assert.Equal(("paper", "email"), (step.Sheet!.Inputs[0].Medium, step.Sheet.Outputs[0].Medium));
        var (_, workspace) = Build(document);
        Assert.Equal(ValueClass.Waste, workspace.All<Step>().Single(candidate => candidate.Label == "Ressaisir").ValueClass);
        Assert.Equal(
            [DocumentMedium.Paper, DocumentMedium.Email],
            workspace.All<Document>().OrderBy(candidate => candidate.Name, StringComparer.Ordinal).Select(candidate => candidate.Medium));
    }

    [Fact]
    public void A_copy_carries_them_in_1_4_and_a_map_without_them_stays_older()
    {
        var (_, workspace) = Build(Read(Classified()));
        var copy = PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(true)).Document, "Test", At);
        Assert.Contains("\"version\": \"1.4\"", copy, StringComparison.Ordinal);
        var (_, again) = Build(Read(copy));
        Assert.Equal(ValueClass.Waste, again.All<Step>().Single(candidate => candidate.Label == "Ressaisir").ValueClass);
        Assert.Equal(2, again.All<Document>().Count(candidate => candidate.Medium is not null));
        foreach (var step in workspace.All<Step>().ToList())
            workspace.Put(step with { ValueClass = null });
        foreach (var document in workspace.All<Document>().ToList())
            workspace.Put(document with { Medium = null });
        var plain = PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(true)).Document, "Test", At);
        Assert.DoesNotContain("\"version\": \"1.4\"", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("value_class", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("medium", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void They_are_unknown_in_a_file_1_3_and_refused_on_a_step_without_content_or_out_of_their_lists()
    {
        Assert.Contains(Faults(Classified("1.3")), fault => fault.Path.EndsWith("value_class", StringComparison.Ordinal));
        Assert.Contains(Faults(Classified(type: "parallel")), fault => fault.Path.EndsWith("value_class", StringComparison.Ordinal));
        Assert.Contains(Faults(Classified(valueClass: "gaspillage")), fault => fault.Path.Contains("value_class", StringComparison.Ordinal));
        Assert.Contains(Faults(Classified(medium: "fax")), fault => fault.Path.Contains("medium", StringComparison.Ordinal));
        Assert.Equal("necessary", Read(Classified(type: "decision", valueClass: "necessary")).Map.Steps.Single(step => step.Id == "e1").ValueClass);
    }

    /// <summary>F-103 (choice of the client of 07/11/2026): the class a proposal of the assistant gives goes in its state wanted; a step without class wants none.</summary>
    [Fact]
    public void The_class_of_a_proposal_is_in_its_state_wanted_and_a_step_without_class_wants_none()
    {
        var format = new PmapFiles();
        var desired = format.Desired(format.Read(Classified(valueClass: "value_added"), Languages, "ACI"));
        Assert.Equal(ValueClass.ValueAdded, desired.Steps.Single(step => step.Id == "e1").ValueClass);
        Assert.Null(desired.Steps.Single(step => step.Id == "e0").ValueClass);
    }
}
