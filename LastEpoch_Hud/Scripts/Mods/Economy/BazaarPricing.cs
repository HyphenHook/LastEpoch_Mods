using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Economy;

namespace LastEpoch_Hud.Scripts.Mods.Economy;

/// <summary>
/// What an item is worth, in gold, from what makes items valuable in Last Epoch:
///  - Forging potential is the crafting budget, so a clean base with a lot of it is worth real gold.
///  - An item's value is mostly its affixes: higher tiers are worth far more, T6 and T7 can only be found
///    (never crafted) and so are scarce, rarer affixes (lower roll weight) are worth more, and good rolls help.
///  - Uniques are worth what is hard to get: how rarely the unique drops, plus its legendary potential or
///    weaver's will, plus the quality of its rolls.
///  - Corrupted items cannot be crafted, items with no forging potential have nothing left to craft, and
///    items with nothing worth having are only worth a few coins.
/// The constants are a first pass to tune in play; the structure is what matters.
/// </summary>
internal static class BazaarPricing
{
    // Craftable affixes (up to T5) cost what the crafting materials and effort cost: a T5 is a few thousand gold.
    // Everything that can only be FOUND (T6, T7, T8, idols, uniques) is priced by farming time in RarityMath.
    private const double AffixUnit = 1800; // gold for one ordinary affix at tier value 1.0
    private const long Floor = 20;
    private const long GarbageCeiling = 500;
    private const long PersonalCeiling = 1500;

    // Displayed tier (1..5) to the value of a craftable affix. T6 and up are found, not crafted: they count as
    // a T5 here (the crafting value they replace) and the rest of their worth comes from RarityMath.
    private static readonly double[] TierValue = { 0, 0.4, 0.6, 0.9, 1.5, 2.6, 2.6, 2.6, 2.6 };

    // The item rarity byte is the count of ordinary affixes (0-6) for gear; 7 = unique, 8 = set, 9 = legendary.
    public static bool IsUnique(int rarity) => rarity >= 7;

    public sealed class Appraisal
    {
        public long Fair;
        public bool Garbage;
        public double AffixScore;
    }

    public static Appraisal Appraise(ItemData item, int levelReq, bool corrupted)
    {
        double level = 0.35 + 0.9 * Math.Clamp((levelReq <= 0 ? 60 : levelReq) / 100.0, 0, 1);
        double affixScore = AffixScore(item, out int top, out int highTiers);
        int fp = item.forgingPotential;

        double value;
        if (IsUnique(item.rarity))
        {
            value = UniqueValue(item, level, corrupted, affixScore, highTiers);
        }
        else
        {
            bool idol = IsIdol(item);
            // What the affixes are worth to craft, what the forging potential can still add (only as much as
            // there are slots to fill or affixes to upgrade), and what the rare finds on it cost to farm.
            double crafting = affixScore * AffixUnit * level;
            double potential = corrupted || idol ? 0 : CraftingPotentialGold(item, level);
            double found = idol ? IdolFoundGold(item) : FoundGold(item);
            // Affixes that push the same way are worth more together; so are good implicit rolls.
            value = (crafting + potential + found) * SynergyFactor(item) * ImplicitFactor(item);
            if (corrupted)
            {
                value *= 0.85; // cannot be crafted any further
            }
            // A finished, well rolled item is something a player can just equip.
            value *= FinalProductPremium(item, fp);
        }

        // Nothing worth having: a few coins, never an absurd price.
        // Idols have no tiers to speak of, so "nothing above T3" says nothing about them.
        bool garbage = !IsUnique(item.rarity) && !IsIdol(item) && top < 4 && fp < 10;
        // Uniques are already priced in gold by UniqueValue; gear is bent onto the market scale here.
        double market = IsUnique(item.rarity) ? value : RarityMath.CompressGear(value);
        long fair = Math.Clamp((long)Math.Min(market, (double)RarityMath.MaxPrice), Floor, RarityMath.MaxPrice);
        // Personal campaign items are levelling gear with no forging potential: worth a few hundred at most.
        if (HasPersonalAffix(item))
        {
            fair = Math.Min(fair, PersonalCeiling);
            garbage = false;
        }
        if (garbage)
        {
            fair = Math.Min(fair, GarbageCeiling);
        }
        return new Appraisal { Fair = fair, Garbage = garbage, AffixScore = affixScore };
    }

