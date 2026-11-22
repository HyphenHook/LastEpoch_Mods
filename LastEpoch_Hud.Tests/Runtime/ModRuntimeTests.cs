using LastEpoch_Hud.Application.Runtime;
using LastEpoch_Hud.Core.Runtime;

namespace LastEpoch_Hud.Tests.Runtime;

public sealed class ModRuntimeTests
{
    private sealed class Sink : IRuntimeFaultSink
    {
        public readonly List<(string Name, LifecyclePhase Phase, Exception Error, int Repeats)> Reports = new();
        public bool Throw;
        public void Report(string module, LifecyclePhase phase, Exception exception, int repeats)
        {
            Reports.Add((module, phase, exception, repeats));
            if (Throw) throw new InvalidOperationException("logger unavailable");
        }
    }

    private sealed class Probe : IInitializeModule, ITickModule, IDrawModule,
        ISceneLoadedModule, ISceneUnloadedModule, IShutdownModule
    {
        public string Name { get; }
        private readonly List<string> events;
        public LifecyclePhase? Fail;
        public Probe(string name, List<string> events) { Name = name; this.events = events; }
        private void Record(LifecyclePhase phase, string detail = "")
        {
            events.Add($"{Name}/{phase}{detail}");
            if (Fail == phase) throw new InvalidOperationException(Name);
        }
        public void Initialize() => Record(LifecyclePhase.Initialize);
        public void Tick() => Record(LifecyclePhase.Tick);
        public void Draw() => Record(LifecyclePhase.Draw);
        public void SceneLoaded(int buildIndex, string sceneName) => Record(LifecyclePhase.SceneLoaded, $"/{buildIndex}/{sceneName}");
        public void SceneUnloaded(int buildIndex, string sceneName) => Record(LifecyclePhase.SceneUnloaded, $"/{buildIndex}/{sceneName}");
        public void Shutdown() => Record(LifecyclePhase.Shutdown);
    }

    private static void Invoke(ModRuntime runtime, LifecyclePhase phase)
    {
        switch (phase)
        {
            case LifecyclePhase.Initialize: runtime.Initialize(); break;
            case LifecyclePhase.Tick: runtime.Tick(); break;
            case LifecyclePhase.Draw: runtime.Draw(); break;
            case LifecyclePhase.SceneLoaded: runtime.SceneLoaded(17, "Echo"); break;
            case LifecyclePhase.SceneUnloaded: runtime.SceneUnloaded(18, "Town"); break;
            case LifecyclePhase.Shutdown: runtime.Shutdown(); break;
        }
    }

    [Theory]
    [InlineData(LifecyclePhase.Initialize)]
    [InlineData(LifecyclePhase.Tick)]
    [InlineData(LifecyclePhase.Draw)]
    [InlineData(LifecyclePhase.SceneLoaded)]
    [InlineData(LifecyclePhase.SceneUnloaded)]
    [InlineData(LifecyclePhase.Shutdown)]
    public void EveryPhasePreservesOrderAndContinuesPastFailure(LifecyclePhase phase)
    {
        var events = new List<string>();
        var sink = new Sink();
        var first = new Probe("first", events);
        var failing = new Probe("broken", events) { Fail = phase };
        var last = new Probe("last", events);
        var runtime = new ModRuntime(new[] { first, failing, last }, sink);
        if (phase != LifecyclePhase.Initialize) runtime.Initialize();
        events.Clear();
        Invoke(runtime, phase);
        Assert.Equal(new[] { "first", "broken", "last" }, events.Select(e => e.Split('/')[0]));
        var fault = Assert.Single(sink.Reports);
        Assert.Equal("broken", fault.Name);
        Assert.Equal(phase, fault.Phase);
        Assert.IsType<InvalidOperationException>(fault.Error);
    }

    [Fact]
    public void InitializationAndShutdownAreOnceOnlyAndShutdownIsTerminal()
    {
        var events = new List<string>();
        var runtime = new ModRuntime(new[] { new Probe("module", events) }, new Sink());
        runtime.Tick();
        runtime.Draw();
        Assert.Empty(events);
        runtime.Initialize();
        runtime.Initialize();
        runtime.Shutdown();
        runtime.Shutdown();
        runtime.Initialize();
        runtime.Tick();
        runtime.Draw();
        runtime.SceneLoaded(1, "ignored");
        runtime.SceneUnloaded(1, "ignored");
        Assert.Equal(new[] { "module/Initialize", "module/Shutdown" }, events);
        Assert.True(runtime.IsInitialized);
        Assert.True(runtime.IsShutdown);
    }

    [Fact]
    public void ShutdownBeforeInitializationStillRunsCleanupExactlyOnce()
    {
        int stops = 0;
        var runtime = new ModRuntime(new[] { ModuleActions.Shutdown("cleanup", () => stops++) }, new Sink());
        runtime.Shutdown();
        runtime.Initialize();
        runtime.Shutdown();
        Assert.Equal(1, stops);
        Assert.False(runtime.IsInitialized);
    }

    [Fact]
    public void ScenesForwardExactArgumentsBeforeInitialization()
    {
        var events = new List<string>();
        var runtime = new ModRuntime(new[] { new Probe("scene", events) }, new Sink());
        runtime.SceneLoaded(-7, "Actual Callback Scene");
        runtime.SceneUnloaded(93, null);
        Assert.Equal(new[] { "scene/SceneLoaded/-7/Actual Callback Scene", "scene/SceneUnloaded/93/" }, events);
    }

