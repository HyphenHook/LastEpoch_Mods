using System;

namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>
/// Rules of the chaos reroll: a number of Glyphs of Chaos buys a reroll of an item's ordinary affixes. Sealed
/// affixes and the found-only top tiers (T6 and above, which cannot be crafted back) are kept, and the new
/// affixes are craftable tiers only.
/// </summary>
public static class ChaosRerollMath
{
    /// <summary>Glyphs of Chaos spent on one reroll.</summary>
    public const int GlyphCost = 4;

    /// <summary>Highest displayed tier a reroll can produce; T6 and T7 can only be found.</summary>
    public const int MaxRerolledTier = 5;

    /// <summary>Odds of displayed tiers 1 to 7 for a rerolled affix: T6 and T7 are never given.</summary>
    public static readonly double[] TierWeights = { 6, 10, 16, 22, 22, 0, 0 };

    public static bool CanAfford(int glyphs) => glyphs >= GlyphCost;

    /// <summary>Is this affix protected from the reroll?</summary>
    public static bool IsKept(int displayedTier, bool isSealed) => isSealed || displayedTier > MaxRerolledTier;

    /// <summary>How many of the item's affixes a reroll would replace, given each one's tier and seal.</summary>
    public static int CountRerollable(System.Collections.Generic.IEnumerable<(int DisplayedTier, bool IsSealed)> affixes)
    {
        int n = 0;
        foreach (var (tier, sealedAffix) in affixes ?? Array.Empty<(int, bool)>())
        {
            if (!IsKept(tier, sealedAffix))
            {
                n++;
            }
        }
        return n;
    }
}