    private static bool IsIdol(ItemData item) => (item.itemType >= 25 && item.itemType <= 33) || item.itemType == 41;

    /// <summary>
    /// What the T6, T7 and T8 affixes on a piece of gear cost to FIND: they cannot be crafted, so the price is the
    /// farming time for all of them to turn up together, at the rarity of each particular affix, scaled by how well
    /// they rolled.
    /// </summary>
    private static double FoundGold(ItemData item)
    {
        if (item.affixes == null)
        {
            return 0;
        }
        var chances = new List<double>();
        double rollSum = 0;
        foreach (ItemAffix a in item.affixes)
        {
            if (a == null)
            {
                continue;
            }
            int tier = a.affixTier + 1;
            if (tier < 6)
            {
                continue;
            }
            chances.Add(RarityMath.TierChance(tier) / AffixRarity(a.affixId));
            rollSum += RollMath.AffixFactor(AffixRollQuality(a)) / RollMath.AffixFactor(0.5);
        }
        if (chances.Count == 0)
        {
            return 0;
        }
        double roll = rollSum / chances.Count;
        return RarityMath.GearAffixGold(chances, RarityMath.GearSearchRate) * roll;
    }

    /// <summary>
    /// Idols have no tiers. What makes one dear is how many wanted affixes it carries, how scarce each is (weaver
    /// and enchanted affixes are the rare ones) and how well they rolled; every one must turn up together.
    /// </summary>
    private static double IdolFoundGold(ItemData item)
    {
        if (item.affixes == null)
        {
            return 0;
        }
        var chances = new List<double>();
        double rollSum = 0;
        foreach (ItemAffix a in item.affixes)
        {
            if (a == null)
            {
                continue;
            }
            chances.Add(RarityMath.IdolAffixChance(a.specialAffixType.ToString()) / AffixRarity(a.affixId));
            rollSum += RollMath.AffixFactor(AffixRollQuality(a)) / RollMath.AffixFactor(0.5);
        }
        if (chances.Count == 0)
        {
            return 0;
        }
        return RarityMath.GearAffixGold(chances, RarityMath.IdolSearchRate) * (rollSum / chances.Count);
    }

    /// <summary>
    /// Affixes that push the same way (spell damage on every line, say) are worth more to a build than the same
    /// quality spread over unrelated stats. Judged by the game's own display category for each affix.
    /// </summary>
    private static double SynergyFactor(ItemData item)
    {
        if (item.affixes == null)
        {
            return 1.0;
        }
        var themes = new List<(string, double)>();
        AffixList list = AffixList.get();
        foreach (ItemAffix a in item.affixes)
        {
            if (a == null)
            {
                continue;
            }
            string category = null;
            try
            {
                category = list?.GetAffix(a.affixId)?.displayCategory.ToString();
            }
            catch (Exception) { }
            if (category == "ATTRUBUTES")
            {
                category = null; // attributes turn up on everything; they are not a build theme
            }
            themes.Add((category, a.affixTier + 1 >= 6 ? 1.5 : 1.0));
        }
        return SynergyMath.Factor(themes);
    }

