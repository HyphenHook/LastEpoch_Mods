using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

// Display tiers are one-based; bits and native tier indexes are zero-based.
// A mask preserves holes in native outcome weights, unlike a single maximum.
public static class ForceDropLegalTiers
{
    // Rune of Corruption thresholds from EHG's 1.5 item-corruption rules.
    // Pre-corrupted area-drop thresholds (50/80) are a different route.
    public static int RuneMaximumDisplayTier(bool forgeableEquipment, int levelRequirement) =>
        !forgeableEquipment || levelRequirement >= 55 ? 7
        : levelRequirement >= 35 ? 6
        : 5;

    public static int FromWeights(IEnumerable<float> weights, int definitionTiers, int nativeMask)
    {
        if (weights == null)
            return 0;
        int result = 0,
            tier = 0;
        foreach (float weight in weights)
        {
            if (tier >= Math.Min(7, definitionTiers))
                break;
            if (weight > 0 && !float.IsNaN(weight) && (nativeMask & (1 << tier)) != 0)
                result |= 1 << tier;
            tier++;
        }
        return result;
    }

    public static bool Supports(int mask, int tier) =>
        tier >= 0 && tier < 7 && (mask & (1 << tier)) != 0;

    public static int MaximumDisplayTier(int mask)
    {
        for (int tier = 6; tier >= 0; tier--)
            if (Supports(mask, tier))
                return tier + 1;
        return 0;
    }

    public static int ClampDisplayTier(int mask, int displayTier)
    {
        for (int tier = Math.Min(6, displayTier - 1); tier >= 0; tier--)
            if (Supports(mask, tier))
                return tier + 1;
        for (int tier = 0; tier < 7; tier++)
            if (Supports(mask, tier))
                return tier + 1;
        return 0;
    }
}

public enum ForceDropAffixFamily
{
    Standard,
    Experimental,
    Personal,
    Set,
    IdolEnchantment,
    IdolWeaver,
    Corrupted,
    UniqueModifier,
    Unknown,
}

public static class ForceDropLegalRules
{
    public static bool OrdinaryFamilyAllowed(
        ForceDropAffixFamily family,
        bool legendaryTransfer,
        bool champion,
        bool idol,
        bool weaverIdol = false,
        bool enchantment = false
    )
    {
        if (idol)
            return !legendaryTransfer
                && (
                    enchantment
                        ? family == ForceDropAffixFamily.IdolEnchantment
                        : family == ForceDropAffixFamily.Standard
                            || (weaverIdol && family == ForceDropAffixFamily.IdolWeaver)
                );
        if (family == ForceDropAffixFamily.Standard || family == ForceDropAffixFamily.Experimental)
            return true;
        if (family == ForceDropAffixFamily.Set)
            return !legendaryTransfer;
        if (family == ForceDropAffixFamily.Personal)
            return !legendaryTransfer || champion;
        return false;
    }

    public static bool SlotAllowed(
        int slot,
        bool idol,
        bool unique,
        bool setItem,
        bool heretical = false
    )
    {
        if (slot < 0 || slot >= AffixLimits.ForceDropRows)
            return false;
        // Sets take no ordinary affixes, but the regular and Primordial seals work on every
        // rarity. Idols take no seals. Every ordinary slot takes any affix, prefix or suffix;
        // on Heretical idols the second and fourth rows (slots 1 and 3) hold the enchantments.
        if (setItem)
            return AffixLimits.IsSealedSlot(slot);
        if (idol)
            return !unique && AffixLimits.IsOrdinarySlot(slot);
        return true;
    }
}
