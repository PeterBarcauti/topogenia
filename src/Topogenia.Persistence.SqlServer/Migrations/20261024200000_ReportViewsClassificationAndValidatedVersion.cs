// 20261024200000_ReportViewsClassificationAndValidatedVersion.cs
// Tenth SQL Server migration: the classification of the project and the latest validated version of a sheet in the report views (AR-100, AR-101).

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// Decisions of the holder of the project (24/10/2026). `bi.project` gains `classification` (AR-100): the exports print
    /// the level of confidentiality, a report must be able to mark or filter it as well. `bi.step_sheet` gains
    /// `validated_version` (AR-101), the number of the latest validated version of the sheet, null when none: the view
    /// keeps the current content, and its `status` already says `draft` when that content is a draft; the version tells a
    /// first draft from a draft that changes a validated sheet. The new columns come last, so that the columns read by an
    /// existing report keep their place. Each view is dropped and created again, so that its definition is exactly the
    /// text below; the right of the reports is given on the schema and survives. The way down gives back the views of
    /// `RolesAndReportViews` and removes the record of the migration (ADR 027, revision of round 3). Generated empty by
    /// `dotnet ef` (the model does not change), then written by hand.
    /// </summary>
    public partial class ReportViewsClassificationAndValidatedVersion : Migration
    {
        private const string Project =
            """
            -- Live projects, with their level of confidentiality (AR-100).
            CREATE VIEW bi.project AS
            SELECT p.id AS project_id, p.code, p.name, p.source_language, p.description, p.created_at, p.classification
            FROM topogenia.project p
            WHERE p.deleted_at IS NULL;
            """;

        private const string StepSheet =
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

        private const string PreviousProject =
            """
            CREATE VIEW bi.project AS
            SELECT p.id AS project_id, p.code, p.name, p.source_language, p.description, p.created_at
            FROM topogenia.project p
            WHERE p.deleted_at IS NULL;
            """;

        private const string PreviousStepSheet =
            """
            -- Sheet of each live step with content ("empty" without a sheet). Completeness: share of the 7 rubrics filled
            -- (purpose, inputs, procedure, outputs, tools, frequency, duration), as SheetCompleteness computes it. Current
            -- content, draft included (status): the reports serve the running of the project.
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
                     ) / 7, 0) AS tinyint) AS completeness_pct
            FROM bi.step s
            LEFT JOIN topogenia.step_sheet f ON f.step_id = s.step_id
            WHERE s.step_type IN ('task', 'wait', 'decision');
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW bi.project;");
            migrationBuilder.Sql(Project);
            migrationBuilder.Sql("DROP VIEW bi.step_sheet;");
            migrationBuilder.Sql(StepSheet);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW bi.step_sheet;");
            migrationBuilder.Sql(PreviousStepSheet);
            migrationBuilder.Sql("DROP VIEW bi.project;");
            migrationBuilder.Sql(PreviousProject);
            migrationBuilder.DeleteData(schema: "topogenia", table: "schema_history", keyColumn: "migration_id", keyValue: "20261024200000_ReportViewsClassificationAndValidatedVersion");
        }
    }
}
