// 20261023124304_ApplicationViewDefinition.cs
// Eighth SQL Server migration: the role of the application sees the definition of the whole database, so that its start checks all of it (ADR 032).

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.SqlServer.Migrations
{
    /// <summary>
    /// Gives `VIEW DEFINITION` on the database to `topogenia_app` (ADR 032). SQL Server hides from an account the metadata
    /// of what it holds no right on: under the account of the application, the check of the start did not see the triggers
    /// of the database, the objects outside the two schemas without any visible right, nor the rights given to another
    /// user, so it accepted a database changed that way (only `--migrate`, run by an administrator, refused it). The right
    /// opens no data (it shows definitions, not rows), and the database holds the application only. The rights on the two
    /// schemas and on the role of the reports that `RolesAndReportViews` gave are kept: this one covers them. Generated
    /// empty by `dotnet ef` (the model does not change), then written by hand.
    /// </summary>
    public partial class ApplicationViewDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("GRANT VIEW DEFINITION TO topogenia_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE VIEW DEFINITION FROM topogenia_app;");
        }
    }
}
