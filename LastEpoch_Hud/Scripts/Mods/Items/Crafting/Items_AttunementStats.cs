using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;
using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>Applies per-item scaling at the native equipment and tooltip calculation entry points.</summary>
internal static class Items_AttunementStats
{
    private sealed class Calculation
    {
        public ItemData Item;
        public AttunementState State;
        public bool ModernTooltip;
        public HashSet<IntPtr> UniqueMods;
        public int AffixChangeDepth;
        public int AffixValueDepth;
        public int ImplicitDepth;
    }

    [ThreadStatic] private static Calculation current;

    private static Calculation Enter(ItemData item, bool modern = false)
    {
        var previous = current;
        current = new Calculation { Item = item, State = Items_Attunement.StateOf(item), ModernTooltip = modern };
        return previous;
    }

    private static bool Matches(byte[] saved, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> rolls)
    {
        if (saved == null || rolls == null || saved.Length != rolls.Count) return false;
        for (int i = 0; i < saved.Length; i++)
        {
            if (saved[i] != rolls[i]) return false;
        }
        return true;
    }

    internal static double ImplicitFactor(ItemData item)
    {
        var state = Items_Attunement.StateOf(item);
        return state != null && state.ItemType == item.itemType && state.SubType == item.subType
            && Matches(state.Implicits, item.implicitRolls) ? state.Multiplier : 1;
    }

    private static double UniqueFactor(UniqueItemMod modifier = null)
    {
        var state = current?.State;
        if (state == null || state.UniqueId != current.Item.uniqueID || !Matches(state.Unique, current.Item.uniqueRolls)) return 1;
        if (modifier != null)
        {
            if (current.UniqueMods == null)
            {
                current.UniqueMods = new HashSet<IntPtr>();
                var definition = UniqueList.getUnique(current.Item.uniqueID);
                if (definition?.mods != null)
                {
                    foreach (var mod in definition.mods) current.UniqueMods.Add(mod.Pointer);
                }
            }
            if (!current.UniqueMods.Contains(modifier.Pointer)) return 1;
        }
        return state.Multiplier;
    }

    private static double AffixFactor(int affixId)
    {
        if (current?.State == null || current.Item.affixes == null) return 1;
        foreach (ItemAffix affix in current.Item.affixes)
        {
            if (affix != null && affix.affixId == affixId)
                return current.State.AffixMultiplier(affixId, affix.affixTier, affix.affixRoll);
        }
        return 1;
    }

    private static float Scale(float value, double factor, SP property, AT tags, byte specialTag, BaseStats.ModType type) =>
        EpochExtensions.GetFixedValueAfterRounding(property, tags, specialTag, type, (float)(value * factor));

    private static ItemList.EquipmentImplicit ScaledImplicit(ItemList.EquipmentImplicit source, double factor) => new()
    {
        property = source.property,
        tags = source.tags,
        specialTag = source.specialTag,
        extraTag = source.extraTag,
        type = source.type,
        implicitValue = Scale(source.implicitValue, factor, source.property, source.tags, source.specialTag, source.type),
        implicitMaxValue = Scale(source.implicitMaxValue, factor, source.property, source.tags, source.specialTag, source.type),
    };

    private static void ScaleRange(ItemTooltipModifierInfo info, double factor)
    {
        if (info == null || Math.Abs(factor - 1) < 0.000001) return;
        var property = info.TryCast<ItemTooltipPropertyModifierInfo>();
        if (property == null) return;
        property.MinValue = Scale(property.MinValue, factor, property._property, property._tags, property._specialTag, property._modType);
        property.MaxValue = Scale(property.MaxValue, factor, property._property, property._tags, property._specialTag, property._modType);
    }

