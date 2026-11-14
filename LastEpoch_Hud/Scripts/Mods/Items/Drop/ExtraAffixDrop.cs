using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.Mods.Economy;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

/// <summary>
/// Items with five or six ordinary affixes, never more than three prefixes or three suffixes. A full four-affix drop
/// may gain a fifth affix, and a five-affix item a sixth, by the chances set on the Drop page. Whatever the game or the
/// Affix Count setting rolled is also brought back within the three-and-three limit. The seals, the primordial affix
/// and the corruption affix are not counted among the six.
/// </summary>
internal static class ExtraAffixDrop
{
    private static readonly Random Rng = new();

    private static bool Ready() =>
        Scenes.IsGameScene()
        && !Save_Manager.instance.IsNullOrDestroyed()
        && !Save_Manager.instance.data.IsNullOrDestroyed()
        && Save_Manager.instance.data.Items.Drop.Enable_ExtraAffixes;

    /// <summary>Counts the item's ordinary affixes: how many, and how many of them are prefixes and suffixes.</summary>
    public static int Count(ItemDataUnpacked item, out int prefixes, out int suffixes)
    {
        prefixes = 0;
        suffixes = 0;
        int ordinary = 0;
        if (item?.affixes == null)
        {
            return 0;
        }
        foreach (ItemAffix a in item.affixes.ToArray())
        {
            if (a == null || a.IsSealed)
            {
                continue;
            }
            ordinary++;
            if (a.affixType == AffixList.AffixType.PREFIX)
            {
                prefixes++;
            }
            else if (a.affixType == AffixList.AffixType.SUFFIX)
            {
                suffixes++;
            }
        }
        return ordinary;
    }

    /// <summary>Gives a drop its extra affixes by chance, then keeps it within three prefixes and three suffixes.</summary>
    public static void Apply(ItemDataUnpacked item)
    {
        if (item.IsNullOrDestroyed() || item.affixes == null || item.itemType >= 25 || item.rarity >= 7)
        {
            return;
        }
        var drop = Save_Manager.instance.data.Items.Drop;
        double fifth = Math.Clamp(drop.ExtraAffix5Chance, 0f, 100f) / 100.0;
        double sixth = Math.Clamp(drop.ExtraAffix6Chance, 0f, 100f) / 100.0;

        int ordinary = Count(item, out _, out _);
        int target = ordinary;
        if (ordinary == 4 && Rng.NextDouble() < fifth)
        {
            target = 5;
        }
        if (target == 5 && Rng.NextDouble() < sixth)
        {
            target = 6;
        }
        bool changed = false;
        if (target > ordinary)
        {
            changed |= GrowTo(item, target) > 0;
        }
        changed |= Balance(item);
        if (changed)
        {
            Finish(item);
        }
    }

    private static void Finish(ItemDataUnpacked item)
    {
        int ordinary = Count(item, out _, out _);
        item.sockets = (byte)item.affixes.Count;
        item.rarity = AffixLimits.LoadableRarity(ordinary); // the game cannot load rarity 5 or 6; the affixes stay
        item.RefreshIDAndValues();
    }

