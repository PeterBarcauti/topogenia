// 20261031001333_ValueStreamReportViews.cs
// Fifteenth SQL Server migration: the entries of the value stream at the end of the report views (ADR 085, spec 14.11).

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// The entries of the value stream in the report views (3.0, lot 7, ADR 085, spec 14.11): `bi.project` gains the work
    /// time, `bi.lane` the headcount, `bi.step` the incoming volume of a start step, `bi.flow` the share of an exit,
    /// `bi.step_sheet` the lead time. The new columns come last, so that the columns read by an existing report keep their
    /// place; the figures computed by the core (throughputs, workload, lead times) are not in the database. Each view is
    /// dropped and created again, so that its definition is exactly the text below; the right of the reports is given on
    /// the schema and survives. The way down gives back the views as the migrations before left them and removes the
    /// record of the migration (ADR 027). Generated empty by `dotnet ef` (the model does not change), then written by hand.
    /// </summary>
    public partial class ValueStreamReportViews : Migration
    {
        private const string Project =
            """
            -- Live projects, with their level of confidentiality (AR-100) and the work time of the value stream (ADR 085).
            CREATE VIEW bi.project AS
            SELECT p.id AS project_id, p.code, p.name, p.source_language, p.description, p.created_at, p.classification,
                   p.workday_hours, p.fte_hours_per_month
            FROM topogenia.project p
            WHERE p.deleted_at IS NULL;
            """;

        private const string Lane =
            """
            -- Live lanes of the maps, with the headcount of a lane of people (ADR 085).
            CREATE VIEW bi.lane AS
            SELECT l.id AS lane_id, l.map_id, m.project_id, l.code, l.name, l.lane_type, l.sort_order, l.headcount
            FROM topogenia.lane l JOIN bi.process_map m ON m.map_id = l.map_id
            WHERE l.deleted_at IS NULL;
            """;

        private const string Step =
            """
            -- Live steps of the maps, with the incoming volume of a start step: a quantity for a period (ADR 085).
            CREATE VIEW bi.step AS
            SELECT s.id AS step_id, s.map_id, m.project_id, s.lane_id, s.code, s.label, s.step_type, s.column_index, s.row_index,
                   s.automation_status, s.assignee_user_id, s.created_at, s.updated_at, s.volume_quantity, s.volume_period
            FROM topogenia.step s JOIN bi.process_map m ON m.map_id = s.map_id
            WHERE s.deleted_at IS NULL;
            """;

        private const string Flow =
            """
            -- The next views start from bi.step once and read the other steps in the tables (a live step is always on a
            -- live map): reading the recursive bi.step again for each row would be costly. The share of the cases of an
            -- exit of a decision or an inclusive gateway, in percent (ADR 085).
            CREATE VIEW bi.flow AS
            SELECT f.id AS flow_id, f.map_id, s.project_id, f.source_step_id, f.target_step_id, f.label, f.share_percent
            FROM topogenia.flow f
            JOIN bi.step s ON s.step_id = f.source_step_id
            WHERE f.deleted_at IS NULL;
            """;

        private const string StepSheet =
            """
            -- Sheet of each live step with content ("empty" without a sheet). Completeness: share of the 7 rubrics filled
            -- (purpose, inputs, procedure, outputs, tools, frequency, duration), as SheetCompleteness computes it. Current
            -- content, draft included (status): the reports serve the running of the project. validated_version: the
            -- number of the latest validated version, null when none; with the status draft, it tells a first draft from a
            -- draft that changes a validated sheet (AR-101). The lead time, a value and its unit (ADR 085), is not one of
            -- the rubrics.
            CREATE VIEW bi.step_sheet AS
            SELECT s.step_id, COALESCE(f.status, 'empty') AS status, f.actor, f.purpose, f.tools, f.frequency, f.duration_minutes, f.updated_at,
                   CAST(ROUND(100.0 * (
                         IIF(f.purpose IS NOT NULL, 1, 0) + IIF(f.tools IS NOT NULL, 1, 0) + IIF(f.frequency IS NOT NULL, 1, 0)
                       + IIF(f.duration_minutes IS NOT NULL, 1, 0)
                       + IIF(EXISTS (SELECT 1 FROM topogenia.step_operation o WHERE o.sheet_id = f.id), 1, 0)
                       + IIF(EXISTS (SELECT 1 FROM topogenia.document d WHERE d.step_id = s.step_id AND d.direction = 'output'), 1, 0)
                       + IIF(EXISTS (SELECT 1 FROM topogenia.document d WHERE d.step_id = s.step_id AND d.direction = 'input')
                             OR EXISTS (SELECT 1 FROM topogenia.document_use u JOIN topogenia.document d ON d.id = u.document_id
                                        JOIN topogenia.step p ON p.id = d.step_id AND p.deleted_at IS NULL WHERE u.step_id = s.step_id), 1, 0)
                     ) / 7, 0) AS tinyint) AS completeness_pct,
                   (SELECT MAX(v.version_number) FROM topogenia.step_sheet_version v WHERE v.sheet_id = f.id) AS validated_version,
                   f.lead_time, f.lead_time_unit
            FROM bi.step s
            LEFT JOIN topogenia.step_sheet f ON f.step_id = s.step_id
            WHERE s.step_type IN ('task', 'wait', 'decision');
            """;

        private const string PreviousProject =
            """
            -- Live projects, with their level of confidentiality (AR-100).
            CREATE VIEW bi.project AS
            SELECT p.id AS project_id, p.code, p.name, p.source_language, p.description, p.created_at, p.classification
            FROM topogenia.project p
            WHERE p.deleted_at IS NULL;
            """;

        private const string PreviousLane =
            """
            CREATE VIEW bi.lane AS
            SELECT l.id AS lane_id, l.map_id, m.project_id, l.code, l.name, l.lane_type, l.sort_order
            FROM topogenia.lane l JOIN bi.process_map m ON m.map_id = l.map_id
            WHERE l.deleted_at IS NULL;
            """;

        private const string PreviousStep =
            """
            CREATE VIEW bi.step AS
            SELECT s.id AS step_id, s.map_id, m.project_id, s.lane_id, s.code, s.label, s.step_type, s.column_index, s.row_index,
                   s.automation_status, s.assignee_user_id, s.created_at, s.updated_at
            FROM topogenia.step s JOIN bi.process_map m ON m.map_id = s.map_id
            WHERE s.deleted_at IS NULL;
            """;

        private const string PreviousFlow =
            """
            -- The next views start from bi.step once and read the other steps in the tables (a live step is always on a
            -- live map): reading the recursive bi.step again for each row would be costly.
            CREATE VIEW bi.flow AS
            SELECT f.id AS flow_id, f.map_id, s.project_id, f.source_step_id, f.target_step_id, f.label
            FROM topogenia.flow f
            JOIN bi.step s ON s.step_id = f.source_step_id
            WHERE f.deleted_at IS NULL;
            """;

        private const string PreviousStepSheet =
            """
            -- Sheet of each live step with content ("empty" without a sheet). Completeness: share of the 7 rubrics filled
            -- (purpose, inputs, procedure, outputs, tools, frequency, duration), as SheetCompleteness computes it. Current
            -- content, draft included (status): the reports serve the running of the project. validated_version: the
            -- number of the latest validated version, null when none; with the status draft, it tells a first draft from a
            -- draft that changes a validated sheet (AR-101).
            CREATE VIEW bi.step_sheet AS
            SELECT s.step_id, COALESCE(f.status, 'empty') AS status, f.actor, f.purpose, f.tools, f.frequency, f.duration_minutes, f.updated_at,
                   CAST(ROUND(100.0 * (
                         IIF(f.purpose IS NOT NULL, 1, 0) + IIF(f.tools IS NOT NULL, 1, 0) + IIF(f.frequency IS NOT NULL, 1, 0)
                       + IIF(f.duration_minutes IS NOT NULL, 1, 0)
                       + IIF(EXISTS (SELECT 1 FROM topogenia.step_operation o WHERE o.sheet_id = f.id), 1, 0)
                       + IIF(EXISTS (SELECT 1 FROM topogenia.document d WHERE d.step_id = s.step_id AND d.direction = 'output'), 1, 0)
                       + IIF(EXISTS (SELECT 1 FROM topogenia.document d WHERE d.step_id = s.step_id AND d.direction = 'input')
                             OR EXISTS (SELECT 1 FROM topogenia.document_use u JOIN topogenia.document d ON d.id = u.document_id
                                        JOIN topogenia.step p ON p.id = d.step_id AND p.deleted_at IS NULL WHERE u.step_id = s.step_id), 1, 0)
                     ) / 7, 0) AS tinyint) AS completeness_pct,
                   (SELECT MAX(v.version_number) FROM topogenia.step_sheet_version v WHERE v.sheet_id = f.id) AS validated_version
            FROM bi.step s
            LEFT JOIN topogenia.step_sheet f ON f.step_id = s.step_id
            WHERE s.step_type IN ('task', 'wait', 'decision');
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Replace(migrationBuilder, Project, Lane, Step, Flow, StepSheet);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Replace(migrationBuilder, PreviousProject, PreviousLane, PreviousStep, PreviousFlow, PreviousStepSheet);
            migrationBuilder.DeleteData(schema: "topogenia", table: "schema_history", keyColumn: "migration_id", keyValue: "20261031001333_ValueStreamReportViews");
        }

        /// <summary>The five views dropped, the views that read another first, then created again from the bottom.</summary>
        private static void Replace(MigrationBuilder migrationBuilder, string project, string lane, string step, string flow, string stepSheet)
        {
            migrationBuilder.Sql("DROP VIEW bi.step_sheet;");
            migrationBuilder.Sql("DROP VIEW bi.flow;");
            migrationBuilder.Sql("DROP VIEW bi.step;");
            migrationBuilder.Sql("DROP VIEW bi.lane;");
            migrationBuilder.Sql("DROP VIEW bi.project;");
            migrationBuilder.Sql(project);
            migrationBuilder.Sql(lane);
            migrationBuilder.Sql(step);
            migrationBuilder.Sql(flow);
            migrationBuilder.Sql(stepSheet);
        }
    }
}
