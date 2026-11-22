using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Pointer comparisons behind the renderer snapshot's restore decisions.</summary>
public static class HeadhunterRendererMatch
{
    /// <summary>True when both lists have the same length and equal ids slot by slot.</summary>
    public static bool SameIds(IReadOnlyList<IntPtr> current, IReadOnlyList<IntPtr> expected)
    {
        if (current == null || expected == null || current.Count != expected.Count)
        {
            return false;
        }

        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when the renderer was added to the same manager after the capture.</summary>
    public static bool IsFresh(
        IntPtr previousManager,
        IntPtr manager,
        IReadOnlyCollection<IntPtr> previousRenderers,
        IntPtr renderer
    )
    {
        if (previousManager == IntPtr.Zero || previousManager != manager)
        {
            return false;
        }

        foreach (IntPtr known in previousRenderers)
        {
            if (known == renderer)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when the captured block is still the one the manager holds (edited in place).</summary>
    public static bool ChangedInPlace(IntPtr captured, IntPtr current)
    {
        return captured != IntPtr.Zero && captured == current;
    }

    /// <summary>Restore step for the rarity field from captured, last tinted and current block ids.</summary>
    public static HeadhunterRarityRestore RarityRestore(
        IntPtr captured,
        IntPtr tinted,
        IntPtr current
    )
    {
        if (current == IntPtr.Zero || current != tinted)
        {
            return HeadhunterRarityRestore.Keep;
        }

        return captured == IntPtr.Zero || captured == current
            ? HeadhunterRarityRestore.Clear
            : HeadhunterRarityRestore.PutBack;
    }
}
