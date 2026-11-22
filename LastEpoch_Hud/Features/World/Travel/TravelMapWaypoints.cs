using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Teleport;

internal static class TravelMapWaypoints
{
    sealed class Original
    {
        public UIWaypoint Pin;
        public string Scene;
        public bool NoWaypoint;
        public bool Active;
        public bool Applied;
    }

    static readonly Dictionary<IntPtr, Original> originals = new();
    static bool wasOpen;
    static float nextRefresh;

    static bool MapOpen =>
        !Refs_Manager.game_uibase.IsNullOrDestroyed()
        && Refs_Manager.game_uibase.IsWorldMapPanelOpen();

    public static void Tick()
    {
        bool open = MapOpen && Scenes.IsGameScene();
        if (!open)
        {
            RestoreAll(true);
            wasOpen = false;
            return;
        }
        if (!ModSettings.TravelAnywhere.Enabled.Value)
        {
            // Keep the original state until the map closes, so an already-open
            // synthetic Travel button cannot use the native service after disabling.
            RestoreAll(false);
            wasOpen = false;
            return;
        }
        if (wasOpen && Time.unscaledTime < nextRefresh)
            return;
        wasOpen = true;
        nextRefresh = Time.unscaledTime + .5f;
        TravelDestinations.Refresh();
        foreach (UIWaypoint pin in Resources.FindObjectsOfTypeAll<UIWaypoint>())
            if (!pin.IsNullOrDestroyed() && pin.gameObject.activeInHierarchy)
                Prepare(pin);
    }

    public static bool IsNativeWaypoint(UIWaypoint pin)
    {
        if (pin.IsNullOrDestroyed())
            return false;
        if (originals.TryGetValue(pin.Pointer, out var original) && original.Scene == pin.sceneName)
            return !original.NoWaypoint;
        return !pin.noWaypointInScene;
    }

    public static void Prepare(UIWaypoint pin)
    {
        if (
            pin.IsNullOrDestroyed()
            || !ModSettings.TravelAnywhere.Enabled.Value
            || !Scenes.IsGameScene()
            || !MapOpen
        )
            return;
        if (TravelDestinations.All.Count == 0)
            TravelDestinations.Refresh();
        if (TravelDestinations.Find(pin.sceneName) == null)
            return;
        if (
            !originals.TryGetValue(pin.Pointer, out var original)
            || original.Scene != pin.sceneName
        )
        {
            // Reused widgets must never receive another destination's old flags.
            original = new Original
            {
                Pin = pin,
                Scene = pin.sceneName,
                NoWaypoint = pin.noWaypointInScene,
                Active = pin.isActive,
            };
            originals[pin.Pointer] = original;
        }
        // Track before either write so even a partially applied override can restore.
        original.Applied = true;
        try
        {
            pin.noWaypointInScene = false;
            pin.isActive = true;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Travel Anywhere map flags");
        }
    }

    public static bool BlocksStaleNativeAction(UIWaypoint pin)
    {
        if (
            pin.IsNullOrDestroyed()
            || !originals.TryGetValue(pin.Pointer, out var original)
            || original.Scene != pin.sceneName
        )
            return false;
        return original.NoWaypoint
            || (!original.Active && !Unlocked(original.Scene) && !UnlockAll());
    }

    public static void RestoreAll(bool clear)
    {
        if (originals.Count == 0)
            return;
        var discarded = new List<IntPtr>();
        foreach (var entry in originals)
        {
            var original = entry.Value;
            if (!original.Applied)
            {
                if (clear)
                    discarded.Add(entry.Key);
                continue;
            }
            try
            {
                var pin = original.Pin;
                if (pin.IsNullOrDestroyed() || pin.sceneName != original.Scene)
                {
                    discarded.Add(entry.Key);
                    continue;
                }
                pin.noWaypointInScene = original.NoWaypoint;
                // Preserve genuine unlocks acquired while the map was overridden,
                // and the separate Unlock All Waypoints option's native behaviour.
                pin.isActive = original.Active || Unlocked(original.Scene) || UnlockAll();
                original.Applied = false;
                if (clear)
                    discarded.Add(entry.Key);
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Travel Anywhere map flag restore");
            }
        }
        foreach (var key in discarded)
            originals.Remove(key);
    }

    static bool Unlocked(string scene)
    {
        var data = Refs_Manager.player_data;
        return !data.IsNullOrDestroyed()
            && !data.UnlockedWaypointScenes.IsNullOrDestroyed()
            && data.UnlockedWaypointScenes.Contains(scene);
    }

    static bool UnlockAll() => Character.Character_Waypoints.CanRun();
}
