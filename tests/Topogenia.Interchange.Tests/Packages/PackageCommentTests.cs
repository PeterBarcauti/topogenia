// PackageCommentTests.cs
// The comments of presentation in a package (ADR 099 revised): the format 2.4 as soon as a step carries one, its original on the step and its translations beside it with their source, read back on the new steps by the importer, refused by the schema when malformed.

using System.Text.Json.Nodes;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageCommentTests
{
    private static readonly Guid Importer = Guid.Parse("00000000-0000-0000-0000-0000000000f4");

    private static long next;

    private static OperationContext Context() =>
        new(Importer, SampleProject.At(1000), "fr", AppSettings.Default, () => new Guid($"41000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    private static Step StepOf(SampleProject sample, string code) => sample.Records.OfType<Step>().Single(step => step.Code == code);

    private static PresentationComment Comment(Step step, string text, string language = "fr", CommentSource source = CommentSource.Manual) =>
        new(step.Id, text, language, source, 3, SampleProject.At(400), Guid.Parse("00000000-0000-0000-0000-0000000000aa"));

    private static byte[] Written(SampleProject sample, params PresentationComment[] comments) =>
        PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(false) { Comments = comments }, new PackageStamp(SampleProject.At(500), "EXEMPLE\\alice"));

    private static (string Format, string Schema) VersionsOf(byte[] package)
    {
        var manifest = new PackageFiles(package).Json(PackageFormat.Manifest);
        return (manifest["formatVersion"]!.GetValue<string>(), manifest["schemaVersion"]!.GetValue<string>());
    }

    private static IEnumerable<JsonNode> Steps(byte[] package) =>
        new PackageFiles(package).Json(PackageFormat.Maps)["maps"]!.AsArray().SelectMany(map => map!["steps"]!.AsArray()).Select(step => step!);

    private static DomainException Refused(byte[] package) => Assert.Throws<DomainException>(() => new PackageImporter().Read(package));

    [Fact]
    public void A_comment_of_a_live_step_goes_with_its_step_and_the_package_takes_the_format_2_4()
    {
        var sample = new SampleProject();
        var package = Written(sample, Comment(StepOf(sample, "CONGES-E002"), "Le salarié saisit sa demande dans l'outil."));
        Assert.Equal(("2.4", "2.4"), VersionsOf(package));
        var commented = Steps(package).Where(step => step["presentationComment"] is not null).ToList();
        var step = Assert.Single(commented);
        Assert.Equal("CONGES-E002", step["code"]!.GetValue<string>());
        var comment = step["presentationComment"]!.AsObject();
        Assert.Equal((2, "Le salarié saisit sa demande dans l'outil.", "fr"),
            (comment.Count, comment["text"]!.GetValue<string>(), comment["language"]!.GetValue<string>()));
        // Without a comment, the same project stays in the format of its content.
        Assert.Equal(("2.3", "2.3"), VersionsOf(Written(new SampleProject())));
    }

    [Fact]
    public void A_comment_of_a_step_the_package_does_not_carry_is_left_out()
    {
        var sample = new SampleProject();
        var unknown = new PresentationComment(Guid.Parse("00000000-0000-0000-0000-00000000dead"), "Étape absente.", "fr", CommentSource.Manual, 1,
            SampleProject.At(400), Importer);
        var package = Written(sample, unknown);
        Assert.Equal(("2.3", "2.3"), VersionsOf(package));
        Assert.DoesNotContain(Steps(package), step => step["presentationComment"] is not null);
    }

    [Fact]
    public void The_translations_of_a_comment_go_beside_its_original_by_language_each_with_its_source()
    {
        var sample = new SampleProject();
        var step = StepOf(sample, "CONGES-E002");
        var package = Written(sample, Comment(step, "The employee enters the request.", "en", CommentSource.Assistant), Comment(step, "Le salarié saisit sa demande."),
            Comment(step, "Der Mitarbeiter erfasst den Antrag.", "de", CommentSource.Workbook), Comment(step, "El empleado introduce su solicitud.", "es"));
        var node = Assert.Single(Steps(package), written => written["presentationComment"] is not null);
        Assert.Equal(("Le salarié saisit sa demande.", "fr"), (node["presentationComment"]!["text"]!.GetValue<string>(), node["presentationComment"]!["language"]!.GetValue<string>()));
        Assert.Equal([("de", "Der Mitarbeiter erfasst den Antrag.", "workbook"), ("en", "The employee enters the request.", "assistant"),
            ("es", "El empleado introduce su solicitud.", "manual")],
            node["presentationCommentTranslations"]!.AsArray().Select(line => (line!["language"]!.GetValue<string>(), line["text"]!.GetValue<string>(),
                line["source"]!.GetValue<string>())));
        Assert.All(node["presentationCommentTranslations"]!.AsArray(), line => Assert.Equal(3, line!.AsObject().Count));
    }

    [Fact]
    public void A_translation_without_an_original_is_carried_alone_and_the_package_takes_the_format_2_4()
    {
        var sample = new SampleProject();
        var package = Written(sample, Comment(StepOf(sample, "CONGES-E002"), "Only in English.", "en"));
        Assert.Equal(("2.4", "2.4"), VersionsOf(package));
        var node = Assert.Single(Steps(package), written => written["presentationCommentTranslations"] is not null);
        Assert.Null(node["presentationComment"]);
        Assert.Equal("Only in English.", Assert.Single(node["presentationCommentTranslations"]!.AsArray())!["text"]!.GetValue<string>());
        var importer = new PackageImporter();
        var line = Assert.Single(importer.Rebuild(importer.Read(package), new ImportPlan(Context(), "VIS", _ => null, false)).Comments);
        Assert.Equal(("Only in English.", "en", CommentSource.Manual), (line.Text, line.Language, line.Source));
    }

    [Fact]
    public void Two_exports_of_the_same_comments_give_the_same_maps_whatever_their_order()
    {
        var sample = new SampleProject();
        var first = Comment(StepOf(sample, "CONGES-E002"), "Première.");
        var second = Comment(StepOf(sample, "CONGES-E004"), "Second.", "en");
        Assert.Equal(new PackageFiles(Written(sample, first, second)).Bytes[PackageFormat.Maps], new PackageFiles(Written(sample, second, first)).Bytes[PackageFormat.Maps]);
    }

    [Fact]
    public void The_comments_are_read_back_on_the_new_steps_written_by_the_importer_at_its_time_in_version_1()
    {
        var sample = new SampleProject();
        var package = Written(sample, Comment(StepOf(sample, "CONGES-E002"), "Saisie de la demande."), Comment(StepOf(sample, "CONGES-E004"), "Validation.", "en"),
            Comment(StepOf(sample, "CONGES-E002"), "Entering the request.", "en", CommentSource.Assistant),
            Comment(StepOf(sample, "CONGES-E002"), "Erfassung des Antrags.", "de", CommentSource.Workbook));
        var importer = new PackageImporter();
        var rebuilt = importer.Rebuild(importer.Read(package), new ImportPlan(Context(), "VIS", _ => null, false));
        var steps = rebuilt.Records.OfType<Step>().ToDictionary(step => step.Id);
        Assert.Equal([("VIS-E002", "Entering the request.", "en", CommentSource.Assistant), ("VIS-E002", "Erfassung des Antrags.", "de", CommentSource.Workbook),
            ("VIS-E002", "Saisie de la demande.", "fr", CommentSource.Manual), ("VIS-E004", "Validation.", "en", CommentSource.Manual)],
            rebuilt.Comments.Select(comment => (steps[comment.StepId].Code, comment.Text, comment.Language, comment.Source)).Order());
        Assert.All(rebuilt.Comments, comment => Assert.Equal((1, SampleProject.At(1000), Importer), (comment.Version, comment.UpdatedAt, comment.UpdatedBy)));
        Assert.All(rebuilt.Comments, comment => Assert.Null(steps[comment.StepId].DeletedAt));
    }

    [Fact]
    public void A_package_without_comments_is_rebuilt_as_by_build_with_none()
    {
        var importer = new PackageImporter();
        var read = importer.Read(Written(new SampleProject()));
        var rebuilt = importer.Rebuild(read, new ImportPlan(Context(), "VIS", _ => null, false));
        Assert.Empty(rebuilt.Comments);
        Assert.Equal(importer.Build(read, new ImportPlan(Context(), "VIS", _ => null, false)).Select(record => record.GetType().Name),
            rebuilt.Records.Select(record => record.GetType().Name));
    }

    public static TheoryData<string> Malformed() => new()
    {
        """{"text":"<b>gras</b>","language":"fr"}""",
        """{"text":" rogné ","language":"fr"}""",
        """{"text":"","language":"fr"}""",
        """{"text":"Une phrase.","language":"FR"}""",
        """{"text":"Une phrase."}""",
        """{"text":"Une phrase.","language":"fr","author":"x"}""",
        "{\"text\":\"" + new string('a', 501) + "\",\"language\":\"fr\"}",
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void A_malformed_comment_is_refused_by_the_schema_of_the_maps(string comment)
    {
        var forge = new PackageForge().With(PackageFormat.Maps, node => node["maps"]![0]!["steps"]![0]!["presentationComment"] = JsonNode.Parse(comment));
        var refusal = Refused(forge.Bytes());
        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"schema", (object?)PackageFormat.Maps), (refusal.Code, refusal.Details["reason"], refusal.Details["entry"]));
    }

    public static TheoryData<string> MalformedTranslations() => new()
    {
        "[]",
        """[{"language":"en","text":"A sentence."}]""",
        """[{"language":"en","text":"A sentence.","source":"auto"}]""",
        """[{"language":"en","text":"A sentence.","source":"manual","version":2}]""",
        """[{"language":"EN","text":"A sentence.","source":"manual"}]""",
        """[{"language":"en","text":" trimmed ","source":"manual"}]""",
        """[{"language":"en","text":"<i>markup</i>","source":"manual"}]""",
        """{"language":"en","text":"A sentence.","source":"manual"}""",
        "[{\"language\":\"en\",\"text\":\"" + new string('a', 501) + "\",\"source\":\"manual\"}]",
    };

    [Theory]
    [MemberData(nameof(MalformedTranslations))]
    public void Malformed_translations_of_a_comment_are_refused_by_the_schema_of_the_maps(string translations)
    {
        var forge = new PackageForge().With(PackageFormat.Maps, node => node["maps"]![0]!["steps"]![0]!["presentationCommentTranslations"] = JsonNode.Parse(translations));
        var refusal = Refused(forge.Bytes());
        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"schema", (object?)PackageFormat.Maps), (refusal.Code, refusal.Details["reason"], refusal.Details["entry"]));
    }

    [Fact]
    public void A_package_that_declares_the_format_2_3_carries_no_translation_of_a_comment()
    {
        const string Translations = """[{"language":"en","text":"A sentence.","source":"assistant"}]""";
        var forge = new PackageForge()
            .With(PackageFormat.Maps, node => node["maps"]![0]!["steps"]![0]!["presentationCommentTranslations"] = JsonNode.Parse(Translations))
            .Manifest(manifest =>
            {
                manifest["formatVersion"] = PackageFormat.VersionWithoutComments;
                manifest["schemaVersion"] = PackageFormat.VersionWithoutComments;
            });
        Assert.Equal((object?)"schema", Refused(forge.Bytes()).Details["reason"]);
        var read = new PackageForge().With(PackageFormat.Maps, node => node["maps"]![0]!["steps"]![0]!["presentationCommentTranslations"] = JsonNode.Parse(Translations));
        Assert.Equal("CONGES", new PackageImporter().Read(read.Bytes()).ProjectCode);
    }

    [Fact]
    public void A_package_that_declares_the_format_2_3_carries_no_comment()
    {
        var forge = new PackageForge()
            .With(PackageFormat.Maps, node => node["maps"]![0]!["steps"]![0]!["presentationComment"] = JsonNode.Parse("""{"text":"Une phrase.","language":"fr"}"""))
            .Manifest(manifest =>
            {
                manifest["formatVersion"] = PackageFormat.VersionWithoutComments;
                manifest["schemaVersion"] = PackageFormat.VersionWithoutComments;
            });
        var refusal = Refused(forge.Bytes());
        Assert.Equal(((object?)"schema", (object?)PackageFormat.Maps), (refusal.Details["reason"], refusal.Details["entry"]));
        // The same comment in a package of the format 2.4 is read.
        var read = new PackageForge().With(PackageFormat.Maps, node => node["maps"]![0]!["steps"]![0]!["presentationComment"] = JsonNode.Parse("""{"text":"Une phrase.","language":"fr"}"""));
        Assert.Equal("CONGES", new PackageImporter().Read(read.Bytes()).ProjectCode);
    }
}
