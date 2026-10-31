// OperationStackTests.cs
// Chains, undo order, redo order, depth of 50 and redo stack cleared by a new operation (spec 4.6, ADR 042).

using Topogenia.Application.OperationLog;
using Topogenia.Application.Tests.Support;
using Topogenia.Domain.Model;

namespace Topogenia.Application.Tests.OperationLog;

public sealed class OperationStackTests
{
    private static readonly Guid Map = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid User = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private static Guid Id(int number) => new($"00000000-0000-0000-0000-{number:x12}");

    private static OperationLogEntry Entry(int number, int sequence, bool undone = false, int? redoneFrom = null, Guid? user = null, Guid? map = null) =>
        new(Id(number), Guid.Empty, map ?? Map, user ?? User, "lane.add", sequence, "[]", "[]", "[]", TestProject.Now,
            undone ? TestProject.Now : null, redoneFrom is { } from ? Id(from) : null);

    [Fact]
    public void An_operation_and_its_redos_form_one_chain_whose_latest_entry_gives_its_state()
    {
        var entries = new[] { Entry(1, 0, undone: true), Entry(2, 3, undone: true, redoneFrom: 1), Entry(3, 5, redoneFrom: 1) };
        var chain = Assert.Single(OperationStack.Chains(entries));
        Assert.Equal(Id(1), chain.RootId);
        Assert.Equal(Id(3), chain.Latest.Id);
        Assert.False(chain.IsUndone);
    }

    [Fact]
    public void The_next_undo_is_the_most_recent_live_operation()
    {
        var entries = new[] { Entry(1, 0), Entry(2, 1), Entry(3, 2, undone: true) };
        Assert.Equal([Id(2), Id(1)], OperationStack.Undoable(entries).Select(chain => chain.Latest.Id));
    }

    [Fact]
    public void The_next_redo_is_the_operation_undone_last()
    {
        // A (0) and B (1) done, B then A undone: A was undone last, it is redone first.
        var entries = new[] { Entry(1, 0, undone: true), Entry(2, 1, undone: true) };
        Assert.Equal([Id(1), Id(2)], OperationStack.Redoable(entries).Select(chain => chain.Latest.Id));
    }

    [Fact]
    public void A_redone_operation_leaves_the_redo_stack_and_is_undone_first()
    {
        // B (1) and A (0) undone, A redone as entry 3 (sequence 2): B remains to redo, A is the next undo.
        var entries = new[] { Entry(1, 0, undone: true), Entry(2, 1, undone: true), Entry(3, 2, redoneFrom: 1) };
        Assert.Equal([Id(2)], OperationStack.Redoable(entries).Select(chain => chain.Latest.Id));
        Assert.Equal([Id(3)], OperationStack.Undoable(entries).Select(chain => chain.Latest.Id));
    }

    [Fact]
    public void A_new_operation_clears_the_whole_redo_stack_with_every_entry_of_its_chains()
    {
        var entries = new[]
        {
            Entry(1, 0), Entry(2, 1, undone: true), Entry(3, 2, undone: true, redoneFrom: 2), Entry(4, 3, undone: true),
        };
        Assert.Equal([Id(3), Id(2), Id(4)], OperationStack.ClearedByNewOperation(entries));
    }

    [Fact]
    public void The_stack_keeps_the_depth_once_the_new_operation_is_added()
    {
        var entries = Enumerable.Range(0, OperationStack.Depth).Select(index => Entry(index + 1, index)).ToList();
        Assert.Equal([Id(1)], OperationStack.ClearedByNewOperation(entries));
        Assert.Empty(OperationStack.ClearedByNewOperation(entries.Skip(1).ToList()));
    }

    [Fact]
    public void An_operation_beyond_the_depth_leaves_with_its_whole_chain()
    {
        var entries = new List<OperationLogEntry> { Entry(1, 0, undone: true), Entry(2, 1, redoneFrom: 1) };
        entries.AddRange(Enumerable.Range(0, OperationStack.Depth - 1).Select(index => Entry(index + 10, index + 2)));
        Assert.Equal([Id(2), Id(1)], OperationStack.ClearedByNewOperation(entries));
    }

    [Fact]
    public void The_depth_of_the_stack_is_fifty_as_the_1x() => Assert.Equal(50, OperationStack.Depth);

    [Fact]
    public async Task Logging_a_new_operation_clears_only_the_redo_stack_of_its_user_on_its_map()
    {
        var project = new TestProject();
        var other = project.SubMap(project.Step(0)).MapId;
        project.Database.AddEntry(Entry(1, 0, undone: true, map: project.RootMapId, user: project.Facilitator.Id));
        project.Database.AddEntry(Entry(2, 1, undone: true, map: project.RootMapId, user: project.Admin.Id));
        project.Database.AddEntry(Entry(3, 0, undone: true, map: other, user: project.Facilitator.Id));
        await project.Runner.RunAsync(new AddLaneProbe(project.RootMapId, "Achats"), TestContext.Current.CancellationToken);
        Assert.Equal([Id(2), Id(3)], project.Database.Entries.Select(entry => entry.Id).Where(id => id != project.Database.Entries[^1].Id));
        Assert.Equal(2, project.Database.Entries[^1].Sequence);
    }

    [Fact]
    public async Task Logging_the_fifty_first_operation_removes_the_oldest_with_its_redos()
    {
        var project = new TestProject();
        var map = project.RootMapId;
        var user = project.Facilitator.Id;
        project.Database.AddEntry(Entry(1, 0, undone: true, map: map, user: user));
        project.Database.AddEntry(Entry(2, 1, redoneFrom: 1, map: map, user: user));
        for (var index = 0; index < OperationStack.Depth - 1; index++)
            project.Database.AddEntry(Entry(index + 10, index + 2, map: map, user: user));
        var outcome = await project.Runner.RunAsync(new AddLaneProbe(map, "Achats"), TestContext.Current.CancellationToken);
        Assert.Equal(OperationStack.Depth, project.Database.Entries.Count);
        Assert.DoesNotContain(project.Database.Entries, entry => entry.Id == Id(1) || entry.Id == Id(2));
        Assert.Equal(outcome.OperationId, project.Database.Entries[^1].Id);
        Assert.Equal(OperationStack.Depth + 1, project.Database.Entries[^1].Sequence);
    }
}
