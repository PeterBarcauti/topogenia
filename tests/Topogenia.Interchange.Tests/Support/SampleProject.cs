// SampleProject.cs
// A fictional project, a leave request (CONGES), that uses every record the exchange package carries, and some it leaves out.

using Topogenia.Domain.Dissolutions;
using Topogenia.Domain.Model;
using Topogenia.Domain.Sheets;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Interchange.Tests.Support;

/// <summary>
/// A fictional project built from records: a root map with four lanes, a decision with two outputs, a macro step and its
/// detail (with an AND gateway and its two branches, one in progress on row 1, an inclusive OR gateway and a wait), an empty sub-map, an open frame of a dissolution with its remainder (document, pain point, sheet), sheets
/// (draft, validated twice, needs review), documents and uses, pain points and their history, RACI roles (the hidden S
/// included), members, manual and automatic translations. And what the package must leave out: a deleted step and lane
/// with what hangs on them, a closed frame, deletion batches and an operation log entry. Identifiers are predictable;
/// codes are those of the counters.
/// </summary>
public sealed class SampleProject
{
    public static readonly DateTime Start = new(2026, 9, 1, 7, 30, 0, DateTimeKind.Utc);

    private int next = 0x100;
    private readonly List<IRecord> records = [];

    private readonly bool valueStream;

    /// <summary>
    /// The fictional project; without <paramref name="valueStream"/>, none of the entries of the value stream (ADR 085) and
    /// the work time by default: what a package 2.0 could carry.
    /// </summary>
    public SampleProject(bool valueStream = true)
    {
        this.valueStream = valueStream;
        Project = Id();
        Alice = User("EXEMPLE\\alice", "Alice Martin");
        Bruno = User("EXEMPLE\\bruno", "Bruno Lefort");
        Chloe = User("EXEMPLE\\chloe", "Chloé Duval");
        Denis = User("EXEMPLE\\denis", "Denis Morel");
        records.Add(new Project(Project, "CONGES", "Demande de congés", "fr", "Du besoin du salarié à l'enregistrement en paie.",
            "\\\\partage-fictif\\conges", Classification.Confidential, "Équipe RH",
            "congé payé = paid leave\nsolde de congés = leave balance", 12, At(0), Alice, At(40), Bruno, null, null, 7)
        {
            WorkdayHours = valueStream ? 7.5m : WorkTime.DefaultWorkdayHours,
        });
        Members();
        RootMap = Map(null, "Demande de congés", Alice, 1);
        Root();
        Detail();
        EmptySubMap = Map(Steps["E004"], "Valider la demande", Bruno, 30);
        Frame();
        Content();
        Translations();
        Trash();
        foreach (var (kind, value) in new[] { (CounterKind.Step, 23), (CounterKind.Lane, 7), (CounterKind.PainPoint, 4), (CounterKind.Solution, 0) })
            records.Add(new ProjectCounter(Project, kind, value));
    }

    public Guid Project { get; }

    public Guid Alice { get; }

    public Guid Bruno { get; }

    public Guid Chloe { get; }

    /// <summary>A former member: author of records, no longer a member of the project.</summary>
    public Guid Denis { get; }

    public Guid RootMap { get; }

    public Guid DetailMap { get; private set; }

    public Guid EmptySubMap { get; }

    public Dictionary<string, Guid> Steps { get; } = [];

    public Dictionary<string, Guid> Lanes { get; } = [];

    public IReadOnlyList<IRecord> Records => records;

    /// <summary>The login of every user of the sample, as the application would read them.</summary>
    public IReadOnlyDictionary<Guid, string> Logins =>
        records.OfType<AppUser>().ToDictionary(user => user.Id, user => user.Login);

    public ProjectWorkspace Workspace() => new(Project, records);

    /// <summary>The same records given in another order: the package must not depend on it.</summary>
    public ProjectWorkspace ShuffledWorkspace() => new(Project, records.AsEnumerable().Reverse().OrderBy(record => record.GetType().Name, StringComparer.Ordinal));

