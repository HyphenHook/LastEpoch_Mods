using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>The flasks a slot dropdown offers and which picks swap two flasks.</summary>
public static class MagebloodSlotOptions
{
    /// <summary>Default flasks in defaults order, then custom ones in file order. Defaults missing from the file are skipped.</summary>
    public static IReadOnlyList<string> Order(IReadOnlyList<string> flaskNames)
    {
        var ordered = new List<string>(flaskNames.Count);
        foreach (MagebloodFlaskEntry entry in MagebloodConfigDefaults.Flasks)
        {
            if (flaskNames.Contains(entry.Name))
            {
                ordered.Add(entry.Name);
            }
        }
        foreach (string name in flaskNames)
        {
            if (!ordered.Contains(name))
            {
                ordered.Add(name);
            }
        }
        return ordered;
    }

    /// <summary>True when <paramref name="chosen"/> is another known flask; <paramref name="current"/> is the slot's flask.</summary>
    public static bool TryPickSwap(
        IReadOnlyList<string> flaskNames,
        int slot,
        string chosen,
        out string current
    )
    {
        current = null;
        if (slot < 0 || slot >= flaskNames.Count || slot >= MagebloodFlaskSlots.Max)
        {
            return false;
        }
        if (chosen == null || chosen == flaskNames[slot] || !flaskNames.Contains(chosen))
        {
            return false;
        }

        current = flaskNames[slot];
        return true;
    }
}
