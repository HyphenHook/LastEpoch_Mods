using System;
using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>
/// The rules of the Memory Reflector. One affix of item B is reflected onto item A and B is destroyed, whether or
/// not it worked. An ordinary item needs all four normal affixes (A may also hold five) and the chance depends on how
/// full A is and on the tier of the affix chosen. A legendary item can only be reflected with the same legendary item
/// and the affix is picked at random; the more affixes B has the likelier it is, and the fuller A is the less likely.
/// </summary>
public static class ReflectorMath
{
    /// <summary>Ordinary affixes a full item holds before any extra ones.</summary>
    public const int FullItem = 4;

    /// <summary>Chance of a fifth affix on an item holding four, and of a sixth on an item holding five.</summary>
    public const double FifthAffixChance = 0.20;
    public const double SixthAffixChance = 0.10;

    // How much the tier of the reflected affix changes the odds (T1..T7): the higher the tier, the harder it is to reflect.
    private static readonly double[] TierFactor = { 0, 1.25, 1.20, 1.10, 1.00, 0.80, 0.55, 0.35 };

    /// <summary>Why this pair cannot be reflected, going by how many ordinary affixes each holds. Empty when it can.</summary>
    public static string WhyNot(int itemAAffixes, int itemBAffixes)
    {
        if (itemAAffixes < FullItem)
        {
            return "The first item must have all four normal affixes.";
        }
        if (itemBAffixes < 1)
        {
            return "The second item has no affix to give.";
        }
        if (itemAAffixes >= AffixLimits.MaxOrdinary)
        {
            return "The first item already has six affixes.";
        }
        return "";
    }

    /// <summary>Chance, 0 to 1, that the chosen affix lands on an ordinary item holding this many affixes.</summary>
    public static double Chance(int itemAAffixes, int displayedTier)
    {
        double baseChance = itemAAffixes == FullItem ? FifthAffixChance : itemAAffixes == FullItem + 1 ? SixthAffixChance : 0;
        return Math.Clamp(baseChance * TierFactor[Math.Clamp(displayedTier, 1, 7)], 0, 1);
    }

    // ------------------------------------------------------------ legendary items

    /// <summary>Why this pair of legendary items cannot be reflected. Empty when it can.</summary>
    public static string LegendaryWhyNot(int itemAAffixes, int itemBAffixes)
    {
        if (itemAAffixes < 1 || itemBAffixes < 1)
        {
            return "Both legendary items need at least one affix.";
        }
        if (itemAAffixes >= AffixLimits.MaxOrdinary)
        {
            return "The first item already holds as many affixes as it can.";
        }
        return "";
    }

    // The chance of gaining one more affix by how many item A holds (1 to 5), when item B holds one.
    private static readonly double[] LegendaryByA = { 0, 0.20, 0.15, 0.10, 0.06, 0.03 };

    /// <summary>
    /// Chance that one of B's affixes lands on A. It falls as A fills up (2 to 3 affixes is 15%, 3 to 4 is 10%, 4 to 5 is
    /// 6%, 5 to 6 is 3%, for a B with one affix) and is boosted by the affixes on B: one affix is 100% of that, and
    /// each more adds 20% of it, up to double for a B with six.
    /// </summary>
    public static double LegendaryChance(int itemAAffixes, int itemBAffixes)
    {
        double byA = LegendaryByA[Math.Clamp(itemAAffixes, 1, 5)];
        double byB = 1.0 + 0.20 * (Math.Clamp(itemBAffixes, 1, 6) - 1);
        return Math.Clamp(byA * byB, 0, 1);
    }

    // ------------------------------------------------------------ cost

    // The Memory Amber a full item costs, before the tier of the affix and how full A is are added in. This is an
    // end-game price: it is meant to sit well above the Weaver's own shop.
    public const int AmberBase = 1500;
    public const long GoldBase = 150_000;
    public const int BonesBase = 5_000;

    // Extra cost by the tier of the reflected affix (T1..T7), as a share of the base.
    private static readonly double[] TierCost = { 0, 0, 0.05, 0.1, 0.25, 0.5, 0.9, 1.4 };

    private static double FullnessCost(int itemAAffixes) => itemAAffixes >= FullItem + 1 ? 1.4 : 1.0;

    public static int MemoryAmber(int itemAAffixes, int displayedTier) =>
        (int)Math.Ceiling(AmberBase * FullnessCost(itemAAffixes) * (1 + TierCost[Math.Clamp(displayedTier, 1, 7)]));

    public static long Gold(int itemAAffixes, int displayedTier) =>
        (long)Math.Ceiling(GoldBase * FullnessCost(itemAAffixes) * (1 + TierCost[Math.Clamp(displayedTier, 1, 7)]));

    public static int Bones(int itemAAffixes, int displayedTier) =>
        (int)Math.Ceiling(BonesBase * FullnessCost(itemAAffixes) * (1 + TierCost[Math.Clamp(displayedTier, 1, 7)]));

    // Legendary items have no chosen affix, so cost follows how many affixes are involved instead.
    public static int LegendaryMemoryAmber(int itemAAffixes, int itemBAffixes) =>
        (int)Math.Ceiling((1200 + 500.0 * itemBAffixes) * (itemAAffixes >= 4 ? 1.6 : 1.0));

    public static long LegendaryGold(int itemAAffixes, int itemBAffixes) =>
        (long)Math.Ceiling((100_000 + 50_000.0 * itemBAffixes) * (itemAAffixes >= 4 ? 1.6 : 1.0));

    public static int LegendaryBones(int itemAAffixes, int itemBAffixes) =>
        (int)Math.Ceiling((3_000 + 1_000.0 * itemBAffixes) * (itemAAffixes >= 4 ? 1.6 : 1.0));
}