    public SampleProject With(Func<IRecord, IRecord> change)
    {
        for (var i = 0; i < records.Count; i++)
            records[i] = change(records[i]);
        return this;
    }

    public SampleProject Add(IRecord record)
    {
        records.Add(record);
        return this;
    }

    public static DateTime At(int minutes) => Start.AddMinutes(minutes);

    private Guid Id() => new($"00000000-0000-0000-0000-{next++:x12}");

    private Guid User(string login, string name)
    {
        var id = Id();
        records.Add(new AppUser(id, login, login.ToUpperInvariant(), name, null, "fr", false, true, null, At(-100), null, null, 1));
        return id;
    }

    private void Members()
    {
        records.Add(new ProjectMember(Project, Alice, MemberRole.Facilitator, true, false, 0, At(0), Alice, null, null));
        records.Add(new ProjectMember(Project, Bruno, MemberRole.Contributor, false, true, 1, At(2), Alice, At(41), Alice) { Version = 3 });
        records.Add(new ProjectMember(Project, Chloe, MemberRole.Reader, false, false, 2, At(3), Alice, null, null));
    }

    private Guid Map(Guid? parent, string name, Guid author, int minute)
    {
        var id = Id();
        records.Add(new ProcessMap(id, Project, parent, name, "fr", At(minute), author, null, null, null, null, 1));
        return id;
    }

    private Guid Lane(Guid map, string number, string name, LaneType type, int order, Guid? actor = null, Guid? source = null,
        DateTime? deletedAt = null, Guid? batch = null)
    {
        var id = Id();
        Lanes[number] = id;
        records.Add(new Lane(id, map, "CONGES-" + number, name, "fr", type, actor, LaneColors.ForIndex(order), order, source,
            At(order + 1), Alice, null, null, deletedAt, batch, 1));
        return id;
    }

    private Guid Step(string number, string lane, int column, string label, StepType type = StepType.Task, int row = 0,
        Guid? assignee = null, AutomationStatus automation = AutomationStatus.Manual, DateTime? deletedAt = null, Guid? batch = null)
    {
        var id = Id();
        Steps[number] = id;
        var map = records.OfType<Lane>().Single(item => item.Id == Lanes[lane]).MapId;
        records.Add(new Step(id, map, Lanes[lane], "CONGES-" + number, label, "fr", type, column, row, assignee, automation, null,
            At(10 + column), Bruno, At(50 + column), Alice, deletedAt, batch, 2));
        return id;
    }

    /// <summary>Changes a record already added, kept at its place in the list.</summary>
    private void Change<T>(Guid id, Func<T, T> change)
        where T : IRecord
    {
        var index = records.FindIndex(record => record is T && record.Key == RecordKeyOf<T>(id));
        records[index] = change((T)records[index]);
    }

    private static RecordKey RecordKeyOf<T>(Guid id) => typeof(T) == typeof(Lane) ? Domain.Model.Lane.KeyOf(id)
        : typeof(T) == typeof(Step) ? Domain.Model.Step.KeyOf(id) : Domain.Model.Flow.KeyOf(id);

    private Guid Flow(string source, string target, string? label = null, DateTime? deletedAt = null, Guid? batch = null)
    {
        var id = Id();
        var map = records.OfType<Step>().Single(step => step.Id == Steps[source]).MapId;
        records.Add(new Flow(id, map, Steps[source], Steps[target], label, At(20), Bruno, null, null, deletedAt, batch, 1));
        return id;
    }