    [HarmonyPatch]
    private static class CalculationContext
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ItemEquipManager), nameof(ItemEquipManager.UpdateStats));
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipDefaultContent));
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipFullContent));
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(TooltipItemManager)).Where(m => m.Name == nameof(TooltipItemManager.FormatAffix)))
                yield return method;
            yield return AccessTools.Method(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetItemTooltipInfo));
        }

        [HarmonyPrefix]
        private static void Prefix(ItemDataUnpacked __0, MethodBase __originalMethod, out Calculation __state) =>
            __state = Enter(__0, __originalMethod.DeclaringType == typeof(ItemTooltipContentBuilder));

        [HarmonyFinalizer]
        private static void Finalizer(Calculation __state) => current = __state;
    }

    [HarmonyPatch]
    private static class ItemCalculationContext
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ItemData), nameof(ItemData.AddAffixValueToStatList));
            yield return AccessTools.Method(typeof(ItemData), nameof(ItemData.AddImplicitValuesToStatList));
            yield return AccessTools.Method(typeof(ItemData), nameof(ItemData.GetDisplayValuesOfAffixAtTier));
        }

        [HarmonyPrefix]
        private static void Prefix(ItemData __instance, out Calculation __state) => __state = Enter(__instance);

        [HarmonyFinalizer]
        private static void Finalizer(Calculation __state) => current = __state;
    }

    [HarmonyPatch(typeof(AffixList), nameof(AffixList.ChangeAffixModifier))]
    private static class EquipmentAffixScaling
    {
        [HarmonyPrefix]
        private static void Prefix(ref float __7, ItemAffix __2, out int __state)
        {
            __state = current?.AffixChangeDepth ?? 0;
            // Both effect modifiers are zero-based bonuses. Adjust the post-rounding
            // bonus so the native (1 + bonus) multiplies the final rolled value once.
            if (__state == 0 && __2 != null)
                __7 = AttunementState.ScaleEffectBonus(__7, AffixFactor(__2.affixId));
            if (current != null) current.AffixChangeDepth++;
        }

        [HarmonyFinalizer]
        private static void Finalizer(int __state)
        {
            if (current != null) current.AffixChangeDepth = __state;
        }
    }

    private static int EnterAffixValue()
    {
        int depth = current?.AffixValueDepth ?? 0;
        if (current != null) current.AffixValueDepth++;
        return depth;
    }

    private static void ExitAffixValue(int depth)
    {
        if (current != null) current.AffixValueDepth = depth;
    }

    [HarmonyPatch]
    private static class SingleAffixScaling
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(AffixList)).Where(m => m.Name == nameof(AffixList.GetSingleAffixValue));

        [HarmonyPrefix]
        private static void Prefix(out int __state) => __state = EnterAffixValue();

        [HarmonyPostfix]
        private static void Postfix(AffixList.SingleAffix __0, int __state, ref float __result)
        {
            if (__state != 0 || (current?.AffixChangeDepth ?? 0) != 0) return;
            double factor = AffixFactor(__0.affixId);
            if (Math.Abs(factor - 1) > 0.000001)
                __result = Scale(__result, factor, __0.property, __0.tags, __0.specialTag, __0.modifierType);
        }

        [HarmonyFinalizer]
        private static void Finalizer(int __state) => ExitAffixValue(__state);
    }

    [HarmonyPatch]
    private static class MultiAffixScaling
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(AffixList)).Where(m => m.Name == nameof(AffixList.GetMultiAffixPropertyValue));

        [HarmonyPrefix]
        private static void Prefix(out int __state) => __state = EnterAffixValue();

        [HarmonyPostfix]
        private static void Postfix(AffixList.MultiAffix __0, object[] __args, int __state, ref float __result)
        {
            if (__state != 0 || (current?.AffixChangeDepth ?? 0) != 0) return;
            double factor = AffixFactor(__0.affixId);
            if (Math.Abs(factor - 1) < 0.000001) return;
            int index = (int)__args[__args.Length - 2];
            if (index < 0 || index >= __0.affixProperties.Count) return;
            var property = __0.affixProperties[index];
            __result = Scale(__result, factor, property.property, property.tags, property.specialTag, property.modifierType);
        }

        [HarmonyFinalizer]
        private static void Finalizer(int __state) => ExitAffixValue(__state);
    }

    [HarmonyPatch(typeof(UniqueItemMod), nameof(UniqueItemMod.getValue))]
    private static class UniqueScaling
    {
        [HarmonyPostfix]
        private static void Postfix(UniqueItemMod __instance, ref float __result)
        {
            double factor = UniqueFactor(__instance);
            if (Math.Abs(factor - 1) < 0.000001) return;
            __result = Scale(__result, factor, __instance.property, __instance.tags, __instance.specialTag, __instance.type);
        }
    }

    [HarmonyPatch(typeof(ItemList), nameof(ItemList.GetItemImplicits))]
    private static class ImplicitScaling
    {
        [HarmonyPrefix]
        private static void Prefix(out int __state)
        {
            __state = current?.ImplicitDepth ?? 0;
            if (current != null) current.ImplicitDepth++;
        }

        [HarmonyFinalizer]
        private static void Finalizer(int __state)
        {
            if (current != null) current.ImplicitDepth = __state;
        }

        [HarmonyPostfix]
        private static void Postfix(ItemData __0, ref Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit> __result)
        {
            double factor = ImplicitFactor(__0);
            if (__result == null || Math.Abs(factor - 1) < 0.000001) return;
            var scaled = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit>();
            foreach (var implicitStat in __result) scaled.Add(ScaledImplicit(implicitStat, factor));
            __result = scaled;
        }
    }

    [HarmonyPatch(typeof(ItemList), nameof(ItemList.GetItemImplicit))]
    private static class LegacyImplicitRange
    {
        [HarmonyPostfix]
        private static void Postfix(ref ItemList.EquipmentImplicit __result)
        {
            if (current?.State == null || current.ModernTooltip || current.ImplicitDepth > 0 || __result == null) return;
            double factor = ImplicitFactor(current.Item);
            if (Math.Abs(factor - 1) > 0.000001) __result = ScaledImplicit(__result, factor);
        }
    }

    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetItemImplicitInfo))]
    private static class ModernImplicitRange
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, ItemTooltipModifierInfo __result) =>
            ScaleRange(__result, ImplicitFactor(__0));
    }

    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetItemAffixInfo))]
    private static class ModernAffixRange
    {
        [HarmonyPostfix]
        private static void Postfix(ItemAffix __1, ItemTooltipAffixInfo __result)
        {
            if (__1 == null || __result == null) return;
            double factor = AffixFactor(__1.affixId);
            ScaleRange(__result.Modifier1, factor);
            ScaleRange(__result.Modifier2, factor);
        }
    }

    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetUniqueModifierInfo))]
    private static class ModernUniqueRange
    {
        [HarmonyPostfix]
        private static void Postfix(ItemTooltipModifierInfo __result) => ScaleRange(__result, UniqueFactor());
    }
}