    /// <summary>Adds ordinary affixes until the item holds <paramref name="target" />. Returns how many were added.</summary>
    public static int GrowTo(ItemDataUnpacked item, int target)
    {
        int added = 0;
        try
        {
            int ordinary = Count(item, out int prefixes, out int suffixes);
            while (ordinary < Math.Min(target, AffixLimits.MaxOrdinary))
            {
                AffixList.Affix def = PickLegal(item, prefixes, suffixes);
                if (def == null)
                {
                    break;
                }
                item.affixes.Add(NewAffix(item, def));
                ordinary++;
                added++;
                if (def.type == AffixList.AffixType.PREFIX)
                {
                    prefixes++;
                }
                else if (def.type == AffixList.AffixType.SUFFIX)
                {
                    suffixes++;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Extra affixes");
        }
        return added;
    }

    /// <summary>
    /// If one side holds more than three, the extra affixes are swapped for legal ones of the other side.
    /// Returns true when something changed.
    /// </summary>
    public static bool Balance(ItemDataUnpacked item)
    {
        bool changed = false;
        try
        {
            for (int pass = 0; pass < 6; pass++)
            {
                Count(item, out int prefixes, out int suffixes);
                AffixList.AffixType over;
                if (prefixes > AffixLimits.MaxPerSide)
                {
                    over = AffixList.AffixType.PREFIX;
                }
                else if (suffixes > AffixLimits.MaxPerSide)
                {
                    over = AffixList.AffixType.SUFFIX;
                }
                else
                {
                    break;
                }
                ItemAffix extra = item.affixes.ToArray().LastOrDefault(a => a != null && !a.IsSealed && a.affixType == over);
                if (extra == null)
                {
                    break;
                }
                item.affixes.Remove(extra);
                // count again without it, then put in an affix of the side with room
                Count(item, out prefixes, out suffixes);
                AffixList.Affix def = PickLegal(item, prefixes, suffixes);
                if (def == null)
                {
                    break; // nothing legal: the item simply holds one fewer
                }
                item.affixes.Add(NewAffix(item, def));
                changed = true;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Extra affixes balance");
        }
        return changed;
    }

    private static AffixList.Affix PickLegal(ItemDataUnpacked item, int prefixes, int suffixes)
    {
        int baseType = item.itemType;
        int subType = item.subType;
        List<AffixList.Affix> pool = BazaarCustomItems.Pool(baseType, subType, false, false);
        var existing = item.affixes.ToArray().Where(a => a != null).Select(a => (int)a.affixId).ToList();
        var legal = new ForceDropLegalAffixes(baseType, subType, 2, existing);
        for (int tries = 0; tries < 120 && pool.Count > 0; tries++)
        {
            AffixList.Affix def = BazaarCustomItems.WeightedPick(pool, Rng);
            if (def == null || def.specialAffixType != AffixList.SpecialAffixType.Standard || existing.Contains(def.affixId))
            {
                continue;
            }
            if (def.type == AffixList.AffixType.PREFIX && prefixes >= AffixLimits.MaxPerSide)
            {
                continue;
            }
            if (def.type == AffixList.AffixType.SUFFIX && suffixes >= AffixLimits.MaxPerSide)
            {
                continue;
            }
            if (legal.OrdinaryReason(def, false, false).Length == 0)
            {
                return def;
            }
        }
        return null;
    }

    /// <summary>A new affix about as strong as what the item already has: the average tier, give or take one.</summary>
    private static ItemAffix NewAffix(ItemDataUnpacked item, AffixList.Affix def)
    {
        var tiers = item.affixes.ToArray().Where(a => a != null && !a.IsSealed).Select(a => a.affixTier + 1).ToList();
        int average = tiers.Count == 0 ? 3 : (int)Math.Round(tiers.Average());
        int most = def.tiers == null ? 1 : Math.Min(def.tiers.Count, 7);
        int tier = Math.Clamp(average + Rng.Next(-1, 2), 1, Math.Max(1, most));
        return new ItemAffix((ushort)def.affixId, (byte)(tier - 1), (byte)BazaarCustomItems.Roll(Rng, 0.5), (byte)item.itemType, SealedAffixType.None);
    }

    [HarmonyPatch(typeof(GenerateItems), "DropItemAtPoint")]
    public class DropItemAtPoint_ExtraAffixes
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        static void Prefix(ref ItemDataUnpacked __0)
        {
            try
            {
                if (Hud_Manager.IsPauseOpen() && ModUI.ForceDrop.ForceDropModel.Enabled)
                {
                    return; // items made by Force Drop are exactly what was asked for
                }
                if (Ready())
                {
                    Apply(__0);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Extra affixes drop");
            }
        }
    }
}
