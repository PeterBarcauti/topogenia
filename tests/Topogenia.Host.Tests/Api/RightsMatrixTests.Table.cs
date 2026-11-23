// RightsMatrixTests.Table.cs
// The table of the rights matrix: each route with a valid request, its right, what it needs drawn and the version its If-Match carries.

using System.Text.Json;
using Topogenia.Host.Tests.Dissolutions;
using Topogenia.Host.Tests.Exchange;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Tests.Sheets;
using Topogenia.Host.Tests.Steps;

namespace Topogenia.Host.Tests.Api;

public sealed partial class RightsMatrixTests
{
    /// <summary>The version a client sends when it read no record (a use not made yet, ADR 045).</summary>
    private const int NoRecord = StepSheets.NoRecord;

    /// <summary>
    /// What a case draws: the project, its detailed row, the flow Before to Macro, a second lane of the root map, the first
    /// lane of the sub-map, the codes of Before and After, the frame of a dissolution and a project in the trash when asked.
    /// </summary>
    private sealed record Targets(ApiProject Project, DetailedRow Row, Guid Flow, Guid OtherLane, Guid SubMapLane, (string Before, string After) Codes,
        Guid? Frame, Guid? Trashed, Guid? Snapshot = null, Guid? TargetState = null, Guid? PainPoint = null)
    {
        public Guid Lane => Project.FirstLaneId;

        public Guid Root => Project.RootMapId;

        public Guid Document => Row.Documents[0];

        /// <summary>The use of the document of Macro by After.</summary>
        public string UseUrl => $"/api/steps/{Row.After}/uses/{Document}";

        /// <summary>The status of the pain point that the use of the document by After raised on Macro (ADR 116).</summary>
        public string PainPointStatusUrl => $"/api/steps/{Row.After}/pain-points/{PainPoint}/status";
    }

    /// <summary>
    /// A route, its request and its right. <see cref="Stack"/> fills the history of the granted role first: undo and redo
    /// answer INTROUVABLE on an empty stack (ADR 042), which the table must not take for a refusal. <see cref="Version"/>
    /// reads, as the client would before writing, the version of the record that the route checks: it is sent in
    /// `If-Match` when the handler reads it. <see cref="NeedsUse"/> makes the use of <see cref="Targets.UseUrl"/> first,
    /// <see cref="NeedsPainPoint"/> the same use with a pain point (<see cref="Targets.PainPoint"/>),
    /// <see cref="Sheet"/> the sheet of Before that the route needs (a draft to validate, a validated sheet to question, a
    /// sheet questioned whose question is changed or withdrawn).
    /// </summary>
    private sealed record Entry(string Name, string Method, Func<Targets, string> Url, Func<Targets, object?> Body, Right Right,
        bool NeedsFrame = false, bool NeedsTrash = false, int Stack = 0, Func<Targets, ApiClient, Task<int>>? Version = null, bool NeedsUse = false,
        SheetState Sheet = SheetState.None, Func<Targets, Task>? Prepare = null, bool NeedsSnapshot = false, bool NeedsTarget = false,
        bool NeedsPainPoint = false);

    private enum SheetState
    {
        None,
        Draft,
        Validated,
        Questioned,
    }

