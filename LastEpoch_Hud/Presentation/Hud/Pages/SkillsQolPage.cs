using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>Skills &gt; QOL HUD page. State resets on HUD re-bind (Build).</summary>
internal static class SkillsQolPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;
        var general = page.AddCard("General", "General");
        AddToggle(
            general,
            "UnlockAllSkills",
            "Unlock All Skills",
            () => Save_Manager.instance.data.Skills.Enable_AllSkills,
            v => Save_Manager.instance.data.Skills.Enable_AllSkills = v
        );

        var autoCast = page.AddCard("AutoCast", "Auto-Cast");
        page.AddText(
            autoCast,
            "Description",
            "Hold the modifier key and press the skill's normal keybind to toggle auto-cast on or off.",
            58f
        );
        page.AddKeybind(
            autoCast,
            "Modifier",
            "Modifier Key",
            ModSettings.SkillsAutoCast.ModifierKey,
            "Reset"
        );
        page.AddToggle(
            autoCast,
            "PauseZoneChange",
            "Pause Auto-Cast On Zone Change",
            () => ModSettings.SkillsAutoCast.PauseOnZoneChange.Value,
            ModSettings.SkillsAutoCast.PauseOnZoneChange.Set
        );
        page.AddToggle(
            autoCast,
            "PauseNonCombat",
            "Pause Auto-Cast In Non-Combat Zones",
            () => ModSettings.SkillsAutoCast.DisableInNonCombatZone.Value,
            ModSettings.SkillsAutoCast.DisableInNonCombatZone.Set
        );

        var movement = page.AddCard("MovementSkills", "Movement Skills");
        AddToggle(
            movement,
            "NoTarget",
            "No Target Required",
            () => Save_Manager.instance.data.Skills.MovementSkills.Enable_NoTarget,
            v => Save_Manager.instance.data.Skills.MovementSkills.Enable_NoTarget = v
        );
        AddToggle(
            movement,
            "Immune",
            "Immune During Movement",
            () => Save_Manager.instance.data.Skills.MovementSkills.Enable_ImmuneDuringMovement,
            v => Save_Manager.instance.data.Skills.MovementSkills.Enable_ImmuneDuringMovement = v
        );
        AddToggle(
            movement,
            "SimplePath",
            "Disable Simple Path Requirement",
            () => Save_Manager.instance.data.Skills.MovementSkills.Disable_SimplePath,
            v => Save_Manager.instance.data.Skills.MovementSkills.Disable_SimplePath = v
        );
    }

    public static void Show() => page?.Show();

    public static void Hide() => page?.Hide();

    public static void Refresh() => page?.RefreshValues();

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
