using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.Jewels;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

/// <summary>Registers independent, serializable jewel affixes without modifying native idol definitions.</summary>
internal static class JewelAffixes
{
    private static AffixList registeredCatalog;
    private static readonly Dictionary<int, AffixList.Affix> definitions = new();
    internal static bool Registered => registeredCatalog != null && definitions.Count != 0;
    internal static IReadOnlyCollection<AffixList.Affix> Definitions => definitions.Values;
    internal static bool IsJewelAffix(int id) => definitions.ContainsKey(id);
    internal static int MinimumTier(int id) => JewelAffixRules.MinimumTier(id);
    internal static AffixList.Affix Get(int id) => definitions.TryGetValue(id, out var value) ? value : null;
    internal static AffixList.Affix FindById(int id) => Get(id);
    internal static IEnumerable<AffixList.Affix> RollableDefinitions(int maxTier = 7) =>
        definitions.Values.Where(x => MinimumTier(x.affixId) <= maxTier && x.weighting > 0);

    /// <summary>Call after the base catalog and subtypes 0..3 exist. Safe to retry before they exist.</summary>
    internal static void EnsureRegistered()
    {
        if (Registered) return;
        var items = ItemList.get();
        var catalog = AffixList.get();
        if (items == null || catalog == null || catalog.singleAffixes == null || catalog.multiAffixes == null) return;
        var jewelBase = items.EquippableItems?.FirstOrDefault(x => x != null && x.baseTypeID == JewelAffixRules.BaseType);
        if (jewelBase?.subItems == null || Enumerable.Range(0, 4).Any(id => !jewelBase.subItems.ToArray().Any(s => s != null && s.subTypeID == id))) return;
        if (registeredCatalog == catalog && Registered) return;

        // Stage everything before publishing, so a collision cannot leave half a catalog registered.
        var singles = catalog.singleAffixes.ToArray().ToList();
        var multis = catalog.multiAffixes.ToArray().ToList();
        var staged = new Dictionary<int, AffixList.Affix>();
        foreach (var source in singles.Cast<AffixList.Affix>().Concat(multis).ToArray())
        {
            if (source == null || ((int)source.classSpecificity & ~1) != 0) continue;
            // RollsOn has only Equipment/Idols in this game build. Also inspect the actual
            // applicability list so mixed equipment/idol definitions are not lost.
            if (source.rollsOn != AffixList.RollsOn.Idols
                && (source.canRollOn == null || !source.canRollOn.ToArray().Any(type => ItemList.isIdol(type)))) continue;
            if (source.tiers == null || source.tiers.Count == 0)
                throw new InvalidOperationException("Idol affix has no ranges: " + source.affixId);
            var single = source.TryCast<AffixList.SingleAffix>();
            AffixList.Affix copy;
            if (single != null)
            {
                var target = new AffixList.SingleAffix();
                CopyProperties(single, target, typeof(AffixList.SingleAffix));
                singles.Add(target);
                copy = target;
            }
            else
            {
                var target = new AffixList.MultiAffix();
                CopyProperties(source.TryCast<AffixList.MultiAffix>(), target, typeof(AffixList.MultiAffix));
                var properties = new Il2CppSystem.Collections.Generic.List<AffixList.AffixProperty>();
                foreach (var property in target.affixProperties)
                {
                    var cloned = new AffixList.AffixProperty();
                    CopyProperties(property, cloned, typeof(AffixList.AffixProperty));
                    properties.Add(cloned);
                }
                target.affixProperties = properties;
                multis.Add(target);
                copy = target;
            }
            Configure(copy, JewelAffixRules.InheritedId(source.affixId));
            copy.tiers = ScaledTiers(source.tiers[0]);
            staged.Add(copy.affixId, copy);
        }
        foreach (var affix in ExclusiveAffixes())
        {
            staged.Add(affix.affixId, affix);
            singles.Add(affix);
        }
        var oldIds = catalog.singleAffixes.Where(x => x != null).Select(x => x.affixId)
            .Concat(catalog.multiAffixes.Where(x => x != null).Select(x => x.affixId)).ToHashSet();
        foreach (int id in staged.Keys)
        {
            if (id > JewelAffixRules.MaximumPackedAffixId)
                throw new InvalidOperationException("Jewel affix ID cannot survive native serialization: " + id);
            if (oldIds.Contains(id)) throw new InvalidOperationException("Jewel affix ID collision: " + id);
        }
        var previousSingles = catalog.singleAffixes;
        var previousMultis = catalog.multiAffixes;
        try
        {
            catalog.singleAffixes = new Il2CppReferenceArray<AffixList.SingleAffix>(singles.OrderBy(x => x?.affixId ?? int.MaxValue).ToArray());
            catalog.multiAffixes = new Il2CppReferenceArray<AffixList.MultiAffix>(multis.OrderBy(x => x?.affixId ?? int.MaxValue).ToArray());
            catalog.RefreshAllAffixesList();
            foreach (var pair in staged)
                if (catalog.GetAffix(pair.Key)?.Pointer != pair.Value.Pointer)
                    throw new InvalidOperationException("Native jewel affix lookup failed: " + pair.Key);
        }
        catch
        {
            catalog.singleAffixes = previousSingles;
            catalog.multiAffixes = previousMultis;
            catalog.RefreshAllAffixesList();
            throw;
        }
        definitions.Clear();
        foreach (var pair in staged) definitions.Add(pair.Key, pair.Value);
        registeredCatalog = catalog;
        Main.logger_instance?.Msg($"Jewels: registered {staged.Count} exclusive affixes (including {staged.Count - 35} idol copies).");
    }

