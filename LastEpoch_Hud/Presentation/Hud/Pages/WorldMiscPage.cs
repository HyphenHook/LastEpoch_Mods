using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>World &gt; Misc HUD page. Page, session totals label and pause button reset on HUD re-bind (Build).</summary>
internal static class WorldMiscPage
{
    private static HudFormPage page;
    private static UnityEngine.UI.Text sessionTotals;
    private static UnityEngine.UI.Button sessionPause;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;

        var dungeons = page.AddCard("Dungeons", "Dungeons");
        page.AddToggle(
            dungeons,
            "RevealObjectives",
            "Reveal Dungeon Objectives",
            () => ModSettings.DungeonReveal.Enabled.Value,
            ModSettings.DungeonReveal.Enabled.Set
        );
        AddToggle(
            dungeons,
            "EnterWithoutKey",
            "Enter Without Key",
            () => Save_Manager.instance.data.Scenes.Dungeons.Enable_EnterWithoutKey,
            v => Save_Manager.instance.data.Scenes.Dungeons.Enable_EnterWithoutKey = v
        );

        var misc = page.AddCard("Misc", "Misc");
        page.AddToggle(
            misc,
            "SafeTeleport",
            "Enable Safe Teleport",
            () => ModSettings.SafeTeleport.Enabled.Value,
            ModSettings.SafeTeleport.Enabled.Set
        );
        page.AddKeybind(misc, "SafeTeleportKey", "Teleport Key", ModSettings.SafeTeleport.Key);
        page.AddText(
            misc,
            "SafeTeleportNote",
            "End of Time waypoint required. Bind a key or modifier + key."
        );
        page.AddToggle(
            misc,
            "TravelAnywhere",
            "Enable Travel Anywhere",
            () => ModSettings.TravelAnywhere.Enabled.Value,
            value => ModSettings.TravelAnywhere.Enabled.Set(value)
        );

        page.AddToggle(
            misc,
            "BazaarWarmUp",
            "Bazaar Warm-Up",
            () => ModSettings.Bazaar.WarmUp.Value,
            ModSettings.Bazaar.WarmUp.Set
        );
        page.AddText(
            misc,
            "BazaarWarmUpNote",
            "Stocks every Bazaar stall while the game is held after a character loads. Off fills them in the background instead. Applies from the next load.",
            72f
        );

        var gains = page.AddCard("SessionGains", "Session Gains");
        sessionTotals = page.AddText(
            gains,
            "SessionTotals",
            Mods.UI.SessionGainCounters.Format(),
            124f
        );
        page.AddToggle(
            gains,
            "ShowSessionOverlay",
            "Show counter HUD",
            () => ModSettings.SessionStats.ShowOverlay.Value,
            value => ModSettings.SessionStats.ShowOverlay.Set(value)
        );
        sessionPause = page.AddButton(
            gains,
            "PauseSession",
            "Pause",
            () =>
            {
                Mods.UI.SessionGainCounters.SetPaused(!Mods.UI.SessionGainCounters.Session.Paused);
                RefreshSession();
            }
        );
        page.AddButton(
            gains,
            "ResetSession",
            "Reset",
            () =>
            {
                Mods.UI.SessionGainCounters.Reset();
                RefreshSession();
            }
        );
        page.AddButton(
            gains,
            "ResetCounterPosition",
            "Reset counter position",
            Mods.UI.SessionGainCounters.ResetPosition
        );
        page.AddText(gains, "DragCounterHint", "Hold Alt and drag the counter to move it.", 48f);

        var minimap = page.AddCard("Minimap", "Minimap");
        AddToggle(
            minimap,
            "MaxZoomOut",
            "Max Zoom Out",
            () => Save_Manager.instance.data.Scenes.Minimap.Enable_MaxZoomOut,
            v => Save_Manager.instance.data.Scenes.Minimap.Enable_MaxZoomOut = v
        );
        AddToggle(
            minimap,
            "RemoveFog",
            "Remove Fog Of War",
            () => Save_Manager.instance.data.Scenes.Minimap.Enable_RemoveFogOfWar,
            v => Save_Manager.instance.data.Scenes.Minimap.Enable_RemoveFogOfWar = v
        );
    }

    public static void Show()
    {
        page?.Show();
        RefreshSession();
    }

    public static void Hide() => page?.Hide();

    public static void Refresh()
    {
        page?.RefreshValues();
        RefreshSession();
    }

    public static void RefreshSession()
    {
        if (!sessionTotals.IsNullOrDestroyed())
            sessionTotals.text = Mods.UI.SessionGainCounters.Format();
        if (sessionPause.IsNullOrDestroyed())
            return;
        var text = sessionPause.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (!text.IsNullOrDestroyed())
            LocaleRegistry.Apply(
                text,
                Mods.UI.SessionGainCounters.Session.Paused ? "Resume" : "Pause"
            );
    }

    private static void AddToggle(
        HudFormPage.Card card,
        string id,
        string label,
        Func<bool> read,
        Action<bool> write
    ) =>
        page.AddToggle(
            card,
            id,
            label,
            () => HasSave() && read(),
            v =>
            {
                if (HasSave())
                    write(v);
            }
        );

    private static bool HasSave() =>
        !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;
}
