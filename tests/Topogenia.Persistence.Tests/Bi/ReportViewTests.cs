// ReportViewTests.cs
// The report views of SQL Server read by the report role: columns of the 1.x and those added, live content of a project, nothing of the trash (A-5-29, AR-100, AR-101).

using Topogenia.Domain.Values;
using Topogenia.Persistence.SqlServer;
using Topogenia.Persistence.Tests.Infrastructure;
using static Topogenia.Persistence.SqlServer.SqlServerAccessProbe;

namespace Topogenia.Persistence.Tests.Bi;

/// <summary>
/// Each view is read with the rights of the report role (a user without login, member of the role), as Power BI would:
/// the read itself proves that the role reaches the tables through the views only (same owner, spec 5.3). The expected
/// content is computed apart by <see cref="ReportViewOracle"/>; the views are those of the 1.x (`db/004`, `db/006`),
/// columns adapted to the 2.0 model (`docs/modele-donnees.md` § 10).
/// </summary>
public sealed class ReportViewTests(MigratedDatabases databases) : IClassFixture<MigratedDatabases>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> Views => [.. ReportViewOracle.Views];

    private async Task<TestDatabase> WrittenAsync(params ReportProject[] projects)
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        await using var context = database.Context();
        foreach (var project in projects)
            context.AddRange(project.Records);
        await context.SaveChangesAsync(Token);
        return database;
    }

    private static async Task<ReportViewRows> ReadAsReportsAsync(TestDatabase database, string view)
    {
        await using var session = await RoleSession.OpenAsync(database, SqlServerAccess.ReportRole);
        return await SqlServerAccessProbe.ReadViewAsync(session.Connection, view, Token);
    }

    /// <summary>The rows of a view whose first column (the identifier of the row or of its step) belongs to the project.</summary>
    private static List<string> RowsOf(ReportViewRows rows, ReportProject project)
    {
        var identifiers = project.Identifiers;
        return rows.Rows.Where(row => row[0] is Guid id && identifiers.Contains(id)).Select(row => ReportViewOracle.Row(row)).Order(StringComparer.Ordinal).ToList();
    }

    [Theory]
    [MemberData(nameof(Views))]
    public async Task Each_report_view_has_the_columns_of_the_1x_view_in_their_order(string view)
    {
        var database = await databases.OfAsync(EngineKind.SqlServer);
        var rows = await ReadAsReportsAsync(database, view);
        Assert.Equal(ReportViewOracle.Columns[view], rows.Columns);
    }

    [Theory]
    [MemberData(nameof(Views))]
    public async Task Each_report_view_gives_the_live_content_of_a_project_and_nothing_deleted_or_hidden(string view)
    {
        var project = ReportProject.New();
        var database = await WrittenAsync(project);
        var expected = new ReportViewOracle(project.ProjectId, project.Records).RowsOf(view).Order(StringComparer.Ordinal).ToList();
        Assert.NotEmpty(expected);
        Assert.Equal(expected, RowsOf(await ReadAsReportsAsync(database, view), project));
    }

    [Fact]
    public async Task Nothing_of_a_project_in_the_trash_reaches_the_report_views()
    {
        var live = ReportProject.New();
        var trashed = ReportProject.New(inTrash: true);
        var database = await WrittenAsync(live, trashed);
        var hidden = trashed.Identifiers;
        foreach (var view in ReportViewOracle.Views)
        {
            var rows = await ReadAsReportsAsync(database, view);
            Assert.DoesNotContain(rows.Rows.SelectMany(row => row), cell => cell is Guid id && hidden.Contains(id));
            Assert.NotEmpty(RowsOf(rows, live));
        }
    }

    [Fact]
    public async Task The_completeness_of_the_report_view_is_the_one_of_the_domain_for_every_step_with_content()
    {
        var project = ReportProject.New();
        var database = await WrittenAsync(project);
        var oracle = new ReportViewOracle(project.ProjectId, project.Records);
        var rows = await ReadAsReportsAsync(database, "step_sheet");
        var completeness = rows.Rows.Where(row => row[0] is Guid id && project.Identifiers.Contains(id))
            .ToDictionary(row => (Guid)row[0]!, row => Convert.ToInt32(row[Column("step_sheet", "completeness_pct")], System.Globalization.CultureInfo.InvariantCulture));
        var steps = project.Graph.Steps;
        // Five rubrics and an output, the input of a deleted step not counted (86); five rubrics and an input (86); four
        // rubrics (57); no sheet (0).
        Assert.Equal(86, completeness[steps[0].Id]);
        Assert.Equal(86, completeness[steps[1].Id]);
        Assert.Equal(57, completeness[steps[4].Id]);
        Assert.Equal(0, completeness[project.Graph.SubStep.Id]);
        Assert.All(completeness, pair => Assert.Equal(oracle.CompletenessOf(pair.Key), pair.Value));
        Assert.Equal(4, completeness.Count);
    }

    /// <summary>
    /// AR-101: the view keeps the current content and says it is a draft (status), with the number of the latest
    /// validated version, null when the sheet was never validated. AR-100: the project gives its classification.
    /// </summary>
    [Fact]
    public async Task The_report_views_signal_a_draft_with_its_latest_validated_version_and_give_the_classification_of_the_project()
    {
        var project = ReportProject.New();
        var graph = project.Graph;
        var draftOverVersions = graph.Sheets[0] with { Status = SheetStatus.Draft };
        var records = project.Records.Select(record => ReferenceEquals(record, graph.Sheets[0]) ? draftOverVersions : record).ToList();
        var database = await databases.OfAsync(EngineKind.SqlServer);
        await using (var context = database.Context())
        {
            context.AddRange(records);
            await context.SaveChangesAsync(Token);
        }

        var sheets = (await ReadAsReportsAsync(database, "step_sheet")).Rows.Where(row => row[0] is Guid id && project.Identifiers.Contains(id))
            .ToDictionary(row => (Guid)row[0]!);
        var status = Column("step_sheet", "status");
        var version = Column("step_sheet", "validated_version");
        Assert.Equal(("draft", (object?)2), ((string?)sheets[graph.Steps[0].Id][status], sheets[graph.Steps[0].Id][version]));
        Assert.Equal(("draft", (object?)null), ((string?)sheets[graph.Steps[1].Id][status], sheets[graph.Steps[1].Id][version]));
        var projects = (await ReadAsReportsAsync(database, "project")).Rows.Where(row => row[0] is Guid id && id == project.ProjectId).ToList();
        Assert.Equal("strictly_confidential", Assert.Single(projects)[Column("project", "classification")]);
    }

    private static int Column(string view, string column) => ReportViewOracle.Columns[view].ToList().IndexOf(column);

    [Fact]
    public async Task The_raci_of_the_report_view_is_the_visible_raci_of_the_domain()
    {
        var project = ReportProject.New();
        var database = await WrittenAsync(project);
        var graph = project.Graph;
        var rows = RowsOf(await ReadAsReportsAsync(database, "step_raci"), project);
        // Stored: 7 roles. Shown: A and C of the first step, I of the second; hidden: the role of a parallel gateway, of a
        // deleted lane, of a lane of another map and of the lane of the step itself.
        Assert.Equal(new[]
        {
            ReportViewOracle.Row(graph.Steps[0].Id, graph.Lanes[1].Id, "A"),
            ReportViewOracle.Row(graph.Steps[0].Id, graph.Lanes[2].Id, "C"),
            ReportViewOracle.Row(graph.Steps[1].Id, graph.Lanes[2].Id, "I"),
        }.Order(StringComparer.Ordinal), rows);
        Assert.Equal(new ReportViewOracle(project.ProjectId, project.Records).RowsOf("step_raci").Order(StringComparer.Ordinal), rows);
    }
}