    /// <summary>
    /// The forging potential, in gold: what the crafts it pays for would add. Open slots take a new affix; an
    /// affix below T5 can be upgraded a step (T6 and T7 cannot be crafted); a sealed affix cannot be changed.
    /// </summary>
    private static double CraftingPotentialGold(ItemData item, double level)
    {
        int fp = item.forgingPotential;
        if (fp <= 0 || item.rarity >= 7)
        {
            return 0;
        }
        int ordinary = 0;
        int sealedAffixes = 0;
        var upgrades = new List<double>();
        if (item.affixes != null)
        {
            foreach (ItemAffix a in item.affixes)
            {
                if (a == null)
                {
                    continue;
                }
                if (a.IsSealed)
                {
                    // Only a regular seal takes one of the four slots; a primordial seal is a slot of its own.
                    if (a.IsSealedRegular)
                    {
                        sealedAffixes++;
                    }
                    continue;
                }
                ordinary++;
                int tier = a.affixTier + 1;
                if (tier < 5)
                {
                    double gain = AffixUnit * level * (TierValue[5] - TierValue[tier]) * AffixRarity(a.affixId) * RollMath.AffixFactor(0.6);
                    upgrades.Add(gain);
                }
            }
        }
        // An item holds four affixes; a regular seal is one of them, a primordial seal is a slot of its own.
        int open = Math.Max(0, 4 - ordinary - Math.Min(1, sealedAffixes));
        // A new affix is typically worth about a well-rolled T5 of average scarcity.
        double fill = AffixUnit * level * TierValue[5] * 2.0 * RollMath.AffixFactor(0.6);
        return CraftingMath.Potential(fp, open, fill, upgrades, PotentialKindFactor(item));
    }

    /// <summary>
    /// Grows with the square of how many affixes are top tier (T6 and T7 count fully, T5 partly), so one good
    /// affix is a bonus but four are a different class of item.
    /// </summary>
    private static double FinalProductPremium(ItemData item, int forgingPotential)
    {
        if (item.affixes == null)
        {
            return 1;
        }
        double top = 0;
        var qualities = new List<double>();
        foreach (ItemAffix a in item.affixes)
        {
            if (a == null)
            {
                continue;
            }
            int tier = a.affixTier + 1;
            if (tier >= 6) top += 1;
            else if (tier == 5) top += 0.4;
            qualities.Add(AffixRollQuality(a));
        }
        double premium = 1.0; // rarity of the top affixes is already in FoundGold
        // Across the whole item: every affix rolled high is a different item from every affix rolled low.
        premium *= RollMath.ItemRollPremium(qualities);
        if (top >= 2 && forgingPotential <= 3)
        {
            premium *= 1.2; // finished: no forging potential left to spend
        }
        if (top >= 3 && qualities.Count > 0 && RollMath.Average(qualities) >= 0.85)
        {
            premium *= 1.5; // and rolled near perfectly
        }
        return premium;
    }

    private static double AffixScore(ItemData item, out int topDisplayedTier, out int highTiers)
    {
        topDisplayedTier = 0;
        highTiers = 0;
        double score = 0;
        if (item.affixes == null)
        {
            return 0;
        }
        foreach (ItemAffix a in item.affixes)
        {
            if (a == null)
            {
                continue;
            }
            int tier = Math.Clamp(a.affixTier + 1, 1, 8);
            topDisplayedTier = Math.Max(topDisplayedTier, tier);
            if (tier >= 6)
            {
                highTiers++;
            }
            double roll = RollMath.AffixFactor(AffixRollQuality(a));
            // A corrupted affix is locked in and often strong: it is worth more than an ordinary one of its tier.
            double corruptedAffix = a.IsSealedCorrupted ? 1.3 : 1.0;
            score += TierValue[tier] * AffixRarity(a.affixId) * roll * corruptedAffix * KindFactor(a);
        }
        return score;
    }

    /// <summary>
    /// Is the high end of this stat's roll range the good end? That depends on the stat: more damage is good, but
    /// a smaller drawback, a lower cost or a shorter delay is good too. The game knows; ask it, and only if it
    /// cannot answer assume a larger number is better.
    /// </summary>
    private static bool MaxRollIsBest(float valueAtMinRoll, float valueAtMaxRoll, SP property, AT tags, byte specialTag)
    {
        try
        {
            return ItemData.MaxRollIsBest(valueAtMinRoll, valueAtMaxRoll, property, tags, specialTag);
        }
        catch (Exception)
        {
            return RollMath.MaxRollIsBestByValue(valueAtMinRoll, valueAtMaxRoll);
        }
    }

