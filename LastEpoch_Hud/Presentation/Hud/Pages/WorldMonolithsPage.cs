using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>World &gt; Monoliths HUD page. Page and the timeline editor reset on HUD re-bind (Build).</summary>
internal static class WorldMonolithsPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;
        var card = page.AddCard("Monoliths", "Monoliths");
        AddSlider(
            card,
            "MaxStability",
            "Max Stability",
            SceneSliderLimits.MonolithMaxStability,
            () => Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStability,
            v => Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStability = v,
            () => Save_Manager.instance.data.Scenes.Monoliths.MaxStability,
            v => Save_Manager.instance.data.Scenes.Monoliths.MaxStability = v
        );
        AddToggle(
            card,
            "MaxStabilityOnStart",
            "Stability = Max Stability",
            () => Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStabilityOnStart,
            v => Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStabilityOnStart = v
        );
        AddSlider(
            card,
            "MobsDensity",
            "Mobs Density",
            SceneSliderLimits.MonolithMobsDensity,
            () => Save_Manager.instance.data.Scenes.Monoliths.Enable_MobsDensity,
            v => Save_Manager.instance.data.Scenes.Monoliths.Enable_MobsDensity = v,
            () => Save_Manager.instance.data.Scenes.Monoliths.MobsDensity,
            v => Save_Manager.instance.data.Scenes.Monoliths.MobsDensity = v
        );
        AddSlider(
            card,
            "MobsDefeat",
            "Mobs Defeat",
            SceneSliderLimits.MonolithMobsDefeat,
            () => Save_Manager.instance.data.Scenes.Monoliths.Enable_MobsDefeatOnStart,
            v => Save_Manager.instance.data.Scenes.Monoliths.Enable_MobsDefeatOnStart = v,
            () => Save_Manager.instance.data.Scenes.Monoliths.MobsDefeatOnStart,
            v => Save_Manager.instance.data.Scenes.Monoliths.MobsDefeatOnStart = v
        );
        AddToggle(
            card,
            "ObjectiveReveal",
            "Objective Reveal",
            () => Save_Manager.instance.data.Scenes.Monoliths.Enable_ObjectiveReveal,
            v => Save_Manager.instance.data.Scenes.Monoliths.Enable_ObjectiveReveal = v
        );
        AddToggle(
            card,
            "CompleteObjective",
            "Complete Objective",
            () => Save_Manager.instance.data.Scenes.Monoliths.Enable_CompleteObjective,
            v => Save_Manager.instance.data.Scenes.Monoliths.Enable_CompleteObjective = v
        );
        page.AddText(
            card,
            "StartNote",
            "The options above must be set before entering a Monolith."
        );
        AddSlider(
            card,
            "BlessingSlots",
            "Blessing Slots Reward",
            SceneSliderLimits.MonolithBlessingSlots,
            () => Save_Manager.instance.data.Scenes.Monoliths.Enable_BlessingSlots,
            v => Save_Manager.instance.data.Scenes.Monoliths.Enable_BlessingSlots = v,
            () => Save_Manager.instance.data.Scenes.Monoliths.BlessingSlots,
            v => Save_Manager.instance.data.Scenes.Monoliths.BlessingSlots = (int)v
        );
        AddToggle(
            card,
            "NoLoss",
            "No Stability Lost When You Die",
            () => Save_Manager.instance.data.Scenes.Monoliths.Enable_NoLostWhenDie,
            v => Save_Manager.instance.data.Scenes.Monoliths.Enable_NoLostWhenDie = v
        );
        AddToggle(
            card,
            "MaxOnChange",
            "Stability = Max Stability On Stability Change",
            () => Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStabilityOnStabilityChanged,
            v =>
                Save_Manager.instance.data.Scenes.Monoliths.Enable_MaxStabilityOnStabilityChanged =
                    v
        );

        var encounters = page.AddCard("EchoEncounters", "Echo Encounters");
        AddAlways(
            encounters,
            "AlwaysNemesis",
            "Always Spawn Nemesis",
            ModSettings.EchoEncounters.AlwaysNemesis
        );
        AddAlways(
            encounters,
            "AlwaysBloodRitual",
            "Always Spawn Blood Ritual",
            ModSettings.EchoEncounters.AlwaysBloodRitual
        );
        AddAlways(
            encounters,
            "AlwaysLostCache",
            "Always Spawn Lost Cache",
            ModSettings.EchoEncounters.AlwaysLostCache
        );
        AddAlways(
            encounters,
            "AlwaysRiftBeast",
            "Always Spawn Rift Beast",
            ModSettings.EchoEncounters.AlwaysRiftBeast
        );
        AddAlways(
            encounters,
            "AlwaysOmenWindow",
            "Always Spawn Omen Window",
            ModSettings.EchoEncounters.AlwaysOmenWindow
        );
        AddCount(
            encounters,
            "RunePrisonCount",
            "Rune Prisons Per Echo",
            ModSettings.EchoEncounters.RunePrisonCount
        );
        AddCount(
            encounters,
            "FavourCacheCount",
            "Favour Caches Per Echo",
            ModSettings.EchoEncounters.FavourCacheCount
        );
        page.AddText(
            encounters,
            "EncountersNote",
            "Applies from the next echo zone. Off = the game's own rates."
        );
        page.AddText(
            encounters,
            "EncountersSpotsWarning",
            "Warning: echoes have a limited number of encounter spots. High Rune Prison or Favour Cache counts can crowd out other spawns such as shrines, champions and Nemesis."
        );

        MonolithTimelineEditor.Build(page, page.AddCard("Timelines", "Timelines").Body, font);
    }

    public static void Show()
    {
        page?.Show();
        MonolithTimelineEditor.Reload();
    }

    public static void Hide() => page?.Hide();

    public static void Refresh()
    {
        page?.RefreshValues();
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

    private static void AddSlider(
        HudFormPage.Card card,
        string id,
        string label,
        SliderRange range,
        Func<bool> readEnabled,
        Action<bool> writeEnabled,
        Func<float> read,
        Action<float> write
    )
    {
        page.AddToggleSlider(
            card,
            id,
            label,
            string.Empty,
            range.Min,
            range.Max,
            range.WholeNumbers,
            () => HasSave() && readEnabled(),
            v =>
            {
                if (HasSave())
                    writeEnabled(v);
            },
            () => HasSave() ? read() : range.Min,
            v =>
            {
                if (HasSave())
                    write(v);
            }
        );
    }

    // Echo encounter: checked = guaranteed in every echo; unchecked = the game's own rate.
    private static void AddAlways(
        HudFormPage.Card card,
        string id,
        string label,
        BoolSetting setting
    ) => page.AddToggle(card, id, label, () => setting.Value, v => setting.Set(v));

    // Echo encounter count: off = the game's own rate; on = exactly 0-10 per echo zone.
    private static void AddCount(
        HudFormPage.Card card,
        string id,
        string label,
        FloatSetting setting
    ) =>
        page.AddToggleSlider(
            card,
            id,
            label,
            string.Empty,
            0f,
            10f,
            true,
            () => setting.Enabled,
            v => setting.SetEnabled(v),
            () => setting.Value,
            v => setting.SetValue(v)
        );

    private static bool HasSave() =>
        !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;
}
