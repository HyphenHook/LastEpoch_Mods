using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;
using LastEpoch_Hud.Scripts.Core.Jewels;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

/// <summary>Custom jewel mechanics only. The slot owner applies/removes all real native stats.</summary>
internal static class JewelStatEffects
{
    private static Actor equippedActor;
    private static bool hasExtraDamage;
    private static readonly float[] manaLeech = new float[4];
    private static readonly float[] extraDamage = new float[7];
    private static readonly List<LeechInstance> pendingLeech = new();
    private static int extraLogged;

    /// <summary>What the Jewels currently give to damage and leech, for the inspector.</summary>
    internal static string Inspect() => "Jewel damage as extra (" + (hasExtraDamage ? "on" : "off") + "): "
        + string.Join("/", extraDamage.Select(x => x.ToString("0.###"))) + "; mana leech global/spell/melee/throwing: "
        + string.Join("/", manaLeech.Select(x => x.ToString("0.####"))) + "; pending leech instances: " + pendingLeech.Count;
    private static readonly AT[] leechTags = { AT.None, AT.Spell, AT.Melee, AT.Throwing };
    private static readonly SP[] attributes = { SP.Strength, SP.Dexterity, SP.Intelligence, SP.Attunement, SP.Vitality };
    private struct LeechInstance { internal float Remaining, BaseSeconds; }

    /// <summary>Call after native slot modifiers change. No per-frame call or external Tick is required.</summary>
    internal static void Refresh(Actor actor, IEnumerable<ItemDataUnpacked> equippedJewels)
    {
        if (actor.IsNullOrDestroyed() || equippedJewels == null) { Clear(); return; }
        var nextLeech = new float[4];
        var nextExtra = new float[7];
        foreach (var item in equippedJewels)
        {
            if (item.IsNullOrDestroyed() || item.itemType != JewelAffixRules.BaseType || item.subType >= 4 || item.affixes == null) continue;
            foreach (var affix in item.affixes)
            {
                if (affix == null || !JewelAffixRules.IsTierAllowed(affix.affixId, affix.affixTier + 1)) continue;
                float value = CustomValue(item, affix);
                if (JewelAffixRules.IsManaLeech(affix.affixId)) nextLeech[affix.affixId - JewelAffixRules.ManaLeechIdStart] += value;
                else if (JewelAffixRules.IsExtraDamage(affix.affixId)) nextExtra[affix.affixId - JewelAffixRules.ExtraDamageIdStart] += value;
            }
        }
        if (equippedActor != actor) pendingLeech.Clear();
        equippedActor = actor;
        Array.Copy(nextLeech, manaLeech, 4);
        Array.Copy(nextExtra, extraDamage, 7);
        hasExtraDamage = nextExtra.Any(x => x > 0);
    }

    internal static void Clear()
    {
        equippedActor = null;
        hasExtraDamage = false;
        Array.Clear(manaLeech, 0, manaLeech.Length);
        Array.Clear(extraDamage, 0, extraDamage.Length);
        pendingLeech.Clear();
    }

