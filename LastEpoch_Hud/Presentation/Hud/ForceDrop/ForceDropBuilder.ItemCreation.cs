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

    static bool ValidSelectedItem()
    {
        var list = ItemList.get();
        if (list.IsNullOrDestroyed() || FD.ItemType < 0 || FD.ItemSubtype < 0)
        {
            return false;
        }
        bool baseExists = false;
        foreach (var type in list.EquippableItems)
        {
            if (type.baseTypeID == FD.ItemType)
            {
                foreach (var item in type.subItems)
                {
                    if (item.subTypeID == FD.ItemSubtype)
                    {
                        baseExists = true;
                        break;
                    }
                }
            }
        }
        foreach (var type in list.nonEquippableItems)
        {
            if (type.baseTypeID == FD.ItemType)
            {
                foreach (var item in type.subItems)
                {
                    if (item.subTypeID == FD.ItemSubtype)
                    {
                        baseExists = true;
                        break;
                    }
                }
            }
        }
        if (!baseExists)
        {
            return false;
        }
        if (FD.ItemRarity == 0)
        {
            return true;
        }
        if (UniqueList.instance.IsNullOrDestroyed())
        {
            return false;
        }
        foreach (var entry in UniqueList.instance.uniques)
        {
            if (
                entry.uniqueID == FD.ItemUniqueId
                && entry.baseType == FD.ItemType
                && entry.isSetItem == (FD.ItemRarity == 8)
            )
            {
                foreach (var subType in entry.subTypes)
                {
                    if (subType == FD.ItemSubtype)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    static string Validate()
    {
        if (
            FD.TypeDropdown.value <= 0
            || FD.RarityDropdown.value <= 0
            || FD.ItemsDropdown.value <= 0
            || FD.ItemSubtype < 0
        )
            return "Choose a category, rarity and item.";
        if (
            Refs_Manager.player_actor.IsNullOrDestroyed()
            || Refs_Manager.ground_item_manager.IsNullOrDestroyed()
        )
            return "Enter the game before dropping items.";
        var ids = new HashSet<int>();
        foreach (var r in rows)
            if (r.id >= 0 && !ids.Add(r.id))
                return "Each affix must be different, including the sealed affix.";
        if (!ValidSelectedItem())
            return "The selected item does not match its category. Choose it again.";
        var rageEntry = SelectedRageEntry();
        int variantCount = UniqueVariantAdapter.VariantCount(rageEntry);
        if (variantCount > 0)
        {
            if (!UniqueVariantAdapter.HasVariants(rageEntry))
                return "Unique modifier pool unavailable";
            var variantCatalog = UniqueVariantAdapter.Catalog(rageEntry);
            for (int slot = 0; slot < variantCount; slot++)
            {
                if (!variantCatalog.Exists(a => a.affixId == variantIds[slot]))
                    return "Choose every exclusive unique modifier.";
                if (!ids.Add(variantIds[slot]))
                    return "Exclusive unique modifiers must be different and separate from ordinary affixes.";
            }
        }
        if (ids.Count + (corrupted && corruptionId >= 0 ? 1 : 0) > AffixLimits.MaxTotal)
            return "An item holds at most "
                + AffixLimits.MaxTotal
                + " affixes, including sealed, Primordial, corruption and unique modifiers.";
        for (int slot = 0; slot < rows.Length; slot++)
        {
            var row = rows[slot];
            if (row.id < 0)
                continue;
            var definition = FindAffix(row.id);
            var context = LegalContext(slot);
            if (
                !allowIllegal
                && !ForceDropLegalRules.SlotAllowed(
                    slot,
                    context.IsIdol,
                    context.IsUnique,
                    context.IsSet,
                    context.IsHereticalIdol,
                    context.BaseType
                )
            )
                return "This item has no legal affix slot here. Clear the selection.";
            string reason = context.OrdinaryReason(
                definition,
                AffixLimits.IsSealedSlot(slot),
                context.IsHereticalIdol && (slot == 1 || slot == 3)
            );
            if (!allowIllegal && reason.Length > 0)
                return NativeItemNames.AffixName(row.id, row.name) + ": " + reason;
            if (
                slot == AffixLimits.PrimordialSlot
                && (
                    row.tier.value != 8
                    || !ForceDropModeRules.CanSealPrimordial(
                        FD.ItemType,
                        FD.ItemRarity,
                        ForceDropLegalAffixes.Family(definition),
                        ForceDropCatalog.MaximumTier(definition, 8),
                        allowIllegal ? ForceDropMode.Illegal : ForceDropMode.Legal
                    )
                )
            )
                return "The Primordial slot requires an eligible T8 affix.";
            if (
                row.tier.value
                > ForceDropCatalog.MaximumTier(definition, RowMaximum(slot, definition))
            )
                return "The selected tier does not exist for this affix.";
        }
        var itemContext = LegalContext(-1);
        if (!allowIllegal && itemContext.IsWeaverIdol)
        {
            bool hasWeaver = false;
            foreach (var row in rows)
            {
                var definition = FindAffix(row.id);
                hasWeaver |=
                    !definition.IsNullOrDestroyed()
                    && definition.specialAffixType == AffixList.SpecialAffixType.IdolWeaver;
            }
            if (!hasWeaver)
                return "A Weaver idol requires at least one Weaver affix.";
        }
        if (corrupted && corruptionId >= 0)
        {
            var pool = allowIllegal ? null : CorruptionPool();
            if (!CorruptedAffixAdapter.IsSupported || (!allowIllegal && pool == null))
                return "Corruption selection unavailable: " + corruptionPoolError;
            if (
                !allowIllegal
                && !ForceDropLegalTiers.Supports(
                    pool.TierMask(corruptionId),
                    corruptionTier.value - 1
                )
            )
                return "The chosen corruption affix or tier is not permitted by this item's native outcomes.";
            if (
                allowIllegal
                && corruptionTier.value > ForceDropCatalog.MaximumTier(FindAffix(corruptionId), 8)
            )
                return "The selected tier does not exist for this affix.";
            if (!ids.Add(corruptionId))
                return "Corruption cannot duplicate another affix.";
        }
        foreach (var n in numbers)
            if (!int.TryParse(n.input.text, out int value) || value < n.min || value > n.max)
                return "Use whole numbers within each field's range.";
        if (FD.ItemType >= 100 && corrupted)
        {
            return "Corruption is available for equipment only.";
        }
        return "";
    }

    static void RefreshPreview()
    {
        var s = new StringBuilder(
            FD.ItemsDropdown.value > 0 ? ItemName(FD.ItemsDropdown.value) : L("Choose an item")
        );
        s.Append("\n\n")
            .Append(SelectedCategoryName(""))
            .Append("\n")
            .Append(NativeItemNames.RarityName(Selected(FD.RarityDropdown, "")));
        if (FD.ItemType < 100)
        {
            if (FD.ItemRarity < 7)
            {
                s.Append("\n")
                    .Append(L("Forging potential"))
                    .Append(": ")
                    .Append(
                        corrupted ? "0"
                        : forging.random ? L("Random")
                        : forging.value.ToString()
                    );
            }
            foreach (var r in rows)
                if (r.id >= 0)
                    s.Append("\n\n")
                        .Append(
                            r == rows[AffixLimits.SealedSlot] ? L("Sealed") + ": "
                            : r == rows[AffixLimits.PrimordialSlot] ? L("Primordial") + ": "
                            : ""
                        )
                        .Append(r.name)
                        .Append("\nT")
                        .Append(r.tier.value)
                        .Append(" · ")
                        .Append((r.roll.random ? L("Random") : r.roll.value.ToString()))
                        .Append(" %");
            if (FD.ItemRarity > 6)
            {
                s.Append("\n\n")
                    .Append(
                        FD.ItemLegendaryType == UniqueList.LegendaryType.LegendaryPotential
                            ? L("LP")
                                + ": "
                                + (
                                    corrupted || CreatesLegendary() ? "0"
                                    : lp.random ? L("Random")
                                    : lp.value.ToString()
                                )
                            : L("Weaver's Will")
                                + ": "
                                + (
                                    corrupted ? "0"
                                    : ww.random ? L("Random")
                                    : ww.value.ToString()
                                )
                    );
            }
        }
        int variantCount = UniqueVariantAdapter.VariantCount(SelectedRageEntry());
        for (int slot = 0; slot < variantCount; slot++)
            s.Append("\n\n")
                .Append(
                    L(
                        variantCount == 1 ? "Ring variant"
                        : slot == 0 ? "Glove modifier 1"
                        : "Glove modifier 2"
                    )
                )
                .Append(": ")
                .Append(L(variantNames[slot]));
        s.Append("\n\n").Append(L("Corrupted")).Append(": ").Append(corrupted ? L("Yes") : L("No"));
        if (corrupted && corruptionId >= 0)
            s.Append("\n")
                .Append(corruptionName)
                .Append("\nT")
                .Append(corruptionTier.value)
                .Append(" · ")
                .Append(corruptionRoll.value)
                .Append(" %");
        preview.text = s.ToString();
        string problem = Validate();
        status.text = L(
            problem.Length > 0 ? problem
            : result.Length > 0 ? result
            : "Drops at your character."
        );
        dropButton.interactable = problem.Length == 0;
    }

    static int Sample(Number n) => n.random ? UnityEngine.Random.Range(n.min, n.max + 1) : n.value;

    static int Roll(Number n) =>
        n.random ? UnityEngine.Random.Range(0, 256) : Mathf.RoundToInt(n.value / 100f * 255f);

    static ResolvedForceDrop ResolveItem(bool previewOnly = false, bool includeCorruption = true)
    {
        bool equipment = FD.ItemType < 100;
        bool unique = FD.ItemRarity >= 7;
        int ResolvedRoll(Number n) => previewOnly ? 255 : Roll(n);
        int ResolvedNumber(Number n) => previewOnly ? n.value : Sample(n);
        var selected = new List<ResolvedForceDropAffix>();
        if (equipment)
            for (int slot = 0; slot < rows.Length; slot++)
            {
                var row = rows[slot];
                if (row.id >= 0)
                    selected.Add(
                        new ResolvedForceDropAffix(
                            row.id,
                            row.tier.value - 1,
                            ResolvedRoll(row.roll),
                            slot == AffixLimits.SealedSlot ? ForceDropSeal.Regular
                                : slot == AffixLimits.PrimordialSlot ? ForceDropSeal.Primordial
                                : ForceDropSeal.None
                        )
                    );
            }
        var implicitValues = new int[implicits.Length];
        for (int i = 0; i < implicitValues.Length; i++)
            implicitValues[i] = equipment ? ResolvedRoll(implicits[i]) : 0;
        var uniqueValues = new int[uniqueRolls.Length];
        if (unique)
            for (int i = 0; i < uniqueValues.Length; i++)
                uniqueValues[i] = ResolvedRoll(uniqueRolls[i]);
        bool usesLP = FD.ItemType == Core.Items.EquipmentTypeRules.JewelBaseType
            || (unique && FD.ItemLegendaryType == UniqueList.LegendaryType.LegendaryPotential);
        return new ResolvedForceDrop(
            FD.ItemType,
            FD.ItemSubtype,
            unique ? FD.ItemUniqueId : 0,
            FD.ItemRarity,
            equipment && !unique && !corrupted ? ResolvedNumber(forging) : 0,
            usesLP
            && !corrupted
            && !ForceDropPotentialRules.CreatesLegendary(FD.ItemRarity, selected.Count)
                ? ResolvedNumber(lp)
                : 0,
            unique && !usesLP && !corrupted ? ResolvedNumber(ww) : 0,
            corrupted,
            implicitValues,
            uniqueValues,
            selected,
            UniqueVariantAdapter.VariantCount(SelectedRageEntry()) > 0
                ? SelectedVariantIds()
                : Array.Empty<int>(),
            includeCorruption && corrupted && corruptionId >= 0
                ? new ResolvedForceDropAffix(
                    corruptionId,
                    corruptionTier.value - 1,
                    ResolvedRoll(corruptionRoll),
                    ForceDropSeal.Corruption
                )
                : null,
            allowIllegal ? ForceDropMode.Illegal : ForceDropMode.Legal
        );
    }

    static void Drop()
    {
        RefreshTierLimits();
        foreach (var n in numbers)
            n.Read();
        RefreshPotentialControls();
        string problem = Validate();
        if (problem.Length != 0)
        {
            result = problem;
            return;
        }
        int requested = quantity.value;
        int dropped = 0;
        try
        {
            for (int copy = 0; copy < requested; copy++)
            {
                // Each copy gets one immutable request and independently resolved
                // rolls. Construction and verification never sample them again.
                ForceDropItemCreator.Drop(ResolveItem());
                dropped++;
            }
            result = "Dropped " + dropped + " item(s).";
        }
        catch (Exception ex)
        {
            result =
                "Dropped " + dropped + " of " + requested + " item(s). Drop failed: " + ex.Message;
            Main.logger_instance.Error(result);
        }
    }

    static void LogCorruptionMetadata()
    {
        if (metadataLogged)
            return;
        metadataLogged = true;
        foreach (var nested in typeof(AffixList).GetNestedTypes())
            if (
                nested.IsEnum
                && nested.Name.IndexOf("special", StringComparison.OrdinalIgnoreCase) >= 0
            )
                Main.logger_instance.Msg(
                    "Force Drop " + nested.Name + ": " + string.Join(", ", Enum.GetNames(nested))
                );
        Main.logger_instance.Msg(
            "Force Drop affix types: "
                + string.Join(", ", Enum.GetNames(typeof(AffixList.AffixType)))
        );
        foreach (var member in typeof(AffixList).GetMembers())
            if (
                member.MemberType == System.Reflection.MemberTypes.Field
                || member.MemberType == System.Reflection.MemberTypes.Property
            )
                Main.logger_instance.Msg("Force Drop catalog API: " + member);
        foreach (
            var type in new[]
            {
                typeof(ItemData),
                typeof(ItemDataUnpacked),
                typeof(ItemAffix),
                typeof(AffixList),
                typeof(AffixList.Affix),
                typeof(AffixList.SingleAffix),
                typeof(AffixList.MultiAffix),
            }
        )
        foreach (var member in type.GetMembers())
            if (
                member.Name.IndexOf("corrupt", StringComparison.OrdinalIgnoreCase) >= 0
                || member.Name.IndexOf("special", StringComparison.OrdinalIgnoreCase) >= 0
            )
                Main.logger_instance.Msg("Force Drop corruption API: " + type.Name + "." + member);
    }
}