    private static readonly Entry[] ProfileAndProjects =
    [
        new("GET /api/me", "GET", _ => "/api/me", _ => null, Right.Authenticated),
        new("PATCH /api/me", "PATCH", _ => "/api/me", _ => new { first_visit_done = true }, Right.Authenticated, Version: (_, caller) => ProfileVersionAsync(caller)),
        new("PUT /api/me/last-project", "PUT", _ => "/api/me/last-project", t => new { project_id = t.Project.Id }, Right.Reader),
        new("GET /api/projects", "GET", _ => "/api/projects", _ => null, Right.Authenticated),
        new("POST /api/projects", "POST", _ => "/api/projects", _ => new { code = ApiProject.NewCode(), name = "Visites" }, Right.Admin),
        // The code proposed from the name of a new project (3.4, lot 3, ADR 111 point 7): a read of the administrator, the name in the body.
        new("POST /api/projects/code-proposal", "POST", _ => "/api/projects/code-proposal", _ => new { name = "Visites" }, Right.Admin),
        new("GET /api/projects/{id}", "GET", t => $"/api/projects/{t.Project.Id}", _ => null, Right.Reader),
        new("DELETE /api/projects/{id}", "DELETE", t => $"/api/projects/{t.Project.Id}?confirmation={t.Project.Code}", _ => null, Right.Facilitator,
            Version: (t, _) => ProjectVersionAsync(t.Project)),
        new("PUT /api/projects/{id}/members", "PUT", t => $"/api/projects/{t.Project.Id}/members",
            _ => new object[] { new { login = Logins.Facilitator, role = "facilitator", is_sponsor = false } }, Right.Facilitator,
            Version: (t, _) => ProjectVersionAsync(t.Project)),
        new("GET /api/projects/{id}/participants", "GET", t => $"/api/projects/{t.Project.Id}/participants", _ => null, Right.Reader),
        new("PUT /api/projects/{id}/sharing-group-label", "PUT", t => $"/api/projects/{t.Project.Id}/sharing-group-label", _ => new { label = "Achats" },
            Right.Facilitator, Version: (t, _) => ProjectVersionAsync(t.Project)),
        new("PUT /api/projects/{id}/classification", "PUT", t => $"/api/projects/{t.Project.Id}/classification", _ => new { classification = "confidential" },
            Right.Facilitator, Version: (t, _) => ProjectVersionAsync(t.Project)),
        new("PUT /api/projects/{id}/translation-glossary", "PUT", t => $"/api/projects/{t.Project.Id}/translation-glossary",
            _ => new { glossary = "achat = purchase" }, Right.Facilitator, Version: (t, _) => ProjectVersionAsync(t.Project)),
        // The language of origin changes only for a project wholly translated (ADR 100): the granted call translates it first.
        new("PUT /api/projects/{id}/origin-language", "PUT", t => $"/api/projects/{t.Project.Id}/origin-language", _ => new { language = "en" },
            Right.Facilitator, Version: (t, _) => ProjectVersionAsync(t.Project), Prepare: TranslatedIntoEnglishAsync),
        new("PUT /api/projects/{id}/work-time", "PUT", t => $"/api/projects/{t.Project.Id}/work-time",
            _ => new { workday_hours = 7.5m, fte_hours_per_month = 160m }, Right.Facilitator, Version: (t, _) => ProjectVersionAsync(t.Project)),
        // The voices a project keeps for its presentation (ADR 103): read with the project, kept by its facilitator.
        new("GET /api/projects/{id}/presentation-voices", "GET", t => $"/api/projects/{t.Project.Id}/presentation-voices", _ => null, Right.Reader),
        new("PUT /api/projects/{id}/presentation-voices/{language}", "PUT", t => $"/api/projects/{t.Project.Id}/presentation-voices/fr",
            _ => new { name = "Hortense", local = true }, Right.Facilitator, Version: (t, caller) => VoiceVersionAsync(t, caller)),
        new("POST /api/projects/{id}/export", "POST", t => $"/api/projects/{t.Project.Id}/export", _ => new { without_people = true }, Right.Facilitator),
        // The search of a word in the whole project (3.4, lot 2, ADR 110): a read of every member, the tools under the rights of the sheets.
        new("POST /api/projects/{id}/search", "POST", t => $"/api/projects/{t.Project.Id}/search", _ => new { text = "commande", language = "en" }, Right.Reader),
        new("POST /api/projects/import/preview", "POST", _ => "/api/projects/import/preview?with_members=false", _ => TestPackages.Minimal("PKG"), Right.Admin),
        new("POST /api/projects/import", "POST", _ => $"/api/projects/import?with_members=false&code={ApiProject.NewCode()}", _ => TestPackages.Minimal("PKG"),
            Right.Admin),
        new("POST /api/projects/{id}/import/preview", "POST", t => $"/api/projects/{t.Project.Id}/import/preview?with_members=false",
            _ => TestPackages.Minimal("PKG"), Right.Facilitator),
        new("POST /api/projects/{id}/import", "POST", t => $"/api/projects/{t.Project.Id}/import?with_members=false", _ => TestPackages.Minimal("PKG"),
            Right.Facilitator, Version: (t, _) => ProjectVersionAsync(t.Project)),
        new("GET /api/projects/{id}/pmap", "GET", t => $"/api/projects/{t.Project.Id}/pmap?with_sheets=true", _ => null, Right.Reader),
        new("POST /api/projects/import-pmap", "POST", _ => "/api/projects/import-pmap", _ => new { content = TestPmaps.Minimal, code = ApiProject.NewCode() }, Right.Admin),
        // The preview of a template collection (3.4, lot 6, ADR 114): a read of the administrator, the collection in the body.
        new("POST /api/projects/import-collection/preview", "POST", _ => "/api/projects/import-collection/preview",
            _ => new { content = TestPmaps.Collection(TestPmaps.Minimal) }, Right.Admin),
        new("POST /api/projects/{id}/duplicate", "POST", t => $"/api/projects/{t.Project.Id}/duplicate", _ => new { code = ApiProject.NewCode() }, Right.Admin),
        // The photos of the indicators and the target state (Makigami M6, M8, ADR 095).
        new("GET /api/projects/{id}/snapshots", "GET", t => $"/api/projects/{t.Project.Id}/snapshots", _ => null, Right.Reader),
        new("POST /api/projects/{id}/snapshots", "POST", t => $"/api/projects/{t.Project.Id}/snapshots", _ => new { label = "Atelier" }, Right.Facilitator),
        new("DELETE /api/projects/{id}/snapshots/{snapshotId}", "DELETE", t => $"/api/projects/{t.Project.Id}/snapshots/{t.Snapshot}", _ => null,
            Right.Facilitator, NeedsSnapshot: true),
        new("POST /api/projects/{id}/target", "POST", t => $"/api/projects/{t.Project.Id}/target",
            _ => new { code = ApiProject.NewCode(), name = "État cible", label = "État actuel" }, Right.Admin),
        new("POST /api/projects/{id}/baseline", "POST", t => $"/api/projects/{t.TargetState}/baseline", _ => new { label = "État actuel" }, Right.Facilitator,
            NeedsTarget: true),
        new("GET /api/projects/{id}/translations", "GET", t => $"/api/projects/{t.Project.Id}/translations", _ => null, Right.Facilitator),
        new("PUT /api/projects/{id}/translations", "PUT", t => $"/api/projects/{t.Project.Id}/translations",
            _ => new { scope = "all", rows = Array.Empty<object>() }, Right.Facilitator, Version: (t, _) => ProjectVersionAsync(t.Project)),
        new("GET /api/trash", "GET", _ => "/api/trash", _ => null, Right.Authenticated),
        new("POST /api/trash/{id}/restore", "POST", t => $"/api/trash/{t.Trashed}/restore", _ => null, Right.Facilitator, NeedsTrash: true),
        new("DELETE /api/trash/{id}", "DELETE", t => $"/api/trash/{t.Trashed}", _ => null, Right.Admin, NeedsTrash: true),
        new("GET /api/admin/audit", "GET", _ => "/api/admin/audit", _ => null, Right.Admin),
        new("GET /api/admin/assistant", "GET", _ => "/api/admin/assistant", _ => null, Right.Admin),
        new("PUT /api/admin/assistant", "PUT", _ => "/api/admin/assistant", _ => new { connector = "copy_paste" }, Right.Admin,
            Version: (t, _) => AssistantVersionAsync(t)),
        new("POST /api/admin/assistant/test", "POST", _ => "/api/admin/assistant/test", _ => new { connector = "anthropic", secret = "sk-test-0000" },
            Right.Admin),
        // A project yet to create, described from the home page (3.0, lot 2): the exchange needs a connector that calls a model.
        new("POST /api/assistant/exchanges", "POST", _ => "/api/assistant/exchanges", _ => Ask, Right.Admin, Prepare: UseModelAsync),
        new("POST /api/assistant/prompt", "POST", _ => "/api/assistant/prompt", _ => Ask, Right.Admin),
        new("POST /api/assistant/replies", "POST", _ => "/api/assistant/replies", _ => Pasted, Right.Admin),
    ];

