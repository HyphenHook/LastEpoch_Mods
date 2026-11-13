using System;
using System.Collections.Generic;
using System.Linq;

namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>One ordinary affix offered to the recombinator, from item A or item B.</summary>
public sealed class RecombAffix
{
    public int AffixId { get; init; }
    public int DisplayedTier { get; init; } // 1..7
    public double Weight { get; init; } // the game's roll weight for this affix: higher is commoner
    public int Source { get; init; } // 0 = item A, 1 = item B
    public bool LegalOnA { get; init; } = true; // may this affix sit on item A's base?
    public bool LegalOnB { get; init; } = true; // and on item B's base?
    public bool LegalOn(int baseIndex) => baseIndex == 0 ? LegalOnA : LegalOnB;
    public bool IsExalted => DisplayedTier >= 6;
}

/// <summary>
/// The rules of the recombinator. Two items of the same base are merged into one that keeps the base of either
/// (equal chance) and a mix of their ordinary affixes. In the indeterministic mode the mix is drawn by the game's
/// roll weights. In the deterministic mode you pick the affixes you want and are told the chance of getting
/// exactly those; on a failure the result is an indeterministic one. Cost grows with how many affixes there are,
/// their tiers and how exalted the items are.
/// </summary>
public static class RecombinatorMath
{
    /// <summary>Ordinary affix slots on an item before any seal.</summary>
    public const int MaxAffixes = 4;

    /// <summary>
    /// Ordinary affixes the result can hold on this base. The result keeps the base item whole, including its
    /// sealed and primordial affixes: a regular seal uses one of the four slots, a primordial seal has its own.
    /// </summary>
    public static int Capacity(bool baseHasRegularSeal) => MaxAffixes - (baseHasRegularSeal ? 1 : 0);

    // Chance that one chosen affix of this tier survives the merge (T1..T7).
    private static readonly double[] TierSurvival = { 0, 0.95, 0.95, 0.95, 0.90, 0.80, 0.55, 0.40 };

    // Keeping more affixes at once is harder (1..4 chosen).
    private static readonly double[] CountFactor = { 0, 1.0, 0.92, 0.80, 0.65 };

    // Cost units per affix by tier: higher tiers are worth much more.
    private static readonly double[] TierUnit = { 0, 1, 1.5, 2.5, 4, 7, 20, 50 };

    public const double GoldPerUnit = 500;
    public const double UnitsPerEmber = 5;
    public const double ExaltedItemFactor = 1.5;

    /// <summary>Gold and Soul Embers for the indeterministic mode: a flat fee that ignores what the items hold.</summary>
    public const long FlatGold = 5_000;
    public const int FlatEmbers = 3;

    private static int Clamp(int tier) => Math.Clamp(tier, 1, 7);

    /// <summary>Why this selection cannot be made, or an empty string when it can.</summary>
    public static string Validate(IReadOnlyList<RecombAffix> selected, int capacity = MaxAffixes)
    {
        if (selected == null || selected.Count == 0)
        {
            return "Pick at least one affix.";
        }
        if (selected.Count > Math.Min(MaxAffixes, capacity))
        {
            return "At most " + Math.Min(MaxAffixes, capacity) + " affixes.";
        }
        if (selected.Select(a => a.AffixId).Distinct().Count() != selected.Count)
        {
            return "The same affix cannot be picked twice.";
        }
        return "";
    }

    /// <summary>Chance that the chosen affixes all survive the merge, once the base is settled.</summary>
    public static double SurvivalChance(IReadOnlyList<RecombAffix> selected)
    {
        if (selected == null || selected.Count == 0 || selected.Count > MaxAffixes)
        {
            return 0;
        }
        double survive = CountFactor[selected.Count];
        foreach (RecombAffix a in selected)
        {
            survive *= TierSurvival[Clamp(a.DisplayedTier)];
        }
        return survive;
    }