    /// <summary>
    /// How good this affix's roll is for the player: 0 is the worst roll it could have, 1 the best. An affix with
    /// more than one stat averages them, each judged by its own direction.
    /// </summary>
    private static double AffixRollQuality(ItemAffix a)
    {
        int roll = a.affixRoll;
        try
        {
            AffixList.Affix def = AffixList.get()?.GetAffix(a.affixId);
            if (def != null && def.tiers != null && a.affixTier < def.tiers.Count)
            {
                var tier = def.tiers[a.affixTier];
                // The stats this affix gives: one for an ordinary affix, several for a hybrid one.
                var stats = new List<(SP Property, AT Tags, byte SpecialTag)>();
                var single = def.TryCast<AffixList.SingleAffix>();
                if (single != null)
                {
                    stats.Add((single.property, single.tags, single.specialTag));
                }
                var multi = def.TryCast<AffixList.MultiAffix>();
                if (multi != null && multi.affixProperties != null)
                {
                    foreach (var p in multi.affixProperties)
                    {
                        stats.Add((p.property, p.tags, p.specialTag));
                    }
                }
                double sum = 0;
                int n = 0;
                for (int i = 0; tier != null && i < stats.Count; i++)
                {
                    float low = tier.minRoll;
                    float high = tier.maxRoll;
                    if (i > 0)
                    {
                        if (tier.extraRolls == null || i - 1 >= tier.extraRolls.Count)
                        {
                            break;
                        }
                        low = tier.extraRolls[i - 1].minRoll;
                        high = tier.extraRolls[i - 1].maxRoll;
                    }
                    if (Math.Abs(high - low) < 1e-6f)
                    {
                        continue; // a fixed stat: the roll does not move it
                    }
                    sum += RollMath.Quality(roll, MaxRollIsBest(low, high, stats[i].Property, stats[i].Tags, stats[i].SpecialTag));
                    n++;
                }
                if (n > 0)
                {
                    return sum / n;
                }
            }
        }
        catch (Exception) { }
        return RollMath.Quality(roll, true);
    }

    /// <summary>
    /// The price effect of a base's implicit rolls (0.8 worst, 1.3 best). Each implicit is judged by its own
    /// direction, and a base with no implicit stats is unaffected.
    /// </summary>
    private static double ImplicitFactor(ItemData item)
    {
        var qualities = new List<double>();
        try
        {
            var rolls = item.implicitRolls;
            ItemList list = ItemList.get();
            if (list != null && rolls != null)
            {
                foreach (var type in list.EquippableItems)
                {
                    if (type.baseTypeID != item.itemType)
                    {
                        continue;
                    }
                    foreach (var sub in type.subItems)
                    {
                        if (sub.subTypeID != item.subType || sub.implicits == null)
                        {
                            continue;
                        }
                        for (int i = 0; i < sub.implicits.Count && i < rolls.Length; i++)
                        {
                            var implicitStat = sub.implicits[i];
                            if (implicitStat == null)
                            {
                                continue;
                            }
                            float low = implicitStat.implicitValue;
                            float high = implicitStat.implicitMaxValue;
                            if (Math.Abs(high - low) < 1e-6f)
                            {
                                continue;
                            }
                            qualities.Add(RollMath.Quality(rolls[i], MaxRollIsBest(low, high, implicitStat.property, implicitStat.tags, implicitStat.specialTag)));
                        }
                    }
                }
            }
        }
        catch (Exception) { }
        return RollMath.ImplicitFactor(qualities);
    }

    // Experimental affixes are strong hybrids found only on drops; weaver and enchanted idol affixes come from
    // the endgame; a personal affix belongs to levelling gear and is worth little.
    private static double KindFactor(ItemAffix a)
    {
        switch (a.specialAffixType.ToString())
        {
            case "Experimental":
                return 1.25;
            case "IdolWeaver":
                return 1.5;
            case "IdolEnchantment":
                return 1.3;
            case "Set":
                return 1.1;
            case "Personal":
                return 0.6;
            default:
                return 1.0;
        }
    }