    private void Root()
    {
        Lane(RootMap, "C01", "Salarié", LaneType.Person, 0, actor: Chloe);
        Lane(RootMap, "C02", "Manager", LaneType.Person, 1);
        Lane(RootMap, "C03", "Ressources humaines", LaneType.Department, 2);
        Lane(RootMap, "C04", "Logiciel de paie", LaneType.System, 3);
        Step("E001", "C01", 0, "Besoin de congés", StepType.Start);
        Step("E002", "C01", 1, "Saisir la demande");
        Step("E003", "C02", 2, "Demande conforme ?", StepType.Decision);
        Step("E004", "C02", 3, "Valider la demande", assignee: Bruno);
        Step("E008", "C01", 3, "Corriger la demande", row: 1);
        Step("E006", "C04", 7, "Enregistrer dans la paie", automation: AutomationStatus.Automated);
        Step("E007", "C01", 8, "Congés posés", StepType.End);
        Flow("E001", "E002");
        Flow("E002", "E003");
        var yes = Flow("E003", "E004", "oui");
        Flow("E003", "E008", "non");
        if (valueStream)
        {
            // The value stream (ADR 085): 40 requests a month enter, 90 % are accepted; the managers and the human resources
            // have their headcount.
            Change<Flow>(yes, flow => flow with { SharePercent = 90m });
            Change<Step>(Steps["E001"], step => step with { VolumeQuantity = 40m, VolumePeriod = VolumePeriod.Month });
            Change<Lane>(Lanes["C02"], lane => lane with { Headcount = 12 });
            Change<Lane>(Lanes["C03"], lane => lane with { Headcount = 3 });
        }
        Flow("E008", "E002");
        Flow("E006", "E007");
        records.Add(new StepRaci(Steps["E004"], Lanes["C03"], RaciRole.C, At(30), Bruno));
        records.Add(new StepRaci(Steps["E004"], Lanes["C01"], RaciRole.I, At(31), Bruno) { Version = 4, UpdatedAt = At(42), UpdatedBy = Alice });
        records.Add(new StepRaci(Steps["E004"], Lanes["C04"], RaciRole.S, At(32), Denis));
        records.Add(new StepRaci(Steps["E006"], Lanes["C03"], RaciRole.A, At(33), Bruno));
    }

    private void Detail()
    {
        DetailMap = Map(Steps["E002"], "Saisir la demande", Bruno, 25);
        Lane(DetailMap, "C05", "Salarié", LaneType.Person, 0, actor: Chloe, source: Lanes["C01"]);
        Step("E009", "C05", 0, "Début de saisie", StepType.Start);
        Step("E010", "C05", 1, "Remplir le formulaire");
        Step("E017", "C05", 2, "En même temps", StepType.Parallel);
        Step("E018", "C05", 3, "Joindre le justificatif");
        Step("E019", "C05", 3, "Prévenir l'équipe", row: 1, automation: AutomationStatus.InProgress);
        Step("E020", "C05", 4, "Fin des tâches simultanées", StepType.Parallel);
        Step("E021", "C05", 5, "Pièces à fournir ?", StepType.Inclusive);
        Step("E022", "C05", 6, "Attendre l'accusé de réception", StepType.Wait);
        Step("E023", "C05", 7, "Fin des pièces", StepType.Inclusive);
        Step("E011", "C05", 8, "Demande envoyée", StepType.End);
        Flow("E009", "E010");
        Flow("E010", "E017");
        Flow("E017", "E018");
        Flow("E017", "E019");
        Flow("E018", "E020");
        Flow("E019", "E020");
        Flow("E020", "E021");
        Flow("E021", "E022", "justificatif");
        Flow("E021", "E023", "aucun");
        Flow("E022", "E023");
        Flow("E023", "E011");
    }

