using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>
/// Items &gt; Drop. This replaces the legacy mixed Items page with one focused card.
/// State resets on HUD re-bind (Build).
/// </summary>
internal static class ItemsDropPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;

        var card = page.AddCard("Drop", "Drop");
        AddExclusiveRarity(card, "ForceUnique", "Force Unique", 0);
        AddExclusiveRarity(card, "ForceSet", "Force Set", 1);
        // The legacy rarity-9 override produces unrelated unique identities.
        // Do not expose it as a working item-generation feature until a proper
        // native legendary creation path has been implemented.
        page.AddText(
            card,
            "ForceLegendaryUnavailable",
            "Force Legendary is temporarily unavailable: forcing rarity 9 creates invalid unique identities. Use Force Drop for explicit legendary items.",
            76f
        );
        AddToggle(
            card,
            "ForceSeal",
            "Force Seal",
            () => Save_Manager.instance.data.Items.Drop.Enable_ForceSeal,
            value => Save_Manager.instance.data.Items.Drop.Enable_ForceSeal = value
        );

        AddRange(
            card,
            "Implicits",
            "Implicit Value",
            ItemSliderLimits.DropImplicits,
            () => Save_Manager.instance.data.Items.Drop.Enable_Implicits,
            value => Save_Manager.instance.data.Items.Drop.Enable_Implicits = value,
            () => Save_Manager.instance.data.Items.Drop.Implicits_Min,
            value => Save_Manager.instance.data.Items.Drop.Implicits_Min = value,
            () => Save_Manager.instance.data.Items.Drop.Implicits_Max,
            value => Save_Manager.instance.data.Items.Drop.Implicits_Max = value
        );
        AddRange(
            card,
            "ForgingPotential",
            "Forging Potential",
            ItemSliderLimits.DropForgingPotential,
            () => Save_Manager.instance.data.Items.Drop.Enable_ForginPotencial,
            value => Save_Manager.instance.data.Items.Drop.Enable_ForginPotencial = value,
            () => Save_Manager.instance.data.Items.Drop.ForginPotencial_Min,
            value => Save_Manager.instance.data.Items.Drop.ForginPotencial_Min = value,
            () => Save_Manager.instance.data.Items.Drop.ForginPotencial_Max,
            value => Save_Manager.instance.data.Items.Drop.ForginPotencial_Max = value
        );
        AddRange(
            card,
            "SealTier",
            "Seal Tier",
            ItemSliderLimits.DropSealTier,
            () => Save_Manager.instance.data.Items.Drop.Enable_SealTier,
            v => Save_Manager.instance.data.Items.Drop.Enable_SealTier = v,
            () => Save_Manager.instance.data.Items.Drop.SealTier_Min,
            v => Save_Manager.instance.data.Items.Drop.SealTier_Min = v,
            () => Save_Manager.instance.data.Items.Drop.SealTier_Max,
            v => Save_Manager.instance.data.Items.Drop.SealTier_Max = v
        );
        AddRange(
            card,
            "SealValue",
            "Seal Value",
            ItemSliderLimits.DropSealValue,
            () => Save_Manager.instance.data.Items.Drop.Enable_SealValue,
            v => Save_Manager.instance.data.Items.Drop.Enable_SealValue = v,
            () => Save_Manager.instance.data.Items.Drop.SealValue_Min,
            v => Save_Manager.instance.data.Items.Drop.SealValue_Min = v,
            () => Save_Manager.instance.data.Items.Drop.SealValue_Max,
            v => Save_Manager.instance.data.Items.Drop.SealValue_Max = v
        );
        AddRange(
            card,
            "AffixCount",
            "Affix Count",
            ItemSliderLimits.DropAffixCount,
            () => Save_Manager.instance.data.Items.Drop.Enable_AffixCount,
            v => Save_Manager.instance.data.Items.Drop.Enable_AffixCount = v,
            () => Save_Manager.instance.data.Items.Drop.AffixCount_Min,
            v => Save_Manager.instance.data.Items.Drop.AffixCount_Min = v,
            () => Save_Manager.instance.data.Items.Drop.AffixCount_Max,
            v => Save_Manager.instance.data.Items.Drop.AffixCount_Max = v
        );
        AddToggle(
            card,
            "ExtraAffixes",
            "Extra Affixes (5 and 6)",
            () => Save_Manager.instance.data.Items.Drop.Enable_ExtraAffixes,
            v => Save_Manager.instance.data.Items.Drop.Enable_ExtraAffixes = v
        );
        var fifthSlider = page.AddSlider(
            card,
            "ExtraAffix5",
            "Chance of a 5th affix (%)",
            string.Empty,
            0f,
            100f,
            true,
            () => HasSave() ? Save_Manager.instance.data.Items.Drop.ExtraAffix5Chance : 0f,
            value =>
            {
                if (HasSave())
                    Save_Manager.instance.data.Items.Drop.ExtraAffix5Chance = value;
            }
        );
        var sixthSlider = page.AddSlider(
            card,
            "ExtraAffix6",
            "Chance of a 6th affix (%)",
            string.Empty,
            0f,
            100f,
            true,
            () => HasSave() ? Save_Manager.instance.data.Items.Drop.ExtraAffix6Chance : 0f,
            value =>
            {
                if (HasSave())
                    Save_Manager.instance.data.Items.Drop.ExtraAffix6Chance = value;
            }
        );
        page.SetVisibleWhen(fifthSlider, () => HasSave() && Save_Manager.instance.data.Items.Drop.Enable_ExtraAffixes);
        page.SetVisibleWhen(sixthSlider, () => HasSave() && Save_Manager.instance.data.Items.Drop.Enable_ExtraAffixes);
        AddRange(
            card,
            "IdolAffixCount",
            "Idol Affix Count",
            ItemSliderLimits.DropIdolAffixCount,
            () => Save_Manager.instance.data.Items.Drop.Enable_IdolAffixCount,
            v => Save_Manager.instance.data.Items.Drop.Enable_IdolAffixCount = v,
            () => Save_Manager.instance.data.Items.Drop.IdolAffixCount_Min,
            v => Save_Manager.instance.data.Items.Drop.IdolAffixCount_Min = v,
            () => Save_Manager.instance.data.Items.Drop.IdolAffixCount_Max,
            v => Save_Manager.instance.data.Items.Drop.IdolAffixCount_Max = v
        );
        AddRange(
            card,
            "AffixTiers",
            "Affix Tier",
            ItemSliderLimits.DropAffixTiers,
            () => Save_Manager.instance.data.Items.Drop.Enable_AffixTiers,
            v => Save_Manager.instance.data.Items.Drop.Enable_AffixTiers = v,
            () => Save_Manager.instance.data.Items.Drop.AffixTiers_Min,
            v => Save_Manager.instance.data.Items.Drop.AffixTiers_Min = v,
            () => Save_Manager.instance.data.Items.Drop.AffixTiers_Max,
            v => Save_Manager.instance.data.Items.Drop.AffixTiers_Max = v
        );
        AddRange(
            card,
            "AffixValues",
            "Affix Value",
            ItemSliderLimits.DropAffixValues,
            () => Save_Manager.instance.data.Items.Drop.Enable_AffixValues,
            v => Save_Manager.instance.data.Items.Drop.Enable_AffixValues = v,
            () => Save_Manager.instance.data.Items.Drop.AffixValues_Min,
            v => Save_Manager.instance.data.Items.Drop.AffixValues_Min = v,
            () => Save_Manager.instance.data.Items.Drop.AffixValues_Max,
            v => Save_Manager.instance.data.Items.Drop.AffixValues_Max = v
        );
        AddRange(
            card,
            "UniqueMods",
            "Unique Mod Value",
            ItemSliderLimits.DropUniqueMods,
            () => Save_Manager.instance.data.Items.Drop.Enable_UniqueMods,
            v => Save_Manager.instance.data.Items.Drop.Enable_UniqueMods = v,
            () => Save_Manager.instance.data.Items.Drop.UniqueMods_Min,
            v => Save_Manager.instance.data.Items.Drop.UniqueMods_Min = v,
            () => Save_Manager.instance.data.Items.Drop.UniqueMods_Max,
            v => Save_Manager.instance.data.Items.Drop.UniqueMods_Max = v
        );
        AddRange(
            card,
            "LegendaryPotential",
            "Legendary Potential",
            ItemSliderLimits.DropLegendaryPotential,
            () => Save_Manager.instance.data.Items.Drop.Enable_LegendaryPotencial,
            v => Save_Manager.instance.data.Items.Drop.Enable_LegendaryPotencial = v,
            () => Save_Manager.instance.data.Items.Drop.LegendaryPotencial_Min,
            v => Save_Manager.instance.data.Items.Drop.LegendaryPotencial_Min = v,
            () => Save_Manager.instance.data.Items.Drop.LegendaryPotencial_Max,
            v => Save_Manager.instance.data.Items.Drop.LegendaryPotencial_Max = v
        );
        AddRange(
            card,
            "WeaverWill",
            "Weaver Will",
            ItemSliderLimits.DropWeaverWill,
            () => Save_Manager.instance.data.Items.Drop.Enable_WeaverWill,
            v => Save_Manager.instance.data.Items.Drop.Enable_WeaverWill = v,
            () => Save_Manager.instance.data.Items.Drop.WeaverWill_Min,
            v => Save_Manager.instance.data.Items.Drop.WeaverWill_Min = v,
            () => Save_Manager.instance.data.Items.Drop.WeaverWill_Max,
            v => Save_Manager.instance.data.Items.Drop.WeaverWill_Max = v
        );
    }

    public static void Show() => page?.Show();

    public static void Hide() => page?.Hide();

    public static void Refresh() => page?.RefreshValues();

    private static void AddExclusiveRarity(
        HudFormPage.Card card,
        string id,
        string label,
        int rarity
    )
    {
        AddToggle(
            card,
            id,
            label,
            () =>
                rarity == 0 ? Save_Manager.instance.data.Items.Drop.Enable_ForceUnique
                : rarity == 1 ? Save_Manager.instance.data.Items.Drop.Enable_ForceSet
                : Save_Manager.instance.data.Items.Drop.Enable_ForceLegendary,
            value =>
            {
                if (rarity == 0)
                    Save_Manager.instance.data.Items.Drop.Enable_ForceUnique = value;
                else if (rarity == 1)
                    Save_Manager.instance.data.Items.Drop.Enable_ForceSet = value;
                else
                    Save_Manager.instance.data.Items.Drop.Enable_ForceLegendary = value;
                if (value)
                {
                    if (rarity != 0)
                        Save_Manager.instance.data.Items.Drop.Enable_ForceUnique = false;
                    if (rarity != 1)
                        Save_Manager.instance.data.Items.Drop.Enable_ForceSet = false;
                    if (rarity != 2)
                        Save_Manager.instance.data.Items.Drop.Enable_ForceLegendary = false;
                    page.RefreshValues();
                }
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
            value =>
            {
                if (HasSave())
                    write(value);
            }
        );
    }

    private static void AddRange(
        HudFormPage.Card card,
        string id,
        string label,
        SliderRange range,
        Func<bool> readEnabled,
        Action<bool> writeEnabled,
        Func<float> readMinimum,
        Action<float> writeMinimum,
        Func<float> readMaximum,
        Action<float> writeMaximum
    )
    {
        AddToggle(card, id, label, readEnabled, writeEnabled);
        var minimumSlider = page.AddSlider(
            card,
            id + "Minimum",
            "Minimum",
            string.Empty,
            range.Min,
            range.Max,
            range.WholeNumbers,
            () => HasSave() ? readMinimum() : range.Min,
            value =>
            {
                if (!HasSave())
                    return;
                MinMaxPair pair = new MinMaxPair(readMinimum(), readMaximum()).WithMin(value);
                writeMinimum(pair.Min);
                writeMaximum(pair.Max);
                page.RefreshValues();
            }
        );
        var maximumSlider = page.AddSlider(
            card,
            id + "Maximum",
            "Maximum",
            string.Empty,
            range.Min,
            range.Max,
            range.WholeNumbers,
            () => HasSave() ? readMaximum() : range.Max,
            value =>
            {
                if (!HasSave())
                    return;
                MinMaxPair pair = new MinMaxPair(readMinimum(), readMaximum()).WithMax(value);
                writeMinimum(pair.Min);
                writeMaximum(pair.Max);
                page.RefreshValues();
            }
        );
        page.SetVisibleWhen(minimumSlider, () => HasSave() && readEnabled());
        page.SetVisibleWhen(maximumSlider, () => HasSave() && readEnabled());
    }

    private static bool HasSave() =>
        !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;
}
