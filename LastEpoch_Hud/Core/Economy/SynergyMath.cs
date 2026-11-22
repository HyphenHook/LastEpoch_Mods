using System;
using System.Collections.Generic;
using System.Linq;

namespace LastEpoch_Hud.Scripts.Core.Economy;

/// <summary>
/// How well an item's affixes work together. A build wants a few things done very well, so an item whose
/// affixes all push the same way (say, spell damage on every line) is worth far more to that build than one
/// with the same total quality scattered over unrelated stats. Affixes are grouped by the game's own display
/// category; the biggest group sets the factor, and top-tier affixes count for more.
/// </summary>
public static class SynergyMath
{
    public const double MaxFactor = 3.5;

    /// <summary>1.0 for an item with no shared theme, rising steeply with how many affixes share one.</summary>
    public static double Factor(IEnumerable<(string Category, double Weight)> affixes)
    {
        if (affixes == null)
        {
            return 1.0;
        }
        double dominant = affixes
            .Where(a => !string.IsNullOrEmpty(a.Category))
            .GroupBy(a => a.Category)
            .Select(g => g.Sum(a => Math.Max(0, a.Weight)))
            .DefaultIfEmpty(0)
            .Max();
        if (dominant <= 1)
        {
            return 1.0;
        }
        return Math.Min(MaxFactor, 1 + 0.22 * Math.Pow(dominant - 1, 1.5));
    }
}