    /// <summary>A request of the panel of the assistant (3.0).</summary>
    private static readonly object Ask = new { intent = "ask", request = "Que fait-on ?" };

    /// <summary>A reply pasted from the AI of a person (3.0).</summary>
    private static readonly object Pasted = new { intent = "ask", request = "Que fait-on ?", reply = """{"form":"answer","message":"OK"}""" };

    /// <summary>
    /// A proposal of new objects only, valid on any map drawn (3.0, lot 3): its preview lists them, its application keeps
    /// the detailed step of the row with its lane.
    /// </summary>
    private static readonly object Proposed = new
    {
        pmap = """{"format":"topogenia","version":"1.1","project":{"name":"Matrice"},"map":{"lanes":[{"id":"a","name":"A"}],"steps":[{"id":"s","lane":"a","label":"Étape"}]}}""",
    };

    private static readonly Entry[] Maps =
    [
        new("GET /api/maps/{id}", "GET", t => $"/api/maps/{t.Root}", _ => null, Right.Reader),
        new("GET /api/maps/{id}/pmap", "GET", t => $"/api/maps/{t.Root}/pmap", _ => null, Right.Reader),
        new("PUT /api/maps/{id}/summaries", "PUT", t => $"/api/maps/{t.Root}/summaries",
            t => new { summaries = new[] { new { map_id = t.Root, text = "Le commercial traite la demande du client." } } }, Right.Facilitator),
        // The panel of the assistant on a map (3.0, lots 2 and 3).
        new("GET /api/maps/{id}/assistant", "GET", t => $"/api/maps/{t.Root}/assistant", _ => null, Right.Reader),
        new("POST /api/maps/{id}/assistant/exchanges", "POST", t => $"/api/maps/{t.Root}/assistant/exchanges", _ => Ask, Right.Reader, Prepare: UseModelAsync),
        new("POST /api/maps/{id}/assistant/prompt", "POST", t => $"/api/maps/{t.Root}/assistant/prompt", _ => Ask, Right.Reader),
        new("POST /api/maps/{id}/assistant/replies", "POST", t => $"/api/maps/{t.Root}/assistant/replies", _ => Pasted, Right.Reader),
        new("POST /api/maps/{id}/assistant/preview", "POST", t => $"/api/maps/{t.Root}/assistant/preview", _ => Proposed, Right.Facilitator),
        new("POST /api/maps/{id}/assistant/apply", "POST", t => $"/api/maps/{t.Root}/assistant/apply", _ => Proposed, Right.Facilitator,
            Version: (t, caller) => PreviewVersionAsync(t, caller)),
        // A .pmap file inserted as a detailed step (3.3, F-208, ADR 108): the facilitator's, as the opening of a file.
        new("POST /api/maps/{id}/insert-pmap/preview", "POST", t => $"/api/maps/{t.Root}/insert-pmap/preview", _ => new { content = TestPmaps.Minimal },
            Right.Facilitator),
        new("POST /api/maps/{id}/insert-pmap", "POST", t => $"/api/maps/{t.Root}/insert-pmap",
            t => new { content = TestPmaps.Minimal, lane_id = t.Lane, flow_id = t.Flow }, Right.Facilitator, Version: (t, caller) => InsertionVersionAsync(t, caller)),
        new("DELETE /api/maps/{id}", "DELETE", t => $"/api/maps/{t.Row.SubMap}", _ => null, Right.Facilitator, Version: (t, _) => MapVersionAsync(t, t.Row.SubMap)),
        new("GET /api/maps/{id}/history", "GET", t => $"/api/maps/{t.Root}/history", _ => null, Right.Reader),
        new("GET /api/maps/{id}/action-plan", "GET", t => $"/api/maps/{t.Root}/action-plan", _ => null, Right.Reader),
        new("GET /api/maps/{id}/presentation-comments", "GET", t => $"/api/maps/{t.Root}/presentation-comments", _ => null, Right.Reader),
        new("GET /api/maps/{id}/flow-presentation-comments", "GET", t => $"/api/maps/{t.Root}/flow-presentation-comments", _ => null, Right.Reader),
        new("POST /api/maps/{id}/presentation-comments", "POST", t => $"/api/maps/{t.Root}/presentation-comments",
            t => new { comments = new[] { new { step_id = t.Row.Before, text = "La commande part chez le fournisseur." } } }, Right.Facilitator),
        new("POST /api/maps/{id}/undo", "POST", t => $"/api/maps/{t.Root}/undo", _ => null, Right.Facilitator, Stack: 1),
        new("POST /api/maps/{id}/redo", "POST", t => $"/api/maps/{t.Root}/redo", _ => null, Right.Facilitator, Stack: 2),
        new("POST /api/maps/{id}/lanes", "POST", t => $"/api/maps/{t.Root}/lanes", _ => new { name = "Achats" }, Right.Facilitator),
        new("PUT /api/maps/{id}/lane-order", "PUT", t => $"/api/maps/{t.Root}/lane-order", t => new[] { t.OtherLane, t.Lane }, Right.Facilitator),
        new("POST /api/maps/{id}/steps", "POST", t => $"/api/maps/{t.Root}/steps", t => new { lane_id = t.Lane, column = 9, label = "Archiver" },
            Right.Facilitator),
        new("PUT /api/maps/{id}/layout", "PUT", t => $"/api/maps/{t.Root}/layout",
            t => new { steps = new[] { new { id = t.Row.Before, lane_id = t.Lane, column = 9, row = 0 } } }, Right.Facilitator),
        new("POST /api/maps/{id}/move", "POST", t => $"/api/maps/{t.Root}/move",
            t => new { steps = new[] { new { id = t.Row.Before, lane_id = t.Lane, column = 9, row = 0 } } }, Right.Facilitator),
        new("POST /api/maps/{id}/flows", "POST", t => $"/api/maps/{t.Root}/flows", t => new { source_step_id = t.Row.After, target_step_id = t.Row.Before },
            Right.Facilitator),
        new("POST /api/maps/{id}/command", "POST", t => $"/api/maps/{t.Root}/command", t => new { text = $"{t.Codes.After} > {t.Codes.Before}" },
            Right.Facilitator),
        new("POST /api/maps/{id}/deletion", "POST", t => $"/api/maps/{t.Root}/deletion", t => new { steps = new[] { t.Row.After }, stitch = true },
            Right.Facilitator),
        new("POST /api/maps/{id}/reattach", "POST", t => $"/api/maps/{t.Row.SubMap}/reattach", t => new { step_id = t.Row.Before }, Right.Facilitator,
            Version: (t, _) => MapVersionAsync(t, t.Row.SubMap)),
        new("POST /api/maps/{id}/paste", "POST", t => $"/api/maps/{t.Root}/paste",
            t => new { steps = new[] { new { key = "x", label = "Valider", lane_id = t.Lane, column = 9 } } }, Right.Facilitator),
        new("POST /api/maps/{id}/transfer", "POST", t => $"/api/maps/{t.Row.SubMap}/transfer",
            t => new { steps = new[] { new { id = t.Row.After, lane_id = t.SubMapLane, column = 5 } } }, Right.Facilitator),
        new("POST /api/maps/{id}/group", "POST", t => $"/api/maps/{t.Root}/group", t => new { steps = new[] { t.Row.Before }, label = "Achats" },
            Right.Facilitator),
        new("PATCH /api/lanes/{id}", "PATCH", t => $"/api/lanes/{t.Lane}", _ => new { name = "Achats" }, Right.Facilitator, Version: (t, _) => LaneVersionAsync(t)),
        new("DELETE /api/lanes/{id}", "DELETE", t => $"/api/lanes/{t.Lane}?steps=delete", _ => null, Right.Facilitator, Version: (t, _) => LaneVersionAsync(t)),
        new("POST /api/lanes/{id}/rows", "POST", t => $"/api/lanes/{t.Lane}/rows", _ => new { row = 9 }, Right.Facilitator),
        new("DELETE /api/lanes/{id}/rows/{row}", "DELETE", t => $"/api/lanes/{t.Lane}/rows/9", _ => null, Right.Facilitator),
    ];

