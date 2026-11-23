// PmapPainPointStatusTests.cs
// The status of the pain points at the opening of a .pmap (ADR 116 point 10): a whole copy and a file inserted as the detail of a step keep each of the seven, open or dealt with, their history starting at the copy; a proposal keeps none.

using Topogenia.Application.Ports;
using Topogenia.Domain.Documents;
using Topogenia.Domain.Maps;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapBuildTests;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapPainPointStatusTests
{
    private static readonly DateTime At = new(2026, 11, 21, 8, 0, 0, DateTimeKind.Utc);

    private static readonly PainPointStatus[] Seven = Enum.GetValues<PainPointStatus>();

    /// <summary>A file 1.5 of two steps: the first produces seven documents, the second uses each with a pain point of one of the seven statuses, its hours its rank.</summary>
    private static string SevenStatuses()
    {
        var outputs = string.Join(", ", Seven.Select((_, index) => $$"""{ "id": "d{{index}}", "name": "Document {{index}}" }"""));
        var uses = string.Join(",\n", Seven.Select((status, index) =>
            $$"""{ "document": "d{{index}}", "pain_point": "Irritant {{Codes.Of(status)}}", "lost_hours_per_month": {{index + 1}}, "pain_point_status": "{{Codes.Of(status)}}" }"""));
        return $$"""
            {
              "format": "topogenia",
              "version": "1.5",
              "project": { "name": "États", "code": "ETA" },
              "map": {
                "lanes": [{{TwoLanes}}],
                "steps": [
                  { "id": "e1", "lane": "c1", "label": "Recevoir", "sheet": { "outputs": [{{outputs}}] } },
                  { "id": "e2", "lane": "c2", "label": "Traiter", "sheet": { "uses": [{{uses}}] } }
                ],
                "flows": [ { "source": "e1", "target": "e2" } ]
              }
            }
            """;
    }

    private static Dictionary<string, PainPointStatus> StatusesOf(ProjectWorkspace workspace) =>
        workspace.All<PainPoint>().ToDictionary(point => point.Description, point => point.Status, StringComparer.Ordinal);

    private static decimal OpenHours(ProjectWorkspace workspace) =>
        workspace.All<PainPoint>().Where(MapMarkers.IsOpen).Sum(point => point.LostHoursPerMonth ?? 0m);

    private static List<(PainPointStatus? Before, PainPointStatus After)> HistoryOf(ProjectWorkspace workspace, PainPoint point) =>
        [.. workspace.All<PainPointStatusChange>().Where(change => change.PainPointId == point.Id)
            .OrderBy(change => change.StatusBefore is not null).Select(change => (change.StatusBefore, change.StatusAfter))];

    private static string WholeCopy(ProjectWorkspace workspace) =>
        PmapWriter.Write(PmapOutline.Of(workspace, new PmapOutlineOptions(true, _ => new SheetViewer(false, true)) { Extras = new PmapExtras([], []) }).Document,
            "Test", At);

    [Fact]
    public void A_whole_copy_keeps_the_status_of_its_pain_points_open_or_dealt_with()
    {
        var (_, source) = Build(Read(SevenStatuses()));
        Assert.Equal(Seven.ToDictionary(status => $"Irritant {Codes.Of(status)}", status => status), StatusesOf(source));
        // Two statuses changed in the application after the opening: one dealt with, one reopened.
        var handle = source.All<Step>().Single(step => step.Label == "Traiter");
        PainPoint Named(ProjectWorkspace workspace, PainPointStatus status) =>
            workspace.All<PainPoint>().Single(point => point.Description == $"Irritant {Codes.Of(status)}");
        PainPointRules.ChangeStatus(source, Context(), handle.Id, Named(source, PainPointStatus.Reported).Id, PainPointStatus.Implemented);
        PainPointRules.ChangeStatus(source, Context(), handle.Id, Named(source, PainPointStatus.Dropped).Id, PainPointStatus.InProgress);

        var (_, copy) = Build(Read(WholeCopy(source)), "COP");

        Assert.Equal(StatusesOf(source), StatusesOf(copy));
        Assert.Equal(OpenHours(source), OpenHours(copy));
        Assert.Equal(2m + 3m + 4m + 7m, OpenHours(copy));
        // The history of the copy starts at the copy: raised reported, then the status of the file when it is another.
        Assert.All(copy.All<PainPoint>(), point =>
        {
            List<(PainPointStatus?, PainPointStatus)> expected = point.Status == PainPointStatus.Reported
                ? [(null, PainPointStatus.Reported)]
                : [(null, PainPointStatus.Reported), (PainPointStatus.Reported, point.Status)];
            Assert.Equal(expected, HistoryOf(copy, point));
        });
        Assert.Equal(3, HistoryOf(source, Named(source, PainPointStatus.Dropped)).Count);
    }

    [Fact]
    public void A_file_inserted_as_the_detail_of_a_step_keeps_the_status_of_its_pain_points_open_or_dealt_with()
    {
        // Review m6: the insertion of a file (3.3, F-208) builds its sheets by the way of the whole copy, so each pain point
        // keeps its status in the map that receives it; its history starts at the insertion (ADR 116 point 10).
        var (_, target) = Build(Read($$"""
            {
              "format": "topogenia", "version": "1.5", "project": { "name": "Cible", "code": "CIB" },
              "map": { "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Accueillir" }] }
            }
            """));
        var host = target.All<Step>().Single(step => step.Label == "Accueillir");
        var format = new PmapFiles();
        var file = format.ReadInsertion(SevenStatuses(), Languages, "ACI");

        format.Detail(Context(), target, file.Document, host.Id, Languages, withSheets: true);

        Assert.Equal(Seven.ToDictionary(status => $"Irritant {Codes.Of(status)}", status => status), StatusesOf(target));
        Assert.Equal(1m + 2m + 3m + 4m, OpenHours(target));
        Assert.All(target.All<PainPoint>(), point =>
        {
            List<(PainPointStatus?, PainPointStatus)> expected = point.Status == PainPointStatus.Reported
                ? [(null, PainPointStatus.Reported)]
                : [(null, PainPointStatus.Reported), (PainPointStatus.Reported, point.Status)];
            Assert.Equal(expected, HistoryOf(target, point));
        });
    }

    [Fact]
    public void A_proposal_still_keeps_no_status()
    {
        var format = new PmapFiles();

        var opened = format.Build(Context(), format.ReadProposal(SevenStatuses(), Languages, "ACI"), "NEW", "Nouveau", Inputs());

        var records = opened.Changes.Changes.Select(change => change.After).ToList();
        var points = records.OfType<PainPoint>().ToList();
        Assert.Equal(Seven.Length, points.Count);
        Assert.All(points, point => Assert.Equal(PainPointStatus.Reported, point.Status));
        Assert.All(records.OfType<PainPointStatusChange>(), change => Assert.Equal((null, PainPointStatus.Reported), (change.StatusBefore, change.StatusAfter)));
        Assert.Equal(Seven.Length, records.OfType<PainPointStatusChange>().Count());
    }
}
