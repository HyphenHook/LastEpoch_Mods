using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Kills;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Probe;
using LastEpoch_Hud.Scripts.ModUI;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter;

[RegisterTypeInIl2Cpp]
public class Items_HeadHunter : MonoBehaviour
{
    private const double ScenePollSeconds = 0.25;

    private static readonly CustomUniqueRegistrar _registrar = new(CreateDefinition());

    private static readonly HeadhunterActiveSceneGate _sceneGate = new();

    private static readonly IntervalGate _scenePoll = new(ScenePollSeconds);

    public Items_HeadHunter(System.IntPtr ptr)
        : base(ptr) { }

    private void Awake()
    {
        HeadhunterConfigLoader.Load();
        SceneManager.add_sceneLoaded(new System.Action<Scene, LoadSceneMode>(OnSceneLoaded));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HeadhunterIconLoads.AllowRetry();
        EnterActiveScene(SceneManager.GetActiveScene());
        ResetRunIfCharacterExit(scene.name);
    }

    /// <summary>Enters the active scene when the game switched it without a load event.</summary>
    private static void PollActiveScene(double now)
    {
        if (!_scenePoll.IsDue(now))
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!_sceneGate.IsNew(scene.handle))
        {
            return;
        }

        EnterActiveScene(scene);
    }

    /// <summary>Re-evaluates the zone pause and probe once per newly active scene.</summary>
    private static void EnterActiveScene(Scene scene)
    {
        string name = scene.name;
        if (!_sceneGate.TryEnter(scene.handle, name))
        {
            return;
        }

        HeadhunterTimerPause.OnActiveSceneChanged(name, Time.unscaledTime);
        HeadhunterProbe.OnActiveSceneChanged(name, Time.unscaledTime);
    }

    /// <summary>Clears the HH run on login/character select.</summary>
    private static void ResetRunIfCharacterExit(string sceneName)
    {
        if (!HeadhunterRunReset.IsCharacterExit(sceneName))
        {
            return;
        }

        HeadhunterBuffClearer.ClearAll(HeadhunterConfigLoader.Resolved);
        if (ModSettings.Debug.Enabled.Value)
        {
            Main.logger_instance?.Msg("Headhunter run state reset");
        }
    }

    private void Update()
    {
        _registrar.Update();
        HeadhunterKillSource.EnsureHooked();
        HeadhunterConfigLoader.ReloadIfChanged(Time.unscaledTime);
        PollActiveScene(Time.unscaledTime);
        HeadhunterTimerPause.Tick(Time.unscaledTime);
        HeadhunterBuffBar.Tick(Time.unscaledTime);
        HeadhunterBarHover.Tick();
        HeadhunterHeartbeat.Tick(Time.unscaledTime);
        HeadhunterProbe.Tick(Time.unscaledTime);
        MonsterModDump.Tick(Time.unscaledTime);
    }

    private static CustomUniqueDefinition CreateDefinition()
    {
        return new CustomUniqueDefinition
        {
            Spec = CustomUniqueSpecs.Headhunter,
            SubtypeNameKey = CustomItemLocaleKeys.HeadhunterSubtype,
            UniqueNameKey = CustomItemLocaleKeys.HeadhunterName,
            LoreKey = CustomItemLocaleKeys.HeadhunterLore,
            Description = HeadhunterLocales.Description,
            Flags = () =>
                new CustomUniqueFlags(
                    Save_Manager.instance.data.Items.Headhunter.WeaverWill,
                    Save_Manager.instance.data.Items.Headhunter.UniqueDrop,
                    Save_Manager.instance.data.Items.Headhunter.BaseDrop
                ),
            Implicits = CustomUniqueAffixes.HeadhunterImplicits,
            Mods = CustomUniqueAffixes.HeadhunterMods,
            TooltipEntries = () => CustomUniqueAffixes.HeadhunterTooltip,
        };
    }
}