    // Copy all writable native metadata, including name morphology and tooltip metadata. Replace mutable
    // ranges, reroll lists, equipment applicability, and multi-properties with jewel-owned objects below.
    private static void CopyProperties(object source, object target, Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                property.SetValue(target, property.GetValue(source));
    }

    private static void Configure(AffixList.Affix affix, int id)
    {
        affix.affixId = id;
        affix.toCopyFrom = 0;
        affix.rollsOn = AffixList.RollsOn.Equipment;
        affix.classSpecificity = AffixList.ClassSpecificity.NonSpecific;
        affix.specialAffixType = AffixList.SpecialAffixType.Standard;
        affix.uniqueId = 0;
        affix.canRollOn = new Il2CppSystem.Collections.Generic.List<EquipmentType>();
        affix.canRollOn.Add((EquipmentType)JewelAffixRules.BaseType);
        affix.specificRerollChances = new Il2CppSystem.Collections.Generic.List<AffixList.SpecificReroll>();
        affix.convertOnIncompatibleItemType = false;
        affix.affixIDToConvertTo = 0;
        affix.t6Compatibility = AffixList.T6Compatibility.Normal;
    }

    private static Il2CppSystem.Collections.Generic.List<AffixList.Tier> ScaledTiers(AffixList.Tier baseline)
    {
        var tiers = new Il2CppSystem.Collections.Generic.List<AffixList.Tier>();
        for (int tier = 1; tier <= 7; tier++)
        {
            float multiplier = JewelAffixRules.IdolMultiplier(tier);
            var range = new AffixList.Tier { minRoll = baseline.minRoll * multiplier, maxRoll = baseline.maxRoll * multiplier,
                extraRolls = new Il2CppSystem.Collections.Generic.List<AffixList.RollPair>() };
            if (baseline.extraRolls != null)
                foreach (var extra in baseline.extraRolls)
                    range.extraRolls.Add(new AffixList.RollPair(extra.minRoll * multiplier, extra.maxRoll * multiplier));
            tiers.Add(range);
        }
        return tiers;
    }

    private static AffixList.SingleAffix Exclusive(int index, string name, SP property, BaseStats.ModType type,
        float min, float max, int weight, AT tags = AT.None, bool suffix = false)
    {
        var affix = new AffixList.SingleAffix { affixName = name, affixDisplayName = name, affixTitle = name,
            property = property, modifierType = type, tags = tags, weighting = weight,
            type = suffix ? AffixList.AffixType.SUFFIX : AffixList.AffixType.PREFIX,
            useGeneratedNameForDisplayName = property != SP.None, standardAffixEffectModifier = 0 };
        Configure(affix, JewelAffixRules.ExclusiveIdStart + index);
        affix.tiers = ExclusiveTiers(min, max, min, max, min, max);
        return affix;
    }

    // Exclusive T1..T5 use linear per-tier values: T1=low, T5=high (min=max).
    // T6/T7 retain the exact requested roll ranges. Idol multipliers never apply here.
    private static Il2CppSystem.Collections.Generic.List<AffixList.Tier> ExclusiveTiers(
        float low, float high, float t6min, float t6max, float t7min, float t7max)
    {
        var result = new Il2CppSystem.Collections.Generic.List<AffixList.Tier>();
        for (int tier = 1; tier <= 7; tier++)
        {
            var range = JewelAffixRules.ExclusiveRange(tier, low, high, t6min, t6max, t7min, t7max);
            result.Add(new AffixList.Tier { minRoll = range.Min, maxRoll = range.Max,
                extraRolls = new Il2CppSystem.Collections.Generic.List<AffixList.RollPair>() });
        }
        return result;
    }