    /// <summary>
    /// The open frame on the root map: E012, the check of the balances, was dissolved; its steps E013 and E014 are in the
    /// flow, with the flows and the lane C06 the dissolution created; its remainder holds a document, a pain point and a sheet.
    /// </summary>
    private void Frame()
    {
        var batch = Id();
        records.Add(new DeletionBatch(batch, Project, null, At(60), Bruno, null, null));
        var dissolved = Step("E012", "C03", 4, "Contrôler les soldes", deletedAt: At(60), batch: batch);
        Lane(RootMap, "C06", "Contrôle de gestion", LaneType.Department, 4);
        Step("E013", "C03", 4, "Lire le solde");
        Step("E014", "C06", 5, "Comparer à la demande");
        var detailLane = Id();
        var created = Flow("E013", "E014");
        var entry = Flow("E004", "E013");
        var exit = Flow("E014", "E006");
        var frame = Id();
        records.Add(new Dissolution(frame, RootMap, dissolved, batch, At(60), Bruno, null, null) { Version = 2, UpdatedAt = At(61), UpdatedBy = Bruno });
        records.Add(new DissolutionStep(frame, Steps["E013"], detailLane, 1, 0));
        records.Add(new DissolutionStep(frame, Steps["E014"], detailLane, 2, 0));
        foreach (var flow in new[] { created, entry, exit })
            records.Add(new DissolutionObject(frame, DissolutionObjectTypes.Flow, flow));
        records.Add(new DissolutionObject(frame, DissolutionObjectTypes.Lane, Lanes["C06"]));
        records.Add(new Document(Id(), dissolved, DocumentDirection.Output, "Relevé des soldes", null, null, null, false, "fr", At(15), Bruno, null, null, 1));
        records.Add(new PainPoint(Id(), Project, dissolved, "CONGES-I002", "Soldes recalculés à la main", "fr", 2m, PainPointStatus.Reported,
            At(16), Bruno, null, null, 1));
        records.Add(new StepSheet(Id(), dissolved, SheetStatus.Draft, "Gestionnaire RH", null, null, valueStream ? Frequency.PerCase : null, null, null, "fr", null,
            At(17), Bruno, null, null, 1));
    }

    private void Content()
    {
        var validated = Id();
        records.Add(new StepSheet(validated, Steps["E004"], SheetStatus.Validated, "Manager direct", "Accorder ou refuser la demande",
            "Messagerie", Frequency.Weekly, 15.50m, "Vérifier le planning de l'équipe avant de répondre.\nRépondre sous 48 h.", "fr", null,
            At(70), Bruno, At(75), Alice, 5)
        {
            LeadTime = valueStream ? 1m : null,
            LeadTimeUnit = valueStream ? LeadTimeUnit.Day : null,
        });
        records.Add(new SheetOperation(validated, 1, "Ouvrir la demande", "Messagerie", OperationNature.Check));
        records.Add(new SheetOperation(validated, 2, "Consulter le planning", null, null));
        records.Add(new StepSheetVersion(Id(), validated, 2, SheetJson.Freeze(new SheetContent(null, "Accorder ou refuser", null, Frequency.Weekly, 15m,
            [new ProcedureTask("Ouvrir la demande", "Messagerie", OperationNature.Check)], null) { LeadTime = valueStream ? new LeadTime(2m, LeadTimeUnit.Hour) : null }),
            Frequency.Weekly, 15m, Alice, At(74)) { LeadTime = valueStream ? 2m : null, LeadTimeUnit = valueStream ? LeadTimeUnit.Hour : null });
        records.Add(new StepSheetVersion(Id(), validated, 1, "{\"fields\":{},\"procedure\":[{\"text\":\"Ouvrir\"}],\"filled_count\":1}",
            null, null, Denis, At(72)));
        var review = Id();
        records.Add(new StepSheet(review, Steps["E010"], SheetStatus.NeedsReview, null, "Décrire la demande", null, Frequency.Occasional, 5m, null,
            "fr", "Préciser le délai de prévenance", At(76), Chloe, null, null, 2));
        records.Add(new SheetOperation(review, 1, "Choisir les dates", "Formulaire", OperationNature.ReEnter));
        var form = Id();
        records.Add(new Document(form, Steps["E002"], DocumentDirection.Output, "Formulaire de demande", "https://exemple.invalid/formulaire",
            LocationType.Url, null, true, "fr", At(80), Chloe, At(81), Bruno, 2));
        records.Add(new Document(Id(), Steps["E010"], DocumentDirection.Input, "Planning d'équipe", "\\\\partage-fictif\\planning.xlsx",
            LocationType.Path, 2.0m, false, "fr", At(80), Chloe, null, null, 1));
        var relances = Id();
        records.Add(new PainPoint(relances, Project, Steps["E004"], "CONGES-I001", "Relances multiples du manager", "fr", 3.50m, PainPointStatus.Scored,
            At(82), Chloe, At(83), Alice, 2));
        records.Add(new PainPointStatusChange(Id(), relances, PainPointStatus.Reported, PainPointStatus.Scored, "Chiffré en atelier", Alice, At(83)));
        records.Add(new PainPointStatusChange(Id(), relances, null, PainPointStatus.Reported, null, Chloe, At(82)));
        records.Add(new PainPoint(Id(), Project, null, "CONGES-I003", "Aucun outil unique pour les absences", "fr", null, PainPointStatus.UnderReview,
            At(84), Denis, null, null, 1));
        records.Add(new DocumentUse(form, Steps["E004"], 4m, 80, relances, At(85), Bruno, At(86), Bruno) { Version = 2 });
        records.Add(new DocumentUse(form, Steps["E003"], null, null, null, At(85), Bruno, null, null));
    }

