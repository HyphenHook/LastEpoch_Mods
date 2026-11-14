using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.Mods.Items.Requirements;

namespace LastEpoch_Hud.Scripts.Mods.Economy;

/// <summary>
/// Builds the items the game's random generator will not hand out on request: gear with a sealed affix,
/// experimental gloves/boots/belts, six-affix primordials with a sealed T8, personal campaign items, and
/// weaver and heretical idols. Each is assembled from affixes that are legal for the base (the mod's
/// force-drop catalog and rules decide that) and verified by the mod's own item creator, so the result is
/// a real, valid item rather than a hopeful roll.
/// </summary>
internal static class BazaarCustomItems
{
    public enum Kind
    {
        SealedGear, // a crafter sealed one affix and kept going
        Experimental, // gloves, boots or belts carrying an experimental affix
        PrimordialSix, // 4 affixes + a regular seal + a sealed T8: the 6-affix endgame item
        Personal, // campaign reward: a personal affix, no forging potential, levelling gear
        WeaverIdol, // small generic idol with at least one weaver affix
        HereticalIdol, // enchanted class idol: 2 normal + 2 enchanted affixes
        QuadExalted, // four T6/T7 affixes and no primordial: the rarest drop there is
        SixAffix, // five or six ordinary affixes (never more than three prefixes or suffixes): a crafter's masterpiece
    }

    private sealed class SubBase
    {
        public int Sub;
        public string Name;
        public int ClassBits;
    }

    // Display-tier odds for ordinary affixes (T1..T7). Higher tiers are found, not crafted, so they are rarer.
    private static readonly double[] NormalTierWeights = { 6, 10, 16, 22, 22, 12, 6 };
    private static readonly double[] ExaltedTierWeights = { 0, 0, 0, 3, 12, 40, 45 };
    private static readonly double[] TopTierWeights = { 0, 0, 0, 0, 0, 60, 40 }; // T6 or T7 only
    private static readonly double[] LowTierWeights = { 40, 35, 25, 0, 0, 0, 0 };

    // A crafter's six-affix item: mostly well-crafted T4 to T6, a found T7 now and then.
    private static readonly double[] CrafterTierWeights = { 1, 3, 8, 20, 32, 24, 12 };

    private static int _sixExtra;

    /// <summary>
    /// Adds ordinary affixes to a finished item, with a crafter's tiers, never more than three prefixes or three
    /// suffixes. Returns how many were added.
    /// </summary>
    private static int AddExtraOrdinary(ItemDataUnpacked item, int count, Random rng)
    {
        int added = 0;
        try
        {
            int baseType = item.itemType;
            int subType = item.subType;
            List<AffixList.Affix> pool = Pool(baseType, subType, false, false);
            int prefixes = 0, suffixes = 0;
            foreach (ItemAffix a in item.affixes.ToArray())
            {
                if (a == null || a.IsSealed)
                {
                    continue;
                }
                if (a.affixType == AffixList.AffixType.PREFIX)
                {
                    prefixes++;
                }
                else if (a.affixType == AffixList.AffixType.SUFFIX)
                {
                    suffixes++;
                }
            }
            var existing = item.affixes.ToArray().Where(a => a != null).Select(a => (int)a.affixId).ToList();
            for (int n = 0; n < count; n++)
            {
                var legal = new ForceDropLegalAffixes(baseType, subType, 2, existing);
                AffixList.Affix chosen = null;
                for (int tries = 0; tries < 120 && pool.Count > 0; tries++)
                {
                    AffixList.Affix def = WeightedPick(pool, rng);
                    if (def == null || def.specialAffixType != AffixList.SpecialAffixType.Standard || existing.Contains(def.affixId))
                    {
                        continue;
                    }
                    if ((def.type == AffixList.AffixType.PREFIX && prefixes >= AffixLimits.MaxPerSide)
                        || (def.type == AffixList.AffixType.SUFFIX && suffixes >= AffixLimits.MaxPerSide))
                    {
                        continue;
                    }
                    if (legal.OrdinaryReason(def, false, false).Length == 0)
                    {
                        chosen = def;
                        break;
                    }
                }
                if (chosen == null)
                {
                    break;
                }
                int tier = DisplayTier(chosen, rng, CrafterTierWeights);
                item.affixes.Add(new ItemAffix((ushort)chosen.affixId, (byte)Math.Max(0, tier - 1), (byte)Roll(rng, 0.7), (byte)baseType, SealedAffixType.None));
                existing.Add(chosen.affixId);
                if (chosen.type == AffixList.AffixType.PREFIX)
                {
                    prefixes++;
                }
                else if (chosen.type == AffixList.AffixType.SUFFIX)
                {
                    suffixes++;
                }
                added++;
            }
            if (added > 0)
            {
                item.sockets = (byte)item.affixes.Count;
                item.RefreshIDAndValues();
            }
        }
        catch (Exception ex)
        {
            Fail(Kind.SixAffix, "extra affixes: " + ex.Message);
        }
        return added;
    }

