// PmapPrintCopyTests.cs
// The copy the project as PDF reads (3.4, lot 5, ADR 113): the branch of a map with its sheets as the viewer sees them, without the place of any document nor the comments and the glossary of a whole copy, the code and the status of each pain point kept, cut at 12 levels as the branch.

using Topogenia.Application.Ports;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.SubMaps;
using Topogenia.Domain.Values;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapPrintCopyTests
{
    private static readonly DateTime At = new(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>A file 1.5 drawn from the random: maps down to <paramref name="depthMax"/> levels, comments, places, pain points, a glossary.</summary>
    private sealed class Drawing(Random random, int depthMax)
    {
        private int next;

        private string Id(string prefix) => $"{prefix}{++next}";

        public string File() => $$"""
            { "format": "topogenia", "version": "1.5",
              "project": { "name": "Tirage", "code": "TIR", "glossary": "demande = request" },
              "map": {{Map(1)}} }
            """;

        private string Map(int depth)
        {
            var lanes = Enumerable.Range(0, random.Next(1, 4)).Select(_ => Id("c")).ToList();
            var steps = Enumerable.Range(0, random.Next(1, 5)).Select(_ => Id("e")).ToList();
            var outputs = steps.ToDictionary(step => step, _ => Enumerable.Range(0, random.Next(3)).Select(_ => Id("d")).ToList());
            var written = steps.Select((step, column) => Step(step, lanes[random.Next(lanes.Count)], column, outputs, depth));
            var flows = steps.Zip(steps.Skip(1)).Where(_ => random.Next(2) == 0)
                .Select(pair => $$"""{ "source": "{{pair.First}}", "target": "{{pair.Second}}"{{Comment("Le flux passe.")}} }""");
            return $$"""
                { "lanes": [{{string.Join(", ", lanes.Select(lane => $$"""{ "id": "{{lane}}", "name": "Couloir {{lane}}" }"""))}}],
                  "steps": [{{string.Join(",\n", written)}}],
                  "flows": [{{string.Join(", ", flows)}}] }
                """;
        }

        private string Step(string id, string lane, int column, Dictionary<string, List<string>> outputs, int depth)
        {
            var subMap = depth < depthMax && random.Next(3) == 0 ? $", \"sub_map\": {Map(depth + 1)}" : "";
            return $$"""
                { "id": "{{id}}", "lane": "{{lane}}", "label": "Étape {{id}}", "column": {{column}}{{Comment("On fait ceci.")}}{{subMap}},
                  "sheet": { "purpose": "But {{id}}", "outputs": [{{string.Join(", ", outputs[id].Select(Output))}}],
                    "inputs": [{{(random.Next(2) == 0 ? $$"""{ "name": "Entrée {{id}}"{{Place()}} }""" : "")}}],
                    "uses": [{{string.Join(", ", Uses(id, outputs))}}] } }
                """;
        }

        private string Output(string id) => $$"""{ "id": "{{id}}", "name": "Sortie {{id}}"{{Place()}} }""";

        /// <summary>One output of each of some other steps of the map, now and then with a pain point, dealt with or not.</summary>
        private IEnumerable<string> Uses(string step, Dictionary<string, List<string>> outputs) =>
            outputs.Where(pair => pair.Key != step && pair.Value.Count > 0 && random.Next(2) == 0).Select(pair => random.Next(2) == 0
                ? $$"""{ "document": "{{pair.Value[0]}}", "pain_point": "Irritant {{pair.Value[0]}}", "lost_hours_per_month": 2, "pain_point_status": "{{(random.Next(2) == 0 ? "reported" : "dropped")}}" }"""
                : $$"""{ "document": "{{pair.Value[0]}}" }""");

        private string Place() => random.Next(2) == 0 ? $$""", "location": "C:\\Partage\\Lieu{{++next}}" """ : "";

        private string Comment(string text) => random.Next(2) == 0 ? $$""", "comment": { "text": "{{text}}", "translations": { "en": "So." } }""" : "";
    }

    /// <summary>The document without what the print leaves out: the places of the documents, the comments, the glossary.</summary>
    private static PmapDocument Printed(PmapDocument document) =>
        document with { Project = document.Project with { Glossary = null }, Map = Printed(document.Map) };

    private static PmapMap Printed(PmapMap map) => map with
    {
        Steps = [.. map.Steps.Select(step => step with
        {
            Comment = null,
            SubMap = step.SubMap is { } sub ? Printed(sub) : null,
            Sheet = step.Sheet is { } sheet ? sheet with
            {
                Outputs = [.. sheet.Outputs.Select(output => output with { Location = null })],
                Inputs = [.. sheet.Inputs.Select(input => input with { Location = null })],
            } : null,
        })],
        Flows = [.. map.Flows.Select(flow => flow with { Comment = null })],
    };

    private static List<PmapMap> MapsOf(PmapMap map) => [map, .. map.Steps.Where(step => step.SubMap is not null).SelectMany(step => MapsOf(step.SubMap!))];

    /// <summary>
    /// Forty projects drawn from a fixed seed, each copied from a map drawn among its maps, for a viewer that sees the
    /// drafts of half of the steps: the print is, text for text, the branch of the same map without the places, the
    /// comments and the glossary; its sub-maps, its sheets and the code and the status of its pain points are those of
    /// the branch.
    /// </summary>
    [Fact]
    public void OutlinePrint_is_OutlineBranch_without_locations_and_without_extras()
    {
        var random = new Random(113);
        var format = new PmapFiles();
        var (places, comments, subMaps, codes) = (0, 0, 0, 0);
        for (var round = 0; round < 40; round++)
        {
            var (built, workspace) = Build(Read(new Drawing(random, 4).File()));
            var maps = workspace.All<ProcessMap>().ToList();
            var mapId = maps[random.Next(maps.Count)].Id;
            SheetViewer Viewer(Step step) => new(false, step.Column % 2 == 0);

            var branch = format.Write(format.OutlineBranch(workspace, mapId, Viewer, new PmapExtras(built.Comments, built.FlowComments)).Document, "Test", At);
            var print = format.Write(format.OutlinePrint(workspace, mapId, Viewer).Document, "Test", At);

            Assert.Equal(PmapWriter.Write(Printed(Read(branch)), "Test", At), PmapWriter.Write(Read(print), "Test", At));
            Assert.All(new[] { "\"location\"", "Partage", "\"comment\"", "\"glossary\"" }, text => Assert.DoesNotContain(text, print, StringComparison.Ordinal));
            places += Count(branch, "\"location\"");
            comments += Count(branch, "\"comment\"");
            subMaps += Count(print, "\"sub_map\"");
            codes += Count(print, "\"pain_point_code\"");
        }
        // The equivalence is not empty: the branches carried places, comments, sub-maps and pain points, the print kept the last two.
        Assert.All(new[] { places, comments, subMaps, codes }, count => Assert.InRange(count, 20, int.MaxValue));
    }

    private static int Count(string text, string word)
    {
        var (count, at) = (0, 0);
        while ((at = text.IndexOf(word, at, StringComparison.Ordinal)) >= 0)
            (count, at) = (count + 1, at + word.Length);
        return count;
    }

    [Fact]
    public void OutlinePrint_gives_each_sheet_as_the_viewer_sees_it()
    {
        var (_, workspace) = Build(Read(new Drawing(new Random(1130), 1).File()));
        var format = new PmapFiles();
        var root = workspace.RootMap.Id;

        var drafts = Read(format.Write(format.OutlinePrint(workspace, root, _ => new SheetViewer(false, true)).Document, "Test", At));
        var published = Read(format.Write(format.OutlinePrint(workspace, root, _ => new SheetViewer(false, false)).Document, "Test", At));

        // Opened from a file, every sheet is a draft never validated: the one who sees no draft reads none of its content.
        Assert.All(drafts.Map.Steps, step => Assert.Equal($"But {step.Label["Étape ".Length..]}", step.Sheet!.Purpose));
        Assert.All(published.Map.Steps, step => Assert.Null(step.Sheet?.Purpose));
    }

    /// <summary>A map of 11 levels below the root detailed once more beyond the file: the print stops at 12 levels and says so, as the branch.</summary>
    [Fact]
    public void OutlinePrint_stops_at_twelve_levels_as_the_branch_and_reports_the_cut()
    {
        static string Nested(int levels, int level = 1) => level > levels
            ? $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "Fin" }] }"""
            : $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "N", "sub_map": {{Nested(levels, level + 1)}} }] }""";
        var (_, workspace) = Build(Read(Doc(Nested(11))));
        var format = new PmapFiles();
        SheetViewer Viewer(Step step) => new(false, true);
        Assert.False(format.OutlinePrint(workspace, workspace.RootMap.Id, Viewer).DepthCut);
        var deepest = workspace.All<Step>().Single(step => step.Label == "Fin");
        var detail = SubMapRules.Detail(workspace, Context(), deepest.Id);
        workspace.Put(new Step(Guid.NewGuid(), detail.MapId, detail.FirstLaneId!.Value, "PRJ-E099", "Treizième", "fr", StepType.Task, 0, 0, null,
            AutomationStatus.Manual, null, At, Creator, null, null, null, null, 1));

        var print = format.OutlinePrint(workspace, workspace.RootMap.Id, Viewer);
        var branch = format.OutlineBranch(workspace, workspace.RootMap.Id, Viewer, null);

        Assert.Equal((true, true), (print.DepthCut, branch.DepthCut));
        var text = format.Write(print.Document, "Test", At);
        Assert.Equal(12, MapsOf(Read(text).Map).Count);
        Assert.DoesNotContain("Treizième", text, StringComparison.Ordinal);
        Assert.Equal(format.Write(branch.Document, "Test", At), text);
    }
}
