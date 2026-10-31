// 20261023083307_RolesAndReportViews.cs
// Second SQL Server migration: the two database roles, the bi schema and its eleven report views (1.x db/004 and db/006, ADR 032).

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// The access layer of the server mode (1.x `db/004` as changed by `db/006`, spec 4.4, ADR 032): the role of the
    /// account of the application, the role of the reports, the `bi` schema and its views. Generated empty by `dotnet ef`
    /// (the model does not change), then written by hand; the names are frozen here as they were derived from the product
    /// name when the migration was written (ADR 013).
    /// </summary>
    public partial class RolesAndReportViews : Migration
    {
        /// <summary>The report views, each one after those it reads.</summary>
        private static readonly string[] Views =
        [
            """
            CREATE VIEW bi.project AS
            SELECT p.id AS project_id, p.code, p.name, p.source_language, p.description, p.created_at
            FROM topogenia.project p
            WHERE p.deleted_at IS NULL;
            """,
            """
            -- Live maps of live projects, with their depth (1 = root map) and the path of the names from the root.
            CREATE VIEW bi.process_map AS
            WITH tree AS (
                SELECT m.id, m.project_id, m.parent_step_id, m.name, 1 AS depth, CAST(m.name AS nvarchar(4000)) AS path
                FROM topogenia.process_map m
                JOIN topogenia.project p ON p.id = m.project_id AND p.deleted_at IS NULL
                WHERE m.parent_step_id IS NULL AND m.deleted_at IS NULL
                UNION ALL
                SELECT m.id, m.project_id, m.parent_step_id, m.name, t.depth + 1, CAST(t.path + N' > ' + m.name AS nvarchar(4000))
                FROM tree t
                JOIN topogenia.step s ON s.map_id = t.id AND s.deleted_at IS NULL
                JOIN topogenia.process_map m ON m.parent_step_id = s.id AND m.deleted_at IS NULL
            )
            SELECT id AS map_id, project_id, parent_step_id, name, depth, path FROM tree;
            """,
            """
            CREATE VIEW bi.lane AS
            SELECT l.id AS lane_id, l.map_id, m.project_id, l.code, l.name, l.lane_type, l.sort_order
            FROM topogenia.lane l JOIN bi.process_map m ON m.map_id = l.map_id
            WHERE l.deleted_at IS NULL;
            """,
            """
            CREATE VIEW bi.step AS
            SELECT s.id AS step_id, s.map_id, m.project_id, s.lane_id, s.code, s.label, s.step_type, s.column_index, s.row_index,
                   s.automation_status, s.assignee_user_id, s.created_at, s.updated_at
            FROM topogenia.step s JOIN bi.process_map m ON m.map_id = s.map_id
            WHERE s.deleted_at IS NULL;
            """,
            """
            -- The next views start from bi.step once and read the other steps in the tables (a live step is always on a
            -- live map): reading the recursive bi.step again for each row would be costly.
            CREATE VIEW bi.flow AS
            SELECT f.id AS flow_id, f.map_id, s.project_id, f.source_step_id, f.target_step_id, f.label
            FROM topogenia.flow f
            JOIN bi.step s ON s.step_id = f.source_step_id
            WHERE f.deleted_at IS NULL;
            """,
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
            """,
            """
            -- Procedures, documents and uses: only for the steps with content (as bi.step_sheet).
            CREATE VIEW bi.step_operation AS
            SELECT f.step_id, o.position, o.text, o.tool, o.nature
            FROM topogenia.step_operation o
            JOIN topogenia.step_sheet f ON f.id = o.sheet_id
            JOIN bi.step s ON s.step_id = f.step_id
            WHERE s.step_type IN ('task', 'wait', 'decision');
            """,
            """
            CREATE VIEW bi.document AS
            SELECT d.id AS document_id, d.step_id, d.direction, d.name, d.location, d.location_type, d.wait_hours
            FROM topogenia.document d JOIN bi.step s ON s.step_id = d.step_id
            WHERE s.step_type IN ('task', 'wait', 'decision');
            """,
            """
            CREATE VIEW bi.document_use AS
            SELECT u.document_id, u.step_id, u.wait_hours, u.percent_complete_accurate, u.pain_point_id
            FROM topogenia.document_use u
            JOIN bi.step s ON s.step_id = u.step_id
            JOIN topogenia.document d ON d.id = u.document_id
            JOIN topogenia.step p ON p.id = d.step_id AND p.deleted_at IS NULL
            WHERE s.step_type IN ('task', 'wait', 'decision') AND p.step_type IN ('task', 'wait', 'decision');
            """,
            """
            CREATE VIEW bi.pain_point AS
            SELECT i.id AS pain_point_id, i.project_id, i.step_id, i.code, i.description, i.lost_hours_per_month, i.status, i.created_at
            FROM topogenia.pain_point i
            JOIN topogenia.project p ON p.id = i.project_id AND p.deleted_at IS NULL
            WHERE i.step_id IS NULL OR EXISTS (SELECT 1 FROM topogenia.step s WHERE s.id = i.step_id AND s.deleted_at IS NULL);
            """,
            """
            -- RACI as the application shows it (RaciRules.IsVisible): without the hidden roles (lane of another map,
            -- deleted lane, or lane of the step itself) nor those of the starts, ends and gateways.
            CREATE VIEW bi.step_raci AS
            SELECT r.step_id, r.lane_id, r.role
            FROM topogenia.step_raci r
            JOIN bi.step s ON s.step_id = r.step_id
            JOIN topogenia.lane l ON l.id = r.lane_id AND l.map_id = s.map_id AND l.deleted_at IS NULL
            WHERE r.lane_id <> s.lane_id AND s.step_type IN ('task', 'wait', 'decision');
            """,
        ];

        /// <summary>The names of the views, in the order of <see cref="Views"/>.</summary>
        private static readonly string[] ViewNames =
            ["project", "process_map", "lane", "step", "flow", "step_sheet", "step_operation", "document", "document_use", "pain_point", "step_raci"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The views reach the tables through the ownership chain: both schemas belong to dbo (1.x AUTHORIZATION dbo),
            // whoever runs the migrations.
            migrationBuilder.Sql("ALTER AUTHORIZATION ON SCHEMA::topogenia TO dbo;");

            // The account of the application: reads and writes the data; reads the histories of the migrations without
            // writing them; appends to the histories of the data and may purge them, never rewrites them (1.x DENY). It
            // reads the definitions of the schema, which the check of the start describes (ADR 027, ADR 032).
            migrationBuilder.Sql("CREATE ROLE topogenia_app AUTHORIZATION dbo;");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::topogenia TO topogenia_app;");
            migrationBuilder.Sql("DENY INSERT, UPDATE, DELETE ON topogenia.schema_history TO topogenia_app;");
            migrationBuilder.Sql("DENY INSERT, UPDATE, DELETE ON topogenia.__EFMigrationsHistory TO topogenia_app;");
            migrationBuilder.Sql("DENY UPDATE ON topogenia.step_sheet_version TO topogenia_app;");
            migrationBuilder.Sql("DENY UPDATE ON topogenia.pain_point_status_history TO topogenia_app;");
            migrationBuilder.Sql("DENY UPDATE ON topogenia.audit_event TO topogenia_app;");

            // The reports: the bi schema only. Never put a report account in db_datareader: it would read every table.
            migrationBuilder.Sql("CREATE ROLE topogenia_bi AUTHORIZATION dbo;");
            migrationBuilder.Sql("CREATE SCHEMA bi AUTHORIZATION dbo;");
            migrationBuilder.Sql("GRANT SELECT ON SCHEMA::bi TO topogenia_bi;");
            foreach (var view in Views)
                migrationBuilder.Sql(view);

            migrationBuilder.Sql("GRANT VIEW DEFINITION ON SCHEMA::topogenia TO topogenia_app;");
            migrationBuilder.Sql("GRANT VIEW DEFINITION ON SCHEMA::bi TO topogenia_app;");
            migrationBuilder.Sql("GRANT VIEW DEFINITION ON ROLE::topogenia_bi TO topogenia_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The roles must have no member left: the administrator removes the accounts first. The owner of the
            // schema of the tables stays dbo.
            foreach (var view in Enumerable.Reverse(ViewNames))
                migrationBuilder.Sql($"DROP VIEW bi.{view};");
            migrationBuilder.Sql("DROP SCHEMA bi;");
            migrationBuilder.Sql("DROP ROLE topogenia_bi;");
            migrationBuilder.Sql("DROP ROLE topogenia_app;");
        }
    }
}
