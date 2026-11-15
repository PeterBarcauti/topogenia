// PmapSummaryTests.cs
// The summary of a map in a .pmap 1.4 (ADR 094): read with its translations, built as written by hand and describing the map opened, copied only while it describes its map, the copy in 1.4 then and in 1.3 or 1.2 without it; unknown in 1.3, refused on two lines; in a proposal, only its translations.

using Topogenia.Domain.Model;
using Topogenia.Domain.Summaries;
using Topogenia.Domain.Translations;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapSummaryTests
{
    private static readonly DateTime At = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    private const string Sentence = "L'acheteur reçoit la demande puis la traite.";

    /// <summary>A file of two steps whose map carries a summary, with its English translation.</summary>
    private static string Summarised(string version = "1.4", string text = Sentence) => $$"""
        {
          "format": "topogenia",
          "version": "{{version}}",
          "project": { "name": "Résumés", "code": "RES" },
          "map": {
            "lanes": [{{TwoLanes}}],
            "steps": [ { "id": "e1", "lane": "c1", "label": "Recevoir" }, { "id": "e2", "lane": "c1", "label": "Traiter" } ],
            "flows": [ { "source": "e1", "target": "e2" } ],
            "summary": { "text": "{{text}}", "translations": { "en": "The buyer receives the request then handles it." } }
          }
        }
        """;

    [Fact]
    public void A_summary_of_a_file_1_4_is_read_then_built_as_written_by_hand_describing_the_map_opened_with_its_translations()
    {
        var document = Read(Summarised());
        Assert.Equal(Sentence, document.Map.Summary!.Text);
        var (_, workspace) = Build(document);
        var summary = workspace.SummaryOf(workspace.RootMap.Id)!;
        Assert.Equal((Sentence, SummaryOrigin.Manual, (string?)null), (summary.Text, summary.Origin, summary.Model));
        Assert.Equal(MapFingerprint.Of(workspace, workspace.RootMap.Id), summary.Fingerprint);
        Assert.Equal("The buyer receives the request then handles it.", TranslationRules.Of(workspace, TranslationObject.Summary, workspace.RootMap.Id)["en"].Text);
    }

    [Fact]
    public void A_copy_carries_the_summary_in_1_4_while_it_describes_the_map_and_opens_again_with_it_without_it_the_copy_stays_older()
    {
        var (_, workspace) = Build(Read(Summarised()));
        var copy = PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(false)).Document, "Test", At);
        Assert.Contains("\"version\": \"1.4\"", copy, StringComparison.Ordinal);
        var (_, again) = Build(Read(copy));
        Assert.Equal(Sentence, again.SummaryOf(again.RootMap.Id)!.Text);
        // The map changed since: the summary tells another map and stays out of the copy, which goes back to 1.2.
        var step = workspace.All<Step>().First(candidate => candidate.Label == "Traiter");
        workspace.Put(step with { Label = "Classer" });
        var stale = PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(false)).Document, "Test", At);
        Assert.Contains("\"version\": \"1.2\"", stale, StringComparison.Ordinal);
        Assert.DoesNotContain("summary", stale, StringComparison.Ordinal);
    }

    [Fact]
    public void A_summary_is_unknown_in_a_file_1_3_and_refused_on_two_lines_or_beyond_its_bound()
    {
        Assert.Contains(Faults(Summarised("1.3")), fault => fault.Path == "map.summary");
        Assert.Contains(Faults(Summarised(text: "Une ligne,\\nune autre.")), fault => fault.Path == "map.summary.text");
        Assert.Contains(Faults(Summarised(text: new string('a', 201))), fault => fault.Path == "map.summary.text");
    }
}