    private static AffixList.SingleAffix WithRanges(AffixList.SingleAffix affix,
        float low, float high, float t6min, float t6max, float t7min, float t7max)
    {
        affix.tiers = ExclusiveTiers(low, high, t6min, t6max, t7min, t7max);
        return affix;
    }

    private static IEnumerable<AffixList.SingleAffix> ExclusiveAffixes()
    {
        var attributes = new[] { SP.AllAttributes, SP.Strength, SP.Dexterity, SP.Intelligence, SP.Attunement, SP.Vitality };
        for (int i = 0; i < attributes.Length; i++)
        {
            yield return WithRanges(Exclusive(i * 2, "Jewel " + attributes[i], attributes[i], BaseStats.ModType.ADDED,
                0, 0, i == 0 ? 10 : 100), i == 0 ? 1 : 2, i == 0 ? 5 : 8,
                i == 0 ? 6 : 8, i == 0 ? 8 : 12, i == 0 ? 10 : 12, i == 0 ? 12 : 16);
            yield return WithRanges(Exclusive(i * 2 + 1, "Jewel Increased " + attributes[i], attributes[i], BaseStats.ModType.INCREASED,
                0, 0, i == 0 ? 10 : 100), i == 0 ? .02f : .03f, i == 0 ? .07f : .08f,
                i == 0 ? .08f : .10f, i == 0 ? .10f : .12f, i == 0 ? .10f : .12f, i == 0 ? .12f : .15f);
        }
        var manaShield = Exclusive(12, "Jewel Mana Before Health", SP.ManaBeforeHealthPercent, BaseStats.ModType.ADDED, 0, 0, 100, suffix: true);
        for (int i = 0; i < 7; i++) manaShield.tiers[i].minRoll = manaShield.tiers[i].maxRoll = i < 5 ? .10f : i == 5 ? .15f : .20f;
        yield return manaShield;
        yield return WithRanges(Exclusive(13, "Jewel Melee Attack Speed", SP.AttackSpeed, BaseStats.ModType.INCREASED, 0, 0, 1000, AT.Melee), .04f, .15f, .16f, .20f, .21f, .24f);
        yield return WithRanges(Exclusive(14, "Jewel Cast Speed", SP.CastSpeed, BaseStats.ModType.INCREASED, 0, 0, 1000), .04f, .15f, .16f, .20f, .21f, .24f);
        yield return WithRanges(Exclusive(15, "Jewel More Movement Speed", SP.Movespeed, BaseStats.ModType.MORE, 0, 0, 100, suffix: true), .01f, .03f, .04f, .06f, .07f, .10f);
        yield return WithRanges(Exclusive(16, "Jewel Throwing Attack Speed", SP.AttackSpeed, BaseStats.ModType.INCREASED, 0, 0, 1000, AT.Throwing), .05f, .20f, .22f, .26f, .28f, .32f);
        var tags = new[] { AT.None, AT.Spell, AT.Melee, AT.Throwing };
        var leech = new[,] { { .004f, .018f, .02f, .024f, .03f, .036f }, { .005f, .02f, .023f, .031f, .032f, .043f },
            { .012f, .024f, .026f, .036f, .042f, .05f }, { .006f, .022f, .024f, .034f, .034f, .045f } };
        for (int i = 0; i < tags.Length; i++)
            yield return WithRanges(Exclusive(20 + i, "Jewel " + (i == 0 ? "Global" : tags[i].ToString()) + " Mana Leech",
                SP.None, BaseStats.ModType.ADDED, 0, 0, 100, tags[i], true), leech[i, 0], leech[i, 1], leech[i, 2], leech[i, 3], leech[i, 4], leech[i, 5]);
        var taken = new[] { SP.DamageTakenAsPhysical, SP.DamageTakenAsFire, SP.DamageTakenAsCold,
            SP.DamageTakenAsLightning, SP.DamageTakenAsNecrotic, SP.DamageTakenAsVoid, SP.DamageTakenAsPoison };
        for (int i = 0; i < 7; i++)
        {
            var extra = WithRanges(Exclusive(30 + i, "Jewel Damage As Extra " + ((DamageType)i), SP.None, BaseStats.ModType.ADDED, 0, 0, 10), 0, 0, .05f, .07f, .10f, .12f);
            var conversion = WithRanges(Exclusive(40 + i, "Jewel " + taken[i], taken[i], BaseStats.ModType.ADDED, 0, 0, 10, suffix: true), 0, 0, .03f, .06f, .08f, .10f);
            // Native tier indices remain 0..6. Unavailable tiers have no effect; generation must filter
            // using MinimumTier. Never promote a crafted T1 affix into a free exalted affix.
            for (int tier = 0; tier < 5; tier++)
            {
                extra.tiers[tier].minRoll = extra.tiers[tier].maxRoll = 0;
                conversion.tiers[tier].minRoll = conversion.tiers[tier].maxRoll = 0;
            }
            yield return extra;
            yield return conversion;
        }
    }