    // SP.None is an inert catalog marker, never a surrogate ManaGain/HealthLeech stat.
    // Calculate custom fractional values without native SP.None integer rounding.
    internal static float CustomValue(ItemData item, ItemAffix affix)
    {
        if (item == null || affix == null || !JewelAffixRules.IsCustomEffect(affix.affixId)
            || !JewelAffixRules.IsTierAllowed(affix.affixId, affix.affixTier + 1)) return 0;
        var definition = JewelAffixes.Get(affix.affixId);
        if (definition?.tiers == null || affix.affixTier >= definition.tiers.Count) return 0;
        var range = definition.tiers[affix.affixTier];
        float value = JewelAffixRules.RollStepped(range.minRoll, range.maxRoll, affix.affixRoll, JewelAffixes.StepOf(affix.affixId));
        float effectBonus = ItemList.get().getAffixEffectModifier(item); // the bonus above one, 0 for a Jewel
        value *= 1f + effectBonus;
        var attunement = Items_Attunement.StateOf(item);
        if (attunement != null) value *= (float)attunement.AffixMultiplier(affix.affixId, affix.affixTier, affix.affixRoll);
        return value;
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.AddAffixValueToStatList))]
    private static class CustomStatExtraction
    {
        private static bool Prefix(ItemAffix __0) => __0 == null || !JewelAffixRules.IsCustomEffect(__0.affixId);
        private static void Postfix(ItemData __instance, Il2CppSystem.Collections.Generic.List<Stats.Stat> __2)
        {
            if (__instance.itemType == JewelAffixRules.BaseType) ExpandAllAttributes(__2);
        }
    }

    /// <summary>
    /// A Jewel's implicit stats. The game's own implicit code does not handle base type 44, so they are worked out here
    /// from the subtype's implicit definitions and the item's rolls, then All Attributes is spread over the attributes.
    /// </summary>
    internal static void AddImplicitStats(ItemDataUnpacked item, Il2CppSystem.Collections.Generic.List<Stats.Stat> stats)
    {
        if (item.IsNullOrDestroyed() || stats == null || item.itemType != JewelAffixRules.BaseType) return;
        var definitions = item.Implicits;
        var rolls = item.implicitRolls;
        if (definitions == null) return;
        var attunement = Items_Attunement.StateOf(item);
        double scale = attunement == null || rolls == null ? 1 : attunement.ImplicitMultiplier(item.itemType, item.subType, rolls);
        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null) continue;
            byte roll = rolls != null && i < rolls.Length ? rolls[i] : (byte)0;
            float value = (float)(JewelAffixRules.Roll(definition.implicitValue, definition.implicitMaxValue, roll) * scale);
            bool increased = definition.type == BaseStats.ModType.INCREASED;
            stats.Add(new Stats.Stat(definition.property, definition.tags, increased ? 0f : value, increased ? value : 0f, 0f,
                definition.specialTag, definition.extraTag));
        }
        ExpandAllAttributes(stats);
    }

    // The game does not spread an All Attributes stat over the five attributes for these items, so do it here: each
    // attribute gets the whole added amount and the whole increase, and the original stat is zeroed so nothing counts twice.
    internal static void ExpandAllAttributes(Il2CppSystem.Collections.Generic.List<Stats.Stat> stats)
    {
        if (stats == null) return;
        int count = stats.Count;
        for (int i = 0; i < count; i++)
        {
            var stat = stats[i];
            if (stat == null || stat.property != SP.AllAttributes) continue;
            float added = stat.addedValue, increased = stat.increasedValue;
            if (added == 0 && increased == 0) continue;
            stat.addedValue = 0;
            stat.increasedValue = 0;
            foreach (var attribute in attributes)
                stats.Add(new Stats.Stat(attribute, stat.tags, added, increased, 0f, stat.specialTag, stat.extraTag));
        }
    }

    [HarmonyPatch(typeof(AffixList), nameof(AffixList.ChangeAffixModifier))]
    private static class NoCustomNativeModifier
    {
        private static bool Prefix(ItemAffix __2) => __2 == null || !JewelAffixRules.IsCustomEffect(__2.affixId);
    }

    private sealed class DamageContext
    {
        internal BaseHealth Health;
        internal float WardBefore, HealthDamage, Fraction;
    }
    [ThreadStatic] private static DamageContext currentDamage;

    [HarmonyPatch(typeof(ProtectionClass), nameof(ProtectionClass.ApplyDamage))]
    private static class CustomDamage
    {
        private static void Prefix(ProtectionClass __instance, ref DamageStats __0, Actor __3, out DamageContext __state)
        {
            // __state is the previous context, not a new allocation. Clearing the current context
            // also prevents nested non-player damage from contributing to its parent's leech.
            __state = currentDamage;
            currentDamage = null;
            if (equippedActor.IsNullOrDestroyed() || __3 != equippedActor || __0 == null || __instance.actor == equippedActor) return;
            // Work on a per-application copy. Never mutate cached skill damage, and calculate every
            // extra type from the SAME original total, so different extra types cannot feed each other.
            if (hasExtraDamage && __0.damage != null && __0.damage.Length >= 7)
            {
                var copy = __0.Copy();
                var values = __0.damage.ToArray();
                float before = values.Sum();
                JewelAffixRules.ApplyExtraDamage(values, extraDamage);
                if (extraLogged < 5)
                {
                    extraLogged++;
                    Main.logger_instance?.Msg("Jewel damage as extra: a hit of " + before + " became " + values.Sum()
                        + " (extra fractions " + string.Join("/", extraDamage.Select(x => x.ToString("0.###"))) + ")");
                }
                copy.damage = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(values);
                __0 = copy;
            }
            AT tags = __0.getTags();
            float fraction = manaLeech[0];
            for (int i = 1; i < 4; i++) if ((tags & leechTags[i]) != 0) fraction += manaLeech[i];
            if (fraction <= 0 || __instance.actor.IsNullOrDestroyed()) return;
            currentDamage = new DamageContext { Health = __instance.actor.health,
                WardBefore = __instance.currentWard, Fraction = fraction };
        }

        private static void Postfix(ProtectionClass __instance)
        {
            var context = currentDamage;
            if (context == null || context.Fraction <= 0 || equippedActor.IsNullOrDestroyed()) return;
            float damage = context.HealthDamage + Math.Max(0, context.WardBefore - __instance.currentWard);
            if (damage > 0 && float.IsFinite(damage))
                pendingLeech.Add(new LeechInstance { Remaining = damage * context.Fraction, BaseSeconds = 3f });
        }

        private static void Finalizer(DamageContext __state) => currentDamage = __state;
    }

    [HarmonyPatch(typeof(BaseHealth), nameof(BaseHealth.HealthDamage))]
    private static class DamageActuallyDealt
    {
        private static void Prefix(BaseHealth __instance, float __0)
        {
            if (currentDamage?.Health == __instance && __0 > 0)
                currentDamage.HealthDamage += Math.Min(__0, Math.Max(0, __instance.currentHealth));
        }
    }

    [HarmonyPatch(typeof(BaseMana), nameof(BaseMana.OnUpdateTick))]
    private static class RecoverManaLeech
    {
        private static void Postfix(BaseMana __instance, float __0)
        {
            if (pendingLeech.Count == 0 || equippedActor.IsNullOrDestroyed() || equippedActor.mana != __instance || __0 <= 0) return;
            if (equippedActor.health.IsNullOrDestroyed() || equippedActor.health.currentHealth <= 0) { pendingLeech.Clear(); return; }
            float increased = equippedActor.stats.IsNullOrDestroyed() ? 0 : equippedActor.stats.getIncreasedLeechRate();
            float recovered = 0;
            for (int i = pendingLeech.Count - 1; i >= 0; i--)
            {
                var entry = pendingLeech[i];
                recovered += JewelAffixRules.RecoverLeech(ref entry.Remaining, ref entry.BaseSeconds, __0, increased);
                if (entry.BaseSeconds <= 0 || entry.Remaining <= 0) pendingLeech.RemoveAt(i);
                else pendingLeech[i] = entry;
            }
            if (recovered > 0) __instance.gainMana(recovered);
        }
    }

    private static string Describe(int id, float value)
    {
        string number = FormatCustomPercentage(id, value);
        if (JewelAffixRules.IsExtraDamage(id))
            return "Gain " + number + " of Damage as Extra " + ((DamageType)(id - JewelAffixRules.ExtraDamageIdStart)).ToString().ToLowerInvariant() + " Damage";
        int tag = id - JewelAffixRules.ManaLeechIdStart;
        return number + " of " + (tag == 0 ? "Damage" : leechTags[tag] + " Damage") + " Leeched as Mana";
    }

    private static string FormatCustomPercentage(int id, float value) =>
        (value * 100f).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    // The legacy hover builder can format rows without using the modifier-info path.
    // Match the stat wording, never a rounded numeric value shared with another affix.
    [HarmonyPatch]
    private static class HoverCustomValues
    {
        private static readonly HashSet<string> loggedRows = new();
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipDefaultContent));
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipFullContent));
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(ItemDataUnpacked __0, TooltipMode __1, UITooltipItem.ItemTooltipInfo __result)
        {
            if (!JewelBaseType.IsJewel(__0) || __0.affixes == null || __result == null) return;
            foreach (var affix in __0.affixes)
            {
                if (affix == null || !JewelAffixRules.IsCustomEffect(affix.affixId)) continue;
                var definition = JewelAffixes.Get(affix.affixId);
                if (definition?.tiers == null || affix.affixTier >= definition.tiers.Count) continue;
                var rows = definition.type == AffixList.AffixType.SUFFIX ? __result.suffixesText : __result.prefixesText;
                if (rows == null) continue;
                string description = Describe(affix.affixId, CustomValue(__0, affix));
                string wording = description.Substring(description.IndexOf('%') + 1).Trim();
                var range = definition.tiers[affix.affixTier];
                bool matched = false;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (!JewelTooltipText.Matches(row.Item1, wording)) continue;
                    row.Item1 = JewelTooltipText.ReplaceValues(row.Item1,
                        FormatCustomPercentage(affix.affixId, CustomValue(__0, affix)),
                        FormatCustomPercentage(affix.affixId, range.minRoll),
                        FormatCustomPercentage(affix.affixId, range.maxRoll),
                        __1 == TooltipMode.DefaultModRanges || __1 == TooltipMode.CompareModRanges);
                    rows[i] = row;
                    matched = true;
                    if (loggedRows.Add(affix.affixId + ":" + __1))
                        Main.logger_instance?.Msg("Jewel custom hover " + affix.affixId + " " + __1 + ": " + row.Item1);
                    break;
                }
                if (!matched && loggedRows.Add("unmatched:" + affix.affixId + ":" + __1))
                    Main.logger_instance?.Warning("Jewel custom hover unmatched " + affix.affixId + " " + __1 + ": "
                        + string.Join(" | ", rows.ToArray().Select(row => row.Item1)));
            }
        }
    }

    // Range and comparison values also pass through the native number formatter.
    [HarmonyPatch(typeof(ItemTooltipPropertyModifierInfo), nameof(ItemTooltipPropertyModifierInfo.ConvertValueToString))]
    private static class CustomPercentageValues
    {
        private static bool Prefix(ItemTooltipPropertyModifierInfo __instance, float __0, ref string __result)
        {
            if (__instance._property != SP.None || !JewelAffixRules.IsCustomEffect(__instance._extraTag)) return true;
            __result = FormatCustomPercentage(__instance._extraTag, __0);
            return false;
        }
    }

    [HarmonyPatch]
    private static class LegacyCustomTooltip
    {
        private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(TooltipItemManager))
            .Single(m => m.Name == nameof(TooltipItemManager.FormatAffix) && m.GetParameters()[1].ParameterType == typeof(ItemAffix));
        private static bool Prefix(ItemDataUnpacked __0, ItemAffix __1, bool __7, bool __10, ref string __result)
        {
            if (__1 == null || !JewelAffixRules.IsCustomEffect(__1.affixId)) return true;
            __result = Describe(__1.affixId, CustomValue(__0, __1));
            if (__7) __result = __result.Replace(FormatCustomPercentage(__1.affixId, CustomValue(__0, __1)), "X%");
            if (__10) __result += "\n";
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetItemAffixInfo))]
    private static class ModernCustomTooltip
    {
        private static bool Prefix(ItemDataUnpacked __0, ItemAffix __1, TooltipItemManager.SlotType __2, ref ItemTooltipAffixInfo __result)
        {
            if (__1 == null || !JewelAffixRules.IsCustomEffect(__1.affixId)) return true;
            var definition = JewelAffixes.Get(__1.affixId);
            if (definition == null) return true;
            var range = definition.tiers[Math.Min(__1.affixTier, (byte)6)];
            float value = CustomValue(__0, __1);
            float rolled = JewelAffixRules.RollStepped(range.minRoll, range.maxRoll, __1.affixRoll, JewelAffixes.StepOf(__1.affixId));
            float factor = rolled == 0 ? 1 : value / rolled;
            var info = new ItemTooltipPropertyModifierInfo(Describe(__1.affixId, value),
                new BasePropertyInfo { hideModifierValue = true, displayAddedAsPercentage = true,
                    roundingForAdded = PropertyRounding.Hundredth },
                SP.None, AT.None, 0, __1.affixId, BaseStats.ModType.ADDED, value);
            info.MinValue = range.minRoll * factor;
            info.MaxValue = range.maxRoll * factor;
            info.SlotType = __2;
            __result = new ItemTooltipAffixInfo(definition) { Modifier1 = info, Tier = __1.DisplayTier, IsExalted = __1.IsExalted };
            return false;
        }
    }

    // Custom text bypasses SP.None's native formatter while retaining the native affix row, tier,
    // range/comparison container, and the existing encapsulation/reminder tooltip decorations.
    [HarmonyPatch(typeof(ItemTooltipPropertyModifierInfo), nameof(ItemTooltipPropertyModifierInfo.GetModifierString))]
    private static class CustomModifierText
    {
        private static bool Prefix(ItemTooltipPropertyModifierInfo __instance, ref string __result)
        {
            if (__instance._property != SP.None || !JewelAffixRules.IsCustomEffect(__instance._extraTag)) return true;
            __result = __instance._descriptor;
            return false;
        }
    }
}
