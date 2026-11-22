using LastEpoch_Hud.Bootstrap.Composition;

namespace LastEpoch_Hud;

/// <summary>MelonLoader adapter. Feature wiring belongs to the composition root.</summary>
public class Main : MelonLoader.MelonMod
{
    public static MelonLoader.MelonLogger.Instance logger_instance = null;
    public const string company_name = "Eleventh Hour Games";
    public const string game_name = "Last Epoch";
    public const string mod_name = "LastEpoch_Hud";
    public const string mod_version = "5.1.1";
    public static bool debug = false;

    public override void OnInitializeMelon()
    {
        logger_instance = LoggerInstance;
        LoggerInstance.Msg(Scripts.Core.Diagnostics.BuildStamp.Format(BuildInfo.Commit, BuildInfo.Dirty, BuildInfo.Time));
        LoggerInstance.Msg("[Offline] Click Play Offline to continue. Online actions are blocked.");
        ModComposition.Create(HarmonyInstance).Initialize();
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName) =>
        ModComposition.Runtime?.SceneLoaded(buildIndex, sceneName);

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) =>
        ModComposition.Runtime?.SceneUnloaded(buildIndex, sceneName);

    public override void OnLateUpdate() => ModComposition.Runtime?.Tick();
    public override void OnGUI() => ModComposition.Runtime?.Draw();
    public override void OnApplicationQuit() => ModComposition.Runtime?.Shutdown();
}