    /// <summary>Weighted demo rolls, unrestricted sides, no duplicates, and legal display tiers.</summary>
    internal static Il2CppSystem.Collections.Generic.List<ItemAffix> CreateAffixes(Random random, int count = 4, int maxTier = 7)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (count < 0 || count > 6 || maxTier < 1 || maxTier > 7) throw new ArgumentOutOfRangeException();
        EnsureRegistered();
        if (!Registered) throw new InvalidOperationException("The jewel base and affix catalogs are not ready.");
        var result = new Il2CppSystem.Collections.Generic.List<ItemAffix>();
        var candidates = definitions.Values.Where(x => MinimumTier(x.affixId) <= maxTier && x.weighting > 0).ToList();
        while (result.Count < count && candidates.Count != 0)
        {
            double roll = random.NextDouble() * candidates.Sum(x => (double)x.weighting);
            var pick = candidates[candidates.Count - 1];
            foreach (var candidate in candidates) { roll -= candidate.weighting; if (roll < 0) { pick = candidate; break; } }
            result.Add(new ItemAffix((ushort)pick.affixId, (byte)(random.Next(MinimumTier(pick.affixId), maxTier + 1) - 1),
                (byte)random.Next(256), JewelAffixRules.BaseType, SealedAffixType.None));
            candidates.RemoveAll(x => x.affixId == pick.affixId);
        }
        return result;
    }

    // Prevent ordinary native equipment/idol affixes leaking onto jewels, and jewel copies leaking off.
    [HarmonyPatch]
    private static class Applicability
    {
        private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(AffixList.Affix))
            .Where(m => m.Name == nameof(AffixList.Affix.CanRollOnItemType));
        private static bool Prefix(AffixList.Affix __instance, EquipmentType __0, ref bool __result)
        {
            bool jewel = (int)__0 == JewelAffixRules.BaseType;
            bool affix = IsJewelAffix(__instance.affixId);
            if (!jewel && !affix) return true;
            __result = jewel && affix;
            return false;
        }
    }

    [ThreadStatic] private static bool canGenerateExalted;

    // Native Tier stores only ranges, not weights. Filter affix weights while ordinary
    // tiers are being generated, then constrain the native tier roll to indices 5/6.
    [HarmonyPatch(typeof(GenerateItems), nameof(GenerateItems.RollAffixes))]
    private static class NativeGenerationScope
    {
        private static void Prefix(ItemData __0, bool __2, out bool __state)
        {
            __state = canGenerateExalted;
            canGenerateExalted = __0 != null && __0.itemType == JewelAffixRules.BaseType && __2;
        }
        private static void Finalizer(bool __state) => canGenerateExalted = __state;
    }

    [HarmonyPatch(typeof(AffixList.Affix), nameof(AffixList.Affix.GetWeighting))]
    private static class NativeTierWeight
    {
        private static void Postfix(AffixList.Affix __instance, ref float __result)
        {
            if (IsJewelAffix(__instance.affixId) && MinimumTier(__instance.affixId) == 6 && !canGenerateExalted) __result = 0;
        }
    }

    [HarmonyPatch(typeof(GenerateItems), nameof(GenerateItems.ApplyRollModifiersToAffixWeight))]
    private static class NativeModifiedTierWeight
    {
        private static void Postfix(AffixList.Affix __1, ref float __result)
        {
            if (__1 != null && IsJewelAffix(__1.affixId) && MinimumTier(__1.affixId) == 6 && !canGenerateExalted) __result = 0;
        }
    }

    [HarmonyPatch(typeof(GenerateItems), nameof(GenerateItems.RollTier))]
    private static class NativeTierFloor
    {
        private static void Postfix(ItemData __0, AffixList.Affix __1, ref int __result)
        {
            if (__0 == null || __0.itemType != JewelAffixRules.BaseType || __1 == null || !IsJewelAffix(__1.affixId)) return;
            __result = Math.Clamp(__result, MinimumTier(__1.affixId) - 1, 6);
        }
    }
}
