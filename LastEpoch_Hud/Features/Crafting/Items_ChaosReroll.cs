using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.Mods.Economy;
using LastEpoch_Hud.Scripts.Mods.Items.Jewels;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static class Items_ChaosReroll
{
    private static readonly Random Rng = new();

    public static string WhyNot(ItemData item)
    {
        string blocked = CraftProtection.Blocked(item);
        if (blocked.Length != 0) return blocked;
        if (item.affixes == null) return "There are no rerollable affixes.";
        return item.affixes.ToArray().Any(a => Eligible(item, a)) ? "" : "There are no rerollable affixes.";
    }

    private static bool Eligible(ItemData item, ItemAffix affix) =>
        affix != null && !CraftProtection.IsAttached(item, affix)
        && (affix.affixType == AffixList.AffixType.PREFIX || affix.affixType == AffixList.AffixType.SUFFIX);

    public static bool UseOn(ItemData item, out string message)
    {
        message = WhyNot(item);
        if (message.Length != 0) return false;

        var ids = item.affixes.ToArray().Where(a => a != null).Select(a => (int)a.affixId).ToHashSet();
        var pool = BazaarCustomItems.Pool(item.itemType, item.subType, false, false);
        var replacements = new List<(int Index, ItemAffix Original, ItemAffix Replacement)>();
        for (int i = 0; i < item.affixes.Count; i++)
        {
            ItemAffix original = item.affixes[i];
            if (!Eligible(item, original)) continue;

            // Keep the position and side of every slot. Pick against the affixes that will remain and all new choices.
            var compatible = item.affixes.ToArray().Where(a => a != null && !Eligible(item, a)).Select(a => (int)a.affixId)
                .Concat(replacements.Select(p => (int)p.Replacement.affixId));
            var legal = new ForceDropLegalAffixes(item.itemType, item.subType, item.rarity, compatible);
            var candidates = pool.Where(a => a != null && a.type == original.affixType
                && a.specialAffixType == AffixList.SpecialAffixType.Standard
                && ChaosRerollMath.CanRollDefinition(JewelAffixes.MinimumTier(a.affixId))
                && !ids.Contains(a.affixId) && legal.OrdinaryReason(a, false, false).Length == 0).ToList();
            var choice = candidates.Count == 0 ? null : BazaarCustomItems.WeightedPick(candidates, Rng);
            if (choice == null)
            {
                message = "No legal replacement was found for every eligible affix.";
                return false;
            }

            int tier = Math.Clamp(BazaarCustomItems.DisplayTier(choice, Rng, ChaosRerollMath.TierWeights), 1, ChaosRerollMath.MaxRerolledTier);
            var replacement = new ItemAffix((ushort)choice.affixId, (byte)(tier - 1), (byte)Rng.Next(256), item.itemType, SealedAffixType.None);
            replacements.Add((i, original, replacement));
            ids.Add(choice.affixId);
        }

        if (!AdvancedMaterials.TryTake(AdvancedMaterials.ChaosOrb))
        {
            message = "You have no Orbs of Chaos.";
            return false;
        }

        byte originalRarity = item.rarity;
        try
        {
            foreach (var entry in replacements) item.affixes[entry.Index] = entry.Replacement;
            item.rarity = LastEpoch_Hud.Scripts.Core.Items.AffixLimits.LoadableRarity(item.affixes.ToArray().Count(a => a != null && !a.IsSealed));
            item.RefreshIDAndValues();
        }
        catch (Exception ex)
        {
            foreach (var entry in replacements) item.affixes[entry.Index] = entry.Original;
            item.rarity = originalRarity;
            AdvancedMaterials.Add(AdvancedMaterials.ChaosOrb, 1);
            item.RefreshIDAndValues();
            Main.logger_instance?.Warning("Orb of Chaos: " + ex.Message);
            message = "The Chaos reroll could not be applied.";
            return false;
        }

        message = $"Chaos rerolled {replacements.Count} affix{(replacements.Count == 1 ? "" : "es")}.";
        return true;
    }
}
