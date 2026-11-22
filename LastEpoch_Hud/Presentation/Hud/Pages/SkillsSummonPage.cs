using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>Skills &gt; Summon HUD page. State resets on HUD re-bind (Build).</summary>
internal static class SkillsSummonPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;
        var card = page.AddCard("Summon", "Summon");
        AddToggle(
            card,
            "GodMode",
            "Summon God Mode",
            () => Save_Manager.instance.data.Summon.Enable_GodMode,
            v => Save_Manager.instance.data.Summon.Enable_GodMode = v
        );
        AddToggle(
            card,
            "Forever",
            "Forever",
            () => Save_Manager.instance.data.Summon.Enable_Forever,
            v => Save_Manager.instance.data.Summon.Enable_Forever = v
        );
        AddToggle(
            card,
            "DontCollide",
            "Don't Collide",
            () => Save_Manager.instance.data.Summon.Enable_DontCollide,
            v => Save_Manager.instance.data.Summon.Enable_DontCollide = v
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
