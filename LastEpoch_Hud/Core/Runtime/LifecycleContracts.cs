using System;

namespace LastEpoch_Hud.Core.Runtime;

public interface IRuntimeModule { string Name { get; } }
public interface IInitializeModule : IRuntimeModule { void Initialize(); }
public interface ITickModule : IRuntimeModule { void Tick(); }
public interface IDrawModule : IRuntimeModule { void Draw(); }
public interface ISceneLoadedModule : IRuntimeModule { void SceneLoaded(int buildIndex, string sceneName); }
public interface ISceneUnloadedModule : IRuntimeModule { void SceneUnloaded(int buildIndex, string sceneName); }
public interface IShutdownModule : IRuntimeModule { void Shutdown(); }

public enum LifecyclePhase { Initialize, Tick, Draw, SceneLoaded, SceneUnloaded, Shutdown }

/// <summary>Called on the host thread. Repeats counts failures suppressed since the last report.</summary>
public interface IRuntimeFaultSink
{
    void Report(string module, LifecyclePhase phase, Exception exception, int repeats);
}
