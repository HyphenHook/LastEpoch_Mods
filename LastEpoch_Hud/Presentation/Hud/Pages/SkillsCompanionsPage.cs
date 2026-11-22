using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>Skills &gt; Companions HUD page. State resets on HUD re-bind (Build).</summary>
internal static class SkillsCompanionsPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;
        var general = page.AddCard("General", "General");
        AddSlider(
            general,
            "MaximumCompanions",
            "Maximum Companions",
            SkillSliderLimits.CompanionsMaximum,
            () => Save_Manager.instance.data.Skills.Companion.Enable_Limit,
            v => Save_Manager.instance.data.Skills.Companion.Enable_Limit = v,
            () => Save_Manager.instance.data.Skills.Companion.Limit,
            v => Save_Manager.instance.data.Skills.Companion.Limit = (int)v
        );

        var wolves = page.AddCard("Wolves", "Wolves");
        AddToggle(
            wolves,
            "SummonMaximum",
            "Summon To Maximum",
            () => Save_Manager.instance.data.Skills.Companion.Wolf.Enable_SummonMax,
            v => Save_Manager.instance.data.Skills.Companion.Wolf.Enable_SummonMax = v
        );
        AddSlider(
            wolves,
            "SummonLimit",
            "Summon Limit",
            SkillSliderLimits.WolfSummonLimit,
            () => Save_Manager.instance.data.Skills.Companion.Wolf.Enable_SummonLimit,
            v => Save_Manager.instance.data.Skills.Companion.Wolf.Enable_SummonLimit = v,
            () => Save_Manager.instance.data.Skills.Companion.Wolf.SummonLimit,
            v => Save_Manager.instance.data.Skills.Companion.Wolf.SummonLimit = (int)v
        );
        AddToggle(
            wolves,
            "StunImmunity",
            "Stun Immunity",
            () => Save_Manager.instance.data.Skills.Companion.Wolf.Enable_StunImmunity,
            v => Save_Manager.instance.data.Skills.Companion.Wolf.Enable_StunImmunity = v
        );

        var scorpions = page.AddCard("Scorpions", "Scorpions");
        AddSlider(
            scorpions,
            "BabyQuantity",
            "Baby Scorpion Quantity",
            SkillSliderLimits.ScorpionBabyQuantity,
            () => Save_Manager.instance.data.Skills.Companion.Scorpion.Enable_BabyQuantity,
            v => Save_Manager.instance.data.Skills.Companion.Scorpion.Enable_BabyQuantity = v,
            () => Save_Manager.instance.data.Skills.Companion.Scorpion.BabyQuantity,
            v => Save_Manager.instance.data.Skills.Companion.Scorpion.BabyQuantity = (int)v
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

    private static bool HasSave() =>
        !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;
}
