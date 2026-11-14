using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.Items;

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
    public int Placement { get; init; } // 0 neither (idols), 1 prefix, 2 suffix
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
    /// <summary>Ordinary affixes an item can hold: up to six, never more than three prefixes or three suffixes.</summary>
    public const int MaxAffixes = AffixLimits.MaxOrdinary;

    public const int MaxPerSide = AffixLimits.MaxPerSide;

    /// <summary>
    /// Ordinary affixes the result can hold on this base. The seals and the corruption affix are not counted among
    /// them, so a sealed base holds as many ordinary affixes as any other.
    /// </summary>
    public static int Capacity(bool baseHasRegularSeal) => MaxAffixes;

    // Chance that one chosen affix of this tier survives the merge (T1..T7).
    private static readonly double[] TierSurvival = { 0, 0.95, 0.95, 0.95, 0.90, 0.80, 0.55, 0.40 };

    // Keeping more affixes at once is harder (1..6 chosen). The steps are gentler than the per-affix chances, since
    // those already multiply: six affixes cost the same tier chance six times over.
    private static readonly double[] CountFactor = { 0, 1.0, 0.94, 0.86, 0.77, 0.68, 0.60 };

    // Cost units per affix by tier: higher tiers are worth much more.
    private static readonly double[] TierUnit = { 0, 1, 1.5, 2.5, 4, 7, 20, 50 };

    /// <summary>Primordial recombination: costs ancient bones and makes a deterministic merge 1.5 times as likely.</summary>
    public const double PrimordialBoost = 1.5;
    public const int PrimordialBaseBones = 2500;
    public const double BonesPerUnit = 100; // on top of the base, by the same units that price gold and embers

    public const double GoldPerUnit = 500;
    /// <summary>Soul Embers: a base fee plus a share of the cost units, so it always costs more than the Soul Ember shop (15 to 70+).</summary>
    public const int BaseEmbers = 40;
    public const double EmbersPerUnit = 1.5;
    public const double ExaltedItemFactor = 1.5;

    /// <summary>Gold and Soul Embers for the indeterministic mode: a flat fee that ignores what the items hold.</summary>
    public const long FlatGold = 5_000;
    public const int FlatEmbers = 60;

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
        if (selected.Count(a => a.Placement == 1) > MaxPerSide || selected.Count(a => a.Placement == 2) > MaxPerSide)
        {
            return "At most " + MaxPerSide + " prefixes and " + MaxPerSide + " suffixes.";
        }
        return "";
    }

    /// <summary>Chance that the chosen affixes all survive the merge, once the base is settled.</summary>
    public static double SurvivalChance(IReadOnlyList<RecombAffix> selected, bool primordial = false)
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
        return Math.Min(1.0, survive * (primordial ? PrimordialBoost : 1.0));
    }

    /// <summary>
    /// Chance, 0 to 1, that a deterministic recombination yields exactly the chosen affixes. The base is picked at
    /// random (half each), and the result is only right when every chosen affix can sit on the base that wins and
    /// fits in its slots; the chosen affixes must then survive the merge.
    /// </summary>
    public static double SuccessChance(IReadOnlyList<RecombAffix> selected, int capacityA = MaxAffixes, int capacityB = MaxAffixes, bool primordial = false)
    {
        if (Validate(selected, Math.Max(capacityA, capacityB)).Length > 0)
        {
            return 0;
        }
        double survive = SurvivalChance(selected, primordial);
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

    /// <summary>
    /// How many ordinary affixes the result holds: the average of the two items' counts, rounded (a half goes up or
    /// down with equal chance). With <paramref name="varied" /> the result usually keeps that count but sometimes
    /// holds one more or one fewer: 70% as averaged, 15% one fewer, 15% one more.
    /// </summary>
    public static int ResultCount(int countA, int countB, int poolSize, Func<double> rng, int capacity = MaxAffixes, bool varied = false)
    {
        int max = Math.Min(Math.Min(MaxAffixes, capacity), Math.Max(1, poolSize));
        int sum = Math.Max(0, countA) + Math.Max(0, countB);
        int count = sum / 2 + (sum % 2 == 1 && rng() < 0.5 ? 1 : 0);
        if (varied)
        {
            double r = rng();
            if (r < 0.15)
            {
                count--;
            }
            else if (r > 0.85)
            {
                count++;
            }
        }
        return Math.Clamp(count, 1, max);
    }

    /// <summary>
    /// Draws up to <paramref name="count" /> different affixes from the pool, each pick weighted by the affix's own
    /// roll weight. An affix on both items appears once. No more than three prefixes or three suffixes are ever
    /// drawn, counting the <paramref name="prefixes" /> and <paramref name="suffixes" /> the result already holds.
    /// </summary>
    public static List<RecombAffix> Draw(IReadOnlyList<RecombAffix> pool, int count, Func<double> rng, int prefixes = 0, int suffixes = 0)
    {
        var left = pool
            .GroupBy(a => a.AffixId)
            .Select(g => g.OrderBy(_ => rng()).First()) // the copy on either item, picked at random
            .ToList();
        var result = new List<RecombAffix>();
        while (result.Count < count && left.Count > 0)
        {
            left.RemoveAll(a => (a.Placement == 1 && prefixes >= MaxPerSide) || (a.Placement == 2 && suffixes >= MaxPerSide));
            if (left.Count == 0)
            {
                break;
            }
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
            if (pick.Placement == 1)
            {
                prefixes++;
            }
            else if (pick.Placement == 2)
            {
                suffixes++;
            }
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

    /// <summary>Ancient bones for a primordial recombination: a base amount plus the same scaling as gold.</summary>
    public static int Bones(IReadOnlyList<RecombAffix> bothItemsAffixes, bool anyExalted)
    {
        double b = Units(bothItemsAffixes) * BonesPerUnit * (anyExalted ? ExaltedItemFactor : 1.0);
        return PrimordialBaseBones + (int)Math.Ceiling(b);
    }

    /// <summary>Soul Ember cost of a deterministic recombination of these two items.</summary>
    public static int Embers(IReadOnlyList<RecombAffix> bothItemsAffixes, bool anyExalted)
    {
        double e = BaseEmbers + Units(bothItemsAffixes) * EmbersPerUnit * (anyExalted ? ExaltedItemFactor : 1.0);
        return (int)Math.Ceiling(e);
    }
}
