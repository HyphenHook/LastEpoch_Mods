using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_MemoryReflectorWindow
{
    private static void ReturnItems()
    {
        foreach (ItemData item in new[] { itemA, itemB })
        {
            GiveBack(item);
        }
        itemA = itemB = null;
        offer = null;
    }

    private static void GiveBack(ItemData item)
    {
        ItemContainersManager manager = ItemContainersManager.Instance;
        if (item.IsNullOrDestroyed() || manager.IsNullOrDestroyed())
        {
            return;
        }
        if (!manager.inventory.TryAddItem(item, 1, Context.DEFAULT))
        {
            manager.cursor.TryAddItem(item, 1, Context.DEFAULT);
        }
    }

    private static void ClickSlot(int index)
    {
        if (animating)
        {
            return;
        }
        try
        {
            ItemContainersManager manager = ItemContainersManager.Instance;
            if (manager.IsNullOrDestroyed())
            {
                return;
            }
            ItemData current = index == 0 ? itemA : itemB;
            if (current != null)
            {
                GiveBack(current);
                if (index == 0) itemA = null; else itemB = null;
                icons[index]?.Dispose();
                icons[index] = null;
            }
            else
            {
                var content = manager.cursor.GetContent();
                if (content != null && content.Count > 0)
                {
                    ItemContainerEntry entry = content[0];
                    ItemData item = entry.data;
                    if (!item.IsNullOrDestroyed() && manager.cursor.TryRemoveItem(entry, 1, Context.DEFAULT))
                    {
                        if (index == 0) itemA = item; else itemB = item;
                    }
                }
            }
            selectedId = -1;
            dirty = true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector slot failed: " + ex.Message);
        }
    }

    private static void DoReflect(long gold, int amber, int bones)
    {
        try
        {
            if (animating || offer == null)
            {
                return;
            }
            Items_MemoryReflector.Candidate chosen = Chosen();
            if (!offer.Legendary && chosen == null)
            {
                R.Notify("Pick an affix to reflect.");
                return;
            }
            if (R.HaveGold() < gold)
            {
                R.Notify("Not enough gold.");
                return;
            }
            Faction weaver = Weaver();
            if (weaver == null || HaveAmber() < amber)
            {
                R.Notify("Not enough Memory Amber.");
                return;
            }
            if (R.HaveBones() < bones)
            {
                R.Notify("Not enough Ancient Bones.");
                return;
            }
            // pay first: item B is spent whatever happens
            if (!weaver.TrySpendFavor(amber, false))
            {
                R.Notify("Not enough Memory Amber.");
                return;
            }
            weaver.SaveAndSync(false);
            Refs_Manager.player_gold_tracker?.modifyGold((int)-Math.Min(gold, int.MaxValue));
            R.BonesTracker()?.modifyAncientBones(-bones);

            bool success = Items_MemoryReflector.Reflect(offer, chosen, out _);
            StartAnimation(success);
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector failed: " + ex.Message);
            R.Notify("Something went wrong.");
        }
    }
}
