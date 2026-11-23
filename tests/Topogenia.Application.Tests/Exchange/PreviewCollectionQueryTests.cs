// PreviewCollectionQueryTests.cs
// The preview of a template collection (ADR 114 points 4 and 6): a free code proposed for each process, the codes already proposed counted, nothing written, the administrator alone, the content bounded before it is read.

using Topogenia.Application.Exchange;
using Topogenia.Application.Ports;
using Topogenia.Application.Tests.Users;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Raci;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Tests.Exchange;

public sealed class PreviewCollectionQueryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record Document(string? ProjectCode, string ProjectName, IReadOnlyList<string> Uncertainties) : IPmapDocument
    {
        public PmapCounts Counts { get; init; } = new(1, 2, 3, 2, 1);

        public int RemoteLocations { get; init; }
    }

    /// <summary>A valid process of the collection: its document, its language and its description, its text as the host read it.</summary>
    private static PmapCollectionItem Valid(int index, string? code, string name, string language = "fr", string? description = null) =>
        new(index, $"{{\"process\":{index}}}", new Document(code, name, [$"À vérifier {index}"]), []) { Language = language, Description = description };

    private static PmapCollectionItem Invalid(int index, string? name = null) =>
        new(index, $"{{\"process\":{index}}}", null, [new FileError("map.steps[1].lane", FileErrorCodes.UnknownLane, new Dictionary<string, object?> { ["lane"] = "c9" })]) { Name = name };

    /// <summary>A format that gives the collection it holds and counts its reads; it never reads anything else.</summary>
    private sealed class CollectionFormat(params PmapCollectionItem[] processes) : IPmapFormat
    {
        public List<(string Content, LanguageSettings Languages, string Letters)> Reads { get; } = [];

        public PmapCollection ReadCollection(string content, LanguageSettings languages, string raciLetters)
        {
            Reads.Add((content, languages, raciLetters));
            return new PmapCollection("Achats", "Les processus d'achat.", processes);
        }

        public IPmapDocument Read(string content, LanguageSettings languages, string raciLetters) => throw Unexpected();

        public IPmapDocument ReadProposal(string content, LanguageSettings languages, string raciLetters) => throw Unexpected();

        public PmapInsertionFile ReadInsertion(string content, LanguageSettings languages, string raciLetters) => throw Unexpected();

        public PmapDetailBuild Detail(OperationContext context, ProjectWorkspace workspace, IPmapDocument document, Guid stepId, LanguageSettings languages,
            bool withSheets) => throw Unexpected();

        public PmapOutlineResult Outline(ProjectWorkspace workspace, Func<Step, SheetViewer>? viewerOf, PmapExtras? extras) => throw Unexpected();

        public PmapOutlineResult OutlineMap(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf) => throw Unexpected();

        public PmapOutlineResult OutlineView(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf) => throw Unexpected();

        public PmapOutlineResult OutlinePrint(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf) => throw Unexpected();

        public PmapOutlineResult OutlineBranch(ProjectWorkspace workspace, Guid mapId, Func<Step, SheetViewer> viewerOf, PmapExtras? extras) => throw Unexpected();

        public Domain.Proposals.DesiredMap Desired(IPmapDocument document) => throw Unexpected();

        public PmapOutlineResult OutlineTree(ProjectWorkspace workspace, Guid mapId) => throw Unexpected();

        public IPmapDocument Carry(IPmapDocument proposal, ProjectWorkspace origin, Guid mapId, Func<Step, SheetViewer> viewerOf) => throw Unexpected();

        public PmapOutlineResult Model(ProjectWorkspace workspace, PmapExtras? extras) => throw Unexpected();

        public string Write(IPmapDocument document, string generatedBy, DateTime generatedAt) => throw Unexpected();

        public PmapProjectBuild Build(OperationContext context, IPmapDocument document, string code, string name, ProjectCreationInputs inputs) => throw Unexpected();

        public string FileName(Project project, DateTime exportedAt) => throw Unexpected();

        private static InvalidOperationException Unexpected() => new("The preview of a collection reads the collection only.");
    }

    /// <summary>The administrator acting, with projects of the given codes, live or in the trash.</summary>
    private static UsersFixture Administrator(bool inTrash, params string[] codes)
    {
        var fixture = new UsersFixture();
        foreach (var code in codes)
            fixture.OtherProject(code, fixture.Project.Reader, MemberRole.Facilitator, inTrash);
        return fixture.ActingAs(fixture.Project.Admin);
    }

    private static async Task<IReadOnlyList<CollectionProcessPreview>> PreviewAsync(UsersFixture fixture, CollectionFormat format) =>
        (await fixture.Runner.RunAsync(new PreviewCollectionQuery(format, "collection"), Token)).Processes;

    [Fact]
    public async Task The_proposed_code_is_the_one_of_the_file_when_free()
    {
        var fixture = Administrator(false, "ACHATA", "LIVRE");
        var format = new CollectionFormat(Valid(0, "ACHAT", "Demande d'achat", "fr", "Du besoin à la commande."), Valid(1, "RECEP", "Réception", "en"));

        var preview = await fixture.Runner.RunAsync(new PreviewCollectionQuery(format, "collection"), Token);

        Assert.Equal(("Achats", "Les processus d'achat."), (preview.Name, preview.Description));
        var first = preview.Processes[0];
        Assert.Equal((0, "Demande d'achat", "fr", "ACHAT", "ACHAT", "Du besoin à la commande."),
            (first.Index, first.Name, first.Language, first.FileCode, first.ProposedCode, first.Description));
        Assert.Equal(new PmapCounts(1, 2, 3, 2, 1), first.Counts);
        Assert.Equal(["À vérifier 0"], first.Uncertainties);
        Assert.Empty(first.Faults);
        Assert.Equal("{\"process\":0}", first.Content);
        Assert.Equal((1, "RECEP", "RECEP", "en"), (preview.Processes[1].Index, preview.Processes[1].FileCode, preview.Processes[1].ProposedCode,
            preview.Processes[1].Language));
        // Read once, as the opening of a file reads it: the languages of the installation and every letter of the RACI.
        var read = Assert.Single(format.Reads);
        Assert.Equal(("collection", UsersFixture.Languages, RaciRules.KnownLetters), read);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_taken_code_or_one_in_the_trash_is_replaced_by_the_first_free_one(bool inTrash)
    {
        var fixture = Administrator(inTrash, "ACHAT", "ACHATA");

        var processes = await PreviewAsync(fixture, new CollectionFormat(Valid(0, "ACHAT", "Demande d'achat")));

        Assert.Equal(("ACHAT", "ACHATB"), (processes[0].FileCode, processes[0].ProposedCode));
        Assert.Equal(ProjectCodeRules.TrashCode("ACHAT", ["ACHAT", "ACHATA"]), processes[0].ProposedCode);
    }

    [Fact]
    public async Task Two_processes_with_the_same_code_get_two_codes()
    {
        var free = await PreviewAsync(Administrator(false), new CollectionFormat(Valid(0, "ACHAT", "Un"), Valid(1, "ACHAT", "Deux"), Valid(2, "ACHAT", "Trois")));
        Assert.Equal(["ACHAT", "ACHATA", "ACHATB"], free.Select(process => process.ProposedCode));

        var taken = await PreviewAsync(Administrator(true, "ACHAT"), new CollectionFormat(Valid(0, "ACHAT", "Un"), Valid(1, "ACHAT", "Deux")));
        Assert.Equal(["ACHATA", "ACHATB"], taken.Select(process => process.ProposedCode));

        // A code proposed above counts whatever its start: the file code ACHATA, then ACHAT taken, gives ACHATB.
        var above = await PreviewAsync(Administrator(false, "ACHAT"), new CollectionFormat(Valid(0, "ACHATA", "Un"), Valid(1, "ACHAT", "Deux")));
        Assert.Equal(["ACHATA", "ACHATB"], above.Select(process => process.ProposedCode));
        Assert.All(above, process => Assert.Equal(process.FileCode, process.Index == 0 ? "ACHATA" : "ACHAT"));
    }

    [Fact]
    public async Task A_process_without_code_gets_the_code_proposed_from_its_name_or_none()
    {
        var fixture = Administrator(false, "GDA");
        var format = new CollectionFormat(Valid(0, null, "Achats"), Invalid(1), Valid(2, null, "RH"), Valid(3, null, "Gestion des achats"), Valid(4, null, "Achats"));

        var processes = await PreviewAsync(fixture, format);

        Assert.Equal(["ACHATS", null, null, "GDAA", "ACHATA"], processes.Select(process => process.ProposedCode));
        Assert.All(processes, process => Assert.Null(process.FileCode));
        // A process in fault has neither code nor counts, but its faults with their values, and its text.
        var invalid = processes[1];
        Assert.Equal((null, null, null, null, (PmapCounts?)null), (invalid.Name, invalid.Language, invalid.Description, invalid.ProposedCode, invalid.Counts));
        Assert.Empty(invalid.Uncertainties);
        var fault = Assert.Single(invalid.Faults);
        Assert.Equal(("map.steps[1].lane", FileErrorCodes.UnknownLane, (object?)"c9"), (fault.Path, fault.Code, fault.Values["lane"]));
        Assert.Equal("{\"process\":1}", invalid.Content);
        // A name that gives no code has none; the person types one.
        Assert.Equal(("RH", (string?)null), (processes[2].Name, processes[2].ProposedCode));
        Assert.NotNull(processes[2].Counts);
    }

    [Fact]
    public async Task A_process_in_fault_gives_the_name_the_file_writes_and_none_when_the_reader_gives_none()
    {
        var format = new CollectionFormat(Invalid(0, "Demande d'achat"), Invalid(1), Valid(2, null, "Achats"));

        var processes = await PreviewAsync(Administrator(false), format);

        Assert.Equal(["Demande d'achat", null, "Achats"], processes.Select(process => process.Name));
        Assert.Null(processes[0].ProposedCode);
        Assert.Null(processes[0].Counts);
        Assert.NotEmpty(processes[0].Faults);
    }

    [Fact]
    public async Task Each_line_says_how_many_addresses_of_its_documents_reach_another_machine()
    {
        // A file received from outside may carry a network path or a web address: told before a link is followed (pre-audit 68, P3-02).
        var remote = Valid(0, "ACHAT", "Demande d'achat") with { Document = new Document("ACHAT", "Demande d'achat", []) { RemoteLocations = 2 } };
        var processes = await PreviewAsync(Administrator(false), new CollectionFormat(remote, Valid(1, "RECEP", "Réception"), Invalid(2)));

        Assert.Equal([2, 0, 0], processes.Select(process => process.RemoteLocations));
    }

    [Fact]
    public async Task The_preview_writes_nothing()
    {
        var fixture = Administrator(true, "ACHAT");
        var before = fixture.Database.All<IRecord>().Count();
        var events = fixture.Database.All<AuditEvent>().Count();

        var processes = await PreviewAsync(fixture, new CollectionFormat(Valid(0, "ACHAT", "Un"), Valid(1, null, "Livres"), Invalid(2)));

        Assert.Equal(["ACHATA", "LIVRES", null], processes.Select(process => process.ProposedCode));
        Assert.Equal(0, fixture.Database.Commits);
        Assert.DoesNotContain("save", fixture.Database.Calls);
        Assert.DoesNotContain("lock", fixture.Database.Calls);
        Assert.Empty(fixture.Database.Entries);
        Assert.Equal(events, fixture.Database.All<AuditEvent>().Count());
        Assert.Equal(before, fixture.Database.All<IRecord>().Count());
        // Previewed again, the same codes: nothing was kept of the first preview.
        Assert.Equal(["ACHATA", "LIVRES", null], (await PreviewAsync(fixture, new CollectionFormat(Valid(0, "ACHAT", "Un"), Valid(1, null, "Livres"), Invalid(2))))
            .Select(process => process.ProposedCode));
    }

    [Fact]
    public async Task A_user_who_is_not_administrator_is_refused_before_the_collection_is_read()
    {
        var fixture = Administrator(false);
        var format = new CollectionFormat(Valid(0, "ACHAT", "Un"));
        foreach (var user in new[] { fixture.Project.Facilitator, fixture.Project.Contributor, fixture.Project.Reader, fixture.Project.Stranger })
        {
            fixture.ActingAs(user);
            Assert.Equal(ErrorCodes.AccessDenied, (await Assert.ThrowsAsync<DomainException>(() =>
                fixture.Runner.RunAsync(new PreviewCollectionQuery(format, "collection"), Token))).Code);
        }
        Assert.Empty(format.Reads);
        Assert.Empty(fixture.Database.Calls);
    }

    /// <summary>The bound of the opening of a file, written here as the specification gives it (8 million characters), not read from the code.</summary>
    [Fact]
    public async Task A_content_longer_than_8_million_characters_is_refused_before_it_is_read()
    {
        var fixture = Administrator(false);
        var format = new CollectionFormat(Valid(0, "ACHAT", "Un"));

        var refusal = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Runner.RunAsync(new PreviewCollectionQuery(format, new string(' ', (8 * 1024 * 1024) + 1)), Token));

        Assert.Equal((ErrorCodes.FileTooLarge, (object?)8), (refusal.Code, refusal.Details["max"]));
        Assert.Empty(format.Reads);
        await fixture.Runner.RunAsync(new PreviewCollectionQuery(format, new string(' ', 8 * 1024 * 1024)), Token);
        Assert.Equal(8 * 1024 * 1024, format.Reads[^1].Content.Length);
        await fixture.Runner.RunAsync(new PreviewCollectionQuery(format, null), Token);
        Assert.Equal(string.Empty, format.Reads[^1].Content);
    }
}
