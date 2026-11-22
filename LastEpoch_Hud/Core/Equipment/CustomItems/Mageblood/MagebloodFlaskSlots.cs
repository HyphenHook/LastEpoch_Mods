using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

/// <summary>How many leftmost flasks a Mageblood affects, rolled per item.</summary>
public static class MagebloodFlaskSlots
{
    public const int Min = 2;
    public const int Max = 4;

    /// <summary>Index in the item's unique rolls that holds the slot roll.</summary>
    public const byte RollIndex = 0;

    /// <summary>Range shown in item-less views and in the description template.</summary>
    public const string RangeText = "(2-4)";

    /// <summary>Slot count from an item's unique rolls; false when the slot roll is missing.</summary>
    public static bool TryFromRolls(IList<byte> rolls, out int slots)
    {
        slots = 0;
        if (rolls == null || rolls.Count <= RollIndex)
        {
            return false;
        }

        slots = FromRoll(rolls[RollIndex]);
        return true;
    }

    /// <summary>Slot count for a unique roll, spread evenly over Min..Max.</summary>
    public static int FromRoll(byte roll)
    {
        return Min + ((roll * (Max - Min + 1)) / 256);
    }
}