    private static readonly Entry[] Steps =
    [
        new("PATCH /api/steps/{id}", "PATCH", t => $"/api/steps/{t.Row.Before}", _ => new { label = "Demander" }, Right.Facilitator,
            Version: (t, _) => t.Project.StepVersionAsync(t.Row.Before)),
        new("DELETE /api/steps/{id}", "DELETE", t => $"/api/steps/{t.Row.After}", _ => null, Right.Facilitator,
            Version: (t, _) => t.Project.StepVersionAsync(t.Row.After)),
        new("POST /api/steps/{id}/detail", "POST", t => $"/api/steps/{t.Row.Before}/detail", _ => null, Right.Facilitator,
            Version: (t, _) => t.Project.StepVersionAsync(t.Row.Before)),
        new("PUT /api/steps/{id}/raci", "PUT", t => $"/api/steps/{t.Row.Before}/raci", t => new { lane_id = t.OtherLane, role = "C" }, Right.Facilitator,
            Version: (t, _) => t.Project.StepVersionAsync(t.Row.Before)),
        new("POST /api/steps/{id}/insert", "POST", t => $"/api/steps/{t.Row.Before}/insert", _ => new { position = "after", label = "Contrôler" },
            Right.Facilitator),
        new("POST /api/steps/{id}/replace-pmap", "POST", t => $"/api/steps/{t.Row.Macro}/replace-pmap", _ => new { content = TestPmaps.Minimal },
            Right.Facilitator, Version: (t, caller) => InsertionVersionAsync(t, caller)),
        new("POST /api/steps/{id}/dissolve", "POST", t => $"/api/steps/{t.Row.Macro}/dissolve", _ => null, Right.Facilitator,
            Version: (t, _) => t.Row.MacroVersionAsync()),
        new("PUT /api/steps/{id}/translations", "PUT", t => $"/api/steps/{t.Row.Before}/translations", _ => new { translations = new { en = "Order" } },
            Right.Facilitator, Version: (t, _) => t.Project.StepVersionAsync(t.Row.Before)),
        new("PUT /api/steps/{id}/presentation-comment", "PUT", t => $"/api/steps/{t.Row.Before}/presentation-comment",
            _ => new { text = "Ici la commande part chez le fournisseur.", language = "fr" }, Right.Facilitator, Version: (t, caller) => CommentVersionAsync(t, caller)),
    ];

