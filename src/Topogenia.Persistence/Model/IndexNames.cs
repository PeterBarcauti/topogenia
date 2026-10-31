// IndexNames.cs
// Names of the unique indexes that only hold for some rows, whose filters each provider adds (spec 4.2, ADR 025).

namespace Topogenia.Persistence.Model;

/// <summary>The partial unique indexes of spec 4.2, with the 1.x names (`db/001`, `db/007`).</summary>
public static class IndexNames
{
    /// <summary>RG-C04: one live step per cell (lane, column, row).</summary>
    public const string LiveStepOfCell = "UX_step_lane_id_column_index_row_index";

    /// <summary>RG-C06: one live flow per (source, target).</summary>
    public const string LiveFlowOfPair = "UX_flow_source_step_id_target_step_id";

    /// <summary>RG-C09: one live detailed map per step.</summary>
    public const string LiveSubMapOfStep = "UX_process_map_parent_step_id";

    /// <summary>One root map per project.</summary>
    public const string RootMapOfProject = "UX_process_map_project_id_root";

    /// <summary>One approver (A) per step.</summary>
    public const string SingleApproverOfStep = "UX_step_raci_step_id_approver";

    /// <summary>One open dissolution frame per map.</summary>
    public const string OpenDissolutionOfMap = "UX_dissolution_map_id_open";

    /// <summary>A pain point is raised by one use at most.</summary>
    public const string PainPointOfUse = "UX_document_use_pain_point_id";
}
