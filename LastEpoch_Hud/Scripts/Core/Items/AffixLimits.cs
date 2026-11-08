using System;

namespace LastEpoch_Hud.Scripts.Core.Items;

/// <summary>
/// The one place that defines how many affixes an item may hold. The total includes the
/// regular sealed, Primordial sealed and corruption affixes, so ordinary affixes get the rest.
/// </summary>
public static class AffixLimits
{
    /// <summary>Most affixes of any kind (ordinary, sealed, Primordial, corruption) on one item.</summary>
    public const int MaxTotal = 12;

    /// <summary>One slot each for the regular seal, the Primordial seal and the corruption affix.</summary>
    public const int SpecialSlots = 3;

    /// <summary>Ordinary affix slots; none of them is tied to a prefix or a suffix.</summary>
    public const int OrdinarySlots = MaxTotal - SpecialSlots;

    /// <summary>Force Drop row index of the regular sealed affix.</summary>
    public const int SealedSlot = OrdinarySlots;

    /// <summary>Force Drop row index of the Primordial sealed affix.</summary>
    public const int PrimordialSlot = OrdinarySlots + 1;

    /// <summary>Force Drop rows. Corruption has its own selector, not a row.</summary>
    public const int ForceDropRows = OrdinarySlots + 2;

    /// <summary>
    /// A non-unique item stores its ordinary affix count in its rarity byte, and 7 and above
    /// mean unique, set and legendary. Rarity therefore tops out here whatever the affix count.
    /// </summary>
    public const byte MaxCountRarity = 6;

    /// <summary>Rarity byte of a non-unique item that holds this many ordinary affixes.</summary>
    public static byte RarityForOrdinaryCount(int ordinaryCount) =>
        (byte)Math.Min(Math.Max(ordinaryCount, 0), MaxCountRarity);

    public static bool IsSealedSlot(int slot) => slot == SealedSlot || slot == PrimordialSlot;

    public static bool IsOrdinarySlot(int slot) => slot >= 0 && slot < OrdinarySlots;
}
