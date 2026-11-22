using LastEpoch_Hud.Application.Testing;
using LastEpoch_Hud.Core.Testing;

namespace LastEpoch_Hud.Tests.Runtime;

public sealed class TestActionCatalogTests
{
    private sealed class Provider(params TestAction[] actions) : ITestActionProvider
    {
        public IEnumerable<TestAction> GetActions() => actions;
    }

    [Fact]
    public void CompositionPreservesOrderWithoutExecutingCommands()
    {
        int executions = 0;
        var catalog = new TestActionCatalog(
            new Provider(new TestAction("equipment", "Equipment", () => executions++)),
            new Provider(new TestAction("orb", "Orb", () => executions += 2)));
        Assert.Equal(new[] { "equipment", "orb" }, catalog.Actions.Select(action => action.Id));
        Assert.Equal(0, executions);
        catalog.Actions[1].Execute();
        Assert.Equal(2, executions);
    }

    [Fact]
    public void DuplicateIdsAcrossFeaturesAreRejected() => Assert.Throws<ArgumentException>(() =>
        new TestActionCatalog(new Provider(new TestAction("same", "First", () => { })),
            new Provider(new TestAction("same", "Second", () => { }))));

    [Fact]
    public void CatalogSnapshotsTheProvidersCommands()
    {
        var actions = new[] { new TestAction("first", "First", () => { }) };
        var catalog = new TestActionCatalog(new Provider(actions));
        actions[0] = new TestAction("changed", "Changed", () => { });
        Assert.Equal("first", catalog.Actions[0].Id);
    }
}
