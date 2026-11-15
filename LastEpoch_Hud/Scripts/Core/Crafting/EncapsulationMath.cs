using System;

namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>
/// The rules of the Encapsulation Orb. Used on an item it fractures one of the item's ordinary affixes, picked at
/// random with every affix equally likely. A fractured affix is held: no crafting may change or remove it. An item
/// can be fractured once, and needs at least four ordinary affixes.
/// </summary>
public static class EncapsulationMath
{
    public const int MinAffixes = 4;

    /// <summary>Why the orb cannot be used on an item, or an empty string. Counts are ordinary affixes (not sealed).</summary>
    public static string WhyNot(int ordinaryAffixes, bool alreadyFractured)
    {
        if (alreadyFractured)
        {
            return "This item already has a fractured affix.";
        }
        if (ordinaryAffixes < MinAffixes)
        {
            return "The item needs at least four affixes.";
        }
        return "";
    }

    /// <summary>The index (0 to count-1) of the affix to fracture. Every affix is equally likely.</summary>
    public static int Pick(int ordinaryAffixes, Random rng)
    {
        if (ordinaryAffixes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinaryAffixes));
        }
        return rng.Next(ordinaryAffixes);
    }
}
