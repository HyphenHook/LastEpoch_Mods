using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FD = LastEpoch_Hud.Scripts.ModUI.ForceDrop.ForceDropModel;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

public static partial class ForceDropBuilder
{

    static string SelectionKey()
    {
        var key = new StringBuilder()
            .Append(FD.ItemType)
            .Append(':')
            .Append(FD.ItemSubtype)
            .Append(':')
            .Append(FD.ItemRarity)
            .Append(':')
            .Append(FD.ItemUniqueId)
            .Append(':')
            .Append(corrupted)
            .Append(':')
            .Append(lp.value)
            .Append(':')
            .Append(ww.value);
        key.Append(':').Append(allowIllegal);
        foreach (var row in rows)
            key.Append(':').Append(row.id).Append('/').Append(row.tier.value);
        foreach (int id in variantIds)
            key.Append(':').Append(id);
        return key.ToString();
    }

    static ForceDropLegalAffixes LegalContext(int editingSlot)
    {
        string key = SelectionKey();
        if (key != legalContextKey)
        {
            legalContextKey = key;
            legalContexts.Clear();
        }
        if (!legalContexts.TryGetValue(editingSlot, out var context))
        {
            var ids = new List<int>();
            for (int slot = 0; slot < rows.Length; slot++)
                if (slot != editingSlot && rows[slot].id >= 0)
                    ids.Add(rows[slot].id);
            context = new ForceDropLegalAffixes(FD.ItemType, FD.ItemSubtype, FD.ItemRarity, ids);
            legalContexts[editingSlot] = context;
        }
        return context;
    }

    static ForceDropCorruptionPool CorruptionPool()
    {
        string key = SelectionKey();
        if (key == corruptionPoolKey)
            return corruptionPool;
        corruptionPoolKey = key;
        corruptionPool = null;
        corruptionPoolError = "";
        try
        {
            var request = ResolveItem(true, false);
            corruptionPool = new ForceDropCorruptionPool(
                ForceDropItemCreator.CreateBeforeCorruption(request)
            );
            corruptionPoolError = corruptionPool.Error;
        }
        catch (Exception ex)
        {
            corruptionPoolError = ex.Message;
        }
        return corruptionPool;
    }

    // Sort by native family, never translated labels. None remains the first
    // choice regardless of language; names are alphabetical within each family.
    static int AffixGroup(Choice choice)
    {
        if (choice.id < 0)
            return -1;
        return choice.affixFamily switch
        {
            ForceDropAffixFamily.Standard => 0,
            ForceDropAffixFamily.Experimental => 1,
            ForceDropAffixFamily.Personal => choice.champion ? 2 : 3,
            ForceDropAffixFamily.Set => 4,
            ForceDropAffixFamily.IdolWeaver => 5,
            ForceDropAffixFamily.IdolEnchantment => 6,
            ForceDropAffixFamily.Corrupted => 7,
            ForceDropAffixFamily.UniqueModifier => 8,
            _ => 9,
        };
    }

    static int CompareAffixChoices(Choice a, Choice b)
    {
        int comparison = AffixGroup(a).CompareTo(AffixGroup(b));
        if (comparison == 0)
            comparison = string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        return comparison != 0 ? comparison : a.id.CompareTo(b.id);
    }

    static Color AffixColor(ForceDropAffixFamily family) =>
        family == ForceDropAffixFamily.Set ? setGreen
        : family == ForceDropAffixFamily.Corrupted ? corruptionPurple
        : gold;

    static Color ChoiceColor(Choice choice) =>
        allowIllegal && choice.id >= 0 && ForceDropLegalAffixes.IsIdolAffix(FindAffix(choice.id))
            ? idolCyan
            : AffixColor(choice.affixFamily);

    static Color SelectedAffixColor(int id) =>
        id < 0 ? gold
        : allowIllegal && ForceDropLegalAffixes.IsIdolAffix(FindAffix(id)) ? idolCyan
        : AffixColor(ForceDropLegalAffixes.Family(FindAffix(id)));

    static void Preset(bool random)
    {
        foreach (var n in numbers)
        {
            if (n == quantity || n == corruptionTier || n == corruptionRoll)
                continue;
            n.random = random && n.mode != null;
            if (!random)
            {
                n.value = n.max;
                n.input.SetTextWithoutNotify(n.value.ToString());
            }
        }
        RefreshModes();
    }

    static void RefreshModes()
    {
        foreach (var n in numbers)
            if (n.mode != null)
            {
                Caption(n.mode, n.random ? "Random" : "Fixed");
                n.input.interactable = !n.random;
            }
        RefreshPotentialControls();
    }

    static bool CreatesLegendary()
    {
        int selected = 0;
        foreach (var row in rows)
            if (row != null && row.id >= 0)
                selected++;
        return ForceDropPotentialRules.CreatesLegendary(FD.ItemRarity, selected);
    }

    static void RefreshPotentialControls()
    {
        if (lp == null || ww == null)
            return;
        bool legendary = CreatesLegendary();
        if (legendary)
        {
            lp.value = 0;
            lp.random = false;
            lp.input.SetTextWithoutNotify("0");
            if (lp.mode != null)
                Caption(lp.mode, "Fixed");
        }
        lp.input.interactable = !corrupted && !legendary && !lp.random;
        if (lp.mode != null)
            lp.mode.interactable = !corrupted && !legendary;
        ww.input.interactable = !corrupted && !ww.random;
        if (ww.mode != null)
            ww.mode.interactable = !corrupted;
    }

