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
/// codes are those of the counters. Written in French (CONGES), with its English translations; or written in English
/// (LEAVE), the sample of an interface in another language than French, with its French translations
/// (<see cref="SampleProjectEnglish"/>).
/// </summary>
public sealed class SampleProject
{
    public static readonly DateTime Start = new(2026, 9, 1, 7, 30, 0, DateTimeKind.Utc);

    private int next = 0x100;
    private readonly List<IRecord> records = [];

    private readonly bool valueStream;

    private readonly bool english;

    /// <summary>
    /// The fictional project; without <paramref name="valueStream"/>, none of the entries of the value stream (ADR 085) and
    /// the work time by default: what a package 2.0 could carry. With <paramref name="english"/>, every text in English.
    /// </summary>
    public SampleProject(bool valueStream = true, bool english = false)
    {
        this.valueStream = valueStream;
        this.english = english;
        Project = Id();
        Alice = User("EXEMPLE\\alice", "Alice Martin");
        Bruno = User("EXEMPLE\\bruno", "Bruno Lefort");
        Chloe = User("EXEMPLE\\chloe", "Chloé Duval");
        Denis = User("EXEMPLE\\denis", "Denis Morel");
        records.Add(new Project(Project, Prefix, T("Demande de congés"), Language, T("Du besoin du salarié à l'enregistrement en paie."),
            T("\\\\partage-fictif\\conges"), Classification.Confidential, T("Équipe RH"),
            T("congé payé = paid leave\nsolde de congés = leave balance"), 12, At(0), Alice, At(40), Bruno, null, null, 7)
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

    /// <summary>The code of the project, the start of every code of its objects.</summary>
    public string Prefix => english ? "LEAVE" : "CONGES";

    /// <summary>The language the project is written in.</summary>
    public string Language => english ? "en" : "fr";

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

    /// <summary>A text of the sample, in the language it is written in.</summary>
    private string T(string french) => english ? SampleProjectEnglish.Of(french) : french;

    /// <summary>A text of the sample in the language of its translations: English for the French sample, French for the English one.</summary>
    private string Other(string french) => english ? french : SampleProjectEnglish.Of(french);

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
        records.Add(new ProcessMap(id, Project, parent, T(name), Language, At(minute), author, null, null, null, null, 1));
        return id;
    }

    private Guid Lane(Guid map, string number, string name, LaneType type, int order, Guid? actor = null, Guid? source = null,
        DateTime? deletedAt = null, Guid? batch = null)
    {
        var id = Id();
        Lanes[number] = id;
        records.Add(new Lane(id, map, $"{Prefix}-{number}", T(name), Language, type, actor, LaneColors.ForIndex(order), order, source,
            At(order + 1), Alice, null, null, deletedAt, batch, 1));
        return id;
    }

    private Guid Step(string number, string lane, int column, string label, StepType type = StepType.Task, int row = 0,
        Guid? assignee = null, AutomationStatus automation = AutomationStatus.Manual, DateTime? deletedAt = null, Guid? batch = null)
    {
        var id = Id();
        Steps[number] = id;
        var map = records.OfType<Lane>().Single(item => item.Id == Lanes[lane]).MapId;
        records.Add(new Step(id, map, Lanes[lane], $"{Prefix}-{number}", T(label), Language, type, column, row, assignee, automation, null,
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
        records.Add(new Flow(id, map, Steps[source], Steps[target], label is null ? null : T(label), At(20), Bruno, null, null, deletedAt, batch, 1));
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
        records.Add(new Document(Id(), dissolved, DocumentDirection.Output, T("Relevé des soldes"), null, null, null, false, Language, At(15), Bruno, null, null, 1));
        records.Add(new PainPoint(Id(), Project, dissolved, $"{Prefix}-I002", T("Soldes recalculés à la main"), Language, 2m, PainPointStatus.Reported,
            At(16), Bruno, null, null, 1));
        records.Add(new StepSheet(Id(), dissolved, SheetStatus.Draft, T("Gestionnaire RH"), null, null, valueStream ? Frequency.PerCase : null, null, null, Language, null,
            At(17), Bruno, null, null, 1));
    }

    private void Content()
    {
        var validated = Id();
        records.Add(new StepSheet(validated, Steps["E004"], SheetStatus.Validated, T("Manager direct"), T("Accorder ou refuser la demande"),
            T("Messagerie"), Frequency.Weekly, 15.50m, T("Vérifier le planning de l'équipe avant de répondre.\nRépondre sous 48 h."), Language, null,
            At(70), Bruno, At(75), Alice, 5)
        {
            LeadTime = valueStream ? 1m : null,
            LeadTimeUnit = valueStream ? LeadTimeUnit.Day : null,
        });
        records.Add(new SheetOperation(validated, 1, T("Ouvrir la demande"), T("Messagerie"), OperationNature.Check));
        records.Add(new SheetOperation(validated, 2, T("Consulter le planning"), null, null));
        records.Add(new StepSheetVersion(Id(), validated, 2, SheetJson.Freeze(new SheetContent(null, T("Accorder ou refuser"), null, Frequency.Weekly, 15m,
            [new ProcedureTask(T("Ouvrir la demande"), T("Messagerie"), OperationNature.Check)], null) { LeadTime = valueStream ? new LeadTime(2m, LeadTimeUnit.Hour) : null }),
            Frequency.Weekly, 15m, Alice, At(74)) { LeadTime = valueStream ? 2m : null, LeadTimeUnit = valueStream ? LeadTimeUnit.Hour : null });
        records.Add(new StepSheetVersion(Id(), validated, 1, $"{{\"fields\":{{}},\"procedure\":[{{\"text\":\"{T("Ouvrir")}\"}}],\"filled_count\":1}}",
            null, null, Denis, At(72)));
        var review = Id();
        records.Add(new StepSheet(review, Steps["E010"], SheetStatus.NeedsReview, null, T("Décrire la demande"), null, Frequency.Occasional, 5m, null,
            Language, T("Préciser le délai de prévenance"), At(76), Chloe, null, null, 2));
        records.Add(new SheetOperation(review, 1, T("Choisir les dates"), T("Formulaire"), OperationNature.ReEnter));
        var form = Id();
        records.Add(new Document(form, Steps["E002"], DocumentDirection.Output, T("Formulaire de demande"), T("https://exemple.invalid/formulaire"),
            LocationType.Url, null, true, Language, At(80), Chloe, At(81), Bruno, 2));
        records.Add(new Document(Id(), Steps["E010"], DocumentDirection.Input, T("Planning d'équipe"), T("\\\\partage-fictif\\planning.xlsx"),
            LocationType.Path, 2.0m, false, Language, At(80), Chloe, null, null, 1));
        var relances = Id();
        records.Add(new PainPoint(relances, Project, Steps["E004"], $"{Prefix}-I001", T("Relances multiples du manager"), Language, 3.50m, PainPointStatus.Scored,
            At(82), Chloe, At(83), Alice, 2));
        records.Add(new PainPointStatusChange(Id(), relances, PainPointStatus.Reported, PainPointStatus.Scored, T("Chiffré en atelier"), Alice, At(83)));
        records.Add(new PainPointStatusChange(Id(), relances, null, PainPointStatus.Reported, null, Chloe, At(82)));
        records.Add(new PainPoint(Id(), Project, null, $"{Prefix}-I003", T("Aucun outil unique pour les absences"), Language, null, PainPointStatus.UnderReview,
            At(84), Denis, null, null, 1));
        records.Add(new DocumentUse(form, Steps["E004"], 4m, 80, relances, At(85), Bruno, At(86), Bruno) { Version = 2 });
        records.Add(new DocumentUse(form, Steps["E003"], null, null, null, At(85), Bruno, null, null));
    }

    /// <summary>
    /// The translations into the other language, a few manual, the others automatic: every live text, so that the sample
    /// shows translated under an interface in that language (ADR 083, half of the texts at least).
    /// </summary>
    private void Translations()
    {
        var language = english ? "fr" : "en";
        void Add(TranslationObject type, Guid id, TranslationField field, string french, TranslationSource source, Guid? author) =>
            records.Add(new Translation(type, id, field, language, Other(french), source, At(90), author));
        Add(TranslationObject.Project, Project, TranslationField.Name, "Demande de congés", TranslationSource.Manual, Alice);
        Add(TranslationObject.Map, RootMap, TranslationField.Name, "Demande de congés", TranslationSource.Auto, null);
        Add(TranslationObject.Map, DetailMap, TranslationField.Name, "Saisir la demande", TranslationSource.Auto, null);
        Add(TranslationObject.Lane, Lanes["C01"], TranslationField.Name, "Salarié", TranslationSource.Manual, Bruno);
        Add(TranslationObject.Step, Steps["E002"], TranslationField.Label, "Saisir la demande", TranslationSource.Auto, null);
        Add(TranslationObject.Step, Steps["E012"], TranslationField.Label, "Contrôler les soldes", TranslationSource.Manual, Bruno);
        Guid FlowLabelled(string french) => records.OfType<Flow>().Single(flow => flow.Label == T(french)).Id;
        Add(TranslationObject.Flow, FlowLabelled("oui"), TranslationField.Label, "oui", TranslationSource.Manual, Bruno);
        Add(TranslationObject.Map, EmptySubMap, TranslationField.Name, "Valider la demande", TranslationSource.Auto, null);
        foreach (var (lane, name) in new[] { ("C02", "Manager"), ("C03", "Ressources humaines"), ("C04", "Logiciel de paie"), ("C05", "Salarié"), ("C06", "Contrôle de gestion") })
            Add(TranslationObject.Lane, Lanes[lane], TranslationField.Name, name, TranslationSource.Auto, null);
        foreach (var (step, label) in new[]
        {
            ("E001", "Besoin de congés"), ("E003", "Demande conforme ?"), ("E004", "Valider la demande"), ("E008", "Corriger la demande"),
            ("E006", "Enregistrer dans la paie"), ("E007", "Congés posés"), ("E013", "Lire le solde"), ("E014", "Comparer à la demande"),
            ("E009", "Début de saisie"), ("E010", "Remplir le formulaire"), ("E017", "En même temps"), ("E018", "Joindre le justificatif"),
            ("E019", "Prévenir l'équipe"), ("E020", "Fin des tâches simultanées"), ("E021", "Pièces à fournir ?"), ("E022", "Attendre l'accusé de réception"),
            ("E023", "Fin des pièces"), ("E011", "Demande envoyée"),
        })
            Add(TranslationObject.Step, Steps[step], TranslationField.Label, label, TranslationSource.Auto, null);
        foreach (var label in new[] { "non", "justificatif", "aucun" })
            Add(TranslationObject.Flow, FlowLabelled(label), TranslationField.Label, label, TranslationSource.Auto, null);
    }

    /// <summary>What the package leaves out: a deleted lane, a deleted step and what hangs on it, a closed frame, the log.</summary>
    private void Trash()
    {
        var batch = Id();
        records.Add(new DeletionBatch(batch, Project, Id(), At(95), Alice, null, null));
        Lane(RootMap, "C07", "Prestataire", LaneType.Department, 5, deletedAt: At(95), batch: batch);
        var deleted = Step("E015", "C07", 6, "Archiver le papier", deletedAt: At(95), batch: batch);
        records.Add(new Flow(Id(), RootMap, Steps["E004"], deleted, "archive", At(20), Bruno, null, null, At(95), batch, 1));
        records.Add(new StepSheet(Id(), deleted, SheetStatus.Draft, T("Archiviste"), null, null, null, null, null, Language, null, At(18), Bruno, null, null, 1));
        records.Add(new Document(Id(), deleted, DocumentDirection.Output, T("Bordereau d'archive"), null, null, null, false, Language, At(19), Bruno, null, null, 1));
        records.Add(new PainPoint(Id(), Project, deleted, $"{Prefix}-I004", T("Papier égaré"), Language, null, PainPointStatus.Dropped, At(19), Bruno, null, null, 1));
        records.Add(new Translation(TranslationObject.Step, deleted, TranslationField.Label, english ? "fr" : "en", Other("Archiver le papier"), TranslationSource.Manual, At(90), Bruno));
        records.Add(new StepRaci(Steps["E004"], Lanes["C07"], RaciRole.C, At(34), Bruno));
        var closedBatch = Id();
        records.Add(new DeletionBatch(closedBatch, Project, null, At(96), Bruno, null, null));
        var closedStep = Step("E016", "C05", 3, "Joindre un justificatif", deletedAt: At(96), batch: closedBatch);
        records.Add(new Dissolution(Id(), DetailMap, closedStep, closedBatch, At(96), Bruno, At(97), Bruno));
        records.Add(new OperationLogEntry(Id(), Project, RootMap, Bruno, "step.create", 1, "{}", "{}", "{}", At(98), null, null));
    }
}
