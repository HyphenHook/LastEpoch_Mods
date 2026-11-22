using System;
using System.Collections.Generic;
using System.Linq;

namespace LastEpoch_Hud.Scripts.Core.Economy;

/// <summary>
/// What an item's forging potential is worth to whoever buys it. Forging potential is a crafting budget: each
/// craft costs some of it (typically 1 to 18, about 9 on average) and either fills an empty affix slot or
/// upgrades an existing affix. So its worth is not a function of the number alone. It depends on what there is
/// still to do with it: an item with no open slots and nothing left to upgrade gains nothing from 60 points,
/// while a clean base with four open slots turns each craft into a new affix.
/// </summary>
public static class CraftingMath
{
    public const double AverageCraftCost = 9;
    public const double SuccessRate = 0.75; // crafts can fail or roll badly

    /// <summary>
    /// The expected gold value of spending the forging potential: the best available crafts, in order of gain,
    /// as many as the budget affords.
    /// </summary>
    /// <param name="forgingPotential">Points left to spend.</param>
    /// <param name="openSlots">Empty affix slots that could take a new affix.</param>
    /// <param name="slotFillGain">Value of a typical new affix added to an open slot.</param>
    /// <param name="upgradeGains">Value gained by upgrading each existing craftable affix one step.</param>
    /// <param name="potentialKindFactor">More than 1 for Ice or Blood forging potential.</param>
    public static double Potential(
        double forgingPotential,
        int openSlots,
        double slotFillGain,
        IReadOnlyList<double> upgradeGains,
        double potentialKindFactor = 1.0
    )
    {
        if (forgingPotential <= 0)
        {
            return 0;
        }
        var actions = new List<double>();
        for (int i = 0; i < Math.Max(0, openSlots); i++)
        {
            actions.Add(slotFillGain);
        }
        if (upgradeGains != null)
        {
            actions.AddRange(upgradeGains.Where(g => g > 0));
        }
        actions.Sort((a, b) => b.CompareTo(a));

        double crafts = forgingPotential / AverageCraftCost; // a fractional last craft counts for its fraction
        double total = 0;
        foreach (double gain in actions)
        {
            if (crafts <= 0)
            {
                break;
            }
            double used = Math.Min(1.0, crafts);
            total += gain * used;
            crafts -= used;
        }
        return total * SuccessRate * potentialKindFactor;
    }
}
