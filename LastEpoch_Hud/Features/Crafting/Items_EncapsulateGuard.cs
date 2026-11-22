using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// Keeps a encapsulated affix as it is when the forge crafts. The forge is left to do its work and the affix is put back
/// afterwards, with its tier and roll, and whatever replaced it is removed. Removing a encapsulated affix gives no shard and
/// the item cannot be shattered. The affix is marked in the item's tooltip.
/// </summary>
internal static class Items_EncapsulateGuard
{
    private sealed class Snapshot
    {
        public ItemData Item;
        public int AffixId;
        public byte Tier, Roll;
        public int OrdinaryCount;
        public HashSet<int> Ids = new();
    }

    private static Snapshot _snapshot;

    private static int Ordinary(ItemData item) => item.affixes.ToArray().Count(a => a != null && !a.IsSealed);

    [HarmonyPatch(typeof(CraftingManager), nameof(CraftingManager.Forge))]
    private static class ForgePatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            _snapshot = null;
            try
            {
                ItemData item = Items_Encapsulation.ForgeItem();
                string blocked = CraftProtection.Blocked(item);
                if (blocked.Length != 0)
                {
                    Items_RecombinatorWindow.Notify(blocked);
                    return false;
                }
                ItemAffix encapsulated = Items_Encapsulate.EncapsulatedOf(item);
                if (encapsulated == null)
                {
                    return true;
                }
                _snapshot = new Snapshot
                {
                    Item = item,
                    AffixId = encapsulated.affixId,
                    Tier = encapsulated.affixTier,
                    Roll = encapsulated.affixRoll,
                    OrdinaryCount = Ordinary(item),
                    Ids = new HashSet<int>(item.affixes.ToArray().Where(a => a != null).Select(a => (int)a.affixId)),
                };
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Encapsulation protection (before): " + ex.Message);
            }
            return true;
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            Snapshot s = _snapshot;
            _snapshot = null;
            if (s == null)
            {
                return;
            }
            try
            {
                Restore(s);
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Encapsulation protection (after): " + ex.Message);
            }
        }
    }

    private static void Restore(Snapshot s)
    {
        ItemData item = s.Item;
        if (item.IsNullOrDestroyed())
        {
            return;
        }
        ItemAffix current = item.affixes.ToArray().FirstOrDefault(a => a != null && a.affixId == s.AffixId);
        bool changed = false;
        if (current != null && current.IsSealed)
        {
            item.affixes.Remove(current);
            current = null;
        }
        if (current != null)
        {
            if (current.affixTier != s.Tier || current.affixRoll != s.Roll)
            {
                current.affixTier = s.Tier;
                current.affixRoll = s.Roll;
                changed = true;
            }
        }
        else
        {
            // it was removed or replaced: put it back and take away what took its place
            item.affixes.Add(new ItemAffix((ushort)s.AffixId, s.Tier, s.Roll, item.itemType, SealedAffixType.None));
            foreach (ItemAffix extra in item.affixes.ToArray().Reverse())
            {
                if (Ordinary(item) <= s.OrdinaryCount)
                {
                    break;
                }
                if (extra != null && !extra.IsSealed && extra.affixId != s.AffixId && !s.Ids.Contains(extra.affixId))
                {
                    item.affixes.Remove(extra);
                }
            }
            changed = true;
        }
        if (changed)
        {
            item.RefreshIDAndValues();
            Items_RecombinatorWindow.Notify("An encapsulated affix cannot be changed.");
            Main.logger_instance?.Msg("Encapsulation protection: restored encapsulated affix " + s.AffixId + ".");
        }
    }

    // ------------------------------------------------------------------ no shards, no shattering

    [HarmonyPatch(typeof(CraftingManager), nameof(CraftingManager.GainShardsFromRemovedAffix))]
    private static class NoShardPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemAffix affix)
        {
            try
            {
                return !Items_Encapsulate.IsEncapsulated(Items_Encapsulation.ForgeItem(), affix);
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(CraftingManager), nameof(CraftingManager.Shatter))]
    private static class NoShatterPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData item)
        {
            if (Items_Encapsulate.EncapsulatedAffix(item) < 0)
            {
                return true;
            }
            Items_RecombinatorWindow.Notify("An item with an encapsulated affix cannot be shattered.");
            return false;
        }
    }

    // ------------------------------------------------------------------ marking it in the tooltip
}
