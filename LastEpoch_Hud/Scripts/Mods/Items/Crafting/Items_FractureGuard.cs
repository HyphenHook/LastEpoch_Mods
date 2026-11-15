using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// Keeps a fractured affix as it is when the forge crafts. The forge is left to do its work and the affix is put back
/// afterwards, with its tier and roll, and whatever replaced it is removed. Removing a fractured affix gives no shard and
/// the item cannot be shattered. The affix is marked in the item's tooltip.
/// </summary>
internal static class Items_FractureGuard
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
        private static void Prefix()
        {
            _snapshot = null;
            try
            {
                ItemData item = Items_Encapsulation.ForgeItem();
                ItemAffix fractured = Items_Fracture.FracturedOf(item);
                if (fractured == null)
                {
                    return;
                }
                _snapshot = new Snapshot
                {
                    Item = item,
                    AffixId = fractured.affixId,
                    Tier = fractured.affixTier,
                    Roll = fractured.affixRoll,
                    OrdinaryCount = Ordinary(item),
                    Ids = new HashSet<int>(item.affixes.ToArray().Where(a => a != null).Select(a => (int)a.affixId)),
                };
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Fracture guard (before): " + ex.Message);
            }
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
                Main.logger_instance?.Warning("Fracture guard (after): " + ex.Message);
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
            Items_RecombinatorWindow.Notify("A fractured affix cannot be changed.");
            Main.logger_instance?.Msg("Fracture guard: put the fractured affix " + s.AffixId + " back.");
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
                return !Items_Fracture.IsFractured(Items_Encapsulation.ForgeItem(), affix);
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
            if (Items_Fracture.FracturedAffix(item) < 0)
            {
                return true;
            }
            Items_RecombinatorWindow.Notify("An item with a fractured affix cannot be shattered.");
            return false;
        }
    }

    // ------------------------------------------------------------------ marking it in the tooltip

    [HarmonyPatch]
    private static class TooltipPatch
    {
        private static readonly System.Text.RegularExpressions.Regex Tint = new("</?color[^>]*>", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static bool _logged;

        private static MethodBase TargetMethod()
        {
            MethodBase found = typeof(TooltipItemManager).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "FormatAffix" && m.GetParameters().Length > 1 && m.GetParameters()[1].ParameterType == typeof(ItemAffix));
            Main.logger_instance?.Msg("Fracture tooltip: FormatAffix " + (found == null ? "NOT FOUND" : "patched"));
            return found;
        }

        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, ItemAffix __1, ref string __result)
        {
            try
            {
                if (!_logged)
                {
                    _logged = true;
                    Main.logger_instance?.Msg("Fracture tooltip: FormatAffix is being called.");
                }
                if (!string.IsNullOrEmpty(__result) && Items_Fracture.IsFractured(__0, __1))
                {
                    // the whole line in the orb's light blue: the game's own colours inside it are taken out
                    string plain = Tint.Replace(__result, "");
                    __result = "<color=#8fd8ff>◆ " + plain + "</color>";
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
