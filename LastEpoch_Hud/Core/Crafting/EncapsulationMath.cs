using System;

using LastEpoch_Hud.Scripts.Core.Tuning;
namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>
/// The rules of the Orb of Encapsulation. Used on an item it encapsulates one of the item's ordinary affixes, picked at
/// random with every affix equally likely. A encapsulated affix is held: no crafting may change or remove it. An item
/// can be encapsulated once, and needs at least four ordinary affixes.
/// </summary>
public static class EncapsulationMath
{
    public static int MinAffixes => Tunables.GetInt("Encapsulation.MinAffixes", 4);

    /// <summary>Why the orb cannot be used on an item, or an empty string. Counts are ordinary affixes (not sealed).</summary>
    public static string WhyNot(int ordinaryAffixes, bool alreadyEncapsulated)
    {
        if (alreadyEncapsulated)
        {
            return "This item already has an encapsulated affix.";
        }
        if (ordinaryAffixes < MinAffixes)
        {
            return "The item needs at least four affixes.";
        }
        return "";
    }

    /// <summary>The index (0 to count-1) of the affix to encapsulate. Every affix is equally likely.</summary>
    public static int Pick(int ordinaryAffixes, Random rng)
    {
        if (ordinaryAffixes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinaryAffixes));
        }
        return rng.Next(ordinaryAffixes);
    }
}
