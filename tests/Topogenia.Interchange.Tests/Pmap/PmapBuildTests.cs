// PmapBuildTests.cs
// A project built from a .pmap document (A-2.17-38 to 42, AR-93): the rules of the domain, codes in reading order, sheets as drafts.

using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;
using Topogenia.Interchange.Pmap;
using static Topogenia.Interchange.Tests.Pmap.PmapReaderTests;

namespace Topogenia.Interchange.Tests.Pmap;

public sealed class PmapBuildTests
{
    public static readonly Guid Creator = Guid.Parse("00000000-0000-0000-0000-0000000000c7");

    private static int next;

    public static OperationContext Context(string raci = "ACI") =>
        new(Creator, new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc), "fr", AppSettings.Default with { RaciLetters = raci },
            () => new Guid($"30000000-0000-0000-0000-{Interlocked.Increment(ref next):x12}"));

    public static ProjectCreationInputs Inputs(params ExistingProjectCode[] codes) =>
        new(codes, Languages, "fr", new PeopleLookup([], new Dictionary<string, DirectoryEntry>(), []));

    /// <summary>The project built from a document, as a workspace of its records.</summary>
    public static (PmapBuilt Built, ProjectWorkspace Workspace) Build(PmapDocument document, string? code = null, string? name = null)
    {
        var built = PmapBuild.Run(Context(), document, code ?? document.Project.Code ?? "PRJ", name ?? document.Project.Name, Inputs());
        var workspace = new ProjectWorkspace(built.Project.Id, [built.Project, .. built.Changes.Changes.Select(change => change.After!)]);
        return (built, workspace);
    }

    [Fact]
    public void The_complete_sample_builds_its_maps_lanes_steps_flows_raci_translations_and_counts()
    {
        var (built, workspace) = Build(Read(Sample("cloture-mensuelle.pmap")));

        Assert.Equal(new PmapCounts(2, 5, 11, 10, 2), built.Counts);
        Assert.Equal(("VTE", "Clôture mensuelle des ventes", "fr"), (built.Project.Code, built.Project.Name, built.Project.SourceLanguage));
        var root = workspace.RootMap;
        Assert.Equal(["VTE-C01", "VTE-C02", "VTE-C03", "VTE-C04"], workspace.LanesOf(root.Id).Select(lane => lane.Code));
        Assert.Equal(["Contrôle de gestion Nord", "ERP", "Contrôle de gestion Sud", "Direction"], workspace.LanesOf(root.Id).Select(lane => lane.Name));
        Assert.Equal([LaneType.Department, LaneType.System, LaneType.Department, LaneType.Person], workspace.LanesOf(root.Id).Select(lane => lane.LaneType));
        var table = workspace.StepsOf(root.Id).Single(step => step.Label == "Construire le tableau croisé");
        Assert.Equal(("VTE-E003", 2), (table.Code, table.Column));
        Assert.Equal(AutomationStatus.Automated, workspace.StepsOf(root.Id).Single(step => step.Label.StartsWith("Extraire", StringComparison.Ordinal)).AutomationStatus);
        var raci = workspace.All<StepRaci>().Where(role => role.StepId == table.Id).OrderBy(role => role.Role).Select(role => (workspace.Lane(role.LaneId)!.Name, role.Role));
        Assert.Equal([("Direction", RaciRole.A), ("Contrôle de gestion Sud", RaciRole.I)], raci);
        var sub = workspace.SubMapOf(table.Id)!;
        Assert.Equal((table.Label, 3), (sub.Name, workspace.StepsOf(sub.Id).Count));
        Assert.Equal(["Contrôle de gestion Nord"], workspace.LanesOf(sub.Id).Select(lane => lane.Name));
        Assert.Equal("Build the pivot table", workspace.Find<Translation>(Translation.KeyOf(TranslationObject.Step, table.Id, TranslationField.Label, "en"))!.Text);
        var yes = workspace.All<Flow>().Single(flow => flow.Label == "oui");
        Assert.Equal("yes", workspace.Find<Translation>(Translation.KeyOf(TranslationObject.Flow, yes.Id, TranslationField.Label, "en"))!.Text);
        Assert.Equal("Controlling North", workspace.Find<Translation>(Translation.KeyOf(TranslationObject.Lane, workspace.LanesOf(root.Id)[0].Id, TranslationField.Name, "en"))!.Text);
        Assert.Equal("Monthly sales closing", workspace.Find<Translation>(Translation.KeyOf(TranslationObject.Project, built.Project.Id, TranslationField.Name, "en"))!.Text);
    }

    [Fact]
    public void Codes_follow_the_reading_order_column_lane_row_whatever_the_order_of_the_file()
    {
        var document = Read(Doc($$"""
            { "lanes": [{{TwoLanes}}], "steps": [{ "id": "d", "lane": "c2", "label": "D", "column": 1 }, { "id": "c", "lane": "c1", "label": "C", "column": 1, "row": 1 },
              { "id": "b", "lane": "c1", "label": "B", "column": 1 }, { "id": "a", "lane": "c2", "label": "A", "column": 0 }] }
            """));

        var (_, workspace) = Build(document);

        Assert.Equal([("PRJ-E001", "A"), ("PRJ-E002", "B"), ("PRJ-E003", "C"), ("PRJ-E004", "D")],
            workspace.All<Step>().OrderBy(step => step.Code, StringComparer.Ordinal).Select(step => (step.Code, step.Label)));
    }

    [Fact]
    public void Without_columns_the_steps_take_the_automatic_placement()
    {
        var (_, workspace) = Build(Read(Sample("minimal.pmap"), "ACI"));

        Assert.Equal([("Besoin identifié", 0), ("Saisir la demande", 1), ("Passer la commande", 2), ("Commande envoyée", 3)],
            workspace.All<Step>().OrderBy(step => step.Column).Select(step => (step.Label, step.Column)));
    }

    [Fact]
    public void The_creator_is_the_only_facilitator_and_the_code_and_name_entered_win_a_new_name_losing_the_translations()
    {
        var document = Read(Sample("cloture-mensuelle.pmap"));

        var (built, workspace) = Build(document, code: "ACH", name: "Achats");

        Assert.Equal(("ACH", "Achats"), (built.Project.Code, built.Project.Name));
        Assert.Equal([(Creator, MemberRole.Facilitator)], workspace.All<ProjectMember>().Select(member => (member.UserId, member.Role)));
        Assert.DoesNotContain(workspace.All<Translation>(), translation => translation.ObjectType is TranslationObject.Project or TranslationObject.Map);
        Assert.All(workspace.All<Lane>(), lane => Assert.StartsWith("ACH-C", lane.Code, StringComparison.Ordinal));
        Assert.Equal("Achats", workspace.RootMap.Name);
    }

    [Fact]
    public void A_code_in_use_or_malformed_is_refused_by_the_rules_of_the_creation()
    {
        var document = Read(Sample("minimal.pmap"));

        var taken = Assert.Throws<DomainException>(() => PmapBuild.Run(Context(), document, "VTE", "X", Inputs(new ExistingProjectCode("VTE", false))));
        var trashed = Assert.Throws<DomainException>(() => PmapBuild.Run(Context(), document, "VTE", "X", Inputs(new ExistingProjectCode("VTE", true))));
        var malformed = Assert.Throws<DomainException>(() => PmapBuild.Run(Context(), document, "", "X", Inputs()));

        Assert.Equal([ErrorCodes.ProjectCodeTaken, ErrorCodes.ProjectCodeInTrash, ErrorCodes.InvalidProjectCode], new[] { taken.Code, trashed.Code, malformed.Code });
    }

    [Fact]
    public void A_sheet_becomes_a_draft_never_validated_with_its_documents_uses_and_the_pain_point_on_the_producing_step()
    {
        var (_, workspace) = Build(Read(Sample("cloture-mensuelle.pmap")));

        // The block of the step that only uses an output holds no field of the form: no sheet record, its use only.
        var sheets = workspace.All<StepSheet>().ToList();
        Assert.Single(sheets);
        Assert.All(sheets, sheet => Assert.Equal(SheetStatus.Draft, sheet.Status));
        Assert.Empty(workspace.All<StepSheetVersion>());
        var table = workspace.All<Step>().Single(step => step.Label == "Construire le tableau croisé");
        var sheet = sheets.Single(item => item.StepId == table.Id);
        Assert.Equal((Frequency.MonthEndClose, 90m, "Tableur"), (sheet.Frequency!.Value, sheet.DurationMinutes!.Value, sheet.Tools));
        Assert.Equal([(1, "Ouvrir l'extraction du mois", OperationNature.Extract), (2, "Recopier les totaux par région", OperationNature.CopyPaste), (3, "Vérifier le total général", OperationNature.Check)],
            workspace.All<SheetOperation>().Where(operation => operation.SheetId == sheet.Id).OrderBy(operation => operation.Position)
                .Select(operation => (operation.Position, operation.Text, operation.Nature!.Value)));
        var output = Assert.Single(workspace.All<Document>());
        Assert.Equal((table.Id, DocumentDirection.Output, LocationType.Url), (output.StepId, output.Direction, output.LocationType!.Value));
        var use = Assert.Single(workspace.All<DocumentUse>());
        var user = workspace.All<Step>().Single(step => step.Label == "Compléter le fichier du mois");
        Assert.Equal((user.Id, 24m, 80), (use.StepId, use.WaitHours!.Value, use.PercentCompleteAccurate!.Value));
        var painPoint = Assert.Single(workspace.All<PainPoint>());
        Assert.Equal((table.Id, "VTE-I001", 3m), (painPoint.StepId!.Value, painPoint.Code, painPoint.LostHoursPerMonth!.Value));
        Assert.Equal(painPoint.Id, use.PainPointId);
    }

    [Fact]
    public void The_built_project_breaks_no_rule_of_the_structure()
    {
        var (_, workspace) = Build(Read(Sample("cloture-mensuelle.pmap")));

        Assert.Empty(ImportedProjects.Violations(workspace));
        Assert.Empty(StructureInvariants.Violations(workspace));
    }
}