    static void Reset()
    {
        foreach (var r in rows)
        {
            r.id = -1;
            r.name = "None";
            Caption(r.select, "None");
        }
        corrupted = false;
        corruptionId = -1;
        corruptionName = "None";
        Caption(corruptButton, "Corrupted: No");
        quantity.value = 1;
        quantity.input.SetTextWithoutNotify("1");
        lp.value = ww.value = 0;
        lp.input.SetTextWithoutNotify("0");
        ww.input.SetTextWithoutNotify("0");
        ResetRage();
        Preset(true);
        result = "";
    }

    static UniqueList.Entry SelectedRageEntry()
    {
        if (
            FD.ItemRarity < 7
            || FD.ItemsDropdown.value <= 0
            || UniqueList.instance.IsNullOrDestroyed()
        )
            return null;
        return UniqueList.getUnique((ushort)FD.ItemUniqueId);
    }

    static void ResetRage()
    {
        bool ring = UniqueVariantAdapter.IsUnsated(SelectedRageEntry());
        for (int slot = 0; slot < variantIds.Length; slot++)
        {
            variantIds[slot] = -1;
            variantNames[slot] =
                ring && slot == 0 ? "Choose Rage"
                : slot == 0 ? "Choose modifier 1"
                : "Choose modifier 2";
            if (!variantSelect[slot].IsNullOrDestroyed())
                Caption(variantSelect[slot], variantNames[slot]);
        }
    }

    static int[] SelectedVariantIds()
    {
        return UniqueVariantAdapter.VariantCount(SelectedRageEntry()) == 2
            ? new[] { variantIds[0], variantIds[1] }
            : new[] { variantIds[0] };
    }

    static AffixList.Affix FindAffix(int id) => ForceDropCatalog.Find(id);

    static int RowMaximum(int slot, AffixList.Affix definition)
    {
        // Native storage has independent regular and Primordial seals.
        // A regular seal stays within T1-T7; the Primordial slot is T8 only.
        if (slot == AffixLimits.SealedSlot)
            return 7;
        if (slot == AffixLimits.PrimordialSlot)
            return 8;
        return RouteMaximum;
    }

    static void RefreshTierLimits()
    {
        for (int slot = 0; slot < rows.Length; slot++)
        {
            var row = rows[slot];
            var definition = FindAffix(row.id);
            int maximum = RowMaximum(slot, definition);
            if (slot == AffixLimits.PrimordialSlot)
            {
                // Primordial is an evolution to T8, not another ordinary tier slider.
                row.tier.min = 8;
                row.tier.max = 8;
                row.tier.value = 8;
                if (!row.tier.input.isFocused)
                    row.tier.input.SetTextWithoutNotify("8");
            }
            else
            {
                row.tier.min = 1;
                SetTierLimit(
                    row.tier,
                    row.id < 0 ? maximum : ForceDropCatalog.MaximumTier(definition, maximum)
                );
            }
            row.selectedLabel.color = SelectedAffixColor(row.id);
        }
        corruptionSelectedLabel.color = SelectedAffixColor(corruptionId);
        if (corruptionId < 0)
            SetTierLimit(corruptionTier, RouteMaximum);
        else if (allowIllegal)
            SetTierLimit(corruptionTier, ForceDropCatalog.MaximumTier(FindAffix(corruptionId), 8));
        else
        {
            var pool = CorruptionPool();
            int mask = pool == null ? 0 : pool.TierMask(corruptionId);
            SetTierLimit(corruptionTier, ForceDropLegalTiers.MaximumDisplayTier(mask));
            int value = ForceDropLegalTiers.ClampDisplayTier(mask, corruptionTier.value);
            if (value > 0 && value != corruptionTier.value)
            {
                corruptionTier.value = value;
                corruptionTier.input.SetTextWithoutNotify(
                    value.ToString(CultureInfo.InvariantCulture)
                );
            }
        }
    }

    static void SetTierLimit(Number number, int maximum)
    {
        number.max = Math.Max(1, maximum);
        if (number.value > number.max)
        {
            number.value = number.max;
            number.input.SetTextWithoutNotify(number.value.ToString(CultureInfo.InvariantCulture));
        }
    }

    static void ChangeMode(bool illegal)
    {
        allowIllegal = illegal;
        // Selections made under one rule set must not leak into the other.
        foreach (var row in rows)
        {
            row.id = -1;
            row.name = "None";
            Caption(row.select, "None");
        }
        corruptionId = -1;
        corruptionName = "None";
        Caption(corruptionSelect, "Corrupted affix: None");
        legalContextKey = corruptionPoolKey = "";
        legalContexts.Clear();
        corruptionPool = null;
        corruptionPoolError = result = "";
        if (!picker.IsNullOrDestroyed())
            picker.SetActive(false);
        RefreshTierLimits();
        RefreshIllegalButton();
    }

    static void RefreshIllegalButton()
    {
        if (illegalModeButton.IsNullOrDestroyed())
            return;
        Caption(illegalModeButton, allowIllegal ? "Illegal: On" : "Illegal: Off");
        StyleSelection(illegalModeButton, allowIllegal);
    }
}