    /// <summary>True when the last six-affix item built was meant to be corrupted: the factory does that part.</summary>
    public static bool LastSixCorrupted { get; private set; }

    private static readonly Dictionary<int, List<SubBase>> _subs = new();
    private static readonly Dictionary<string, List<AffixList.Affix>> _pools = new();
    private static readonly Dictionary<Kind, int> _failures = new();

    // Why the last plan came back empty, so a failure says what was missing rather than just "no plan".
    private static string _why = "";

    // ------------------------------------------------------------ public entry

    public static ItemDataUnpacked Build(Kind kind, int stall, int[] stallTypes, Random rng, out string note)
    {
        note = kind.ToString();
        try
        {
            ResolvedForceDrop request = Plan(kind, stall, stallTypes, rng);
            if (request == null)
            {
                Fail(kind, "no plan: " + _why);
                return null;
            }
            ItemDataUnpacked made = ForceDropItemCreator.Create(request);
            if (kind == Kind.SixAffix && _sixExtra > 0 && made != null)
            {
                if (AddExtraOrdinary(made, _sixExtra, rng) == 0)
                {
                    Fail(kind, "no legal extra affix could be added");
                    return null;
                }
            }
            return made;
        }
        catch (Exception ex)
        {
            Fail(kind, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Turns a unique into a legendary: the game spends its legendary potential on that many extra affixes. Each
    /// is a legal affix for the base, drawn by weight, usually an ordinary tier and sometimes an exalted one.
    /// Returns how many were actually added.
    /// </summary>
    public static int AddLegendaryAffixes(ItemDataUnpacked item, int count, Random rng)
    {
        int added = 0;
        try
        {
            int baseType = item.itemType;
            int subType = item.subType;
            List<AffixList.Affix> pool = Pool(baseType, subType, false, false);
            var existing = new List<int>();
            if (item.affixes != null)
            {
                foreach (ItemAffix a in item.affixes)
                {
                    if (a != null)
                    {
                        existing.Add(a.affixId);
                    }
                }
            }
            for (int n = 0; n < count && item.affixes != null; n++)
            {
                var legal = new ForceDropLegalAffixes(baseType, subType, 2, existing);
                AffixList.Affix chosen = null;
                for (int tries = 0; tries < 80 && pool.Count > 0; tries++)
                {
                    AffixList.Affix def = WeightedPick(pool, rng);
                    if (def == null || def.specialAffixType != AffixList.SpecialAffixType.Standard || existing.Contains(def.affixId))
                    {
                        continue;
                    }
                    if (legal.OrdinaryReason(def, false, false).Length == 0)
                    {
                        chosen = def;
                        break;
                    }
                }
                if (chosen == null)
                {
                    _why = "legendary: no legal affix among " + pool.Count + " for " + baseType + "/" + subType;
                    break;
                }
                int tier = DisplayTier(chosen, rng, rng.NextDouble() < 0.35 ? ExaltedTierWeights : NormalTierWeights);
                var affix = new ItemAffix((ushort)chosen.affixId, (byte)Math.Max(0, tier - 1), (byte)Roll(rng, 0.6), (byte)baseType, SealedAffixType.None);
                item.affixes.Add(affix);
                existing.Add(chosen.affixId);
                added++;
            }
            if (added > 0)
            {
                item.rarity = 9;
                item.sockets = (byte)item.affixes.Count;
                item.RefreshIDAndValues();
            }
            else if (_why.Length > 0)
            {
                Fail(Kind.SealedGear, _why);
            }
        }
        catch (Exception ex)
        {
            Fail(Kind.SealedGear, "legendary affixes: " + ex.Message);
        }
        return added;
    }

    private static void Fail(Kind kind, string why)
    {
        _failures.TryGetValue(kind, out int n);
        _failures[kind] = n + 1;
        if (n < 12)
        {
            OfflineBazaarMarket.Crumb("custom: " + kind + " failed (" + (why.Length > 200 ? why.Substring(0, 200) : why) + ")");
        }
    }

    // ------------------------------------------------------------ planning

    private static ResolvedForceDrop Plan(Kind kind, int stall, int[] stallTypes, Random rng)
    {
        _sixExtra = 0;
        bool idol = kind == Kind.WeaverIdol || kind == Kind.HereticalIdol;
        int baseType = PickBaseType(kind, stall, stallTypes, rng);
        SubBase sub = PickSub(kind, stall, baseType, rng);
        if (sub == null)
        {
            _why = "no usable base: item type " + baseType + " has " + Subs(baseType).Count + " subtypes ("
                + string.Join("|", Subs(baseType).Take(6).Select(x => x.Name)) + "), none fit " + kind + " on stall " + stall;
            return null;
        }
        _why = "";

        var chosen = new List<ResolvedForceDropAffix>();
        int rarityForLegality = 2;

        bool Add(
            AffixList.Affix def,
            int displayTier,
            double quality,
            ForceDropSeal seal = ForceDropSeal.None
        )
        {
            if (def == null)
            {
                return false;
            }
            int tierIndex = seal == ForceDropSeal.Primordial ? 7 : Math.Max(0, displayTier - 1);
            chosen.Add(new ResolvedForceDropAffix(def.affixId, tierIndex, Roll(rng, quality), seal));
            return true;
        }

        AffixList.Affix Pick(Func<AffixList.Affix, bool> accept, bool sealedAffix = false, bool enchantment = false)
        {
            var existing = chosen.Select(a => a.Id).ToList();
            var legal = new ForceDropLegalAffixes(baseType, sub.Sub, rarityForLegality, existing);
            List<AffixList.Affix> pool = Pool(baseType, sub.Sub, sealedAffix, enchantment);
            if (pool.Count == 0)
            {
                _why = "no legal affixes at all for " + kind + " on " + baseType + "/" + sub.Sub + " (" + sub.Name + ")";
                return null;
            }
            for (int tries = 0; tries < 60; tries++)
            {
                AffixList.Affix def = WeightedPick(pool, rng);
                if (def == null || !accept(def) || existing.Contains(def.affixId))
                {
                    continue;
                }
                if (legal.OrdinaryReason(def, sealedAffix, enchantment).Length == 0)
                {
                    return def;
                }
            }
            _why = "no affix of the wanted kind among " + pool.Count + " legal ones for " + kind + " on " + baseType + "/" + sub.Sub + " (" + sub.Name + ")";
            return null;
        }

        bool Standard(AffixList.Affix d) => d.specialAffixType == AffixList.SpecialAffixType.Standard;

        switch (kind)
        {
            case Kind.SealedGear:
            {
                int ordinary = rng.Next(2, 4);
                bool exalted = rng.NextDouble() < 0.45;
                for (int i = 0; i < ordinary; i++)
                {
                    AffixList.Affix d = Pick(Standard);
                    if (d == null || !Add(d, DisplayTier(d, rng, exalted && i == 0 ? ExaltedTierWeights : NormalTierWeights), 0.6))
                    {
                        return null;
                    }
                }
                AffixList.Affix sealedDef = Pick(Standard, sealedAffix: true);
                if (sealedDef == null)
                {
                    return null;
                }
                Add(sealedDef, DisplayTier(sealedDef, rng, NormalTierWeights), 0.7, ForceDropSeal.Regular);
                return Request(baseType, sub, chosen, rng.Next(2, 26), 2);
            }
            case Kind.Experimental:
            {
                AffixList.Affix experimental = Pick(d => d.specialAffixType == AffixList.SpecialAffixType.Experimental);
                if (experimental == null)
                {
                    return null;
                }
                bool exalted = rng.NextDouble() < 0.4;
                Add(experimental, DisplayTier(experimental, rng, exalted ? ExaltedTierWeights : NormalTierWeights), 0.65);
                int others = rng.Next(1, 4);
                for (int i = 0; i < others; i++)
                {
                    AffixList.Affix d = Pick(Standard);
                    if (d == null || !Add(d, DisplayTier(d, rng, NormalTierWeights), 0.6))
                    {
                        return null;
                    }
                }
                return Request(baseType, sub, chosen, rng.Next(4, 41), 2);
            }
            case Kind.PrimordialSix:
            {
                // The seed: an affix that exists up to T8, sealed as the primordial one.
                AffixList.Affix primordial = Pick(d => Standard(d) && d.tiers != null && d.tiers.Count >= 8);
                if (primordial == null)
                {
                    return null;
                }
                Add(primordial, 8, 0.85, ForceDropSeal.Primordial);
                for (int i = 0; i < 4; i++)
                {
                    AffixList.Affix d = Pick(Standard);
                    if (d == null || !Add(d, DisplayTier(d, rng, i < 2 ? ExaltedTierWeights : NormalTierWeights), 0.75))
                    {
                        return null;
                    }
                }
                AffixList.Affix sealedDef = Pick(Standard, sealedAffix: true);
                if (sealedDef == null)
                {
                    return null;
                }
                Add(sealedDef, DisplayTier(sealedDef, rng, ExaltedTierWeights), 0.8, ForceDropSeal.Regular);
                return Request(baseType, sub, chosen, rng.Next(0, 3), 2);
            }
            case Kind.QuadExalted:
            {
                for (int i = 0; i < 4; i++)
                {
                    AffixList.Affix d = Pick(x => Standard(x) && ForceDropCatalog.MaximumTier(x) >= 6);
                    if (d == null || !Add(d, DisplayTier(d, rng, TopTierWeights), 0.75))
                    {
                        return null;
                    }
                }
                return Request(baseType, sub, chosen, rng.Next(0, 10), 2);
            }
            case Kind.SixAffix:
            {
                // 5 or 6 ordinary affixes, three prefixes and three suffixes at most. Seals, the primordial affix and
                // corruption come on top of them, and a crafter may have used any of them or none.
                int count = rng.NextDouble() < 0.6 ? 5 : 6;
                // The creator packs and re-reads every item it makes and refuses a seal-less one with five or six
                // ordinary affixes. So it makes four, and the rest are added afterwards (see AddExtraOrdinary).
                int built = Math.Min(count, 4);
                _sixExtra = count - built;
                double v = rng.NextDouble();
                // plain 34%, sealed 24%, primordial (always with its regular seal, as on the endgame six) 22%,
                // corrupted 12%, corrupted and sealed 8%
                bool regularSeal = (v >= 0.34 && v < 0.80) || v >= 0.92;
                bool primordial = v >= 0.58 && v < 0.80;
                LastSixCorrupted = v >= 0.80;
                if (primordial)
                {
                    AffixList.Affix seed = Pick(d => Standard(d) && d.tiers != null && d.tiers.Count >= 8);
                    if (seed == null)
                    {
                        return null;
                    }
                    Add(seed, 8, 0.85, ForceDropSeal.Primordial);
                }
                int prefixes = 0, suffixes = 0;
                bool SideOpen(AffixList.Affix d) =>
                    d.type == AffixList.AffixType.PREFIX ? prefixes < AffixLimits.MaxPerSide
                    : d.type == AffixList.AffixType.SUFFIX ? suffixes < AffixLimits.MaxPerSide
                    : true;
                for (int i = 0; i < built; i++)
                {
                    AffixList.Affix d = Pick(x => Standard(x) && SideOpen(x));
                    double[] weights = i < 2 && rng.NextDouble() < 0.25 ? ExaltedTierWeights : CrafterTierWeights;
                    if (d == null || !Add(d, DisplayTier(d, rng, weights), 0.7))
                    {
                        return null;
                    }
                    if (d.type == AffixList.AffixType.PREFIX)
                    {
                        prefixes++;
                    }
                    else if (d.type == AffixList.AffixType.SUFFIX)
                    {
                        suffixes++;
                    }
                }
                if (regularSeal)
                {
                    AffixList.Affix sealedDef = Pick(Standard, sealedAffix: true);
                    if (sealedDef == null)
                    {
                        return null;
                    }
                    Add(sealedDef, DisplayTier(sealedDef, rng, CrafterTierWeights), 0.7, ForceDropSeal.Regular);
                }
                return Request(baseType, sub, chosen, rng.Next(0, 6), 2);
            }
            case Kind.Personal:
            {
                AffixList.Affix personal = Pick(d => d.specialAffixType == AffixList.SpecialAffixType.Personal);
                if (personal == null)
                {
                    return null;
                }
                Add(personal, DisplayTier(personal, rng, LowTierWeights), 0.5);
                int others = rng.Next(1, 3);
                for (int i = 0; i < others; i++)
                {
                    AffixList.Affix d = Pick(Standard);
                    if (d == null || !Add(d, DisplayTier(d, rng, LowTierWeights), 0.5))
                    {
                        return null;
                    }
                }
                return Request(baseType, sub, chosen, 0, 2); // personal items drop with no forging potential
            }
            case Kind.WeaverIdol:
            {
                AffixList.Affix weaver = Pick(d => d.specialAffixType == AffixList.SpecialAffixType.IdolWeaver);
                if (weaver == null)
                {
                    return null;
                }
                Add(weaver, DisplayTier(weaver, rng, rng.NextDouble() < 0.3 ? ExaltedTierWeights : NormalTierWeights), 0.7);
                // Most often the second affix is an ordinary idol affix, sometimes another weaver one.
                bool secondWeaver = rng.NextDouble() < 0.25;
                AffixList.Affix second = secondWeaver
                    ? Pick(d => d.specialAffixType == AffixList.SpecialAffixType.IdolWeaver)
                    : Pick(d => d.specialAffixType != AffixList.SpecialAffixType.IdolWeaver && d.specialAffixType != AffixList.SpecialAffixType.IdolEnchantment);
                if (second != null)
                {
                    Add(second, DisplayTier(second, rng, NormalTierWeights), 0.6);
                }
                return Request(baseType, sub, chosen, 0, 2, idol: true);
            }
            case Kind.HereticalIdol:
            {
                for (int i = 0; i < 2; i++)
                {
                    AffixList.Affix d = Pick(x => x.specialAffixType != AffixList.SpecialAffixType.IdolEnchantment && x.specialAffixType != AffixList.SpecialAffixType.IdolWeaver);
                    if (d == null || !Add(d, DisplayTier(d, rng, NormalTierWeights), 0.6))
                    {
                        return null;
                    }
                }
                for (int i = 0; i < 2; i++)
                {
                    AffixList.Affix e = Pick(x => x.specialAffixType == AffixList.SpecialAffixType.IdolEnchantment, enchantment: true);
                    if (e == null || !Add(e, DisplayTier(e, rng, rng.NextDouble() < 0.4 ? ExaltedTierWeights : NormalTierWeights), 0.7))
                    {
                        return null;
                    }
                }
                return Request(baseType, sub, chosen, 0, 2, idol: true);
            }
        }
        return null;
    }

    private static ResolvedForceDrop Request(int baseType, SubBase sub, List<ResolvedForceDropAffix> affixes, int forgingPotential, int rarity, bool idol = false) =>
        new ResolvedForceDrop(
            baseType,
            sub.Sub,
            0,
            rarity,
            forgingPotential,
            0,
            0,
            false,
            idol ? new[] { 0, 0, 0 } : new[] { 128, 128, 128 },
            Array.Empty<int>(),
            affixes,
            Array.Empty<int>(),
            null
        );

    // ------------------------------------------------------------ bases

    private static int PickBaseType(Kind kind, int stall, int[] stallTypes, Random rng)
    {
        if (kind == Kind.Experimental)
        {
            return new[] { 2, 3, 4 }[rng.Next(3)]; // belts, boots, gloves are the only bases that take them
        }
        if (kind == Kind.WeaverIdol)
        {
            return 25 + rng.Next(4); // the small generic idol sizes
        }
        if (stallTypes != null && stallTypes.Length > 0)
        {
            return stallTypes[rng.Next(stallTypes.Length)];
        }
        return stall;
    }

    private static SubBase PickSub(Kind kind, int stall, int baseType, Random rng)
    {
        List<SubBase> all = Subs(baseType);
        IEnumerable<SubBase> pool = all;
        if (kind == Kind.WeaverIdol)
        {
            pool = all.Where(s => (s.Name ?? "").IndexOf("Weaver", StringComparison.Ordinal) >= 0);
        }
        else if (kind == Kind.HereticalIdol)
        {
            pool = all.Where(s => (s.Name ?? "").StartsWith("Heretical ", StringComparison.Ordinal));
            if (stall >= 24 && stall <= 28)
            {
                int bit = 1 << (stall - 24);
                pool = pool.Where(s => (s.ClassBits & bit) != 0);
            }
        }
        else if (stall >= 24 && stall <= 28)
        {
            int bit = 1 << (stall - 24);
            pool = pool.Where(s => (s.ClassBits & bit) != 0);
        }
        List<SubBase> list = pool.ToList();
        return list.Count == 0 ? null : list[rng.Next(list.Count)];
    }

    private static List<SubBase> Subs(int baseType)
    {
        if (_subs.TryGetValue(baseType, out List<SubBase> cached))
        {
            return cached;
        }
        var result = new List<SubBase>();
        ItemList list = ItemList.get();
        if (!list.IsNullOrDestroyed())
        {
            foreach (var type in list.EquippableItems)
            {
                if (type.baseTypeID != baseType)
                {
                    continue;
                }
                foreach (var sub in type.subItems)
                {
                    if (sub.IsUniqueOnlyBase || sub.isCorruptedSubtype || sub.obsoleteItem)
                    {
                        continue;
                    }
                    if (!Items_Req_Class.TryGetOriginalRequirement(baseType, sub.subTypeID, sub.classRequirement, out var original))
                    {
                        continue;
                    }
                    int bits = original == ItemList.ClassRequirement.None || original == ItemList.ClassRequirement.Any ? 31 : (int)original;
                    result.Add(new SubBase { Sub = sub.subTypeID, Name = sub.name, ClassBits = bits });
                }
            }
        }
        _subs[baseType] = result;
        return result;
    }

    // ------------------------------------------------------------ affix pools and rolls

    /// <summary>Every affix the force-drop rules allow on this base, remembered so a build is cheap.</summary>
    internal static List<AffixList.Affix> Pool(int baseType, int subType, bool sealedAffix, bool enchantment)
    {
        string key = baseType + ":" + subType + ":" + sealedAffix + ":" + enchantment;
        if (_pools.TryGetValue(key, out List<AffixList.Affix> cached))
        {
            return cached;
        }
        var legal = new ForceDropLegalAffixes(baseType, subType, 2, Array.Empty<int>());
        var pool = new List<AffixList.Affix>();
        foreach (AffixList.Affix def in ForceDropCatalog.Definitions())
        {
            try
            {
                if (legal.OrdinaryReason(def, sealedAffix, enchantment).Length == 0)
                {
                    pool.Add(def);
                }
            }
            catch (Exception) { }
        }
        _pools[key] = pool;
        return pool;
    }

    /// <summary>Common affixes come up more than rare ones, as in the game.</summary>
    internal static AffixList.Affix WeightedPick(List<AffixList.Affix> pool, Random rng)
    {
        double total = 0;
        foreach (AffixList.Affix d in pool)
        {
            total += Math.Max(0.01, d.weighting);
        }
        double r = rng.NextDouble() * total;
        foreach (AffixList.Affix d in pool)
        {
            r -= Math.Max(0.01, d.weighting);
            if (r <= 0)
            {
                return d;
            }
        }
        return pool[pool.Count - 1];
    }

    /// <summary>A display tier (1..7) drawn from the weights, limited to what this affix actually has.</summary>
    internal static int DisplayTier(AffixList.Affix def, Random rng, double[] weights)
    {
        int max = Math.Max(1, ForceDropCatalog.MaximumTier(def));
        double total = 0;
        for (int t = 1; t <= Math.Min(7, max); t++)
        {
            total += weights[t - 1];
        }
        if (total <= 0)
        {
            return max;
        }
        double r = rng.NextDouble() * total;
        for (int t = 1; t <= Math.Min(7, max); t++)
        {
            r -= weights[t - 1];
            if (r <= 0)
            {
                return t;
            }
        }
        return max;
    }

    internal static int Roll(Random rng, double quality)
    {
        double gaussian = Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());
        return (int)Math.Clamp(255 * quality + 50 * gaussian, 0, 255);
    }
}
