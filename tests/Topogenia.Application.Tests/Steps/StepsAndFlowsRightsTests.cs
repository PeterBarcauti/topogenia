// StepsAndFlowsRightsTests.cs
// Every command of the steps, flows and layout requires the facilitator: a contributor or a reader is refused, nothing is written (annex A 1).

using Topogenia.Application.Flows;
using Topogenia.Application.Layout;
using Topogenia.Application.Steps;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Flows;
using Topogenia.Domain.Layout;
using Topogenia.Domain.Model;
using Topogenia.Domain.Steps;
using Topogenia.Domain.Structure;

namespace Topogenia.Application.Tests.Steps;

public sealed class StepsAndFlowsRightsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string[] Names = ["create", "update", "insert", "delete", "layout", "move", "flow", "relabel", "reverse", "unlink", "bar"];

    public static TheoryData<string> Commands() => [.. Names];

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var command in Names)
        {
            data.Add(command, "contributor");
            data.Add(command, "reader");
        }
        return data;
    }

    /// <summary>Runs the named command on a map of two steps A and B linked by one flow.</summary>
    private static Task Run(TestProject project, string command, Guid a, Guid b, Guid flow) => command switch
    {
        "create" => project.Runner.RunAsync(new CreateStepCommand(new CreateStepRequest(project.RootMapId, project.LaneId, 5, "Commander")), Token),
        "update" => project.Runner.RunAsync(new UpdateStepCommand(new UpdateStepRequest(a) { Label = new FieldChange<string?>("Valider") }), Token),
        "insert" => project.Runner.RunAsync(new InsertStepCommand(new InsertStepRequest(a, InsertPosition.After, "Vérifier")), Token),
        "delete" => project.Runner.RunAsync(new DeleteStepCommand(a, stitch: true), Token),
        "layout" => project.Runner.RunAsync(new ApplyLayoutCommand(new LayoutRequest(project.RootMapId, Steps: [new StepPlacement(b, project.LaneId, 4)]),
            LayoutPurpose.Align), Token),
        "move" => project.Runner.RunAsync(new MoveStepsCommand(new GroupMoveRequest(project.RootMapId, [new StepPlacement(b, project.LaneId, 4)])), Token),
        "flow" => project.Runner.RunAsync(new CreateFlowCommand(new CreateFlowRequest(project.RootMapId, b, a, null)), Token),
        "relabel" => project.Runner.RunAsync(new RelabelFlowCommand(new RelabelFlowRequest(flow, "non")), Token),
        "reverse" => project.Runner.RunAsync(new ReverseFlowCommand(flow), Token),
        "unlink" => project.Runner.RunAsync(new DeleteFlowCommand(flow), Token),
        "bar" => project.Runner.RunAsync(new ApplyCommandBarCommand(project.RootMapId, "E002 > E001"), Token),
        _ => throw new ArgumentOutOfRangeException(nameof(command)),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_member_below_the_facilitator_is_refused_and_nothing_is_written(string command, string role)
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var flow = project.Flow(a, b);
        project.ActingAs(role == "reader" ? project.Reader : project.Contributor);
        var refusal = await Assert.ThrowsAsync<DomainException>(() => Run(project, command, a, b, flow));
        Assert.Equal(ErrorCodes.AccessDenied, refusal.Code);
        Assert.Equal(2, project.Database.All<Step>().Count(step => step.DeletedAt is null));
        Assert.Equal(((project.LaneId, 0, 0), (project.LaneId, 1, 0)), (project.CellOf(a), project.CellOf(b)));
        Assert.Equal([(a, b, null)], project.LiveFlows());
        Assert.Empty(project.Database.Entries);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task The_facilitator_runs_the_command_and_it_is_logged(string command)
    {
        var project = new TestProject();
        var (a, b) = (project.Step(0), project.Step(1));
        var flow = project.Flow(a, b);
        await Run(project, command, a, b, flow);
        Assert.Single(project.Database.Entries);
    }
}
