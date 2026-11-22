using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Data;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Bank;

public partial class Bank_Quad : MonoBehaviour
{

    private static bool IsQuadContainer(ItemContainer container)
    {
        return !container.IsNullOrDestroyed()
            && container.id == ContainerID.STASH
            && container.size == quad_size;
    }

    // Returns false when an item could not be moved and is still in the tab.
    private static bool EjectOverflow(ItemContainer container, Vector2Int bounds)
    {
        if (container.IsNullOrDestroyed() || container.content == null)
        {
            return true;
        }
        var overflow = new System.Collections.Generic.List<ItemContainerEntry>();
        foreach (ItemContainerEntry entry in container.content)
        {
            if ((entry == null) || (entry.data.IsNullOrDestroyed()))
            {
                continue;
            }
            Vector2Int itemSize = entry.size;
            if (itemSize.x < 1)
            {
                itemSize.x = 1;
            }
            if (itemSize.y < 1)
            {
                itemSize.y = 1;
            }
            Vector2Int pos = entry.Position;
            if (
                (pos.x < 0)
                || (pos.y < 0)
                || (pos.x + itemSize.x > bounds.x)
                || (pos.y + itemSize.y > bounds.y)
            )
            {
                overflow.Add(entry);
            }
        }
        if (overflow.Count == 0)
        {
            return true;
        }

        ItemContainer inventory = null;
        if (!ItemContainersManager.Instance.IsNullOrDestroyed())
        {
            inventory = ItemContainersManager.Instance.inventory;
        }
        bool keptAll = true;
        foreach (ItemContainerEntry entry in overflow)
        {
            if ((entry == null) || (entry.data.IsNullOrDestroyed()))
            {
                continue;
            }
            int qty = entry.Quantity;
            if (qty < 1)
            {
                qty = 1;
            }
            bool moved = false;
            if (!inventory.IsNullOrDestroyed())
            {
                moved = inventory.TryAddItem(entry.data, qty, Context.SILENT);
            }
            if (moved)
            {
                if (!container.TryRemoveItem(entry, qty, Context.SILENT))
                {
                    keptAll = false;
                }
                continue;
            }

            Actor player = Refs_Manager.player_actor;
            if (player.IsNullOrDestroyed())
            {
                player = PlayerFinder.getPlayerActor();
            }
            GroundItemManager ground = Refs_Manager.ground_item_manager;
            if (ground.IsNullOrDestroyed())
            {
                ground = GroundItemManager.instance;
            }
            if (ground.IsNullOrDestroyed())
            {
                ground = UnityEngine.Object.FindObjectOfType<GroundItemManager>();
            }
            if (player.IsNullOrDestroyed() || ground.IsNullOrDestroyed())
            {
                keptAll = false;
                continue;
            }

            var drops = new System.Collections.Generic.List<ItemData>();
            for (int i = 0; i < qty; i++)
            {
                ItemData dropData = entry.data.CreateGameplayDuplicate();
                if (dropData.IsNullOrDestroyed())
                {
                    drops.Clear();
                    break;
                }
                drops.Add(dropData);
            }
            if (drops.Count != qty || !container.TryRemoveItem(entry, qty, Context.SILENT))
            {
                keptAll = false;
                continue;
            }

            Refs_Manager.player_actor = player;
            Refs_Manager.ground_item_manager = ground;
            Vector3 dropPosition = player.position();
            foreach (ItemData dropData in drops)
            {
                ModItemDrop.Drop(ground, player, dropData, dropPosition);
            }
        }
        occupied_slots.Remove(container.Pointer);
        return keptAll;
    }

    // Each stash tab has its own container. Only that tab's size changes.
    // The game's own occupancy array is left alone; quad checks are handled separately.
    static void SyncTabSize(int index)
    {
        if (stash_item_container.IsNullOrDestroyed())
        {
            return;
        }
        if ((index < 0) || (index >= stash_item_container.containers.Count))
        {
            return;
        }
        ItemContainer container = stash_item_container.containers[index];
        if (container.IsNullOrDestroyed() || (container.id != ContainerID.STASH))
        {
            return;
        }
        if (!Get.TryGetTab(index, out _))
        {
            return;
        }
        bool quad = Get.IsQuadStash(index);
        if (!quad && (container.size != quad_size))
        {
            return;
        }
        Vector2Int target = quad ? quad_size : default_size;
        if (container.size == target)
        {
            return;
        }
        if ((container.size.x > target.x) || (container.size.y > target.y))
        {
            if (!EjectOverflow(container, target))
            {
                return;
            }
        }
        container.size = target;
        occupied_slots.Remove(container.Pointer);
    }

    static void SyncEveryTab()
    {
        if (stash_item_container.IsNullOrDestroyed())
        {
            return;
        }
        Get.MigrateIdsFromNames();
        int count = stash_item_container.containers.Count;
        for (int i = 0; i < count; i++)
        {
            SyncTabSize(i);
        }
    }

    private static bool Fits(Vector2Int position, Vector2Int size, Vector2Int bounds)
    {
        return position.x >= 0
            && position.y >= 0
            && size.x > 0
            && size.y > 0
            && size.x <= bounds.x
            && size.y <= bounds.y
            && position.x <= bounds.x - size.x
            && position.y <= bounds.y - size.y;
    }

    private static bool[] GetOccupiedSlots(ItemContainer container)
    {
        if (occupied_slots.TryGetValue(container.Pointer, out var slots))
        {
            return slots;
        }
        slots = new bool[quad_size.x * quad_size.y];
        // A tab may already contain items when it first becomes quad. Reconstruct
        // occupancy instead of assuming a missing cache means an empty tab.
        foreach (var entry in container.content)
        {
            if (!Fits(entry.Position, entry.size, quad_size))
            {
                // Preserve invalid/out-of-range contents; never permit overlapping writes.
                System.Array.Fill(slots, true);
                break;
            }
            foreach (int index in Get.SlotsPosition(entry.Position, entry.size))
            {
                if ((index >= 0) && (index < slots.Length))
                {
                    slots[index] = true;
                }
            }
        }
        occupied_slots[container.Pointer] = slots;
        return slots;
    }
}