    /// <summary>
    /// Chance, 0 to 1, that a deterministic recombination yields exactly the chosen affixes. The base is picked at
    /// random (half each), and the result is only right when every chosen affix can sit on the base that wins and
    /// fits in its slots; the chosen affixes must then survive the merge.
    /// </summary>
    public static double SuccessChance(IReadOnlyList<RecombAffix> selected, int capacityA = MaxAffixes, int capacityB = MaxAffixes)
    {
        if (Validate(selected, Math.Max(capacityA, capacityB)).Length > 0)
        {
            return 0;
        }
        double survive = SurvivalChance(selected);
        double p = 0;
        for (int b = 0; b < 2; b++)
        {
            int capacity = b == 0 ? capacityA : capacityB;
            if (selected.Count <= capacity && selected.All(a => a.LegalOn(b)))
            {
                p += 0.5 * survive;
            }
        }
        return Math.Clamp(p, 0, 1);
    }

    /// <summary>How many affixes the result of an indeterministic merge holds: near the average of the two items.</summary>
    public static int ResultCount(int countA, int countB, int poolSize, Func<double> rng, int capacity = MaxAffixes)
    {
        int max = Math.Min(Math.Min(MaxAffixes, capacity), Math.Max(1, poolSize));
        double mean = (Math.Max(0, countA) + Math.Max(0, countB)) / 2.0;
        var weights = new double[max];
        for (int c = 1; c <= max; c++)
        {
            double d = c - mean;
            weights[c - 1] = Math.Exp(-d * d / (2 * 0.9 * 0.9));
        }
        double r = rng() * weights.Sum();
        for (int i = 0; i < max; i++)
        {
            r -= weights[i];
            if (r <= 0)
            {
                return i + 1;
            }
        }
        return max;
    }

    /// <summary>
    /// Draws up to <paramref name="count" /> different affixes from the pool, each pick weighted by the affix's own
    /// roll weight. An affix on both items appears once.
    /// </summary>
    public static List<RecombAffix> Draw(IReadOnlyList<RecombAffix> pool, int count, Func<double> rng)
    {
        var left = pool
            .GroupBy(a => a.AffixId)
            .Select(g => g.OrderBy(_ => rng()).First()) // the copy on either item, picked at random
            .ToList();
        var result = new List<RecombAffix>();
        while (result.Count < count && left.Count > 0)
        {
            double total = left.Sum(a => Math.Max(1e-6, a.Weight));
            double r = rng() * total;
            RecombAffix pick = left[left.Count - 1];
            foreach (RecombAffix a in left)
            {
                r -= Math.Max(1e-6, a.Weight);
                if (r <= 0)
                {
                    pick = a;
                    break;
                }
            }
            result.Add(pick);
            left.Remove(pick);
        }
        return result;
    }

    /// <summary>Equal chance for the base of item A or item B: returns 0 or 1.</summary>
    public static int PickBase(Func<double> rng) => rng() < 0.5 ? 0 : 1;

    /// <summary>Cost units of the affixes on both items.</summary>
    public static double Units(IEnumerable<RecombAffix> bothItemsAffixes) =>
        (bothItemsAffixes ?? Enumerable.Empty<RecombAffix>()).Sum(a => TierUnit[Clamp(a.DisplayedTier)]);

    /// <summary>Gold cost of a deterministic recombination of these two items.</summary>
    public static long Gold(IReadOnlyList<RecombAffix> bothItemsAffixes, bool anyExalted)
    {
        double g = Units(bothItemsAffixes) * GoldPerUnit * (anyExalted ? ExaltedItemFactor : 1.0);
        return (long)Math.Ceiling(g);
    }

    /// <summary>Soul Ember cost of a deterministic recombination of these two items.</summary>
    public static int Embers(IReadOnlyList<RecombAffix> bothItemsAffixes, bool anyExalted)
    {
        double e = Units(bothItemsAffixes) / UnitsPerEmber * (anyExalted ? ExaltedItemFactor : 1.0);
        return Math.Max(1, (int)Math.Ceiling(e));
    }
}