    private static readonly Entry[] SheetsAndDocuments =
    [
        new("GET /api/steps/{id}/sheet", "GET", t => $"/api/steps/{t.Row.Before}/sheet", _ => null, Right.Reader),
        new("PUT /api/steps/{id}/sheet", "PUT", t => $"/api/steps/{t.Row.Before}/sheet", _ => new { fields = new { purpose = new { value = "Commander" } } },
            Right.SheetFill, Version: (t, _) => SheetVersionAsync(t)),
        new("POST /api/steps/{id}/sheet/validate", "POST", t => $"/api/steps/{t.Row.Before}/sheet/validate", _ => null, Right.SheetFill,
            Version: (t, _) => SheetVersionAsync(t), Sheet: SheetState.Draft),
        new("POST /api/steps/{id}/sheet/clarification", "POST", t => $"/api/steps/{t.Row.Before}/sheet/clarification", _ => new { text = "Pourquoi ?" },
            Right.Facilitator, Version: (t, _) => SheetVersionAsync(t), Sheet: SheetState.Validated),
        new("PUT /api/steps/{id}/sheet/clarification", "PUT", t => $"/api/steps/{t.Row.Before}/sheet/clarification", _ => new { text = "Comment ?" },
            Right.Facilitator, Version: (t, _) => SheetVersionAsync(t), Sheet: SheetState.Questioned),
        new("DELETE /api/steps/{id}/sheet/clarification", "DELETE", t => $"/api/steps/{t.Row.Before}/sheet/clarification", _ => null,
            Right.Facilitator, Version: (t, _) => SheetVersionAsync(t), Sheet: SheetState.Questioned),
        new("POST /api/steps/{id}/documents", "POST", t => $"/api/steps/{t.Row.Macro}/documents", _ => new { direction = "output", name = "Devis" },
            Right.SheetFill),
        new("PUT /api/steps/{id}/documents/{documentId}", "PUT", t => $"/api/steps/{t.Row.Macro}/documents/{t.Document}",
            _ => new { direction = "output", name = "Devis" }, Right.SheetFill, Version: (t, _) => DocumentVersionAsync(t)),
        new("DELETE /api/steps/{id}/documents/{documentId}", "DELETE", t => $"/api/steps/{t.Row.Macro}/documents/{t.Document}", _ => null, Right.SheetFill,
            Version: (t, _) => DocumentVersionAsync(t)),
        new("GET /api/steps/{id}/documents/available", "GET", t => $"/api/steps/{t.Row.After}/documents/available", _ => null, Right.Reader),
        new("PUT /api/steps/{id}/uses/{documentId}", "PUT", t => t.UseUrl, _ => new { wait_hours = 1 }, Right.SheetFill, Version: (t, _) => UseVersionAsync(t)),
        new("DELETE /api/steps/{id}/uses/{documentId}", "DELETE", t => t.UseUrl, _ => null, Right.SheetFill, Version: (t, _) => UseVersionAsync(t), NeedsUse: true),
        // The status of a pain point (3.4, lot 4b, ADR 116): the right to fill the sheet of the receiving step, If-Match on the pain point.
        new("PUT /api/steps/{id}/pain-points/{painPointId}/status", "PUT", t => t.PainPointStatusUrl, _ => new { status = "implemented" }, Right.SheetFill,
            Version: (t, _) => PainPointVersionAsync(t), NeedsPainPoint: true),
    ];

