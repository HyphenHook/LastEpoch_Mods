using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>World &gt; Difficulty HUD page. State resets on HUD re-bind (Build).</summary>
internal static class WorldDifficultyPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;
        var card = page.AddCard("Difficulty", "Difficulty");
        AddMultiplier(
            card,
            "EnemyHealth",
            "Enemy Health Multiplier",
            ModSettings.Difficulty.EnemyHealthMult
        );
        AddMultiplier(
            card,
            "EnemyDamage",
            "Enemy Damage Multiplier",
            ModSettings.Difficulty.EnemyDamageMult
        );
        AddMultiplier(
            card,
            "EnemySpeed",
            "Enemy Speed Multiplier",
            ModSettings.Difficulty.EnemySpeedMult
        );
        page.AddToggle(
            card,
            "ScaleZone",
            "Raise Zone Level To Match Player",
            () => ModSettings.Difficulty.ScaleZoneToPlayer.Value,
            ModSettings.Difficulty.ScaleZoneToPlayer.Set
        );
        page.AddToggle(
            card,
            "CapLevel",
            "Cap Player Level Gain To Zone Level",
            () => ModSettings.Difficulty.CapLevelToZone.Value,
            ModSettings.Difficulty.CapLevelToZone.Set
        );
        page.AddText(
            card,
            "RarityExplanation",
            "Force all normal monsters to spawn as a specific rarity tier."
        );
        AddRarity(card, 0, "Off");
        AddRarity(card, 1, "Magic");
        AddRarity(card, 2, "Rare");
        page.AddButton(
            card,
            "Reset",
            "Reset Difficulty",
            () =>
            {
                ModSettings.Difficulty.Reset.Invoke();
                page.RefreshValues();
            }
        );
    }

    public static void Show() => page?.Show();

    public static void Hide() => page?.Hide();

    public static void Refresh() => page?.RefreshValues();

    private static void AddMultiplier(
        HudFormPage.Card card,
        string id,
        string label,
        FloatSetting setting
    )
    {
        page.AddToggleSlider(
            card,
            id,
            label,
            "x",
            setting.MinLimit,
            setting.MaxLimit,
            false,
            () => setting.Enabled,
            setting.SetEnabled,
            () => setting.Value,
            setting.SetValue
        );
    }

    private static void AddRarity(HudFormPage.Card card, int index, string label)
    {
        page.AddToggle(
            card,
            "Rarity" + label,
            label,
            () => ModSettings.Difficulty.ForceMonsterRarity.IsSelected(index),
            value =>
            {
                if (value)
                    ModSettings.Difficulty.ForceMonsterRarity.Select(index, true);
                page.RefreshValues();
            }
        );
    }
}
