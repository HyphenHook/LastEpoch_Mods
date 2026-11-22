using System;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Keybind;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Character;

[RegisterTypeInIl2Cpp]
public class Character_TpSafe : MonoBehaviour
{
    const string Destination = "EoT";
    bool focused = true;
    bool wasHeld;
    string previousBinding = "";
    bool previousEnabled;
    string pendingScene;
    float retryAfter;
    public static Character_TpSafe instance { get; private set; }

    public Character_TpSafe(IntPtr ptr)
        : base(ptr) { }

    void Awake()
    {
        instance = this;
    }

    void OnApplicationFocus(bool value)
    {
        focused = value;
        wasHeld = true;
    }

    void Update()
    {
        string binding = ModSettings.SafeTeleport.Key.Value ?? "";
        bool enabled = ModSettings.SafeTeleport.Enabled.Value;
        bool held = KeybindMatcher.IsHeld(binding);
        bool changed = binding != previousBinding || enabled != previousEnabled;
        previousBinding = binding;
        previousEnabled = enabled;
        bool pressed = held && !wasHeld && !changed;
        // Track input even when blocked. Closing a menu while holding the key
        // must not act as a fresh press.
        wasHeld = held;
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (pendingScene != null && (scene != pendingScene || Time.unscaledTime >= retryAfter))
            pendingScene = null;
        if (!pressed || !enabled || string.IsNullOrEmpty(binding) || !CanRun(scene))
            return;

        pendingScene = scene;
        retryAfter = Time.unscaledTime + 10f;
        try
        {
            // Reuse the same native transition service as the Scenes teleport UI.
            // Never change the saved God Mode preference for an asynchronous trip.
            Teleport.Teleport_ToScene.StartTpToScene(Destination);
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Error("Safe teleport failed: " + ex.Message);
        }
    }

    bool CanRun(string scene)
    {
        if (
            !focused
            || KeybindCapture.Active
            || Hud_Manager.mod_menu_open
            || Hud_Manager.IsPauseOpen()
            || Time.timeScale <= 0f
            || pendingScene != null
            || Time.unscaledTime < retryAfter
            || !Scenes.IsGameScene()
            || scene == Destination
            || Save_Manager.instance.IsNullOrDestroyed()
            || !Save_Manager.instance.initialized
            || Save_Manager.instance.data.IsNullOrDestroyed()
            || Refs_Manager.game_uibase.IsNullOrDestroyed()
            || Refs_Manager.player_actor.IsNullOrDestroyed()
            || Refs_Manager.player_data.IsNullOrDestroyed()
            || Teleport.Teleport_ToScene.instance.IsNullOrDestroyed()
        )
            return false;
        ModUI.Settings.SaveManager settings = ModUI.Settings.SaveManager.instance;
        if (settings.IsNullOrDestroyed() || !settings.initialized)
            return false;
        if (Typing())
            return false;
        var waypoints = Refs_Manager.player_data.UnlockedWaypointScenes;
        // This shortcut must not unlock a destination on a fresh character.
        return waypoints != null && waypoints.Contains(Destination);
    }

    static bool Typing()
    {
        var events = EventSystem.current;
        if (events.IsNullOrDestroyed())
            return false;
        var selected = events.currentSelectedGameObject;
        if (selected.IsNullOrDestroyed())
            return false;
        var input = selected.GetComponentInParent<InputField>();
        if (!input.IsNullOrDestroyed() && input.isFocused)
            return true;
        var tmp = selected.GetComponentInParent<TMP_InputField>();
        return !tmp.IsNullOrDestroyed() && tmp.isFocused;
    }
}