    private static readonly Entry[] DissolutionsAndFlows =
    [
        new("POST /api/dissolutions/{id}/assign", "POST", t => $"/api/dissolutions/{t.Frame}/assign",
            t => new { kind = "document", object_id = t.Document, step_id = t.Row.First }, Right.Facilitator, NeedsFrame: true),
        new("POST /api/dissolutions/{id}/remove", "POST", t => $"/api/dissolutions/{t.Frame}/remove", t => new { kind = "document", object_id = t.Document },
            Right.Facilitator, NeedsFrame: true),
        new("POST /api/dissolutions/{id}/finish", "POST", t => $"/api/dissolutions/{t.Frame}/finish", _ => null, Right.Facilitator, NeedsFrame: true),
        new("PATCH /api/flows/{id}", "PATCH", t => $"/api/flows/{t.Flow}", _ => new { label = "oui" }, Right.Facilitator, Version: (t, _) => FlowVersionAsync(t)),
        new("PUT /api/flows/{id}/share", "PUT", t => $"/api/flows/{t.Flow}/share", _ => new { share = (decimal?)null }, Right.Facilitator,
            Version: (t, _) => FlowVersionAsync(t)),
        new("POST /api/maps/{id}/routes/auto", "POST", t => $"/api/maps/{t.Root}/routes/auto", t => new { flows = new[] { t.Flow } }, Right.Facilitator),
        new("PUT /api/flows/{id}/route", "PUT", t => $"/api/flows/{t.Flow}/route", _ => new { source_side = "top", target_side = (string?)null, via = (object?)null },
            Right.Facilitator, Version: (t, _) => FlowVersionAsync(t)),
        new("POST /api/flows/{id}/reverse", "POST", t => $"/api/flows/{t.Flow}/reverse", _ => null, Right.Facilitator, Version: (t, _) => FlowVersionAsync(t)),
        new("DELETE /api/flows/{id}", "DELETE", t => $"/api/flows/{t.Flow}", _ => null, Right.Facilitator, Version: (t, _) => FlowVersionAsync(t)),
        new("PUT /api/flows/{id}/translations", "PUT", t => $"/api/flows/{t.Flow}/translations", _ => new { translations = new { en = "yes" } },
            Right.Facilitator, Version: (t, _) => FlowVersionAsync(t)),
        new("PUT /api/flows/{id}/presentation-comment", "PUT", t => $"/api/flows/{t.Flow}/presentation-comment",
            _ => new { text = "Ici la commande change de mains.", language = "fr" }, Right.Facilitator, Version: (t, caller) => FlowCommentVersionAsync(t, caller)),
    ];

