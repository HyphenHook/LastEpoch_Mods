using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Mods.Character;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_RecombinatorWindow
{
    private static void TakeResult()
    {
        if (resultItem == null || !resultRevealed)
        {
            return;
        }
        GiveBack(resultItem);
        resultItem = null;
        resultRevealed = false;
        icons[2]?.Dispose();
        icons[2] = null;
        message = "";
        dirty = true;
    }

    // ---------------------------------------------------------------- actions

    private static void SetMode(bool det)
    {
        deterministic = det;
        message = "";
        dirty = true;
    }

    private static string Key(RecombAffix a) => a.Source + ":" + a.AffixId;

    private static void Toggle(RecombAffix a)
    {
        if (!deterministic)
        {
            return;
        }
        string key = Key(a);
        if (!selected.Remove(key))
        {
            if (a.Reminded)
            {
                foreach (RecombAffix pick in Picks().Where(pick => pick.Reminded))
                {
                    selected.Remove(Key(pick));
                }
            }
            selected.Add(key);
        }
        message = "";
        dirty = true;
    }

    private static List<RecombAffix> Picks()
    {
        var result = new List<RecombAffix>();
        if (offer == null)
        {
            return result;
        }
        foreach (RecombAffix a in offer.All)
        {
            if (selected.Contains(Key(a)))
            {
                result.Add(a);
            }
        }
        return result;
    }

    private static void ClickSlot(int index)
    {
        if (pouring)
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
            if (slots[index] != null)
            {
                GiveBack(slots[index]);
                slots[index] = null;
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
                        slots[index] = item;
                    }
                }
                else
                {
                    message = "Pick up an item first, then click the slot.";
                }
            }
            selected.Clear();
            dirty = true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombinator slot failed: " + ex.Message);
        }
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

    private static void ReturnItems()
    {
        for (int i = 0; i < 2; i++)
        {
            if (slots[i] != null)
            {
                GiveBack(slots[i]);
                slots[i] = null;
            }
        }
        if (resultItem != null)
        {
            GiveBack(resultItem);
            resultItem = null;
            resultRevealed = false;
        }
        selected.Clear();
    }

    private static void DoRecombine(long gold, int embers, int bones)
    {
        try
        {
            if (offer == null || pouring)
            {
                return;
            }
            if (HaveGold() < gold)
            {
                Notify("Not enough gold.");
                return;
            }
            if (HaveEmbers() < embers)
            {
                try
                {
                    Notifications.NotEnoughSoulEmbers();
                }
                catch (Exception)
                {
                    Notify("Not enough Soul Embers.");
                }
                return;
            }
            if (bones > 0 && HaveBones() < bones)
            {
                Notify("Not enough Ancient Bones.");
                return;
            }
            if (resultItem != null)
            {
                message = "Take the result out of the bottom slot first.";
                dirty = true;
                return;
            }
            List<RecombAffix> picks = Picks();
            if (deterministic && (picks.Count == 0 || RecombinatorMath.Validate(picks, Math.Max(offer.CapacityA, offer.CapacityB), offer.MaxPerSide).Length > 0))
            {
                Notify(picks.Count == 0 ? "Pick the affixes you want to keep." : RecombinatorMath.Validate(picks, Math.Max(offer.CapacityA, offer.CapacityB), offer.MaxPerSide));
                return;
            }
            ItemData result = Items_Recombinator.Recombine(offer, deterministic, picks, out bool exact, out string note, bones > 0);
            if (result.IsNullOrDestroyed())
            {
                message = note;
                dirty = true;
                return;
            }
            // Pay only now that there is a result.
            Refs_Manager.player_gold_tracker?.modifyGold((int)-Math.Min(gold, int.MaxValue));
            DungeonRunManager runs = Character_SoulEmbers.GetManager();
            runs?.modifySoulEmberCountOnSingleplayerOrServer(-embers, true);
            if (bones > 0)
            {
                BonesTracker()?.modifyAncientBones(-bones);
            }

            pourItems[0] = slots[0];
            pourItems[1] = slots[1];
            slots[0] = null;
            slots[1] = null;
            resultItem = result;
            icons[2]?.Dispose();
            icons[2] = BuildIcon(result); // started now so its sprite has loaded by the time it appears
            iconFor[2] = (long)result.Pointer;
            lastFailed = deterministic && !exact;
            StartPour(bones > 0);
            selected.Clear();
            message = "";
            dirty = true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombination failed: " + ex.Message);
            message = "Something went wrong; nothing was spent.";
            dirty = true;
        }
    }
}
