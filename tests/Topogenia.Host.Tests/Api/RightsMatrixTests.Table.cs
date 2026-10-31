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
        Guid? Frame, Guid? Trashed)
    {
        public Guid Lane => Project.FirstLaneId;

        public Guid Root => Project.RootMapId;

        public Guid Document => Row.Documents[0];

        /// <summary>The use of the document of Macro by After.</summary>
        public string UseUrl => $"/api/steps/{Row.After}/uses/{Document}";
    }

    /// <summary>
    /// A route, its request and its right. <see cref="Stack"/> fills the history of the granted role first: undo and redo
    /// answer INTROUVABLE on an empty stack (ADR 042), which the table must not take for a refusal. <see cref="Version"/>
    /// reads, as the client would before writing, the version of the record that the route checks: it is sent in
    /// `If-Match` when the handler reads it. <see cref="NeedsUse"/> makes the use of <see cref="Targets.UseUrl"/> first,
    /// <see cref="Sheet"/> the sheet of Before that the route needs (a draft to validate, a validated sheet to question, a
    /// sheet questioned whose question is changed or withdrawn).
    /// </summary>
    private sealed record Entry(string Name, string Method, Func<Targets, string> Url, Func<Targets, object?> Body, Right Right,
        bool NeedsFrame = false, bool NeedsTrash = false, int Stack = 0, Func<Targets, ApiClient, Task<int>>? Version = null, bool NeedsUse = false,
        SheetState Sheet = SheetState.None, Func<Targets, Task>? Prepare = null);

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
        new("POST /api/projects", "POST", _ => "/api/projects", _ => new { code = ApiProject.NewCode(), name = "Ventes" }, Right.Admin),
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
        new("PUT /api/projects/{id}/work-time", "PUT", t => $"/api/projects/{t.Project.Id}/work-time",
            _ => new { workday_hours = 7.5m, fte_hours_per_month = 160m }, Right.Facilitator, Version: (t, _) => ProjectVersionAsync(t.Project)),
        new("POST /api/projects/{id}/export", "POST", t => $"/api/projects/{t.Project.Id}/export", _ => new { without_people = true }, Right.Facilitator),
        new("POST /api/projects/import/preview", "POST", _ => "/api/projects/import/preview?with_members=false", _ => TestPackages.Minimal("PKG"), Right.Admin),
        new("POST /api/projects/import", "POST", _ => $"/api/projects/import?with_members=false&code={ApiProject.NewCode()}", _ => TestPackages.Minimal("PKG"),
            Right.Admin),
        new("POST /api/projects/{id}/import/preview", "POST", t => $"/api/projects/{t.Project.Id}/import/preview?with_members=false",
            _ => TestPackages.Minimal("PKG"), Right.Facilitator),
        new("POST /api/projects/{id}/import", "POST", t => $"/api/projects/{t.Project.Id}/import?with_members=false", _ => TestPackages.Minimal("PKG"),
            Right.Facilitator, Version: (t, _) => ProjectVersionAsync(t.Project)),
        new("GET /api/projects/{id}/pmap", "GET", t => $"/api/projects/{t.Project.Id}/pmap?with_sheets=true", _ => null, Right.Reader),
        new("POST /api/projects/import-pmap", "POST", _ => "/api/projects/import-pmap", _ => new { content = TestPmaps.Minimal, code = ApiProject.NewCode() }, Right.Admin),
        new("POST /api/projects/{id}/duplicate", "POST", t => $"/api/projects/{t.Project.Id}/duplicate", _ => new { code = ApiProject.NewCode() }, Right.Admin),
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
        // The panel of the assistant on a map (3.0, lots 2 and 3).
        new("GET /api/maps/{id}/assistant", "GET", t => $"/api/maps/{t.Root}/assistant", _ => null, Right.Reader),
        new("POST /api/maps/{id}/assistant/exchanges", "POST", t => $"/api/maps/{t.Root}/assistant/exchanges", _ => Ask, Right.Reader, Prepare: UseModelAsync),
        new("POST /api/maps/{id}/assistant/prompt", "POST", t => $"/api/maps/{t.Root}/assistant/prompt", _ => Ask, Right.Reader),
        new("POST /api/maps/{id}/assistant/replies", "POST", t => $"/api/maps/{t.Root}/assistant/replies", _ => Pasted, Right.Reader),
        new("POST /api/maps/{id}/assistant/preview", "POST", t => $"/api/maps/{t.Root}/assistant/preview", _ => Proposed, Right.Facilitator),
        new("POST /api/maps/{id}/assistant/apply", "POST", t => $"/api/maps/{t.Root}/assistant/apply", _ => Proposed, Right.Facilitator,
            Version: (t, caller) => PreviewVersionAsync(t, caller)),
        new("DELETE /api/maps/{id}", "DELETE", t => $"/api/maps/{t.Row.SubMap}", _ => null, Right.Facilitator, Version: (t, _) => MapVersionAsync(t, t.Row.SubMap)),
        new("GET /api/maps/{id}/history", "GET", t => $"/api/maps/{t.Root}/history", _ => null, Right.Reader),
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
        new("POST /api/steps/{id}/dissolve", "POST", t => $"/api/steps/{t.Row.Macro}/dissolve", _ => null, Right.Facilitator,
            Version: (t, _) => t.Row.MacroVersionAsync()),
        new("PUT /api/steps/{id}/translations", "PUT", t => $"/api/steps/{t.Row.Before}/translations", _ => new { translations = new { en = "Order" } },
            Right.Facilitator, Version: (t, _) => t.Project.StepVersionAsync(t.Row.Before)),
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
        new("POST /api/flows/{id}/reverse", "POST", t => $"/api/flows/{t.Flow}/reverse", _ => null, Right.Facilitator, Version: (t, _) => FlowVersionAsync(t)),
        new("DELETE /api/flows/{id}", "DELETE", t => $"/api/flows/{t.Flow}", _ => null, Right.Facilitator, Version: (t, _) => FlowVersionAsync(t)),
        new("PUT /api/flows/{id}/translations", "PUT", t => $"/api/flows/{t.Flow}/translations", _ => new { translations = new { en = "yes" } },
            Right.Facilitator, Version: (t, _) => FlowVersionAsync(t)),
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

    private static async Task<int> AssistantVersionAsync(Targets targets)
    {
        using var admin = targets.Project.Admin;
        return (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
    }

    /// <summary>The version of the profile of the caller, read by the caller.</summary>
    private static async Task<int> ProfileVersionAsync(ApiClient caller) =>
        (await caller.GetAsync("/api/me")).Ok().GetProperty("version").GetInt32();

    private static async Task<int> ProjectVersionAsync(ApiProject project)
    {
        using var facilitator = project.Facilitator;
        return (await facilitator.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("version").GetInt32();
    }

    private static async Task<int> MapVersionAsync(Targets targets, Guid mapId) => (await targets.Project.MapAsync(mapId)).GetProperty("version").GetInt32();

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

    /// <summary>The version of the use of the document by After, or <see cref="NoRecord"/> when After does not use it yet.</summary>
    private static async Task<int> UseVersionAsync(Targets targets)
    {
        var use = (await SheetAsync(targets, targets.Row.After)).GetProperty("inputs").EnumerateArray()
            .Where(input => input.GetProperty("document").GetProperty("id").GetGuid() == targets.Document)
            .Select(input => input.GetProperty("use")).SingleOrDefault(candidate => candidate.ValueKind == JsonValueKind.Object);
        return use.ValueKind == JsonValueKind.Object ? use.GetProperty("version").GetInt32() : NoRecord;
    }
}
