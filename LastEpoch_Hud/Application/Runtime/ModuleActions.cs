using System;
using LastEpoch_Hud.Core.Runtime;

namespace LastEpoch_Hud.Application.Runtime;

/// <summary>Adapters implement only the lifecycle they actually participate in.</summary>
public static class ModuleActions
{
    private abstract class Named : IRuntimeModule
    {
        public string Name { get; }
        protected Named(string name) { Name = name; }
    }
    private sealed class Init : Named, IInitializeModule
    {
        private readonly Action action;
        public Init(string name, Action action) : base(name) { this.action = action; }
        public void Initialize() => action();
    }
    private sealed class Update : Named, ITickModule
    {
        private readonly Action action;
        public Update(string name, Action action) : base(name) { this.action = action; }
        public void Tick() => action();
    }
    private sealed class Gui : Named, IDrawModule
    {
        private readonly Action action;
        public Gui(string name, Action action) : base(name) { this.action = action; }
        public void Draw() => action();
    }
    private sealed class Loaded : Named, ISceneLoadedModule
    {
        private readonly Action<int, string> action;
        public Loaded(string name, Action<int, string> action) : base(name) { this.action = action; }
        public void SceneLoaded(int index, string name) => action(index, name);
    }
    private sealed class Unloaded : Named, ISceneUnloadedModule
    {
        private readonly Action<int, string> action;
        public Unloaded(string name, Action<int, string> action) : base(name) { this.action = action; }
        public void SceneUnloaded(int index, string name) => action(index, name);
    }
    private sealed class Stop : Named, IShutdownModule
    {
        private readonly Action action;
        public Stop(string name, Action action) : base(name) { this.action = action; }
        public void Shutdown() => action();
    }

    public static IInitializeModule Initialize(string name, Action action) => new Init(name, action ?? throw new ArgumentNullException(nameof(action)));
    public static ITickModule Tick(string name, Action action) => new Update(name, action ?? throw new ArgumentNullException(nameof(action)));
    public static IDrawModule Draw(string name, Action action) => new Gui(name, action ?? throw new ArgumentNullException(nameof(action)));
    public static ISceneLoadedModule SceneLoaded(string name, Action<int, string> action) => new Loaded(name, action ?? throw new ArgumentNullException(nameof(action)));
    public static ISceneUnloadedModule SceneUnloaded(string name, Action<int, string> action) => new Unloaded(name, action ?? throw new ArgumentNullException(nameof(action)));
    public static IShutdownModule Shutdown(string name, Action action) => new Stop(name, action ?? throw new ArgumentNullException(nameof(action)));
}
