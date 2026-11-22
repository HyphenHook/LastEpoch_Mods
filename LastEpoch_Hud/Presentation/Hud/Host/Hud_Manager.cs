using Il2Cpp;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts;

[RegisterTypeInIl2Cpp]
public class Hud_Manager : MonoBehaviour
{
    public Hud_Manager(System.IntPtr ptr)
        : base(ptr) { }

    public static AssetBundle asset_bundle;
    public static GameObject hud_object = null;
    public static bool mod_menu_open = false;

    private bool _hudInitializing;

    public static bool IsPauseOpen() => GamePauseMenu.IsOpen();

    void Awake()
    {
        HudBundleLoader.TryLoad(out asset_bundle);
    }

    void Update()
    {
        if (asset_bundle.IsNullOrDestroyed())
        {
            return;
        }
        mod_menu_open = HudMenuToggle.Next(mod_menu_open, !hud_object.IsNullOrDestroyed());
        HudScale.Apply();
        HudScale.FindCanvases(hud_object);
        GamePauseMenu.Probe(Time.unscaledTime);
        GamePauseMenu.HideDefaultButtons();
        if (ShouldInitHud())
        {
            InitHud();
        }
        UpdateLocale();

        if (hud_object.IsNullOrDestroyed())
        {
            return;
        }
        UpdateMenu(IsPauseOpen() || mod_menu_open);
    }

    private void UpdateMenu(bool menuOpen)
    {
        try
        {
            HudInputLock.Apply(menuOpen);
            if (!menuOpen)
            {
                HideMenu();
                return;
            }
            ShowMenu();
        }
        catch (System.Exception ex)
        {
            ErrorLog.Report(ex, "Hud_Manager.Update");
        }
    }

    private void RefreshHudContent()
    {
        try
        {
            UpdateHudContent();
        }
        catch (System.Exception ex)
        {
            ErrorLog.Report(ex, "Hud_Manager.Update_Hud_Content");
        }
    }

    private void ShowMenu()
    {
        RefreshHudContent();
        hud_object.active = true;
        ModUI.Settings.NumericSliderInputs.Tick(hud_object);
        HudEscapeResume.Tick();
    }

    private void HideMenu()
    {
        if (hud_object.active)
        {
            hud_object.active = false;
        }
    }

    private void InitHud()
    {
        _hudInitializing = true;
        Main.logger_instance?.Msg("[Startup] HUD prefab initialization started.");
        if (Main.debug)
        {
            Main.logger_instance.Msg("Hud Manager : Load hud object in assets");
        }
        if (HudPrefab.TryInstantiate(asset_bundle, out GameObject hud))
        {
            hud_object = hud;
            HudPrefab.Bind(hud_object);
        }
        _hudInitializing = false;
        Main.logger_instance?.Msg("[Startup] HUD prefab initialization finished.");
    }

    // The UI is an in-game overlay. Building Unity UI while the client
    // transitions through splash/login can race native scene initialization.
    // The player reference is populated independently by Refs_Manager.
    private bool ShouldInitHud()
    {
        return Scenes.IsGameScene()
            && !Refs_Manager.player_actor.IsNullOrDestroyed()
            && !asset_bundle.IsNullOrDestroyed()
            && hud_object.IsNullOrDestroyed()
            && !_hudInitializing;
    }

    private void UpdateLocale()
    {
        if (!Locales.update || hud_object.IsNullOrDestroyed())
            return;
        ModUI.Settings.LocaleRegistry.RefreshTree(hud_object);
        Locales.update = false;
    }

    private void UpdateHudContent()
    {
        ModUI.Shell.HudLayout.RefreshActivePage();
        if (!ModUI.ForceDrop.ForceDropModel.Enabled || !ModUI.ForceDrop.ForceDropModel.Initialized)
        {
            return;
        }
        if (!ModUI.ForceDrop.ForceDropModel.TypeInitialized)
        {
            ModUI.ForceDrop.ForceDropModel.InitForceDrop();
            return;
        }
        ModUI.ForceDrop.ForceDropBuilder.Tick();
    }
}
