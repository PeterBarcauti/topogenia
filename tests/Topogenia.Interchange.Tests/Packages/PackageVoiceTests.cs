// PackageVoiceTests.cs
// The voices a project keeps in its package (ADR 103): the format 2.4 as soon as the project carries one, written on the project by language, with or without the people, read back on the new project by the importer, refused by the schema when malformed.

using System.Text.Json.Nodes;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageVoiceTests
{
    /// <summary>The name of the format 2.4 (docs/format-topogenia.md): the voices in the object project.</summary>
    private const string Voices = "presentationVoices";

    private const string Hortense = "Hortense - voix du poste";

    private static readonly Guid Importer = Guid.Parse("00000000-0000-0000-0000-0000000000f6");

    private static long next;

    private static OperationContext Context() =>
        new(Importer, SampleProject.At(1000), "fr", AppSettings.Default, () => new Guid($"52000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    private static PresentationVoice Voice(SampleProject sample, string language, string name, bool local) =>
        new(sample.Project, language, name, local, 4, SampleProject.At(400), sample.Alice);

    private static byte[] Written(SampleProject sample, bool withoutPeople, params PresentationVoice[] voices) =>
        PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(withoutPeople) { Voices = voices },
            new PackageStamp(SampleProject.At(500), withoutPeople ? null : "EXEMPLE\\alice"));

    private static (string Format, string Schema) VersionsOf(byte[] package)
    {
        var manifest = new PackageFiles(package).Json(PackageFormat.Manifest);
        return (manifest["formatVersion"]!.GetValue<string>(), manifest["schemaVersion"]!.GetValue<string>());
    }

    private static JsonObject ProjectOf(byte[] package) => new PackageFiles(package).Json(PackageFormat.Project)["project"]!.AsObject();

    private static DomainException Refused(byte[] package) => Assert.Throws<DomainException>(() => new PackageImporter().Read(package));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_voices_of_a_project_go_on_its_project_by_language_with_or_without_the_people_and_the_package_takes_the_format_2_4(bool withoutPeople)
    {
        var sample = new SampleProject();
        var package = Written(sample, withoutPeople, Voice(sample, "fr", Hortense, true), Voice(sample, "en", "Julie", false));
        Assert.Equal(("2.4", "2.4"), VersionsOf(package));
        var voices = ProjectOf(package)[Voices]!.AsArray();
        Assert.Equal([("en", "Julie", false), ("fr", Hortense, true)],
            voices.Select(voice => (voice!["language"]!.GetValue<string>(), voice["name"]!.GetValue<string>(), voice["local"]!.GetValue<bool>())));
        // A voice carries neither its version nor its author: the importer writes them.
        Assert.All(voices, voice => Assert.Equal(["language", "local", "name"], voice!.AsObject().Select(pair => pair.Key).Order(StringComparer.Ordinal)));
        // Without a voice, the same project stays in the format of its content, and its project names none.
        var without = Written(new SampleProject(), withoutPeople);
        Assert.Equal(("2.3", "2.3"), VersionsOf(without));
        Assert.False(ProjectOf(without).ContainsKey(Voices));
    }

    /// <summary>A project whose content the version 3.0 reads takes the format 2.4 with its first voice, and keeps its own without one.</summary>
    [Fact]
    public void A_project_of_the_oldest_format_takes_the_format_2_4_with_a_voice_alone()
    {
        var sample = new SampleProject().Before23();
        Assert.Equal(PackageFormat.VersionWithoutRoutes, VersionsOf(Written(sample, withoutPeople: true)).Format);
        Assert.Equal(("2.4", "2.4"), VersionsOf(Written(sample, withoutPeople: true, Voice(sample, "fr", Hortense, true))));
    }

    [Fact]
    public void The_voice_of_another_project_is_left_out()
    {
        var sample = new SampleProject();
        var elsewhere = Voice(sample, "fr", Hortense, true) with { ProjectId = Guid.Parse("00000000-0000-0000-0000-00000000dead") };
        var package = Written(sample, withoutPeople: false, elsewhere);
        Assert.Equal(("2.3", "2.3"), VersionsOf(package));
        Assert.False(ProjectOf(package).ContainsKey(Voices));
    }

    [Fact]
    public void Two_exports_of_the_same_voices_give_the_same_project_whatever_their_order()
    {
        var sample = new SampleProject();
        var (french, english) = (Voice(sample, "fr", Hortense, true), Voice(sample, "en", "Julie", false));
        Assert.Equal(new PackageFiles(Written(sample, false, french, english)).Bytes[PackageFormat.Project],
            new PackageFiles(Written(sample, false, english, french)).Bytes[PackageFormat.Project]);
    }

    [Fact]
    public void The_voices_are_read_back_on_the_new_project_written_by_the_importer_at_its_time_in_version_1()
    {
        var sample = new SampleProject();
        var package = Written(sample, withoutPeople: false, Voice(sample, "fr", Hortense, true), Voice(sample, "en", "Julie", false));
        var importer = new PackageImporter();

        var rebuilt = importer.Rebuild(importer.Read(package), new ImportPlan(Context(), "VIS", _ => null, false));

        var project = Assert.IsType<Project>(rebuilt.Records[0]);
        Assert.NotEqual(sample.Project, project.Id);
        Assert.Equal([new PresentationVoice(project.Id, "en", "Julie", false, 1, SampleProject.At(1000), Importer),
            new PresentationVoice(project.Id, "fr", Hortense, true, 1, SampleProject.At(1000), Importer)], rebuilt.Voices);
        // The package written again from what was read carries the same voices.
        var again = Written(sample, false, [.. rebuilt.Voices.Select(voice => voice with { ProjectId = sample.Project })]);
        Assert.Equal(ProjectOf(package)[Voices]!.ToJsonString(), ProjectOf(again)[Voices]!.ToJsonString());
    }

    [Fact]
    public void A_package_without_voices_is_rebuilt_with_none()
    {
        var importer = new PackageImporter();
        var rebuilt = importer.Rebuild(importer.Read(Written(new SampleProject(), withoutPeople: false)), new ImportPlan(Context(), "VIS", _ => null, false));
        Assert.Empty(rebuilt.Voices);
    }

    /// <summary>The schema cannot say that a language comes once: both lines are read, and the application refuses the package (reason <c>values</c>).</summary>
    [Fact]
    public void Two_voices_for_one_language_pass_the_schema_and_are_both_handed_to_the_application()
    {
        var forge = new PackageForge().With(PackageFormat.Project, node => node["project"]![Voices] = JsonNode.Parse(
            """[{"language":"fr","name":"Hortense","local":true},{"language":"fr","name":"Julie","local":false}]"""));
        var importer = new PackageImporter();
        var rebuilt = importer.Rebuild(importer.Read(forge.Bytes()), new ImportPlan(Context(), "VIS", _ => null, false));
        Assert.Equal([("fr", "Hortense"), ("fr", "Julie")], rebuilt.Voices.Select(voice => (voice.Language, voice.Name)));
    }

    public static TheoryData<string> Malformed() => new()
    {
        "[]",
        """{"language":"fr","name":"Hortense","local":true}""",
        """[{"language":"fr","name":"Hortense"}]""",
        """[{"language":"fr","local":true}]""",
        """[{"name":"Hortense","local":true}]""",
        """[{"language":"fr","name":"Hortense","local":true,"version":2}]""",
        """[{"language":"fr","name":"Hortense","local":"true"}]""",
        """[{"language":"fr","name":"Hortense","local":1}]""",
        """[{"language":"FR","name":"Hortense","local":true}]""",
        """[{"language":"fr","name":" Hortense ","local":true}]""",
        """[{"language":"fr","name":"","local":true}]""",
        """[{"language":"fr","name":"<b>Hortense</b>","local":true}]""",
        """[{"language":"fr","name":"Hortense\nJulie","local":true}]""",
        """[{"language":"fr","name":"Hortense\tJulie","local":true}]""",
        "[{\"language\":\"fr\",\"name\":\"" + new string('a', 201) + "\",\"local\":true}]",
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void Malformed_voices_are_refused_by_the_schema_of_the_project(string voices)
    {
        var forge = new PackageForge().With(PackageFormat.Project, node => node["project"]![Voices] = JsonNode.Parse(voices));
        var refusal = Refused(forge.Bytes());
        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"schema", (object?)PackageFormat.Project), (refusal.Code, refusal.Details["reason"], refusal.Details["entry"]));
    }

    /// <summary>A name of 200 characters, the longest the rule keeps, is read.</summary>
    [Fact]
    public void A_voice_named_by_200_characters_is_read()
    {
        var forge = new PackageForge().With(PackageFormat.Project, node => node["project"]![Voices] =
            JsonNode.Parse("[{\"language\":\"fr\",\"name\":\"" + new string('a', 200) + "\",\"local\":true}]"));
        var importer = new PackageImporter();
        Assert.Equal(200, Assert.Single(importer.Rebuild(importer.Read(forge.Bytes()), new ImportPlan(Context(), "VIS", _ => null, false)).Voices).Name.Length);
    }

    [Fact]
    public void A_package_that_declares_the_format_2_3_carries_no_voice()
    {
        const string One = """[{"language":"fr","name":"Hortense","local":true}]""";
        var forge = new PackageForge()
            .With(PackageFormat.Project, node => node["project"]![Voices] = JsonNode.Parse(One))
            .Manifest(manifest =>
            {
                manifest["formatVersion"] = PackageFormat.VersionWithoutComments;
                manifest["schemaVersion"] = PackageFormat.VersionWithoutComments;
            });
        var refusal = Refused(forge.Bytes());
        Assert.Equal(((object?)"schema", (object?)PackageFormat.Project), (refusal.Details["reason"], refusal.Details["entry"]));
        // The same value in a package of the format 2.4 is read.
        var read = new PackageForge().With(PackageFormat.Project, node => node["project"]![Voices] = JsonNode.Parse(One));
        Assert.Equal("CONGES", new PackageImporter().Read(read.Bytes()).ProjectCode);
    }

    /// <summary>The description of the project given to the model of the assistant never carries a voice: the writer is given none.</summary>
    [Fact]
    public void The_description_of_a_project_for_the_assistant_names_no_voice()
    {
        var described = new PackageExporter().Describe(new SampleProject().Workspace());
        Assert.DoesNotContain(described, entry => entry.Content.Contains(Voices, StringComparison.Ordinal));
    }
}
