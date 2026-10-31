// 20261023113557_ReportMapDepthOf101.cs
// Seventh SQL Server migration: the report view of the maps gives the 101 levels that the 1.x showed (AR-87).

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// Bounds the recursion of the report view `bi.process_map` at 101 levels (AR-87) instead of 50 (`ReportMapDepthBound`,
    /// published, not changed). The view of the 1.x (`db/006`) had no bound: it showed every map down to the 101st level
    /// and failed beyond (maximum recursion of 100 of SQL Server, which a view cannot raise). Bounded at 101, the view shows
    /// all that the 1.x showed and never fails; the 50 levels of the walks of the domain (AR-67) do not bound the reports.
    /// The view is dropped and created again, so that its definition is exactly the text below. Generated empty by
    /// `dotnet ef` (the model does not change), then written by hand.
    /// </summary>
    public partial class ReportMapDepthOf101 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW bi.process_map;");
            migrationBuilder.Sql(
                """
                -- Live maps of live projects, with their depth (1 = root map, at most 101 levels as the 1.x showed, AR-87) and
                -- the path of the names from the root.
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
                    WHERE t.depth < 101
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
    }
}
