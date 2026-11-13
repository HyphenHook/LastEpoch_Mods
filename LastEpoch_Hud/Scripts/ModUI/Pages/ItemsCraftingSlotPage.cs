using System;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>
/// Items &gt; Crafting Slot. Every override has an explicit checkbox; slider zero is
/// therefore a valid value and never doubles as an enabled/disabled sentinel.
/// State resets on HUD re-bind (Build).
/// </summary>
internal static class ItemsCraftingSlotPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;

        var card = page.AddCard("CraftingSlot", "Crafting Slot");
        AddToggle(
            card,
            "Enable",
            "Enable Crafting Slot",
            () => Save_Manager.instance.data.Items.CraftingSlot.Enable_Mod,
            v => Save_Manager.instance.data.Items.CraftingSlot.Enable_Mod = v
        );
        AddToggle(
            card,
            "InfiniteForgingPotential",
            "Infinite Forging Potential",
            () => ModSettings.InfiniteForgingPotential.Enabled.Value,
            v => ModSettings.InfiniteForgingPotential.Enabled.Set(v)
        );
        page.AddButton(card, "DeselectAll", "Deselect All", DeselectAll);

        var advancedForgeCard = page.AddCard("AdvancedForge", "Advanced Forge");
        AddToggle(
            advancedForgeCard,
            "AdvancedForgeT7",
            "Craft Affixes to T7",
            () => ModSettings.AdvancedForge.AllowT7Crafting.Value,
            v => ModSettings.AdvancedForge.AllowT7Crafting.Set(v)
        );
        AddToggle(
            advancedForgeCard,
            "AdvancedForgeMaxRoll",
            "Max Crafted Roll",
            () => ModSettings.AdvancedForge.AffixRoll.Enabled,
            v =>
            {
                ModSettings.AdvancedForge.AffixRoll.SetEnabled(v);
                if (v)
                    ModSettings.AdvancedForge.AffixRoll.SetValue(255f);
            }
        );
        AddToggle(
            advancedForgeCard,
            "AdvancedForgeHope",
            "Guarantee Hope",
            () => ModSettings.AdvancedForge.GuaranteedGlyphOfHope.Value,
            v => ModSettings.AdvancedForge.GuaranteedGlyphOfHope.Set(v)
        );
        AddToggle(
            advancedForgeCard,
            "AdvancedForgeDespair",
            "Guarantee Despair",
            () => ModSettings.AdvancedForge.GuaranteedGlyphOfDespair.Value,
            v => ModSettings.AdvancedForge.GuaranteedGlyphOfDespair.Set(v)
        );

        AddOverride(
            card,
            "ForgingPotential",
            "Forging Potential",
            ItemSliderLimits.CraftingSlotForgingPotential,
            () => Save_Manager.instance.data.Items.CraftingSlot.Enable_ForginPotencial,
            v => Save_Manager.instance.data.Items.CraftingSlot.Enable_ForginPotencial = v,
            () => Save_Manager.instance.data.Items.CraftingSlot.ForginPotencial,
            v => Save_Manager.instance.data.Items.CraftingSlot.ForginPotencial = v
        );
        AddOverride(
            card,
            "Implicit0",
            "Implicit 1",
            ItemSliderLimits.CraftingSlotImplicit,
            () => Save_Manager.instance.data.Items.CraftingSlot.Enable_Implicit_0,
            v => Save_Manager.instance.data.Items.CraftingSlot.Enable_Implicit_0 = v,
            () => Save_Manager.instance.data.Items.CraftingSlot.Implicit_0,
            v => Save_Manager.instance.data.Items.CraftingSlot.Implicit_0 = v
        );
        AddOverride(
            card,
            "Implicit1",
            "Implicit 2",
            ItemSliderLimits.CraftingSlotImplicit,
            () => Save_Manager.instance.data.Items.CraftingSlot.Enable_Implicit_1,
            v => Save_Manager.instance.data.Items.CraftingSlot.Enable_Implicit_1 = v,
            () => Save_Manager.instance.data.Items.CraftingSlot.Implicit_1,
            v => Save_Manager.instance.data.Items.CraftingSlot.Implicit_1 = v
        );
        AddOverride(
            card,
            "Implicit2",
            "Implicit 3",
            ItemSliderLimits.CraftingSlotImplicit,
            () => Save_Manager.instance.data.Items.CraftingSlot.Enable_Implicit_2,
            v => Save_Manager.instance.data.Items.CraftingSlot.Enable_Implicit_2 = v,
            () => Save_Manager.instance.data.Items.CraftingSlot.Implicit_2,
            v => Save_Manager.instance.data.Items.CraftingSlot.Implicit_2 = v
        );
        AddOverride(
            card,
            "SealTier",
            "Seal Tier",
            ItemSliderLimits.CraftingSlotSealTier,
            () => Save_Manager.instance.data.Items.CraftingSlot.Enable_Seal_Tier,
            v => Save_Manager.instance.data.Items.CraftingSlot.Enable_Seal_Tier = v,
            () => Save_Manager.instance.data.Items.CraftingSlot.Seal_Tier,
            v => Save_Manager.instance.data.Items.CraftingSlot.Seal_Tier = (int)v
        );
        AddOverride(
            card,
            "SealValue",
            "Seal Value",
            ItemSliderLimits.CraftingSlotSealValue,
            () => Save_Manager.instance.data.Items.CraftingSlot.Enable_Seal_Value,
            v => Save_Manager.instance.data.Items.CraftingSlot.Enable_Seal_Value = v,
            () => Save_Manager.instance.data.Items.CraftingSlot.Seal_Value,
            v => Save_Manager.instance.data.Items.CraftingSlot.Seal_Value = v
        );

        for (int affix = 0; affix < AffixLimits.OrdinarySlots; affix++)
            AddAffix(card, affix);

        AddUnique(card, 0);
        AddUnique(card, 1);
        AddUnique(card, 2);
        AddUnique(card, 3);
        AddUnique(card, 4);
        AddUnique(card, 5);
        AddUnique(card, 6);
        AddUnique(card, 7);

        AddOverride(
            card,
            "LegendaryPotential",
            "Legendary Potential",
            ItemSliderLimits.CraftingSlotLegendaryPotential,
            () => Save_Manager.instance.data.Items.CraftingSlot.Enable_LegendaryPotencial,
            v => Save_Manager.instance.data.Items.CraftingSlot.Enable_LegendaryPotencial = v,
            () => Save_Manager.instance.data.Items.CraftingSlot.LegendaryPotencial,
            v => Save_Manager.instance.data.Items.CraftingSlot.LegendaryPotencial = (int)v
        );
        AddOverride(
            card,
            "WeaverWill",
            "Weaver Will",
            ItemSliderLimits.CraftingSlotWeaverWill,
            () => Save_Manager.instance.data.Items.CraftingSlot.Enable_WeaverWill,
            v => Save_Manager.instance.data.Items.CraftingSlot.Enable_WeaverWill = v,
            () => Save_Manager.instance.data.Items.CraftingSlot.WeaverWill,
            v => Save_Manager.instance.data.Items.CraftingSlot.WeaverWill = (int)v
        );
    }

    public static void Show() => page?.Show();

    public static void Hide() => page?.Hide();

    public static void Refresh() => page?.RefreshValues();

    private static void AddAffix(HudFormPage.Card card, int index)
    {
        AddOverride(
            card,
            "Affix" + index + "Tier",
            "Affix " + (index + 1) + " Tier",
            ItemSliderLimits.CraftingSlotAffixTier,
            () => ReadAffixTierEnabled(index),
            v => WriteAffixTierEnabled(index, v),
            () => ReadAffixTier(index),
            v => WriteAffixTier(index, (int)v)
        );
        AddOverride(
            card,
            "Affix" + index + "Value",
            "Affix " + (index + 1) + " Value",
            ItemSliderLimits.CraftingSlotAffixValue,
            () => ReadAffixValueEnabled(index),
            v => WriteAffixValueEnabled(index, v),
            () => ReadAffixValue(index),
            v => WriteAffixValue(index, v)
        );
    }

    private static void AddUnique(HudFormPage.Card card, int index)
    {
        AddOverride(
            card,
            "UniqueMod" + index,
            "Unique Mod " + (index + 1),
            ItemSliderLimits.CraftingSlotUniqueMod,
            () => ReadUniqueEnabled(index),
            v => WriteUniqueEnabled(index, v),
            () => ReadUnique(index),
            v => WriteUnique(index, v)
        );
    }

    private static void AddOverride(
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

    private static void AddToggle(
        HudFormPage.Card card,
        string id,
        string label,
        Func<bool> read,
        Action<bool> write
    )
    {
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
    }

    private static bool ReadAffixTierEnabled(int i) =>
        i switch
        {
            0 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_0_Tier,
            1 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_1_Tier,
            2 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_2_Tier,
            3 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_3_Tier,
            4 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_4_Tier,
            5 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_5_Tier,
            6 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_6_Tier,
            7 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_7_Tier,
            _ => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_8_Tier,
        };

    private static bool ReadAffixValueEnabled(int i) =>
        i switch
        {
            0 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_0_Value,
            1 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_1_Value,
            2 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_2_Value,
            3 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_3_Value,
            4 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_4_Value,
            5 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_5_Value,
            6 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_6_Value,
            7 => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_7_Value,
            _ => Save_Manager.instance.data.Items.CraftingSlot.Enable_Affix_8_Value,
        };

    private static float ReadAffixTier(int i) =>
        i switch
        {
            0 => Save_Manager.instance.data.Items.CraftingSlot.Affix_0_Tier,
            1 => Save_Manager.instance.data.Items.CraftingSlot.Affix_1_Tier,
            2 => Save_Manager.instance.data.Items.CraftingSlot.Affix_2_Tier,
            3 => Save_Manager.instance.data.Items.CraftingSlot.Affix_3_Tier,
            4 => Save_Manager.instance.data.Items.CraftingSlot.Affix_4_Tier,
            5 => Save_Manager.instance.data.Items.CraftingSlot.Affix_5_Tier,
            6 => Save_Manager.instance.data.Items.CraftingSlot.Affix_6_Tier,
            7 => Save_Manager.instance.data.Items.CraftingSlot.Affix_7_Tier,
            _ => Save_Manager.instance.data.Items.CraftingSlot.Affix_8_Tier,
        };

    private static float ReadAffixValue(int i) =>
        i switch
        {
            0 => Save_Manager.instance.data.Items.CraftingSlot.Affix_0_Value,
            1 => Save_Manager.instance.data.Items.CraftingSlot.Affix_1_Value,
            2 => Save_Manager.instance.data.Items.CraftingSlot.Affix_2_Value,
            3 => Save_Manager.instance.data.Items.CraftingSlot.Affix_3_Value,
            4 => Save_Manager.instance.data.Items.CraftingSlot.Affix_4_Value,
            5 => Save_Manager.instance.data.Items.CraftingSlot.Affix_5_Value,
            6 => Save_Manager.instance.data.Items.CraftingSlot.Affix_6_Value,
            7 => Save_Manager.instance.data.Items.CraftingSlot.Affix_7_Value,
            _ => Save_Manager.instance.data.Items.CraftingSlot.Affix_8_Value,
        };

    private static bool ReadUniqueEnabled(int i) =>
        i switch
        {
            0 => Save_Manager.instance.data.Items.CraftingSlot.Enable_UniqueMod_0,
            1 => Save_Manager.instance.data.Items.CraftingSlot.Enable_UniqueMod_1,
            2 => Save_Manager.instance.data.Items.CraftingSlot.Enable_UniqueMod_2,
            3 => Save_Manager.instance.data.Items.CraftingSlot.Enable_UniqueMod_3,
            4 => Save_Manager.instance.data.Items.CraftingSlot.Enable_UniqueMod_4,
            5 => Save_Manager.instance.data.Items.CraftingSlot.Enable_UniqueMod_5,
            6 => Save_Manager.instance.data.Items.CraftingSlot.Enable_UniqueMod_6,
            _ => Save_Manager.instance.data.Items.CraftingSlot.Enable_UniqueMod_7,
        };

    private static float ReadUnique(int i) =>
        i switch
        {
            0 => Save_Manager.instance.data.Items.CraftingSlot.UniqueMod_0,
            1 => Save_Manager.instance.data.Items.CraftingSlot.UniqueMod_1,
            2 => Save_Manager.instance.data.Items.CraftingSlot.UniqueMod_2,
            3 => Save_Manager.instance.data.Items.CraftingSlot.UniqueMod_3,
            4 => Save_Manager.instance.data.Items.CraftingSlot.UniqueMod_4,
            5 => Save_Manager.instance.data.Items.CraftingSlot.UniqueMod_5,
            6 => Save_Manager.instance.data.Items.CraftingSlot.UniqueMod_6,
            _ => Save_Manager.instance.data.Items.CraftingSlot.UniqueMod_7,
        };

    private static void WriteAffixTierEnabled(int i, bool v)
    {
        var s = Save_Manager.instance.data.Items.CraftingSlot;
        if (i == 0)
            s.Enable_Affix_0_Tier = v;
        else if (i == 1)
            s.Enable_Affix_1_Tier = v;
        else if (i == 2)
            s.Enable_Affix_2_Tier = v;
        else if (i == 3)
            s.Enable_Affix_3_Tier = v;
        else if (i == 4)
            s.Enable_Affix_4_Tier = v;
        else if (i == 5)
            s.Enable_Affix_5_Tier = v;
        else if (i == 6)
            s.Enable_Affix_6_Tier = v;
        else if (i == 7)
            s.Enable_Affix_7_Tier = v;
        else
            s.Enable_Affix_8_Tier = v;
        Save_Manager.instance.data.Items.CraftingSlot = s;
    }

    private static void WriteAffixValueEnabled(int i, bool v)
    {
        var s = Save_Manager.instance.data.Items.CraftingSlot;
        if (i == 0)
            s.Enable_Affix_0_Value = v;
        else if (i == 1)
            s.Enable_Affix_1_Value = v;
        else if (i == 2)
            s.Enable_Affix_2_Value = v;
        else if (i == 3)
            s.Enable_Affix_3_Value = v;
        else if (i == 4)
            s.Enable_Affix_4_Value = v;
        else if (i == 5)
            s.Enable_Affix_5_Value = v;
        else if (i == 6)
            s.Enable_Affix_6_Value = v;
        else if (i == 7)
            s.Enable_Affix_7_Value = v;
        else
            s.Enable_Affix_8_Value = v;
        Save_Manager.instance.data.Items.CraftingSlot = s;
    }

    private static void WriteAffixTier(int i, int v)
    {
        var s = Save_Manager.instance.data.Items.CraftingSlot;
        if (i == 0)
            s.Affix_0_Tier = v;
        else if (i == 1)
            s.Affix_1_Tier = v;
        else if (i == 2)
            s.Affix_2_Tier = v;
        else if (i == 3)
            s.Affix_3_Tier = v;
        else if (i == 4)
            s.Affix_4_Tier = v;
        else if (i == 5)
            s.Affix_5_Tier = v;
        else if (i == 6)
            s.Affix_6_Tier = v;
        else if (i == 7)
            s.Affix_7_Tier = v;
        else
            s.Affix_8_Tier = v;
        Save_Manager.instance.data.Items.CraftingSlot = s;
    }

    private static void WriteAffixValue(int i, float v)
    {
        var s = Save_Manager.instance.data.Items.CraftingSlot;
        if (i == 0)
            s.Affix_0_Value = v;
        else if (i == 1)
            s.Affix_1_Value = v;
        else if (i == 2)
            s.Affix_2_Value = v;
        else if (i == 3)
            s.Affix_3_Value = v;
        else if (i == 4)
            s.Affix_4_Value = v;
        else if (i == 5)
            s.Affix_5_Value = v;
        else if (i == 6)
            s.Affix_6_Value = v;
        else if (i == 7)
            s.Affix_7_Value = v;
        else
            s.Affix_8_Value = v;
        Save_Manager.instance.data.Items.CraftingSlot = s;
    }

    private static void WriteUniqueEnabled(int i, bool v)
    {
        var s = Save_Manager.instance.data.Items.CraftingSlot;
        if (i == 0)
            s.Enable_UniqueMod_0 = v;
        else if (i == 1)
            s.Enable_UniqueMod_1 = v;
        else if (i == 2)
            s.Enable_UniqueMod_2 = v;
        else if (i == 3)
            s.Enable_UniqueMod_3 = v;
        else if (i == 4)
            s.Enable_UniqueMod_4 = v;
        else if (i == 5)
            s.Enable_UniqueMod_5 = v;
        else if (i == 6)
            s.Enable_UniqueMod_6 = v;
        else
            s.Enable_UniqueMod_7 = v;
        Save_Manager.instance.data.Items.CraftingSlot = s;
    }

    private static void WriteUnique(int i, float v)
    {
        var s = Save_Manager.instance.data.Items.CraftingSlot;
        if (i == 0)
            s.UniqueMod_0 = v;
        else if (i == 1)
            s.UniqueMod_1 = v;
        else if (i == 2)
            s.UniqueMod_2 = v;
        else if (i == 3)
            s.UniqueMod_3 = v;
        else if (i == 4)
            s.UniqueMod_4 = v;
        else if (i == 5)
            s.UniqueMod_5 = v;
        else if (i == 6)
            s.UniqueMod_6 = v;
        else
            s.UniqueMod_7 = v;
        Save_Manager.instance.data.Items.CraftingSlot = s;
    }

    private static void DeselectAll()
    {
        if (!HasSave())
            return;
        var s = Save_Manager.instance.data.Items.CraftingSlot;
        s.Enable_ForginPotencial =
            s.Enable_Implicit_0 =
            s.Enable_Implicit_1 =
            s.Enable_Implicit_2 =
                false;
        s.Enable_Seal_Tier = s.Enable_Seal_Value = false;
        s.Enable_Affix_0_Tier =
            s.Enable_Affix_1_Tier =
            s.Enable_Affix_2_Tier =
            s.Enable_Affix_3_Tier =
            s.Enable_Affix_4_Tier =
            s.Enable_Affix_5_Tier =
            s.Enable_Affix_6_Tier =
            s.Enable_Affix_7_Tier =
            s.Enable_Affix_8_Tier =
                false;
        s.Enable_Affix_0_Value =
            s.Enable_Affix_1_Value =
            s.Enable_Affix_2_Value =
            s.Enable_Affix_3_Value =
            s.Enable_Affix_4_Value =
            s.Enable_Affix_5_Value =
            s.Enable_Affix_6_Value =
            s.Enable_Affix_7_Value =
            s.Enable_Affix_8_Value =
                false;
        s.Enable_UniqueMod_0 =
            s.Enable_UniqueMod_1 =
            s.Enable_UniqueMod_2 =
            s.Enable_UniqueMod_3 =
                false;
        s.Enable_UniqueMod_4 =
            s.Enable_UniqueMod_5 =
            s.Enable_UniqueMod_6 =
            s.Enable_UniqueMod_7 =
                false;
        s.Enable_LegendaryPotencial = s.Enable_WeaverWill = false;
        Save_Manager.instance.data.Items.CraftingSlot = s;
        Save_Manager.instance.Save();
        page.RefreshValues();
    }

    private static bool HasSave() =>
        !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;
}