    [Fact]
    public void NestedSceneNotificationsDoNotReplaceOuterArguments()
    {
        var events = new List<string>();
        ModRuntime runtime = null;
        runtime = new ModRuntime(new IRuntimeModule[] {
            ModuleActions.SceneLoaded("trigger", (index, _) =>
            {
                if (index == 1) runtime.SceneLoaded(2, "Nested");
            }),
            ModuleActions.SceneLoaded("observer", (index, name) => events.Add($"{index}/{name}")),
        }, new Sink());
        runtime.SceneLoaded(1, "Outer");
        Assert.Equal(new[] { "2/Nested", "1/Outer" }, events);
    }

    [Fact]
    public void RecurringFailuresAreRetriedButReportsAreThrottledAcrossRecovery()
    {
        long now = 0;
        int attempts = 0;
        bool fail = true;
        var sink = new Sink();
        var runtime = new ModRuntime(new[] { ModuleActions.Tick("worker", () =>
        {
            attempts++;
            if (fail) throw new Exception("failure");
        }) }, sink, () => now, 100);
        runtime.Initialize();
        runtime.Tick();
        now = 1;
        runtime.Tick();
        fail = false;
        runtime.Tick();
        fail = true;
        now = 99;
        runtime.Tick();
        Assert.Single(sink.Reports);
        now = 100;
        runtime.Tick();
        Assert.Equal(5, attempts);
        Assert.Equal(new[] { 0, 2 }, sink.Reports.Select(r => r.Repeats));
        now = 200;
        runtime.Tick();
        Assert.Equal(0, sink.Reports[2].Repeats);
    }

    [Fact]
    public void ThrottlingIsIndependentPerModuleAndPhase()
    {
        var sink = new Sink();
        var events = new List<string>();
        var a = new Probe("a", events) { Fail = LifecyclePhase.Tick };
        var b = new Probe("b", events) { Fail = LifecyclePhase.Tick };
        var runtime = new ModRuntime(new[] { a, b }, sink, () => 0);
        runtime.Initialize();
        runtime.Tick();
        runtime.Tick();
        a.Fail = LifecyclePhase.Draw;
        runtime.Draw();
        Assert.Equal(new[] { ("a", LifecyclePhase.Tick), ("b", LifecyclePhase.Tick), ("a", LifecyclePhase.Draw) },
            sink.Reports.Select(r => (r.Name, r.Phase)));
    }

    [Fact]
    public void FailedFaultSinkCannotBreakDispatchOrSpamReports()
    {
        int later = 0;
        var sink = new Sink { Throw = true };
        var runtime = new ModRuntime(new IRuntimeModule[] {
            ModuleActions.Tick("broken", () => throw new Exception()),
            ModuleActions.Tick("later", () => later++)
        }, sink, () => 0);
        runtime.Initialize();
        runtime.Tick();
        runtime.Tick();
        Assert.Equal(2, later);
        Assert.Single(sink.Reports);
    }

    [Fact]
    public void FailedClockCannotBreakDispatch()
    {
        int later = 0;
        var runtime = new ModRuntime(new IRuntimeModule[] {
            ModuleActions.Tick("broken", () => throw new Exception()),
            ModuleActions.Tick("later", () => later++)
        }, new Sink(), () => throw new Exception("clock"));
        runtime.Initialize();
        runtime.Tick();
        Assert.Equal(1, later);
    }

    [Fact]
    public void ConstructorSnapshotsRegistrations()
    {
        int ticks = 0;
        var registrations = new List<IRuntimeModule> { ModuleActions.Tick("first", () => ticks++) };
        var runtime = new ModRuntime(registrations, new Sink());
        registrations.Clear();
        runtime.Initialize();
        runtime.Tick();
        Assert.Equal(1, ticks);
    }

    [Fact]
    public void NarrowAdaptersParticipateOnlyInTheirOwnPhase()
    {
        Assert.IsAssignableFrom<ITickModule>(ModuleActions.Tick("tick", () => { }));
        Assert.IsNotAssignableFrom<IInitializeModule>(ModuleActions.Tick("tick", () => { }));
        Assert.IsNotAssignableFrom<IDrawModule>(ModuleActions.Tick("tick", () => { }));
        Assert.IsNotAssignableFrom<IShutdownModule>(ModuleActions.Tick("tick", () => { }));
    }

    [Fact]
    public void InvalidRegistrationFailsAtConstruction()
    {
        var sink = new Sink();
        Assert.Throws<ArgumentNullException>(() => new ModRuntime(null, sink));
        Assert.Throws<ArgumentNullException>(() => new ModRuntime(Array.Empty<IRuntimeModule>(), null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModRuntime(Array.Empty<IRuntimeModule>(), sink, reportIntervalMilliseconds: -1));
        Assert.Throws<ArgumentException>(() => new ModRuntime(new IRuntimeModule[] { null }, sink));
        Assert.Throws<ArgumentException>(() => new ModRuntime(new[] { ModuleActions.Tick(" ", () => { }) }, sink));
        Assert.Throws<ArgumentException>(() => new ModRuntime(new IRuntimeModule[] {
            ModuleActions.Tick("same", () => { }), ModuleActions.Draw("same", () => { }) }, sink));
        Assert.Throws<ArgumentNullException>(() => ModuleActions.Tick("tick", null));
    }
}