    private void Translations()
    {
        void Add(TranslationObject type, Guid id, TranslationField field, string text, TranslationSource source, Guid? author) =>
            records.Add(new Translation(type, id, field, "en", text, source, At(90), author));
        Add(TranslationObject.Project, Project, TranslationField.Name, "Leave request", TranslationSource.Manual, Alice);
        Add(TranslationObject.Map, RootMap, TranslationField.Name, "Leave request", TranslationSource.Auto, null);
        Add(TranslationObject.Map, DetailMap, TranslationField.Name, "Enter the request", TranslationSource.Auto, null);
        Add(TranslationObject.Lane, Lanes["C01"], TranslationField.Name, "Employee", TranslationSource.Manual, Bruno);
        Add(TranslationObject.Step, Steps["E002"], TranslationField.Label, "Enter the request", TranslationSource.Auto, null);
        Add(TranslationObject.Step, Steps["E012"], TranslationField.Label, "Check the balances", TranslationSource.Manual, Bruno);
        var yes = records.OfType<Flow>().Single(flow => flow.Label == "oui").Id;
        Add(TranslationObject.Flow, yes, TranslationField.Label, "yes", TranslationSource.Manual, Bruno);
    }

    /// <summary>What the package leaves out: a deleted lane, a deleted step and what hangs on it, a closed frame, the log.</summary>
    private void Trash()
    {
        var batch = Id();
        records.Add(new DeletionBatch(batch, Project, Id(), At(95), Alice, null, null));
        Lane(RootMap, "C07", "Prestataire", LaneType.Department, 5, deletedAt: At(95), batch: batch);
        var deleted = Step("E015", "C07", 6, "Archiver le papier", deletedAt: At(95), batch: batch);
        records.Add(new Flow(Id(), RootMap, Steps["E004"], deleted, "archive", At(20), Bruno, null, null, At(95), batch, 1));
        records.Add(new StepSheet(Id(), deleted, SheetStatus.Draft, "Archiviste", null, null, null, null, null, "fr", null, At(18), Bruno, null, null, 1));
        records.Add(new Document(Id(), deleted, DocumentDirection.Output, "Bordereau d'archive", null, null, null, false, "fr", At(19), Bruno, null, null, 1));
        records.Add(new PainPoint(Id(), Project, deleted, "CONGES-I004", "Papier égaré", "fr", null, PainPointStatus.Dropped, At(19), Bruno, null, null, 1));
        records.Add(new Translation(TranslationObject.Step, deleted, TranslationField.Label, "en", "Archive the paper", TranslationSource.Manual, At(90), Bruno));
        records.Add(new StepRaci(Steps["E004"], Lanes["C07"], RaciRole.C, At(34), Bruno));
        var closedBatch = Id();
        records.Add(new DeletionBatch(closedBatch, Project, null, At(96), Bruno, null, null));
        var closedStep = Step("E016", "C05", 3, "Joindre un justificatif", deletedAt: At(96), batch: closedBatch);
        records.Add(new Dissolution(Id(), DetailMap, closedStep, closedBatch, At(96), Bruno, At(97), Bruno));
        records.Add(new OperationLogEntry(Id(), Project, RootMap, Bruno, "step.create", 1, "{}", "{}", "{}", At(98), null, null));
    }
}
