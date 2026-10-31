// 20261023102021_ReportMapDepthBound.cs
// Fifth SQL Server migration: the report view of the maps bounded at 50 levels, replaced by a later one (AR-67, AR-87).

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// Bounds the recursion of the report view `bi.process_map` at 50 levels, the bound of the domain (AR-67, A-2.2-28,
    /// `ProjectWorkspace.MaxDepth`). The view of `RolesAndReportViews` (1.x `db/006`) had no bound: a chain of sub-maps
    /// deeper than 101 levels, which the domain allows (beyond the recommended depth it only warns), made every read of the
    /// view, and of the views built on it, fail (maximum recursion of 100 of SQL Server, which a view cannot raise). The
    /// maps below the fiftieth level are left out of the reports, as the walks of the domain stop at 50 levels. The view is
    /// dropped and created again, so that its definition is exactly the text below. Generated empty by `dotnet ef` (the
    /// model does not change), then written by hand.
    /// </summary>
    public partial class ReportMapDepthBound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW bi.process_map;");
            migrationBuilder.Sql(
                """
                -- Live maps of live projects, with their depth (1 = root map, at most 50 levels as in the domain) and the path
                -- of the names from the root.
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
                    WHERE t.depth < 50
                )
                SELECT id AS map_id, project_id, parent_step_id, name, depth, path FROM tree;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW bi.process_map;");
            migrationBuilder.Sql(
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
                """);
        }
    }
}