    private static readonly Entry[] Table = [.. ProfileAndProjects, .. Maps, .. Steps, .. SheetsAndDocuments, .. DissolutionsAndFlows];

    /// <summary>The version of the settings of the assistant, read by an administrator (the settings belong to no project).</summary>
    /// <summary>The connector of the test hosts that calls a model, chosen with a key: an exchange is then sent (3.0).</summary>
    private static async Task UseModelAsync(Targets targets)
    {
        using var admin = targets.Project.Admin;
        (await admin.PutAsync("/api/admin/assistant", new { connector = "anthropic", secret = "sk-test-0000" }, await AssistantVersionAsync(targets))).Ok();
    }

    /// <summary>The structure version a preview of the proposal reads, which its application gives back (3.0, lot 3).</summary>
    private static async Task<int> PreviewVersionAsync(Targets targets, ApiClient caller) =>
        (await caller.PostAsync($"/api/maps/{targets.Root}/assistant/preview", Proposed)).Ok().GetProperty("structure_version").GetInt32();

    /// <summary>The structure version the preview of an insertion reads, as the dialog sends it back (ADR 108).</summary>
    private static async Task<int> InsertionVersionAsync(Targets targets, ApiClient caller) =>
        (await caller.PostAsync($"/api/maps/{targets.Root}/insert-pmap/preview", new { content = TestPmaps.Minimal })).Ok().GetProperty("structure_version").GetInt32();