    private static bool HasPersonalAffix(ItemData item)
    {
        if (item.affixes == null)
        {
            return false;
        }
        foreach (ItemAffix a in item.affixes)
        {
            if (a != null && a.specialAffixType.ToString() == "Personal")
            {
                return true;
            }
        }
        return false;
    }

    // Ice forging potential can be kept when spent and Blood raises the crit chance, so a base with either is
    // worth more than the same potential of the ordinary kind.
    private static double PotentialKindFactor(ItemData item)
    {
        switch (item.forgingPotentialType.ToString())
        {
            case "Ice":
                return 1.35;
            case "Blood":
                return 1.2;
            default:
                return 1.0;
        }
    }

    // Rarer affixes (lower roll weight) are worth more: 1.0 for the commonest, up to 4.0 for the rarest.
    private static double AffixRarity(int affixId)
    {
        try
        {
            AffixList list = AffixList.get();
            AffixList.Affix affix = list?.GetAffix(affixId);
            if (affix == null)
            {
                return 1.5;
            }
            double min = Math.Max(1e-3, list.minimumWeighting);
            double max = Math.Max(min * 1.0001, list.maximumWeighting);
            double w = Math.Clamp(affix.weighting, min, max);
            double common = Math.Log(w / min) / Math.Log(max / min); // 0 = rarest, 1 = commonest
            return 1 + 3 * (1 - common);
        }
        catch (Exception)
        {
            return 1.5;
        }
    }

    private static double UniqueValue(ItemData item, double level, bool corrupted, double affixScore, int highTiers)
    {
        // Price = hours of farming for this unique with at least this much potential x what an hour earns.
        double scarcity = 1;
        double specificity = 1;
        double potentialChance = 1;
        try
        {
            UniqueList.Entry entry = UniqueList.getUnique((ushort)item.uniqueID);
            if (entry != null)
            {
                // rerollChance is how likely the game is to throw this unique back: the higher, the rarer.
                scarcity = 1 / (1 - Math.Clamp(entry.rerollChance, 0, 0.97));
                if (!entry.canDropRandomly)
                {
                    specificity *= 3; // boss-only and event uniques: the right fight, over and over
                }
                if (entry.isSetItem)
                {
                    specificity *= 1.5; // a set piece is worth little alone, but its set is what builds want
                }
                if (entry.isPrimordialItem)
                {
                    specificity *= 8;
                }
                if (entry.legendaryType == UniqueList.LegendaryType.WeaversWill)
                {
                    potentialChance = RarityMath.WeaversWillChance(item.weaversWill);
                }
                else
                {
                    potentialChance = RarityMath.LegendaryPotentialChance(item.legendaryPotential);
                }
            }
        }
        catch (Exception) { }

        if (item.rarity == 9)
        {
            // A legendary is a unique that already spent its legendary potential: one point per affix it gained.
            int spent = 1;
            if (item.affixes != null)
            {
                spent = Math.Clamp(item.affixes.Count, 1, 4);
            }
            // A little cheaper than the potential it came from: that potential has been used up.
            potentialChance = Math.Min(potentialChance, RarityMath.LegendaryPotentialChance(spent) / 0.6);
        }

        double quality = RollMath.UniqueFactor(UniqueRollQuality(item)) * ImplicitFactor(item);
        double value = RarityMath.CompressUnique(RarityMath.UniqueGold(potentialChance, scarcity, specificity) * quality * (0.6 + 0.4 * level));
        if (item.rarity == 9)
        {
            // Plus the exalted affixes it gained, priced like any other gear.
            value += RarityMath.CompressGear((affixScore * AffixUnit * level + FoundGold(item)) * SynergyFactor(item));
        }
        value = Math.Min(value, RarityMath.MaxPrice);
        return corrupted ? value * 0.8 : value;
    }

