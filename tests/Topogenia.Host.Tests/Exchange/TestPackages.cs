// TestPackages.cs
// Packages for the tests of the import routes: the smallest valid project, with or without voices kept for its presentation, or with two steps written in two languages, written by the writer of the export.

using Topogenia.Application.Presentation;
using Topogenia.Domain.Model;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;

namespace Topogenia.Host.Tests.Exchange;

/// <summary>The smallest package a project can give: a root map with its first lane, without the people.</summary>
public static class TestPackages
{
    private static readonly DateTime At = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>The records of the smallest project, with what names them: its project, its root map, its first lane, their author.</summary>
    private sealed record Smallest(Guid Project, Guid Map, Guid Lane, Guid Author, List<IRecord> Records)
    {
        public static Smallest Of(string code)
        {
            var (project, map, lane, author) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
            return new Smallest(project, map, lane, author,
            [
                new Project(project, code, "Projet importé", "fr", null, null, Classification.Internal, null, null, 0, At, author, null, null, null, null, 1),
                new ProcessMap(map, project, null, "Projet importé", "fr", At, author, null, null, null, null, 1),
                new Lane(lane, map, code + "-C01", "Acteur 1", "fr", LaneType.Person, null, LaneColors.ForIndex(0), 0, null, At, author, null, null, null, null, 1),
                new ProjectCounter(project, CounterKind.Lane, 1),
            ]);
        }

        public byte[] Written(PackageOptions options) =>
            PackageWriter.Write(new ProjectWorkspace(Project, Records), new Dictionary<Guid, string>(), options, new PackageStamp(At, null));
    }

    public static byte[] Minimal(string code) => WithVoices(code);

    /// <summary>The smallest package, whose project keeps these voices for its presentation (format 2.4, ADR 103): language, name, voice of the computer or not.</summary>
    public static byte[] WithVoices(string code, params (string Language, string Name, bool Local)[] voices)
    {
        var smallest = Smallest.Of(code);
        return smallest.Written(new PackageOptions(WithoutPeople: true)
        {
            Voices = [.. voices.Select(voice => new PresentationVoice(smallest.Project, voice.Language, voice.Name, voice.Local, 1, At, smallest.Author))],
        });
    }

    /// <summary>
    /// A package whose two steps are written in two languages, the first in French and the second in English, with the flow
    /// from the first to the second: no route writes a step in another language than the one of its project, a package may
    /// (a project written by several people before the 2.0, or brought from another installation).
    /// </summary>
    public static byte[] TwoLanguages(string code)
    {
        var smallest = Smallest.Of(code);
        Step Step(int number, string label, string language) => new(Guid.CreateVersion7(), smallest.Map, smallest.Lane, $"{code}-E{number:D3}", label, language,
            StepType.Task, number - 1, 0, null, AutomationStatus.Manual, null, At, smallest.Author, null, null, null, null, 1);
        var (french, english) = (Step(1, "Commander", "fr"), Step(2, "Deliver", "en"));
        smallest.Records.AddRange([french, english, new ProjectCounter(smallest.Project, CounterKind.Step, 2),
            new Flow(Guid.CreateVersion7(), smallest.Map, french.Id, english.Id, null, At, smallest.Author, null, null, null, null, 1)]);
        return smallest.Written(new PackageOptions(WithoutPeople: true));
    }
}
