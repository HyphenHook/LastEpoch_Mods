using System;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.ModUI;

namespace LastEpoch_Hud.Scripts.Mods.Items.Pickup;

/// <summary>Stores materials before an inventory pickup that would not fit.</summary>
[HarmonyPatch(typeof(ItemContainersManager), "attemptToPickupItem")]
public class AutoStoreOnFullInventoryPatch
{
    [HarmonyPrefix]
    private static void Prefix(ItemContainersManager __instance, ItemData __0)
    {
        try
        {
            StoreIfFull(__instance, __0);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoStore on full inventory");
        }
    }

    private static void StoreIfFull(ItemContainersManager manager, ItemData item)
    {
        bool enabled = ModSettings.AutoStore.OnFullInventory.Value;
        bool cursorEmpty = enabled && IsCursorEmpty(manager);
        bool hasSpace = cursorEmpty && HasSpaceFor(manager, item);
        if (!FullInventoryStoreRule.ShouldStoreBeforePickup(enabled, cursorEmpty, hasSpace))
        {
            return;
        }

        Items_AutoStore_OnPickup.StoreNow();
    }

    private static bool IsCursorEmpty(ItemContainersManager manager)
    {
        if (manager.IsNullOrDestroyed() || manager.cursor.IsNullOrDestroyed())
        {
            return false;
        }

        return !manager.cursor.HasContent();
    }

    private static bool HasSpaceFor(ItemContainersManager manager, ItemData item)
    {
        if (item.IsNullOrDestroyed() || manager.inventory.IsNullOrDestroyed())
        {
            return true;
        }

        var list = ItemList.get();
        if (list.IsNullOrDestroyed())
        {
            return true;
        }

        return manager.inventory.hasEmptySpot(list.GetItemSizeFromBaseType(item.itemType));
    }
}
