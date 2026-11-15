// TableNames.cs
// Names of the tables of the model, singular and in snake_case as in the 1.x (MODELE_DONNEES, section 1).

namespace Topogenia.Persistence.Model;

/// <summary>Table names; `docs/modele-donnees.md` describes each table and its correspondence with the domain and the 1.x.</summary>
public static class TableNames
{
    public const string AppUser = "app_user";
    public const string Project = "project";
    public const string ProjectCounter = "project_counter";
    public const string ProjectMember = "project_member";
    public const string DeletionBatch = "deletion_batch";
    public const string ProcessMap = "process_map";
    public const string Lane = "lane";
    public const string Step = "step";
    public const string Flow = "flow";
    public const string StepRaci = "step_raci";
    public const string Dissolution = "dissolution";
    public const string DissolutionStep = "dissolution_step";
    public const string DissolutionObject = "dissolution_object";
    public const string StepSheet = "step_sheet";
    public const string StepOperation = "step_operation";
    public const string StepSheetVersion = "step_sheet_version";
    public const string Document = "document";
    public const string DocumentUse = "document_use";
    public const string PainPoint = "pain_point";
    public const string PainPointStatusHistory = "pain_point_status_history";
    public const string Translation = "translation";

    /// <summary>The summaries of the maps, one per map at most (plan 3.1, lot R).</summary>
    public const string MapSummary = "map_summary";
    public const string StreamSnapshot = "stream_snapshot";
    public const string OperationLog = "operation_log";
    public const string AuditEvent = "audit_event";
    public const string SchemaHistory = "schema_history";

    /// <summary>The settings of the assistant of the installation, one row (3.0).</summary>
    public const string AssistantSettings = "assistant_settings";

    /// <summary>The comments of presentation of the steps, one line per step and language (3.2, ADR 099 revised).</summary>
    public const string PresentationComment = "presentation_comment";

    /// <summary>The comments of presentation of the flows, one line per flow and language (3.2, ADR 099, lot J10).</summary>
    public const string FlowPresentationComment = "flow_presentation_comment";

    /// <summary>The voices the projects keep for their presentation, one line per project and language (3.2, ADR 103).</summary>
    public const string PresentationVoice = "presentation_voice";

    /// <summary>The history table of EF Core itself (migration identifiers), next to <see cref="SchemaHistory"/>.</summary>
    public const string MigrationsHistory = "__EFMigrationsHistory";

    /// <summary>The lock table of the SQLite provider of EF Core (two processes never migrate at once).</summary>
    public const string MigrationsLock = "__EFMigrationsLock";
}
