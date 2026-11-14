using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.Mods.Economy;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The chaos reroll: replaces an item's ordinary affixes with new legal ones. Sealed affixes and found-only
/// T6/T7 affixes stay (they cannot be crafted back). New affixes are craftable tiers drawn by the game's own
/// weights, and legal for the item's base. See <see cref="ChaosRerollMath" /> for the rules.
/// </summary>
internal static class Items_ChaosReroll
{
    private static readonly Random _rng = new();

    /// <summary>Rerolls the item in place. Returns how many affixes were replaced, or 0 with a reason.</summary>
    public static int Reroll(ItemData item, out string reason)
    {
        reason = "";
        if (item.IsNullOrDestroyed() || item.affixes == null)
        {
            reason = "No item.";
            return 0;
        }
        if (item.isUniqueSetOrLegendary())
        {
            reason = "Uniques, sets and legendaries cannot be rerolled.";
            return 0;
        }
        int baseType = item.itemType;
        int subType = item.subType;

        var replace = new List<ItemAffix>();
        foreach (ItemAffix a in item.affixes)
        {
            if (a != null && !ChaosRerollMath.IsKept(a.affixTier + 1, a.IsSealed))
            {
                replace.Add(a);
            }
        }
        if (replace.Count == 0)
        {
            reason = "Nothing to reroll: every affix is sealed or too high a tier.";
            return 0;
        }

        // The affixes that stay decide what the new ones may not repeat.
        var existing = new List<int>();
        foreach (ItemAffix a in item.affixes)
        {
            if (a != null && !replace.Contains(a))
            {
                existing.Add(a.affixId);
            }
        }

        // Choose every replacement first, so a failure leaves the item untouched.
        List<AffixList.Affix> pool = BazaarCustomItems.Pool(baseType, subType, false, false);
        var chosen = new List<(AffixList.Affix Def, int Tier, int Roll)>();
        for (int n = 0; n < replace.Count; n++)
        {
            var legal = new ForceDropLegalAffixes(baseType, subType, 2, existing);
            AffixList.Affix pick = null;
            for (int tries = 0; tries < 120 && pool.Count > 0; tries++)
            {
                AffixList.Affix def = BazaarCustomItems.WeightedPick(pool, _rng);
                if (def == null || def.specialAffixType != AffixList.SpecialAffixType.Standard || existing.Contains(def.affixId))
                {
                    continue;
                }
                if (legal.OrdinaryReason(def, false, false).Length == 0)
                {
                    pick = def;
                    break;
                }
            }
            if (pick == null)
            {
                reason = "No legal affix was found for this base.";
                return 0;
            }
            int tier = BazaarCustomItems.DisplayTier(pick, _rng, ChaosRerollMath.TierWeights);
            chosen.Add((pick, tier, _rng.Next(0, 256)));
            existing.Add(pick.affixId);
        }

        foreach (ItemAffix a in replace)
        {
            item.affixes.Remove(a);
        }
        foreach (var (def, tier, roll) in chosen)
        {
            item.affixes.Add(new ItemAffix((ushort)def.affixId, (byte)Math.Max(0, tier - 1), (byte)roll, (byte)baseType, SealedAffixType.None));
        }
        item.RefreshIDAndValues();
        return replace.Count;
    }
}
