using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Economy;

/// <summary>
/// How much an item's rolls are worth, as pure maths so it can be tested without the game.
///
/// A roll is a byte (0..255) that picks a value between a modifier's minimum and maximum. Whether the high
/// end is the good end depends on the stat: more damage is good, but a smaller drawback, a lower cost or a
/// shorter delay is good too, so for those the LOW roll is the better one. Quality is therefore always
/// "how good for the player", from 0 (worst possible roll) to 1 (best possible roll), never the raw byte.
/// </summary>
public static class RollMath
{
    /// <summary>0 = the worst roll this modifier can have, 1 = the best.</summary>
    public static double Quality(int roll, bool maxRollIsBest)
    {
        double q = Math.Clamp(roll / 255.0, 0, 1);
        return maxRollIsBest ? q : 1 - q;
    }

    /// <summary>
    /// Which end of a modifier's roll range is the good one, from its two end values and whether a smaller
    /// number is better for that stat (a cost, a delay, a drawback). The max roll is better when it gives the
    /// larger number, unless smaller is better, in which case it is better when it gives the smaller one.
    /// A penalty such as -30 to -10 is judged the same way: -10 is the larger number, so the high roll is good.
    /// </summary>
    public static bool MaxRollIsBestByValue(float valueAtMinRoll, float valueAtMaxRoll, bool lessIsBetter = false)
    {
        bool maxGivesLargerNumber = valueAtMaxRoll >= valueAtMinRoll;
        return lessIsBetter ? !maxGivesLargerNumber : maxGivesLargerNumber;
    }

    /// <summary>
    /// The price effect of one affix's roll. Convex, so the best rolls pay far more than middling ones: a
    /// perfect roll is worth about 3.4 times a worst roll of the same affix and tier.
    /// </summary>
    public static double AffixFactor(double quality)
    {
        double q = Math.Clamp(quality, 0, 1);
        return 0.45 + 1.1 * q * q;
    }

    /// <summary>The price effect of a unique's modifier rolls, 0.5 for the worst up to about 1.6 for the best.</summary>
    public static double UniqueFactor(double quality)
    {
        double q = Math.Clamp(quality, 0, 1);
        return 0.5 + 1.1 * Math.Pow(q, 1.5);
    }

    /// <summary>
    /// The price effect of a base's implicit rolls: 0.8 for the worst up to 1.3 for the best, and 1 when the
    /// base has no implicit stats (so there is nothing to roll).
    /// </summary>
    public static double ImplicitFactor(IReadOnlyList<double> implicitQualities)
    {
        if (implicitQualities == null || implicitQualities.Count == 0)
        {
            return 1.0;
        }
        return 0.8 + 0.5 * Average(implicitQualities);
    }

    /// <summary>
    /// Across a whole item: every affix rolled high is a different item from every affix rolled low. Only
    /// meaningful with three or more affixes.
    /// </summary>
    public static double ItemRollPremium(IReadOnlyList<double> affixQualities)
    {
        if (affixQualities == null || affixQualities.Count < 3)
        {
            return 1.0;
        }
        double average = Average(affixQualities);
        return average >= 0.9 ? 1.25 : average <= 0.25 ? 0.85 : 1.0;
    }

    /// <summary>
    /// The mean quality of a unique's modifiers. Modifiers that do not roll (their minimum and maximum are
    /// equal) say nothing about quality and are skipped; a unique with none that roll is simply average.
    /// </summary>
    public static double UniqueQuality(IEnumerable<(int Roll, float ValueAtMin, float ValueAtMax, bool MaxRollIsBest)> modifiers)
    {
        double sum = 0;
        int n = 0;
        foreach (var m in modifiers)
        {
            if (Math.Abs(m.ValueAtMax - m.ValueAtMin) < 1e-6f)
            {
                continue;
            }
            sum += Quality(m.Roll, m.MaxRollIsBest);
            n++;
        }
        return n == 0 ? 0.5 : sum / n;
    }

    public static double Average(IReadOnlyList<double> values)
    {
        if (values == null || values.Count == 0)
        {
            return 0.5;
        }
        double sum = 0;
        for (int i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }
        return sum / values.Count;
    }
}