    /// <summary>
    /// How good a unique's rolls are, 0 to 1. Each modifier is judged by its own direction (a smaller drawback is
    /// a better roll), and modifiers with a fixed value say nothing about quality.
    /// </summary>
    private static double UniqueRollQuality(ItemData item)
    {
        try
        {
            UniqueList.Entry entry = UniqueList.getUnique((ushort)item.uniqueID);
            var rolls = item.uniqueRolls;
            if (entry != null && entry.mods != null && rolls != null)
            {
                var mods = new List<(int Roll, float ValueAtMin, float ValueAtMax, bool MaxRollIsBest)>();
                for (int i = 0; i < entry.mods.Count; i++)
                {
                    UniqueItemMod mod = entry.mods[i];
                    if (mod == null || !mod.canRoll)
                    {
                        continue; // a fixed modifier: nothing about it can be rolled well or badly
                    }
                    int slot = mod.getRollID();
                    if (slot >= rolls.Length)
                    {
                        slot = i;
                    }
                    if (slot >= rolls.Length)
                    {
                        continue;
                    }
                    float low = mod.getValue(0);
                    float high = mod.getValue(255);
                    bool lessIsBetter = false;
                    try
                    {
                        lessIsBetter = mod.GetPropertyInfo()?.lessIsBetter ?? false;
                    }
                    catch (Exception) { }
                    bool maxIsBest;
                    try
                    {
                        maxIsBest = ItemData.MaxRollIsBest(low, high, mod.property, mod.tags, mod.specialTag);
                    }
                    catch (Exception)
                    {
                        maxIsBest = RollMath.MaxRollIsBestByValue(low, high, lessIsBetter);
                    }
                    mods.Add((rolls[slot], low, high, maxIsBest));
                }
                return RollMath.UniqueQuality(mods);
            }
        }
        catch (Exception) { }
        return 0.5;
    }

    // ------------------------------------------------------------ how fast things sell

    /// <summary>
    /// Average minutes until a simulated buyer takes a listing. A fair price sells in about
    /// <paramref name="baseMinutes" />; a lowball is taken within minutes, a greedy price sits for hours,
    /// and expensive items sell slower because fewer people can afford them.
    /// </summary>
    public static double MeanMinutesToSell(long price, long fair, double baseMinutes)
    {
        double ratio = price / Math.Max(1.0, fair);
        // Shoppers compare price to value; a lowball is spotted by flippers and crafters within moments.
        double demand = Math.Clamp(Math.Pow(ratio, 3), 0.02, 60);
        // Budgets follow a heavy-tailed (Pareto) spread: a typical shopper can spend about MedianBudget, and
        // far fewer can afford an item that costs a hundred times that. Cheap items are not made faster.
        double afford = Math.Clamp(Math.Pow(Math.Max(1.0, price / MedianBudget), BudgetTail), 1, 600);
        return Math.Min(baseMinutes * demand * afford, 10 * 24 * 60);
    }

    /// <summary>
    /// Average minutes before a seller takes an unsold listing down. Cheap items turn over fast either way; an
    /// expensive one is left up for a long time (the seller expects to wait), and an overpriced one is pulled
    /// sooner than a fair one because the seller gives up.
    /// </summary>
    public static double MeanMinutesToTakeDown(long price, long fair, double meanMinutesToSell)
    {
        double ratio = price / Math.Max(1.0, fair);
        double patience = Math.Clamp(Math.Pow(Math.Max(1.0, price / MedianBudget), 0.5), 1, 40);
        double minutes = 180 * patience / Math.Max(0.5, ratio);
        return Math.Max(minutes, meanMinutesToSell * 2); // never gone before it had a fair chance to sell
    }

    private const double MedianBudget = 120_000; // about a quarter hour of income: what a typical player spends on one item
    private const double BudgetTail = 1.1; // Pareto exponent: higher means fewer rich players

    /// <summary>A listing priced well under its value is sniped almost at once.</summary>
    public static bool IsSnipeBait(long price, long fair) => price < fair * 0.65;
}
