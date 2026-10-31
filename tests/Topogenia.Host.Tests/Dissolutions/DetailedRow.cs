// DetailedRow.cs
// A detailed step ready to be dissolved, drawn through the routes, and the documents of its remainder written through the application.

using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Commands;
using Topogenia.Application.Documents;
using Topogenia.Application.Ports;
using Topogenia.Domain.Documents;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Host.Users;
using static Topogenia.Host.Tests.Selection.MapDrawing;

namespace Topogenia.Host.Tests.Dissolutions;

/// <summary>
/// On the root map: Before, Macro and After linked in a row; the sub-map of Macro, made by « Detail the selection », holds
/// First and Second linked. Macro produces the documents of the remainder. The document routes belong to another lot
/// (lot C): the documents are written by the command of the application, run in a scope of the host as the facilitator.
/// </summary>
internal sealed record DetailedRow(ApiProject Project, Guid Before, Guid Macro, Guid After, Guid SubMap, Guid First, Guid Second, IReadOnlyList<Guid> Documents)
{
    public static async Task<DetailedRow> CreateAsync(ApiProject project, params string[] documents)
    {
        ArgumentNullException.ThrowIfNull(project);
        var row = await RowAsync(project, "Commander", "Valider", "Signer", "Livrer");
        using var facilitator = project.Facilitator;
        var grouped = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/group", new { steps = new[] { row[1], row[2] }, label = "Achats" })).Ok();
        var macro = grouped.GetProperty("id").GetGuid();
        var written = new List<Guid>();
        foreach (var name in documents)
            written.Add(await OutputAsync(project, macro, name));
        return new DetailedRow(project, row[0], macro, row[3], Ids(grouped)[0], row[1], row[2], written);
    }

    /// <summary>The version of Macro read by the facilitator, for `If-Match`.</summary>
    public async Task<int> MacroVersionAsync() => Step(await Project.MapAsync(), Macro).GetProperty("version").GetInt32();

    private static async Task<Guid> OutputAsync(ApiProject project, Guid stepId, string name)
    {
        using var facilitator = project.Facilitator;
        var me = (await facilitator.GetAsync("/api/me")).Ok();
        await using var scope = project.Host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<RequestUser>().User =
            new CurrentUser(me.GetProperty("id").GetGuid(), Logins.Facilitator, me.GetProperty("display_name").GetString()!, "fr", false);
        var runner = scope.ServiceProvider.GetRequiredService<CommandRunner>();
        var outcome = await runner.RunAsync(new AddDocumentCommand(stepId, new DocumentInput("output", name, null, null, null)), TestContext.Current.CancellationToken);
        return outcome.Result;
    }
}
