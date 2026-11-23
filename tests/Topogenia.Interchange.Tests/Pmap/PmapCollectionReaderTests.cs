// PmapCollectionReaderTests.cs
// The reading of a template collection .pmaps (ADR 114): the envelope and its bounds, a fault of the envelope refuses the whole file, each process read alone by the reader of the .pmap.

using Topogenia.Domain.Errors;
using Topogenia.Domain.Raci;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapCollectionReaderTests
{
    /// <summary>The settings of the opening of a file: the languages configured, every letter of the RACI (PmapExchange).</summary>
    public static readonly PmapReadSettings Settings = new(Languages, RaciRules.KnownLetters);

    /// <summary>A small valid process named <paramref name="name"/>, with a code when one is given.</summary>
    public static string Process(string name, string? code = null) => Doc(
        $$"""{ "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "Saisir" }, { "id": "e2", "lane": "c2", "label": "Valider" }], "flows": [{ "source": "e1", "target": "e2" }] }""",
        code is null ? $$"""{ "name": "{{name}}" }""" : $$"""{ "code": "{{code}}", "name": "{{name}}" }""");

    /// <summary>An envelope around the processes given, with what <paramref name="extra"/> adds to its root.</summary>
    public static string Collection(IEnumerable<string> processes, string extra = "") =>
        $$"""{ "format": "topogenia-collection", "version": "1.0", "name": "Essais"{{extra}}, "processes": [{{string.Join(", ", processes)}}] }""";

    public static PmapCollectionRead Read(string text)
    {
        var read = PmapCollectionReader.Read(text, Settings);
        Assert.True(read.Errors.Count == 0, string.Join("\n", read.Errors.Select(error => $"{error.Path} : {error.Code}")));
        return read;
    }

    /// <summary>The faults of a file refused whole: no process is given.</summary>
    public static List<FileError> Refused(string text)
    {
        var read = PmapCollectionReader.Read(text, Settings);
        Assert.Empty(read.Processes);
        Assert.Null(read.Name);
        Assert.NotEmpty(read.Errors);
        return [.. read.Errors];
    }

    private static IEnumerable<string> Named(int count) => Enumerable.Range(1, count).Select(i => Process($"Processus {i}"));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(PmapFormat.ProcessesMax)]
    public void A_collection_of_one_two_and_fifty_processes_gives_each_document_in_order(int count)
    {
        var read = Read(Collection(Named(count), """, "description": "Ce que contient la collection." """));

        Assert.Equal(("Essais", "Ce que contient la collection."), (read.Name, read.Description));
        Assert.Equal(Enumerable.Range(0, count), read.Processes.Select(process => process.Index));
        Assert.Equal(Enumerable.Range(1, count).Select(i => $"Processus {i}"), read.Processes.Select(process => process.Read.Document!.Project.Name));
        Assert.All(read.Processes, process => Assert.Empty(process.Read.Errors));
        Assert.Equal(50, PmapFormat.ProcessesMax);
    }

    [Fact]
    public void Fifty_one_processes_are_refused_with_their_count_and_the_maximum()
    {
        var fault = Assert.Single(Refused(Collection(Named(PmapFormat.ProcessesMax + 1))));

        Assert.Equal(("processes", CollectionErrors.TooManyProcesses), (fault.Path, fault.Code));
        Assert.Equal(((object?)51, (object?)50), (fault.Values["count"], fault.Values["max"]));
        Assert.Equal("COLLECTION_TROP_DE_PROCESSUS", CollectionErrors.TooManyProcesses);
    }

    [Fact]
    public void An_empty_list_or_a_missing_list_is_refused()
    {
        var empty = Assert.Single(Refused(Collection([])));
        Assert.Equal(("processes", FileErrorCodes.Required, (object?)"processes"), (empty.Path, empty.Code, empty.Values["name"]));

        var missing = Assert.Single(Refused("""{ "format": "topogenia-collection", "version": "1.0", "name": "Essais" }"""));
        Assert.Equal(("processes", FileErrorCodes.Required), (missing.Path, missing.Code));

        var notAList = Assert.Single(Refused("""{ "format": "topogenia-collection", "version": "1.0", "name": "Essais", "processes": { "a": 1 } }"""));
        Assert.Equal(("processes", FileErrorCodes.NotAList), (notAList.Path, notAList.Code));

        Assert.Equal(FileErrorCodes.Empty, Assert.Single(Refused("  \n ")).Code);
        Assert.Equal(FileErrorCodes.NotJson, Assert.Single(Refused("{ \"format\": ")).Code);
        Assert.Equal(FileErrorCodes.NotObject, Assert.Single(Refused("[1, 2]")).Code);
    }

    [Fact]
    public void A_process_that_is_not_an_object_refuses_the_whole_file_at_its_place()
    {
        var faults = Refused(Collection([Process("Un"), "42", Process("Trois"), "\"texte\""]));

        Assert.Equal([("processes[1]", FileErrorCodes.NotAnObject), ("processes[3]", FileErrorCodes.NotAnObject)],
            faults.Select(fault => (fault.Path, fault.Code)));
        Assert.Equal("processes[1]", faults[0].Values["name"]);
    }

    [Fact]
    public void The_name_is_required_and_follows_the_rules_of_every_text()
    {
        var missing = Assert.Single(Refused($$"""{ "format": "topogenia-collection", "version": "1.0", "processes": [{{Process("Un")}}] }"""));
        Assert.Equal(("name", FileErrorCodes.Required), (missing.Path, missing.Code));

        var tooLong = Assert.Single(Refused(Collection([Process("Un")]).Replace("\"Essais\"", $"\"{new string('n', 201)}\"", StringComparison.Ordinal)));
        Assert.Equal(("name", ErrorCodes.TextTooLong, (object?)200), (tooLong.Path, tooLong.Code, tooLong.Values["max"]));

        var html = Assert.Single(Refused(Collection([Process("Un")], """, "description": "<script>x</script>" """)));
        Assert.Equal("description", html.Path);
    }

    [Fact]
    public void Another_format_or_version_is_refused_and_a_pmap_is_not_a_collection()
    {
        var format = Assert.Single(Refused(Collection([Process("Un")]).Replace("topogenia-collection", "topogenia", StringComparison.Ordinal)));
        Assert.Equal(("format", FileErrorCodes.WrongFormat, (object?)"topogenia-collection"), (format.Path, format.Code, format.Values["expected"]));
        Assert.Equal("topogenia-collection", PmapFormat.CollectionName);

        var version = Assert.Single(Refused(Collection([Process("Un")]).Replace("\"1.0\"", "\"1.1\"", StringComparison.Ordinal)));
        Assert.Equal(("version", FileErrorCodes.WrongVersion, (object?)"1.1", (object?)"1.0"),
            (version.Path, version.Code, version.Values["version"], version.Values["supported"]));
        Assert.Equal(("1.0", ".pmaps"), (PmapFormat.CollectionVersion, PmapFormat.CollectionExtension));

        // A .pmap read as a collection, and a collection read as a .pmap: each reader refuses the other's file by its format.
        Assert.Contains(Refused(Sample("minimal.pmap")), fault => fault is { Path: "format", Code: FileErrorCodes.WrongFormat });
        Assert.Contains(Faults(Collection([Process("Un")])), fault => fault is { Path: "format", Code: FileErrorCodes.WrongFormat });
    }

    [Fact]
    public void An_unknown_property_of_the_envelope_is_refused_at_its_place()
    {
        var faults = Refused(Collection([Process("Un")], """, "language": "fr", "code": "ACHAT" """));

        Assert.Equal([("language", FileErrorCodes.UnknownProperty), ("code", FileErrorCodes.UnknownProperty)], faults.Select(fault => (fault.Path, fault.Code)));
        Assert.Equal("format, version, name, description, processes", faults[0].Values["possible"]);
        Assert.Empty(PmapCollectionReader.Read(Collection([Process("Un")], """, "$schema": "topogenia-collection-1.0.schema.json" """), Settings).Errors);
    }

    [Fact]
    public void An_invalid_process_among_valid_ones_keeps_its_faults_and_the_others_their_documents()
    {
        var invalid = Process("Deux").Replace("\"lane\": \"c2\"", "\"lane\": \"c9\"", StringComparison.Ordinal);

        var read = Read(Collection([Process("Un", "ACHAT"), invalid, Process("Trois", "TROIS")]));

        Assert.Equal([0, 1, 2], read.Processes.Select(process => process.Index));
        Assert.Equal(["ACHAT", null, "TROIS"], read.Processes.Select(process => process.Read.Document?.Project.Code));
        var fault = Assert.Single(read.Processes[1].Read.Errors);
        Assert.Equal(("map.steps[1].lane", FileErrorCodes.UnknownLane), (fault.Path, fault.Code));
        Assert.Empty(read.Processes[0].Read.Errors);
        Assert.Empty(read.Processes[2].Read.Errors);
    }

    /// <summary>A process in fault (an unknown lane) whose <c>project</c> is the JSON text given, or has none when it is null.</summary>
    private static string Faulty(string? project) =>
        $$"""{ "format": "topogenia", "version": "1.1",{{(project is null ? "" : " \"project\": " + project + ",")}} "map": { "lanes": [{{TwoLanes}}], "steps": [{ "id": "e1", "lane": "c1", "label": "A" }, { "id": "e2", "lane": "c9", "label": "B" }], "flows": [] } }""";

    [Fact]
    public void A_process_in_fault_gives_its_name_as_written_when_it_is_a_readable_text()
    {
        var longest = new string('x', 200);
        var read = Read(Collection([Faulty("""{ "name": "Demande d'achat" }"""), Faulty($$"""{ "name": "{{longest}}" }"""), Faulty("""{ "name": "<b>a</b> é" }"""),
            Process("Valide")]));

        Assert.All(read.Processes.Take(3), process => Assert.NotEmpty(process.Read.Errors));
        Assert.Equal(["Demande d'achat", longest, "<b>a</b> é", "Valide"], read.Processes.Select(process => process.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{}""")]
    [InlineData("""{ "name": 12 }""")]
    [InlineData("""{ "name": null }""")]
    [InlineData("""{ "name": ["a"] }""")]
    [InlineData("""{ "name": "" }""")]
    [InlineData("""{ "name": "a\nb" }""")]
    [InlineData("""{ "name": "a\u0007b" }""")]
    [InlineData("""{ "name": "a\u007fb" }""")]
    [InlineData("""[ "name" ]""")]
    [InlineData("""12""")]
    public void A_process_in_fault_gives_no_name_when_it_is_missing_not_a_text_empty_or_with_a_control_character(string? project)
    {
        var read = Read(Collection([Faulty(project)]));

        Assert.NotEmpty(read.Processes[0].Read.Errors);
        Assert.Null(read.Processes[0].Name);
    }

    [Fact]
    public void A_name_of_two_hundred_and_one_characters_is_no_name()
    {
        var read = Read(Collection([Faulty($$"""{ "name": "{{new string('x', 201)}}" }""")]));

        Assert.Null(read.Processes[0].Name);
    }

    [Fact]
    public void Each_process_is_read_by_the_rules_of_the_pmap_depth_steps_and_languages()
    {
        static string Nested(int levels, int level = 1) => level > levels
            ? $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "Fin" }] }"""
            : $$"""{ "lanes": [{ "id": "l{{level}}", "name": "X" }], "steps": [{ "id": "s{{level}}", "lane": "l{{level}}", "label": "N", "sub_map": {{Nested(levels, level + 1)}} }] }""";
        var steps = string.Join(",", Enumerable.Range(0, PmapFormat.StepsMax + 1).Select(i => $$"""{ "id": "e{{i}}", "lane": "c1", "label": "X" }"""));
        var german = Process("Drei").Replace("{ \"name\": \"Drei\" }", "{ \"name\": \"Drei\", \"language\": \"de\" }", StringComparison.Ordinal);

        var read = Read(Collection([Doc(Nested(11)), Doc(Nested(12)), Doc($$"""{ "lanes": [{{TwoLanes}}], "steps": [{{steps}}] }"""), german, Process("Quatre")]));

        Assert.NotNull(read.Processes[0].Read.Document);
        Assert.Equal((FileErrorCodes.TooDeep, (object?)12), (Assert.Single(read.Processes[1].Read.Errors).Code, read.Processes[1].Read.Errors[0].Values["max"]));
        Assert.Equal(FileErrorCodes.TooManySteps, Assert.Single(read.Processes[2].Read.Errors).Code);
        Assert.Equal(("project.language", FileErrorCodes.UnknownLanguage), (read.Processes[3].Read.Errors[0].Path, read.Processes[3].Read.Errors[0].Code));
        Assert.NotNull(read.Processes[4].Read.Document);
        // The faults of one process are counted for it alone, 30 at most each, as for a file.
        var many = Doc(string.Concat("{ \"lanes\": [", string.Join(",", Enumerable.Range(0, 40).Select(i => $$"""{ "id": "", "name": "L{{i}}" }""")), "] }"));
        var bounded = Read(Collection([many, many]));
        Assert.All(bounded.Processes, process => Assert.Equal(PmapFormat.ErrorsMax, process.Read.Errors.Count));
    }

    [Fact]
    public void The_envelope_is_cleaned_like_a_pmap_and_accepts_comments_and_trailing_commas()
    {
        var text = "﻿```json\n" + """
            {
              // A collection pasted from a conversation
              "format": "topogenia-collection",
              "version": "1.0",
              "name": "Essais",
              "processes": [
            """ + Process("Un") + ",\n" + Process("Deux") + """
            ,
              ],
            }
            """ + "\n```\n";

        var read = Read(text);

        Assert.Equal(["Un", "Deux"], read.Processes.Select(process => process.Read.Document!.Project.Name));
    }

    [Fact]
    public void A_key_written_twice_in_the_envelope_or_in_a_process_refuses_the_whole_file()
    {
        // A collection could show one text and open another: the parser keeps the last key (pre-audit 68, P3-03).
        var envelope = Assert.Single(Refused(Collection([Process("Un")], """, "name": "Autre" """)));
        Assert.Equal(("", FileErrorCodes.NotJson), (envelope.Path, envelope.Code));
        var process = Process("Un").Replace("\"name\": \"Un\"", "\"name\": \"Un\", \"name\": \"Deux\"", StringComparison.Ordinal);
        var inner = Assert.Single(Refused(Collection([Process("Zéro"), process])));
        Assert.Equal(("", FileErrorCodes.NotJson), (inner.Path, inner.Code));
    }

    [Fact]
    public void A_json_deeper_than_256_is_refused()
    {
        var deep = string.Concat(Enumerable.Repeat("[", 300)) + string.Concat(Enumerable.Repeat("]", 300));
        var fault = Assert.Single(Refused(Collection([Process("Un"), Doc($$"""{ "lanes": [{{TwoLanes}}], "steps": {{deep}} }""")])));
        Assert.Equal(("", FileErrorCodes.NotJson), (fault.Path, fault.Code));

        // The envelope adds two levels: a process that a .pmap alone admits may make the collection too deep.
        var nearLimit = string.Concat(Enumerable.Repeat("[", 254)) + string.Concat(Enumerable.Repeat("]", 254));
        var near = Doc($$"""{ "lanes": [{{TwoLanes}}], "uncertainties": {{nearLimit}} }""");
        Assert.DoesNotContain(PmapReader.Read(near, Settings).Errors, error => error.Code == FileErrorCodes.NotJson);
        Assert.Equal(FileErrorCodes.NotJson, Assert.Single(Refused(Collection([near]))).Code);
    }

    [Fact]
    public void The_content_of_each_process_opens_alone_as_the_same_document()
    {
        var at = new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);
        var alone = new[] { "minimal.pmap", "cloture-mensuelle.pmap" }.Select(name => PmapWriter.Write(PmapReaderTests.Read(Sample(name)), "essai", at)).ToList();

        var read = Read(Sample("exemple.pmaps"));

        Assert.Equal(2, read.Processes.Count);
        Assert.All(read.Processes, process => Assert.Equal(
            PmapWriter.Write(process.Read.Document!, "essai", at),
            PmapWriter.Write(PmapReaderTests.Read(process.Content), "essai", at)));
        Assert.Equal(alone, read.Processes.Select(process => PmapWriter.Write(process.Read.Document!, "essai", at)));
        Assert.All(read.Processes, process => Assert.StartsWith("{", process.Content, StringComparison.Ordinal));
    }

    [Fact]
    public void The_port_refuses_a_faulty_envelope_as_an_invalid_file_and_gives_each_process_its_document_or_its_faults()
    {
        var files = new PmapFiles();
        var invalid = Process("Deux").Replace("\"lane\": \"c2\"", "\"lane\": \"c9\"", StringComparison.Ordinal);

        var collection = files.ReadCollection(Collection([Process("Un", "ACHAT"), invalid], """, "description": "Deux processus." """), Languages, RaciRules.KnownLetters);

        Assert.Equal(("Essais", "Deux processus."), (collection.Name, collection.Description));
        Assert.Equal(("ACHAT", "Un"), (collection.Processes[0].Document!.ProjectCode, collection.Processes[0].Document!.ProjectName));
        Assert.Empty(collection.Processes[0].Faults);
        // The language and the description of each process, as its .pmap gives them, for the preview of the collection (ADR 114 point 4).
        Assert.Equal((Languages.DefaultLanguage, (string?)null), (collection.Processes[0].Language, collection.Processes[0].Description));
        Assert.Equal(((string?)null, (string?)null), (collection.Processes[1].Language, collection.Processes[1].Description));
        var described = files.ReadCollection(Collection([Process("Un").Replace("\"name\": \"Un\"", "\"name\": \"Un\", \"language\": \"en\", \"description\": \"Le premier.\"",
            StringComparison.Ordinal)]), Languages, RaciRules.KnownLetters);
        Assert.Equal(("en", "Le premier."), (described.Processes[0].Language, described.Processes[0].Description));
        Assert.Null(collection.Processes[1].Document);
        Assert.Equal(FileErrorCodes.UnknownLane, Assert.Single(collection.Processes[1].Faults).Code);
        Assert.Equal(1, collection.Processes[1].Index);
        Assert.Equal(new Topogenia.Application.Ports.PmapCounts(1, 2, 2, 1, 0), collection.Processes[0].Document!.Counts);
        // The content of a process opens through the port as a file.
        Assert.Equal("Un", files.Read(collection.Processes[0].Content, Languages, RaciRules.KnownLetters).ProjectName);

        var refusal = Assert.Throws<DomainException>(() => files.ReadCollection(Collection([]), Languages, RaciRules.KnownLetters));
        Assert.Equal((ErrorCodes.InvalidFile, (object?)1), (refusal.Code, refusal.Details["count"]));
        var fault = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<FileError>>(refusal.Details[FileError.DetailsKey]));
        Assert.Equal(("processes", FileErrorCodes.Required), (fault.Path, fault.Code));
    }
}
