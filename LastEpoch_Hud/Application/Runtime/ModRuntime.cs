using System;
using System.Collections.Generic;
using LastEpoch_Hud.Core.Runtime;

namespace LastEpoch_Hud.Application.Runtime;

/// <summary>Single host-thread dispatcher. Registration order applies independently to every phase.
/// Initialization and shutdown are attempted once; recurring callbacks remain retryable after faults.
/// Scene notifications can arrive before initialization. Shutdown makes every callback inert.</summary>
public sealed class ModRuntime
{
    private sealed class Call
    {
        public string Name;
        public Action Invoke;
        public Action<int, string> SceneInvoke;
        public bool Reported;
        public long LastReport;
        public int Suppressed;
    }

    private readonly Dictionary<LifecyclePhase, List<Call>> calls = new();
    private readonly IRuntimeFaultSink faults;
    private readonly Func<long> milliseconds;
    private readonly long reportInterval;
    public bool IsInitialized { get; private set; }
    public bool IsShutdown { get; private set; }

    public ModRuntime(IEnumerable<IRuntimeModule> modules, IRuntimeFaultSink faults,
        Func<long> milliseconds = null, long reportIntervalMilliseconds = 60_000)
    {
        if (modules == null) throw new ArgumentNullException(nameof(modules));
        this.faults = faults ?? throw new ArgumentNullException(nameof(faults));
        this.milliseconds = milliseconds ?? (() => Environment.TickCount64);
        if (reportIntervalMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(reportIntervalMilliseconds));
        reportInterval = reportIntervalMilliseconds;
        foreach (LifecyclePhase phase in Enum.GetValues(typeof(LifecyclePhase))) calls.Add(phase, new List<Call>());
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            if (module == null || string.IsNullOrWhiteSpace(module.Name))
                throw new ArgumentException("Every module must have a name.", nameof(modules));
            if (!names.Add(module.Name)) throw new ArgumentException("Duplicate runtime module: " + module.Name, nameof(modules));
            if (module is IInitializeModule init) Add(module.Name, LifecyclePhase.Initialize, init.Initialize);
            if (module is ITickModule tick) Add(module.Name, LifecyclePhase.Tick, tick.Tick);
            if (module is IDrawModule draw) Add(module.Name, LifecyclePhase.Draw, draw.Draw);
            if (module is ISceneLoadedModule loaded) calls[LifecyclePhase.SceneLoaded].Add(new Call { Name = module.Name, SceneInvoke = loaded.SceneLoaded });
            if (module is ISceneUnloadedModule unloaded) calls[LifecyclePhase.SceneUnloaded].Add(new Call { Name = module.Name, SceneInvoke = unloaded.SceneUnloaded });
            if (module is IShutdownModule stop) Add(module.Name, LifecyclePhase.Shutdown, stop.Shutdown);
        }
    }

    private void Add(string name, LifecyclePhase phase, Action action) =>
        calls[phase].Add(new Call { Name = name, Invoke = action });

    public void Initialize()
    {
        if (IsInitialized || IsShutdown) return;
        IsInitialized = true;
        Dispatch(LifecyclePhase.Initialize);
    }

    public void Tick() { if (IsInitialized && !IsShutdown) Dispatch(LifecyclePhase.Tick); }
    public void Draw() { if (IsInitialized && !IsShutdown) Dispatch(LifecyclePhase.Draw); }
    public void SceneLoaded(int buildIndex, string sceneName)
    {
        if (IsShutdown) return;
        Dispatch(LifecyclePhase.SceneLoaded, buildIndex, sceneName);
    }
    public void SceneUnloaded(int buildIndex, string sceneName)
    {
        if (IsShutdown) return;
        Dispatch(LifecyclePhase.SceneUnloaded, buildIndex, sceneName);
    }
    public void Shutdown()
    {
        if (IsShutdown) return;
        IsShutdown = true;
        Dispatch(LifecyclePhase.Shutdown);
    }

    private void Dispatch(LifecyclePhase phase, int buildIndex = 0, string sceneName = null)
    {
        foreach (var call in calls[phase])
        {
            try
            {
                if (call.SceneInvoke != null) call.SceneInvoke(buildIndex, sceneName);
                else call.Invoke();
            }
            catch (Exception exception)
            {
                // Neither a failed clock nor a failed logger may interrupt later modules.
                try
                {
                    long now = milliseconds();
                    if (call.Reported && now - call.LastReport < reportInterval)
                    {
                        if (call.Suppressed < int.MaxValue) call.Suppressed++;
                        continue;
                    }
                    int repeats = call.Suppressed;
                    call.Reported = true;
                    call.LastReport = now;
                    call.Suppressed = 0;
                    faults.Report(call.Name, phase, exception, repeats);
                }
                catch { /* Fault reporting is best effort. */ }
            }
        }
    }
}
