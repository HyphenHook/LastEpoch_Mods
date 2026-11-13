using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Economy;

/// <summary>
/// Prices things by how long a player must farm to get them. A player who farms earns gold at a steady rate, so
/// an item that turns up once in N hours of play is worth about N hours of income: nobody sells it for less
/// unless they are happy to lose money, and nobody buys it for more than finding it themselves would cost.
///
/// Anchors (Season 5 guides, patch 1.5): about 500k gold an hour from selling loot, and a well rolled single-T7
/// piece sells around 250k. Everything else is derived from drop chances, so rarer means dearer by the maths.
/// </summary>
public static class RarityMath
{
    /// <summary>What an hour of farming earns, in gold.</summary>
    public const double GoldPerHour = 500_000;

    /// <summary>Raw farming time is limited to this, only so the maths cannot overflow.</summary>
    public const double MaxHours = 10_000_000;

    /// <summary>The price ceiling. Only the rarest things come near it: a four-T7 item with a T8, or a top legendary.</summary>
    public const long MaxPrice = 50_000_000;

    /// <summary>Exalted-eligible gear pieces a farmer looks through in an hour, and can filter for in the Bazaar.</summary>
    public const double GearSearchRate = 500;

    /// <summary>Idols looked through in an hour.</summary>
    public const double IdolSearchRate = 400;

    /// <summary>Uniques a player farming for one particular unique sees in an hour.</summary>
    public const double UniqueRate = 60;

    /// <summary>
    /// Chance that a given affix on exalted-eligible gear is of at least this displayed tier. Up to T5 an affix is
    /// craftable, so it is priced by crafting instead; T6 and T7 can only be found, and T8 (primordial) is made
    /// from a found T7 with rare materials.
    /// </summary>
    public static double TierChance(int displayedTier)
    {
        switch (displayedTier)
        {
            case 6: return 0.08;
            case 7: return 0.015;
            case 8: return 0.0015;
            default: return displayedTier > 8 ? 0.0015 : 1.0;
        }
    }

    /// <summary>Chance that an unique drops with at least this much legendary potential.</summary>
    public static double LegendaryPotentialChance(int legendaryPotential)
    {
        double[] chance = { 1, 0.15, 0.035, 0.007, 0.002 };
        return chance[Math.Clamp(legendaryPotential, 0, chance.Length - 1)];
    }

    /// <summary>Chance that a Weaver's Will unique drops with at least this much weaver's will.</summary>
    public static double WeaversWillChance(int weaversWill) => Math.Exp(-Math.Max(0, weaversWill - 4) / 6.0);

    /// <summary>Chance that an idol affix of this kind shows up (ordinary, weaver, or enchanted).</summary>
    public static double IdolAffixChance(string kind)
    {
        switch (kind)
        {
            case "IdolWeaver": return 0.06;
            case "IdolEnchantment": return 0.03;
            default: return 0.8;
        }
    }

    /// <summary>Hours of farming to find something that turns up with this chance per item looked at.</summary>
    public static double HoursToFind(double chancePerItem, double itemsPerHour)
    {
        if (chancePerItem <= 0 || itemsPerHour <= 0)
        {
            return MaxHours;
        }
        return Math.Min(MaxHours, 1.0 / (chancePerItem * itemsPerHour));
    }

    public static double Gold(double hours) => Math.Min(hours, MaxHours) * GoldPerHour;

    /// <summary>
    /// What a find like this is worth in gold: every wanted affix must turn up together, so their chances multiply.
    /// <paramref name="affixChances" /> is, for each affix, its tier chance divided by how scarce that particular affix is.
    /// </summary>
    public static double GearAffixGold(IEnumerable<double> affixChances, double itemsPerHour)
    {
        double chance = 1;
        int n = 0;
        foreach (double c in affixChances)
        {
            chance *= Math.Clamp(c, 1e-9, 1.0);
            n++;
        }
        if (n == 0)
        {
            return 0;
        }
        return Gold(HoursToFind(chance, itemsPerHour));
    }

    /// <summary>
    /// The value of one unique in gold: how long the right unique with at least this much potential takes to find.
    /// <paramref name="scarcity" /> is 1 for an ordinary unique and grows with how often the game rerolls it;
    /// <paramref name="specificity" /> is more than 1 when the drop is harder to aim at (a boss, a primordial).
    /// </summary>
    public static double UniqueGold(double potentialChance, double scarcity, double specificity)
    {
        double hours = scarcity * specificity / (UniqueRate * Math.Max(1e-9, potentialChance));
        return Gold(hours);
    }

    // How the farming-time value of a piece of gear turns into a market price. Gear that is rare to find is mostly
    // a crafting feeder (the buyer still has to finish it), so past the first rare affix the price grows slowly:
    // each pair of lines is (raw farming-time gold, market price). Points between are joined on a log-log line.
    private static readonly (double Raw, double Price)[] GearScale =
    {
        (1, 1),
        (100_000, 100_000), // below this, price = value
        (17_800_000, 250_000), // two T7: a feeder, a couple of hundred thousand
        (2.4e9, 1_500_000), // three T7
        (3e11, 6_000_000), // four T7: under ten million
        (3e12, 25_000_000), // four T7 and a T8
        (5e13, 50_000_000), // the very top
    };

    /// <summary>Market price of gear from its raw farming-time value.</summary>
    public static double CompressGear(double raw)
    {
        if (raw <= GearScale[1].Raw)
        {
            return Math.Max(0, raw);
        }
        for (int i = 2; i < GearScale.Length; i++)
        {
            if (raw <= GearScale[i].Raw)
            {
                var (r0, p0) = GearScale[i - 1];
                var (r1, p1) = GearScale[i];
                double t = Math.Log(raw / r0) / Math.Log(r1 / r0);
                return p0 * Math.Pow(p1 / p0, t);
            }
        }
        return MaxPrice;
    }

    /// <summary>
    /// Market price of a unique from its farming-time value: unchanged for everyday prices, bending smoothly so
    /// nothing passes the ceiling.
    /// </summary>
    public static double CompressUnique(double gold) => gold <= 0 ? 0 : MaxPrice * (1 - Math.Exp(-gold / MaxPrice));
}