    private static async Task<int> AssistantVersionAsync(Targets targets)
    {
        using var admin = targets.Project.Admin;
        return (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
    }

    /// <summary>The version of the profile of the caller, read by the caller.</summary>
    private static async Task<int> ProfileVersionAsync(ApiClient caller) =>
        (await caller.GetAsync("/api/me")).Ok().GetProperty("version").GetInt32();

    /// <summary>The project of the targets wholly translated into English, as a workbook brought back would leave it.</summary>
    private static async Task TranslatedIntoEnglishAsync(Targets targets)
    {
        using var facilitator = targets.Project.Facilitator;
        await ProjectTranslations.TranslateAllAsync(facilitator, targets.Project.Id, "en");
    }

    private static async Task<int> ProjectVersionAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        return (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("version").GetInt32();
    }

    private static async Task<int> MapVersionAsync(Targets targets, Guid mapId) => (await targets.Project.MapAsync(mapId)).GetProperty("version").GetInt32();

    /// <summary>The version of the French line of the comment of presentation of Before, as the presentation reads it, or <see cref="NoRecord"/> without one (ADR 099 revised).</summary>
    private static async Task<int> CommentVersionAsync(Targets targets, ApiClient caller)
    {
        var comments = (await caller.GetAsync($"/api/maps/{targets.Root}/presentation-comments")).Ok().EnumerateArray()
            .Where(comment => comment.GetProperty("step_id").GetGuid() == targets.Row.Before && comment.GetProperty("language").GetString() == "fr").ToList();
        return comments.Count == 0 ? NoRecord : comments[0].GetProperty("version").GetInt32();
    }

    /// <summary>The version of the French line of the comment of presentation of the flow, as the presentation reads it, or <see cref="NoRecord"/> without one (ADR 099, lot J10).</summary>
    private static async Task<int> FlowCommentVersionAsync(Targets targets, ApiClient caller)
    {
        var comments = (await caller.GetAsync($"/api/maps/{targets.Root}/flow-presentation-comments")).Ok().EnumerateArray()
            .Where(comment => comment.GetProperty("flow_id").GetGuid() == targets.Flow && comment.GetProperty("language").GetString() == "fr").ToList();
        return comments.Count == 0 ? NoRecord : comments[0].GetProperty("version").GetInt32();
    }

    /// <summary>The version of the French voice the project keeps, as the presentation reads it, or <see cref="NoRecord"/> without one (ADR 103).</summary>
    private static async Task<int> VoiceVersionAsync(Targets targets, ApiClient caller)
    {
        var voices = (await caller.GetAsync($"/api/projects/{targets.Project.Id}/presentation-voices")).Ok().EnumerateArray()
            .Where(voice => voice.GetProperty("language").GetString() == "fr").ToList();
        return voices.Count == 0 ? NoRecord : voices[0].GetProperty("version").GetInt32();
    }

    private static async Task<int> LaneVersionAsync(Targets targets) =>
        (await targets.Project.MapAsync()).GetProperty("lanes").EnumerateArray()
            .Single(lane => lane.GetProperty("id").GetGuid() == targets.Lane).GetProperty("version").GetInt32();

    private static async Task<int> FlowVersionAsync(Targets targets) =>
        StructureCalls.FlowOf(await targets.Project.MapAsync(), targets.Flow).GetProperty("version").GetInt32();

    private static async Task<JsonElement> SheetAsync(Targets targets, Guid stepId)
    {
        using var facilitator = targets.Project.Facilitator;
        return (await facilitator.GetAsync($"/api/steps/{stepId}/sheet")).Ok();
    }

    private static async Task<int> SheetVersionAsync(Targets targets) => StepSheets.ReadVersionOf(await SheetAsync(targets, targets.Row.Before));

    private static async Task<int> DocumentVersionAsync(Targets targets) =>
        (await SheetAsync(targets, targets.Row.Macro)).GetProperty("outputs").EnumerateArray().Select(output => output.GetProperty("document"))
            .Single(document => document.GetProperty("id").GetGuid() == targets.Document).GetProperty("version").GetInt32();

    /// <summary>The version of the pain point raised by the use of the document by After, as the sheet of After shows it.</summary>
    private static async Task<int> PainPointVersionAsync(Targets targets) =>
        (await SheetAsync(targets, targets.Row.After)).GetProperty("pain_points").EnumerateArray()
            .Single(point => point.GetProperty("id").GetGuid() == targets.PainPoint).GetProperty("version").GetInt32();

    /// <summary>The version of the use of the document by After, or <see cref="NoRecord"/> when After does not use it yet.</summary>
    private static async Task<int> UseVersionAsync(Targets targets)
    {
        var use = (await SheetAsync(targets, targets.Row.After)).GetProperty("inputs").EnumerateArray()
            .Where(input => input.GetProperty("document").GetProperty("id").GetGuid() == targets.Document)
            .Select(input => input.GetProperty("use")).SingleOrDefault(candidate => candidate.ValueKind == JsonValueKind.Object);
        return use.ValueKind == JsonValueKind.Object ? use.GetProperty("version").GetInt32() : NoRecord;
    }
}
