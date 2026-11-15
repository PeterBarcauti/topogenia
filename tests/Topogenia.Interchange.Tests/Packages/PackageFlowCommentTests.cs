// PackageFlowCommentTests.cs
// The comments of presentation of the flows in a package (ADR 099, lot J10): the format 2.4 as soon as a flow carries one, its original on the flow, in the language of origin of its source step, and its translations beside it with their source, read back on the new flows by the importer, refused by the schema when malformed.

using System.Text.Json.Nodes;
using Topogenia.Application.Ports;
using Topogenia.Application.Presentation;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Packages;
using Topogenia.Interchange.Tests.Support;

namespace Topogenia.Interchange.Tests.Packages;

public sealed class PackageFlowCommentTests
{
    private static readonly Guid Importer = Guid.Parse("00000000-0000-0000-0000-0000000000f5");

    private static long next;

    private static OperationContext Context() =>
        new(Importer, SampleProject.At(1000), "fr", AppSettings.Default, () => new Guid($"42000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    private static Step StepOf(SampleProject sample, string code) => sample.Records.OfType<Step>().Single(step => step.Code == code);

    /// <summary>The live flow of the sample between two steps named by their code.</summary>
    private static Flow FlowOf(SampleProject sample, string source, string target) =>
        sample.Records.OfType<Flow>().Single(flow => flow.DeletedAt is null && flow.SourceStepId == StepOf(sample, source).Id && flow.TargetStepId == StepOf(sample, target).Id);

    private static FlowPresentationComment Comment(Flow flow, string text, string language = "fr", CommentSource source = CommentSource.Manual) =>
        new(flow.Id, text, language, source, 3, SampleProject.At(400), Guid.Parse("00000000-0000-0000-0000-0000000000aa"));

    private static byte[] Written(SampleProject sample, params FlowPresentationComment[] comments) =>
        PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(false) { FlowComments = comments }, new PackageStamp(SampleProject.At(500), "EXEMPLE\\alice"));

    private static (string Format, string Schema) VersionsOf(byte[] package)
    {
        var manifest = new PackageFiles(package).Json(PackageFormat.Manifest);
        return (manifest["formatVersion"]!.GetValue<string>(), manifest["schemaVersion"]!.GetValue<string>());
    }

    private static IEnumerable<JsonNode> Flows(byte[] package) =>
        new PackageFiles(package).Json(PackageFormat.Maps)["maps"]!.AsArray().SelectMany(map => map!["flows"]!.AsArray()).Select(flow => flow!);

    private static (string Source, string Target) Ends(JsonNode flow) => (flow["source"]!.GetValue<string>(), flow["target"]!.GetValue<string>());

    private static DomainException Refused(byte[] package) => Assert.Throws<DomainException>(() => new PackageImporter().Read(package));

    [Fact]
    public void A_comment_of_a_live_flow_goes_with_its_flow_and_the_package_takes_the_format_2_4()
    {
        var sample = new SampleProject();
        var package = Written(sample, Comment(FlowOf(sample, "CONGES-E002", "CONGES-E003"), "La demande part chez le manager."));
        Assert.Equal(("2.4", "2.4"), VersionsOf(package));
        var flow = Assert.Single(Flows(package), written => written["presentationComment"] is not null);
        Assert.Equal(("CONGES-E002", "CONGES-E003"), Ends(flow));
        var comment = flow["presentationComment"]!.AsObject();
        Assert.Equal((2, "La demande part chez le manager.", "fr"), (comment.Count, comment["text"]!.GetValue<string>(), comment["language"]!.GetValue<string>()));
        Assert.Null(flow["presentationCommentTranslations"]);
        // The comment of a flow is no comment of its steps.
        var steps = new PackageFiles(package).Json(PackageFormat.Maps)["maps"]!.AsArray().SelectMany(map => map!["steps"]!.AsArray());
        Assert.DoesNotContain(steps, step => step!["presentationComment"] is not null || step["presentationCommentTranslations"] is not null);
        // Without a comment, the same project stays in the format of its content.
        Assert.Equal(("2.3", "2.3"), VersionsOf(Written(new SampleProject())));
    }

    [Fact]
    public void A_comment_of_a_flow_the_package_does_not_carry_is_left_out()
    {
        var sample = new SampleProject();
        var unknown = new FlowPresentationComment(Guid.Parse("00000000-0000-0000-0000-00000000dead"), "Flèche absente.", "fr", CommentSource.Manual, 1,
            SampleProject.At(400), Importer);
        // A flow in the trash keeps its comment in the database, for its restoration: the package leaves it with the flow.
        var deleted = sample.Records.OfType<Flow>().First(flow => flow.DeletedAt is not null);
        var package = Written(sample, unknown, new FlowPresentationComment(deleted.Id, "Flèche supprimée.", "fr", CommentSource.Manual, 1, SampleProject.At(400), Importer));
        Assert.Equal(("2.3", "2.3"), VersionsOf(package));
        Assert.DoesNotContain(Flows(package), flow => flow["presentationComment"] is not null || flow["presentationCommentTranslations"] is not null);
    }

    [Fact]
    public void The_translations_of_a_comment_go_beside_its_original_by_language_each_with_its_source()
    {
        var sample = new SampleProject();
        var flow = FlowOf(sample, "CONGES-E002", "CONGES-E003");
        var package = Written(sample, Comment(flow, "The request goes to the manager.", "en", CommentSource.Assistant), Comment(flow, "La demande part chez le manager."),
            Comment(flow, "Der Antrag geht an den Vorgesetzten.", "de", CommentSource.Workbook), Comment(flow, "La solicitud pasa al responsable.", "es"));
        var node = Assert.Single(Flows(package), written => written["presentationComment"] is not null);
        Assert.Equal(("La demande part chez le manager.", "fr"), (node["presentationComment"]!["text"]!.GetValue<string>(), node["presentationComment"]!["language"]!.GetValue<string>()));
        Assert.Equal([("de", "Der Antrag geht an den Vorgesetzten.", "workbook"), ("en", "The request goes to the manager.", "assistant"),
            ("es", "La solicitud pasa al responsable.", "manual")],
            node["presentationCommentTranslations"]!.AsArray().Select(line => (line!["language"]!.GetValue<string>(), line["text"]!.GetValue<string>(),
                line["source"]!.GetValue<string>())));
        Assert.All(node["presentationCommentTranslations"]!.AsArray(), line => Assert.Equal(3, line!.AsObject().Count));
    }

    /// <summary>A flow has no language of its own: the original of its comment is the line in the language of origin of its source step.</summary>
    [Fact]
    public void The_original_of_the_comment_of_a_flow_is_the_line_in_the_language_of_origin_of_its_source_step()
    {
        var sample = new SampleProject().With(record => record is Step { Code: "CONGES-E002" } step ? step with { SourceLanguage = "en" } : record);
        var leaving = FlowOf(sample, "CONGES-E002", "CONGES-E003");
        var reaching = FlowOf(sample, "CONGES-E001", "CONGES-E002");
        var package = Written(sample, Comment(leaving, "The request goes to the manager.", "en"), Comment(leaving, "La demande part chez le manager.", "fr", CommentSource.Workbook),
            Comment(reaching, "The employee starts the request.", "en", CommentSource.Assistant), Comment(reaching, "Le salarié commence sa demande."));
        var byEnds = Flows(package).Where(flow => flow["presentationComment"] is not null).ToDictionary(Ends);
        // The flow that leaves the step written in English has its original in English, its French line is a translation.
        var first = byEnds[("CONGES-E002", "CONGES-E003")];
        Assert.Equal(("en", "fr", "workbook"), (first["presentationComment"]!["language"]!.GetValue<string>(),
            first["presentationCommentTranslations"]![0]!["language"]!.GetValue<string>(), first["presentationCommentTranslations"]![0]!["source"]!.GetValue<string>()));
        // The flow that reaches it leaves a step written in French: its original is in French, whatever its target.
        var second = byEnds[("CONGES-E001", "CONGES-E002")];
        Assert.Equal(("fr", "en", "assistant"), (second["presentationComment"]!["language"]!.GetValue<string>(),
            second["presentationCommentTranslations"]![0]!["language"]!.GetValue<string>(), second["presentationCommentTranslations"]![0]!["source"]!.GetValue<string>()));
    }

    [Fact]
    public void A_translation_without_an_original_is_carried_alone_and_the_package_takes_the_format_2_4()
    {
        var sample = new SampleProject();
        var package = Written(sample, Comment(FlowOf(sample, "CONGES-E002", "CONGES-E003"), "Only in English.", "en"));
        Assert.Equal(("2.4", "2.4"), VersionsOf(package));
        var node = Assert.Single(Flows(package), written => written["presentationCommentTranslations"] is not null);
        Assert.Null(node["presentationComment"]);
        Assert.Equal("Only in English.", Assert.Single(node["presentationCommentTranslations"]!.AsArray())!["text"]!.GetValue<string>());
        var importer = new PackageImporter();
        var line = Assert.Single(importer.Rebuild(importer.Read(package), new ImportPlan(Context(), "VIS", _ => null, false)).FlowComments);
        Assert.Equal(("Only in English.", "en", CommentSource.Manual), (line.Text, line.Language, line.Source));
    }

    [Fact]
    public void Two_exports_of_the_same_comments_give_the_same_maps_whatever_their_order()
    {
        var sample = new SampleProject();
        var first = Comment(FlowOf(sample, "CONGES-E002", "CONGES-E003"), "Première.");
        var second = Comment(FlowOf(sample, "CONGES-E003", "CONGES-E004"), "Second.", "en");
        Assert.Equal(new PackageFiles(Written(sample, first, second)).Bytes[PackageFormat.Maps], new PackageFiles(Written(sample, second, first)).Bytes[PackageFormat.Maps]);
    }

    [Fact]
    public void The_comments_are_read_back_on_the_new_flows_written_by_the_importer_at_its_time_in_version_1_beside_those_of_the_steps()
    {
        var sample = new SampleProject();
        var leaving = FlowOf(sample, "CONGES-E002", "CONGES-E003");
        var accepted = FlowOf(sample, "CONGES-E003", "CONGES-E004");
        var said = new PresentationComment(StepOf(sample, "CONGES-E002").Id, "Saisie de la demande.", "fr", CommentSource.Manual, 2, SampleProject.At(400), Importer);
        var package = PackageWriter.Write(sample.Workspace(), sample.Logins, new PackageOptions(false)
        {
            Comments = [said],
            FlowComments = [Comment(leaving, "La demande part chez le manager."), Comment(accepted, "Accepted requests go on.", "en"),
                Comment(leaving, "The request goes to the manager.", "en", CommentSource.Assistant),
                Comment(leaving, "Der Antrag geht an den Vorgesetzten.", "de", CommentSource.Workbook)],
        }, new PackageStamp(SampleProject.At(500), "EXEMPLE\\alice"));
        var importer = new PackageImporter();

        var rebuilt = importer.Rebuild(importer.Read(package), new ImportPlan(Context(), "VIS", _ => null, false));

        var steps = rebuilt.Records.OfType<Step>().ToDictionary(step => step.Id, step => step.Code);
        var flows = rebuilt.Records.OfType<Flow>().ToDictionary(flow => flow.Id);
        Assert.Equal([("VIS-E002", "VIS-E003", "Der Antrag geht an den Vorgesetzten.", "de", CommentSource.Workbook),
            ("VIS-E002", "VIS-E003", "La demande part chez le manager.", "fr", CommentSource.Manual),
            ("VIS-E002", "VIS-E003", "The request goes to the manager.", "en", CommentSource.Assistant),
            ("VIS-E003", "VIS-E004", "Accepted requests go on.", "en", CommentSource.Manual)],
            rebuilt.FlowComments.Select(comment => (steps[flows[comment.FlowId].SourceStepId], steps[flows[comment.FlowId].TargetStepId], comment.Text, comment.Language,
                comment.Source)).Order());
        Assert.All(rebuilt.FlowComments, comment => Assert.Equal((1, SampleProject.At(1000), Importer), (comment.Version, comment.UpdatedAt, comment.UpdatedBy)));
        Assert.All(rebuilt.FlowComments, comment => Assert.Null(flows[comment.FlowId].DeletedAt));
        // The comment of the step is read back on its step, apart.
        var onStep = Assert.Single(rebuilt.Comments);
        Assert.Equal(("VIS-E002", "Saisie de la demande."), (steps[onStep.StepId], onStep.Text));
    }

    [Fact]
    public void A_package_without_comments_is_rebuilt_with_none()
    {
        var importer = new PackageImporter();
        var rebuilt = importer.Rebuild(importer.Read(Written(new SampleProject())), new ImportPlan(Context(), "VIS", _ => null, false));
        Assert.Empty(rebuilt.FlowComments);
        Assert.Empty(rebuilt.Comments);
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
    public void A_malformed_comment_of_a_flow_is_refused_by_the_schema_of_the_maps(string comment)
    {
        var forge = new PackageForge().With(PackageFormat.Maps, node => node["maps"]![0]!["flows"]![0]!["presentationComment"] = JsonNode.Parse(comment));
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
    public void Malformed_translations_of_a_comment_of_a_flow_are_refused_by_the_schema_of_the_maps(string translations)
    {
        var forge = new PackageForge().With(PackageFormat.Maps, node => node["maps"]![0]!["flows"]![0]!["presentationCommentTranslations"] = JsonNode.Parse(translations));
        var refusal = Refused(forge.Bytes());
        Assert.Equal((ErrorCodes.InvalidPackage, (object?)"schema", (object?)PackageFormat.Maps), (refusal.Code, refusal.Details["reason"], refusal.Details["entry"]));
    }

    [Fact]
    public void A_package_that_declares_the_format_2_3_carries_no_comment_of_a_flow_nor_its_translations()
    {
        const string Comment = """{"text":"Une phrase.","language":"fr"}""";
        const string Translations = """[{"language":"en","text":"A sentence.","source":"assistant"}]""";
        foreach (var (key, value) in new[] { ("presentationComment", Comment), ("presentationCommentTranslations", Translations) })
        {
            var forge = new PackageForge()
                .With(PackageFormat.Maps, node => node["maps"]![0]!["flows"]![0]![key] = JsonNode.Parse(value))
                .Manifest(manifest =>
                {
                    manifest["formatVersion"] = PackageFormat.VersionWithoutComments;
                    manifest["schemaVersion"] = PackageFormat.VersionWithoutComments;
                });
            var refusal = Refused(forge.Bytes());
            Assert.Equal(((object?)"schema", (object?)PackageFormat.Maps), (refusal.Details["reason"], refusal.Details["entry"]));
            // The same value in a package of the format 2.4 is read.
            var read = new PackageForge().With(PackageFormat.Maps, node => node["maps"]![0]!["flows"]![0]![key] = JsonNode.Parse(value));
            Assert.Equal("CONGES", new PackageImporter().Read(read.Bytes()).ProjectCode);
        }
    }

    /// <summary>The flows an open frame names are named by their ends only: the comment of a flow is carried once, on the flow itself.</summary>
    [Fact]
    public void A_flow_named_by_an_open_frame_carries_no_comment_there()
    {
        var forge = new PackageForge().With(PackageFormat.Maps, node =>
        {
            var framed = node["maps"]!.AsArray().First(map => map!["dissolution"] is not null)!["dissolution"]!["createdFlows"]![0]!;
            framed["presentationComment"] = JsonNode.Parse("""{"text":"Une phrase.","language":"fr"}""");
        });
        var refusal = Refused(forge.Bytes());
        Assert.Equal(((object?)"schema", (object?)PackageFormat.Maps), (refusal.Details["reason"], refusal.Details["entry"]));
    }
}
