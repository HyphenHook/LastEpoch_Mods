using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using LastEpoch_Hud.Scripts.Mods.Items.Requirements;
using Newtonsoft.Json;
using LastEpoch_Hud.Scripts.Infrastructure.Persistence;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

// Registration and copied affix definitions belong to JewelBaseType/JewelAffixes.
// This file owns crafting compatibility and LP for non-unique Jewels, whose
// native packed format has no LP field. No Eternity Cache eligibility is changed.
internal static class JewelCrafting
{
    public const byte Id = EquipmentTypeRules.JewelBaseType;
    [ThreadStatic] private static bool generatingJewel;
    private sealed class Potential
    {
        public int SubType;
        public int Value;
    }

    private static Dictionary<uint, Potential> potentials;
    private static bool potentialLoadFailed;
    private static string SavePath => Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name, "JewelLegendaryPotential.json");
    private static Dictionary<uint, Potential> Potentials => potentials ??= LoadPotentials();

    private static Dictionary<uint, Potential> LoadPotentials()
    {
        try
        {
            var store = new AtomicJsonRepository<Dictionary<uint, Potential>>(SavePath);
            return store.TryLoad(out var loaded) ? loaded : new();
        }
        catch (Exception ex)
        {
            // Keep native item loading usable, but never overwrite unreadable LP data.
            potentialLoadFailed = true;
            ErrorLog.Report(ex, "Jewel legendary potential metadata");
            return new();
        }
    }

    public static bool IsJewel(ItemData item) => !item.IsNullOrDestroyed() && item.itemType == Id;

    public static int LegendaryPotentialOf(ItemData item)
    {
        if (!IsJewel(item) || item.corrupted) return 0;
        if (item.rarity >= 7) return Math.Clamp((int)item.legendaryPotential, 0, 4);
        return item.individualID != 0 && Potentials.TryGetValue(item.individualID, out var saved)
            && saved != null && saved.SubType == item.subType ? Math.Clamp(saved.Value, 0, 4)
            : Math.Clamp((int)item.legendaryPotential, 0, 4);
    }

    // Call after assigning the subtype. A persistent ID is allocated if needed.
    // Save errors propagate so callers cannot report a successfully created LP Jewel
    // when its non-native value has not actually been persisted.
    public static void SetLegendaryPotential(ItemData item, int value)
    {
        if (!IsJewel(item)) throw new ArgumentException("Requires a Jewel.", nameof(item));
        value = item.corrupted ? 0 : Math.Clamp(value, 0, 4);
        if (item.rarity < 7)
        {
            if (item.individualID == 0) item.RollIndividualID();
            Remember(item, value);
        }
        item.legendaryPotential = (byte)value;
        item.RefreshIDAndValues();
    }

    internal static string Inspect(ItemData item)
    {
        if (!IsJewel(item)) return "Place a Jewel in the slot first.";
        var restored = new ItemDataUnpacked(item.GetID());
        return $"Jewel {item.subType}; ID {item.individualID}; FP {item.forgingPotential}; LP {LegendaryPotentialOf(item)}\n"
            + $"Packed round trip: ID {restored.individualID}; FP {restored.forgingPotential}; LP {LegendaryPotentialOf(restored)}\n"
            + "Regular Forge: corruption only; ordinary crafting: four; transfers: six, any prefix/suffix balance.";
    }

    private static void Remember(ItemData item, int value)
    {
        if (item.individualID == 0) return;
        var data = Potentials;
        if (potentialLoadFailed) throw new IOException("Jewel LP metadata could not be loaded; the existing file was preserved.");
        data.TryGetValue(item.individualID, out var previous);
        if (previous != null && previous.SubType == item.subType && previous.Value == value) return;
        if (previous == null && value == 0) return;
        if (value == 0) data.Remove(item.individualID);
        else data[item.individualID] = new Potential { SubType = item.subType, Value = value };
        try
        {
            new AtomicJsonRepository<Dictionary<uint, Potential>>(SavePath).Save(data);
        }
        catch
        {
            if (previous == null) data.Remove(item.individualID);
            else data[item.individualID] = previous;
            throw;
        }
    }

    private static void RestorePotential(ItemData item)
    {
        if (!IsJewel(item) || item.rarity >= 7) return;
        item.legendaryPotential = (byte)LegendaryPotentialOf(item);
    }

    internal static bool CanRollOn(AffixList.Affix definition, int subtype, ItemList.ClassRequirement requirement)
    {
        var list = ItemList.get();
        var sub = list.IsNullOrDestroyed() ? null : list.GetEquipmentSubItem(Id, subtype);
        if (sub.IsNullOrDestroyed()) return false;
        // Ignore equip-restriction cheats when deciding which class affixes fit.
        if (!Items_Req_Class.TryGetOriginalRequirement(Id, subtype, sub.classRequirement, out var original))
            original = sub.classRequirement; // Jewel subtypes are class-neutral even if registered after the backup.
        if (original == ItemList.ClassRequirement.None) original = ItemList.ClassRequirement.Any;
        // Corruption keeps the native jewellery pool. These are native corruption
        // modifiers, separate from ordinary copied Jewel affixes.
        if (!definition.IsNullOrDestroyed() && definition.specialAffixType == AffixList.SpecialAffixType.Corrupted)
            return definition.CanRollOn((int)EquipmentType.RING, 0, original);
        return !definition.IsNullOrDestroyed() && definition.uniqueId == 0
            && definition.specialAffixType == AffixList.SpecialAffixType.Standard
            && (definition.type == AffixList.AffixType.PREFIX || definition.type == AffixList.AffixType.SUFFIX)
            && !definition.canRollOn.IsNullOrDestroyed() && definition.canRollOn.Contains((EquipmentType)Id)
            && !definition.tiers.IsNullOrDestroyed() && definition.tiers.Count > 0
            && definition.CanRollOnClassRequirement(original);
    }

    [HarmonyPatch(typeof(AffixList.Affix), nameof(AffixList.Affix.CanRollOn), new[] { typeof(int), typeof(int), typeof(ItemList.ClassRequirement) })]
    private static class NativeAffixLegality
    {
        [HarmonyPrefix]
        private static bool Prefix(AffixList.Affix __instance, int __0, int __1, ItemList.ClassRequirement __2, ref bool __result)
        {
            if (__0 != Id) return true;
            __result = CanRollOn(__instance, __1, __2);
            return false;
        }
    }

    [HarmonyPatch(typeof(AffixList.Affix), nameof(AffixList.Affix.CanRollOn), new[] { typeof(EquipmentType), typeof(int), typeof(ItemList.ClassRequirement) })]
    private static class NativeEquipmentAffixLegality
    {
        [HarmonyPrefix]
        private static bool Prefix(AffixList.Affix __instance, EquipmentType __0, int __1, ItemList.ClassRequirement __2, ref bool __result)
        {
            if ((int)__0 != Id) return true;
            __result = CanRollOn(__instance, __1, __2);
            return false;
        }
    }

    [HarmonyPatch(typeof(AffixList.Affix), nameof(AffixList.Affix.CanRollOn), new[] { typeof(ItemData) })]
    private static class NativeItemAffixLegality
    {
        [HarmonyPrefix]
        private static bool Prefix(AffixList.Affix __instance, ItemData __0, ref bool __result)
        {
            if (!IsJewel(__0)) return true;
            __result = CanRollOn(__instance, __0.subType, ItemList.ClassRequirement.Any);
            return false;
        }
    }

    [HarmonyPatch]
    private static class NativeDetailedAffixLegality
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(AffixList.Affix), nameof(AffixList.Affix.CanRollOn),
            new[] { typeof(EquipmentType), typeof(int), typeof(ItemList.ClassRequirement), typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType() });

        [HarmonyPrefix]
        private static bool Prefix(AffixList.Affix __instance, EquipmentType __0, int __1, ItemList.ClassRequirement __2,
            ref bool __3, ref bool __4, ref bool __result)
        {
            if ((int)__0 != Id) return true;
            __result = CanRollOn(__instance, __1, __2);
            __3 = !__result && !__instance.CanRollOnClassRequirement(__2);
            __4 = !__result && !__3;
            return false;
        }
    }

    [HarmonyPatch(typeof(AffixList.Affix), nameof(AffixList.Affix.CanForgeOnItem))]
    private static class AdvancedAffixLegality
    {
        [HarmonyPrefix]
        private static bool Prefix(AffixList.Affix __instance, int __0, int __1, ItemList.ClassRequirement __2, bool __3, ref bool __result)
        {
            if (__0 != Id) return true;
            __result = CanRollOn(__instance, __1, __2) && (__3 || JewelAffixes.MinimumTier(__instance.affixId) == 1);
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.AddAffixTier))]
    private static class OrdinaryCraftCapacity
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, int __0, ref bool __result)
        {
            if (!IsJewel(__instance)) return true;
            var existing = __instance.affixes.ToArray().FirstOrDefault(a => a != null && a.affixId == __0);
            // Upgrades may still address a transferred fifth/sixth affix. A seal
            // does not use an ordinary slot; an encapsulated ordinary affix does.
            bool allowed = CraftProtection.Blocked(__instance).Length == 0
                && (existing != null ? !CraftProtection.IsAttached(__instance, existing) && existing.affixTier < 6
                    : JewelAffixes.MinimumTier(__0) == 1
                        && __instance.affixes.ToArray().Count(a => a != null) < EquipmentTypeRules.JewelTransferAffixes
                        && __instance.affixes.ToArray().Count(a => a != null && !a.IsSealed) < EquipmentTypeRules.JewelCraftedAffixes);
            if (allowed) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemList), nameof(ItemList.GetItemMaximumAffixes))]
    private static class MaximumAffixes
    {
        [HarmonyPrefix]
        private static bool Prefix(int __0, ref int __result)
        {
            if (__0 != Id) return true;
            __result = generatingJewel ? EquipmentTypeRules.JewelCraftedAffixes : EquipmentTypeRules.JewelTransferAffixes;
            return false;
        }
    }

    [HarmonyPatch(typeof(GenerateItems), nameof(GenerateItems.RollAffixes))]
    private static class NativeGenerationLimit
    {
        private sealed class GenerationState
        {
            public bool PreviousScope;
            public ItemList.BaseEquipmentItem Category;
            public int PreviousMaximum;
        }

        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void Prefix(ItemData __0, ref int __9, ref int __10, out GenerationState __state)
        {
            __state = null;
            if (!IsJewel(__0)) return;
            var category = ItemList.get()?.GetEquipmentBaseItem(Id);
            __state = new GenerationState { PreviousScope = generatingJewel, Category = category,
                PreviousMaximum = category.IsNullOrDestroyed() ? EquipmentTypeRules.JewelTransferAffixes : category.maximumAffixes };
            generatingJewel = true;
            if (__9 > 0) __9 = Math.Min(__9, EquipmentTypeRules.JewelCraftedAffixes);
            if (__10 > 0) __10 = Math.Min(__10, Math.Max(0, EquipmentTypeRules.JewelCraftedAffixes - Math.Max(0, __9)));
            // Native generation can read either the catalog field or the lookup.
            // Limit both for this synchronous call, then restore even on failure.
            if (!category.IsNullOrDestroyed())
                category.maximumAffixes = Math.Min(category.maximumAffixes, EquipmentTypeRules.JewelCraftedAffixes);
        }

        [HarmonyFinalizer]
        private static void Finalizer(GenerationState __state)
        {
            if (__state == null) return;
            generatingJewel = __state.PreviousScope;
            if (!__state.Category.IsNullOrDestroyed()) __state.Category.maximumAffixes = __state.PreviousMaximum;
        }
    }

    // Evaluate native corruption rarity/state rules on jewellery without changing
    // the real Jewel's type, identity, affixes or packed bytes.
    private static ItemDataUnpacked JewelleryCopy(ItemData item)
    {
        var copy = new ItemDataUnpacked(item.GetID()) { itemType = (byte)EquipmentType.RING, subType = 0 };
        return copy;
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.isForgeableItem))]
    private static class ForgeEligibility
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, ref bool __result)
        {
            if (!IsJewel(__instance)) return true;
            __result = JewelleryCopy(__instance).isForgeableItem();
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.CanBeCorrupted))]
    private static class CorruptionEligibility
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, ref bool __result)
        {
            if (!IsJewel(__instance)) return true;
            __result = JewelleryCopy(__instance).CanBeCorrupted();
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.GetCorruptableItemCategory))]
    private static class CorruptionCategory
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, ref ItemData.CorruptableItemCategory __result)
        {
            if (!IsJewel(__instance)) return true;
            __result = JewelleryCopy(__instance).GetCorruptableItemCategory();
            return false;
        }
    }

    private static bool AddsAffix(CorruptionOutcome outcome) => outcome is
        CorruptionOutcome.AddsLowTierCorruptedAffix or CorruptionOutcome.AddsCorruptedAffix
        or CorruptionOutcome.AddChampionAffix or CorruptionOutcome.AddExperimentalAffix
        or CorruptionOutcome.AddSetAffix or CorruptionOutcome.AddStandardAffix
        or CorruptionOutcome.AddLowTierStandardAffix or CorruptionOutcome.CorruptSubTypeAddStandard
        or CorruptionOutcome.CorruptSubTypeAddCorrupted or CorruptionOutcome.UpgradeItemAndAddCorruptedAffix;

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.CorruptionOutcomeCanApplyToItem))]
    private static class CorruptionCapacity
    {
        [HarmonyPostfix]
        private static void Postfix(ItemData __instance, WeightedCorruptionOutcome __0, ref bool __result)
        {
            if (IsJewel(__instance) && !__0.IsNullOrDestroyed() && AddsAffix(__0.corruptionOutcome)
                && !__0.replacesAffix && __instance.affixes.ToArray().Count(a => a != null) >= EquipmentTypeRules.JewelTransferAffixes)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.AddRandomSpecialAffix))]
    private static class CorruptionExecutionCapacity
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, WeightedCorruptionOutcome __0, ref int __4, ref bool __5, ref bool __result)
        {
            if (!IsJewel(__instance) || __0.IsNullOrDestroyed() || __0.replacesAffix
                || __instance.affixes.ToArray().Count(a => a != null) < EquipmentTypeRules.JewelTransferAffixes) return true;
            __4 = -1;
            __5 = false;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(OneSlotItemContainer), nameof(OneSlotItemContainer.CanAddItemType))]
    private static class ForgeSlot
    {
        [HarmonyPrefix]
        private static bool Prefix(OneSlotItemContainer __instance, int __0, ref bool __result)
        {
            if (__0 != Id || __instance.id != ContainerID.CRAFTING_MAIN) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.RefreshIDAndValues))]
    private static class PreservePotential
    {
        [HarmonyPrefix]
        private static void Prefix(ItemData __instance)
        {
            if (!IsJewel(__instance) || __instance.rarity >= 7) return;
            // A zero decoded from native regular-item bytes must never erase
            // the sidecar. Explicit clearing goes through SetLegendaryPotential.
            if (__instance.corrupted || __instance.legendaryPotential > 0)
                try { Remember(__instance, __instance.corrupted ? 0 : Math.Clamp((int)__instance.legendaryPotential, 0, 4)); }
                catch (Exception ex) { ErrorLog.Report(ex, "Preserve Jewel legendary potential"); }
        }
        [HarmonyPostfix]
        private static void Postfix(ItemData __instance) => RestorePotential(__instance);
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.setValuesFromSerialisation))]
    private static class RestorePackedPotential
    {
        [HarmonyPostfix]
        private static void Postfix(ItemData __instance) => RestorePotential(__instance);
    }

    [HarmonyPatch(typeof(ItemDataUnpacked), nameof(ItemDataUnpacked.AfterIDChange))]
    private static class RestoreUnpackedPotential
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __instance) => RestorePotential(__instance);
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.getLegendaryPotentialTier))]
    private static class PotentialTier
    {
        [HarmonyPostfix]
        private static void Postfix(ItemData __instance, ref int __result)
        {
            if (IsJewel(__instance) && __instance.rarity < 7) __result = LegendaryPotentialOf(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetPotentialInfo))]
    private static class ModernPotential
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, ref ItemTooltipPotentialInfo __result)
        {
            if (!IsJewel(__0) || __0.rarity >= 7 || __0.corrupted) return;
            int lp = LegendaryPotentialOf(__0);
            if (lp == 0) return;
            // The native layout offers only one potential row. Keep its forging
            // type/color and show an explicitly labelled LP value alongside FP.
            __result = __result != null && __result.Type == ItemPotentialType.Forging
                ? new ItemTooltipPotentialInfo(__result.Type, true, __result.Value + " | LP " + lp, __result.ValueColor, __result.ForgingPotentialType)
                : new ItemTooltipPotentialInfo(ItemPotentialType.Legendary, true, lp.ToString());
        }
    }

    [HarmonyPatch(typeof(UITooltipItem), nameof(UITooltipItem.SetAsItemTooltip))]
    private static class LegacyPotential
    {
        [HarmonyPostfix]
        private static void Postfix(UITooltipItem __instance, ItemDataUnpacked __3)
        {
            if (IsJewel(__3) && __3.rarity < 7 && !__3.corrupted && LegendaryPotentialOf(__3) > 0)
                __instance.ShowLegendaryPotential(LegendaryPotentialOf(__3));
        }
    }
}
