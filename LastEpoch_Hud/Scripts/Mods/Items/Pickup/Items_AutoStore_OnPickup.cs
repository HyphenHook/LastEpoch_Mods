using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Items.Pickup;

public class Items_AutoStore_OnPickup
{
    static bool storing;
    static bool pendingStore;
    static float pendingStoreAt;
    const float StoreDebounceSeconds = 0.20f;

    public static void RequestStore()
    {
        if (storing)
        {
            return;
        }
        if (!pendingStore)
        {
            pendingStore = true;
            pendingStoreAt = UnityEngine.Time.realtimeSinceStartup + StoreDebounceSeconds;
        }
    }

    public static void FlushPending()
    {
        if (!pendingStore || storing)
        {
            return;
        }
        if (UnityEngine.Time.realtimeSinceStartup < pendingStoreAt)
        {
            return;
        }

        pendingStore = false;
        StoreNow();
    }

    public static void StoreNow()
    {
        if (storing)
        {
            return;
        }

        // Any explicit/timer/inventory-open store satisfies a queued drop-store request.
        pendingStore = false;

        long profAlloc;
        long profStart = Diagnostics.DiagnosticsDumper.BeginOperation(out profAlloc);
        storing = true;
        try
        {
            ItemContainersManager manager = ItemContainersManager.Instance;
            if (manager.IsNullOrDestroyed())
            {
                return;
            }

            // Let the game bulk-store crafting materials first, then handle the
            // special inventory-backed material types in one inventory scan.
            manager.TryStoreMaterials(false);
            MoveKeysAndEchoes(manager);
        }
        catch
        {
            Main.logger_instance?.Msg("Items_AutoStore.StoreNow() ERROR");
        }
        finally
        {
            storing = false;
            Diagnostics.DiagnosticsDumper.EndOperation("AutoStore.StoreNow", profStart, profAlloc);
        }
    }

    public static void StorePicked(ItemData data)
    {
        if (storing || data.IsNullOrDestroyed())
        {
            return;
        }
        storing = true;
        try
        {
            ItemContainersManager manager = ItemContainersManager.Instance;
            if (manager.IsNullOrDestroyed() || manager.inventory.IsNullOrDestroyed())
            {
                return;
            }
            ItemContainerEntry entry = FindEntry(manager.inventory, data);
            if (entry == null)
            {
                return;
            }

            int type = data.itemType;
            if (Item.isKey(type))
            {
                MoveEntry(
                    manager.inventory,
                    entry,
                    (item, qty) =>
                        (!manager.keys.IsNullOrDestroyed())
                        && manager.keys.TryAddItem(item, qty, Context.SILENT)
                );
                return;
            }
            if (Item.isWovenEcho(type))
            {
                MoveEntry(
                    manager.inventory,
                    entry,
                    (item, qty) =>
                        (!manager.wovenEchoes.IsNullOrDestroyed())
                        && manager.wovenEchoes.TryAddItem(item, qty, Context.SILENT)
                );
                return;
            }
            if (
                ItemList.isShard(type)
                || Item.isResonance(type)
                || type == Item.runeItemType
                || type == Item.glyphItemType
                || ItemList.isCraftingItem(type)
            )
            {
                manager.TryStoreMaterialFromInventory(entry);
            }
        }
        catch { }
        finally
        {
            storing = false;
        }
    }

    static ItemContainerEntry FindEntry(ItemContainer inventory, ItemData data)
    {
        Il2CppSystem.Collections.Generic.List<ItemContainerEntry> content = inventory.GetContent();
        if (content == null)
        {
            return null;
        }
        ItemContainerEntry sameType = null;
        for (int i = content.Count - 1; i >= 0; i--)
        {
            ItemContainerEntry entry = content[i];
            if ((entry == null) || (entry.data.IsNullOrDestroyed()))
            {
                continue;
            }
            if (entry.data == data)
            {
                return entry;
            }
            if ((sameType == null) && (entry.data.itemType == data.itemType))
            {
                sameType = entry;
            }
        }
        return sameType;
    }

    static void MoveEntry(
        ItemContainer inventory,
        ItemContainerEntry entry,
        System.Func<ItemData, int, bool> add
    )
    {
        int qty = entry.Quantity;
        if (qty < 1)
        {
            qty = 1;
        }
        if (add(entry.data, qty))
        {
            inventory.TryRemoveItem(entry, qty, Context.SILENT);
        }
    }

    static void MoveKeysAndEchoes(ItemContainersManager manager)
    {
        ItemContainer inventory = manager.inventory;
        if (inventory.IsNullOrDestroyed())
        {
            return;
        }

        Il2CppSystem.Collections.Generic.List<ItemContainerEntry> content = inventory.GetContent();
        if (content == null)
        {
            return;
        }

        var keys = new System.Collections.Generic.List<ItemContainerEntry>();
        var echoes = new System.Collections.Generic.List<ItemContainerEntry>();

        for (int i = 0; i < content.Count; i++)
        {
            ItemContainerEntry entry = content[i];
            if ((entry == null) || (entry.data.IsNullOrDestroyed()))
            {
                continue;
            }

            int type = entry.data.itemType;
            if (Item.isKey(type))
            {
                keys.Add(entry);
            }
            else if (Item.isWovenEcho(type))
            {
                echoes.Add(entry);
            }
        }

        if (!manager.keys.IsNullOrDestroyed())
        {
            foreach (ItemContainerEntry entry in keys)
            {
                MoveEntry(
                    inventory,
                    entry,
                    (data, qty) => manager.keys.TryAddItem(data, qty, Context.SILENT)
                );
            }
        }

        if (!manager.wovenEchoes.IsNullOrDestroyed())
        {
            foreach (ItemContainerEntry entry in echoes)
            {
                MoveEntry(
                    inventory,
                    entry,
                    (data, qty) => manager.wovenEchoes.TryAddItem(data, qty, Context.SILENT)
                );
            }
        }
    }

    [HarmonyPatch(typeof(ItemContainersManager), "attemptToPickupItem")]
    public class ItemContainersManager_attemptToPickupItem
    {
        [HarmonyPostfix]
        static void Postfix(bool __result, ItemData __0)
        {
            if (
                (!__result)
                || (storing)
                || (__0.IsNullOrDestroyed())
                || (Save_Manager.instance.IsNullOrDestroyed())
                || (Save_Manager.instance.data.IsNullOrDestroyed())
            )
            {
                return;
            }
            if (!Save_Manager.instance.data.Items.Pickup.Enable_AutoStore_OnDrop)
            {
                return;
            }
            int type = __0.itemType;
            if ((ItemList.isCraftingItem(type)) || (Item.isKey(type)) || (Item.isWovenEcho(type)))
            {
                // Collapse a burst of material pickups into one bulk-store pass.
                RequestStore();
            }
        }
    }
}
