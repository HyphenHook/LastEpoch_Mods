using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine.EventSystems;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

internal static class OrbInventoryItems
{
    internal static int CountCarried(string key)
    {
        var inventory = ItemContainersManager.Instance?.inventory;
        if (inventory.IsNullOrDestroyed()) return 0;
        int count = 0;
        var contents = inventory.GetContent();
        if (contents == null) return 0;
        foreach (ItemContainerEntry entry in contents)
        {
            if (Matches(entry, key)) count += entry.Quantity;
        }
        return count;
    }

    internal static bool TrySpendCarried(string key)
    {
        var inventory = ItemContainersManager.Instance?.inventory;
        if (inventory.IsNullOrDestroyed()) return false;
        var contents = inventory.GetContent();
        if (contents == null) return false;
        foreach (ItemContainerEntry entry in contents)
        {
            if (Matches(entry, key)) return inventory.TryRemoveItem(entry, 1, Context.DEFAULT);
        }
        return false;
    }

    private static bool Matches(ItemContainerEntry entry, string key) =>
        !entry.IsNullOrDestroyed() && entry.Quantity > 0 && OrbDrop.IsPhysical(entry.data) && OrbDrop.KeyOf(entry.data) == key;

    internal static void TransferCarried()
    {
        var inventory = ItemContainersManager.Instance?.inventory;
        if (inventory.IsNullOrDestroyed()) return;
        var contents = inventory.GetContent();
        if (contents == null) return;
        for (int i = contents.Count - 1; i >= 0; i--)
        {
            Deposit(contents[i], inventory.TryRemoveItem);
        }
    }

    private static bool Deposit(ItemContainerEntry entry, Func<ItemContainerEntry, int, Context, bool> remove)
    {
        if (entry.IsNullOrDestroyed() || !OrbDrop.IsPhysical(entry.data)) return false;
        string key = OrbDrop.KeyOf(entry.data);
        int count = entry.Quantity;
        if (!AdvancedMaterials.TryCreditStack(key, count)) return false;
        if (remove(entry, count, Context.DEFAULT)) return true;
        AdvancedMaterials.TryTakeFromStack(key, count);
        return false;
    }

    internal static bool MoveStackOrb(string key, out bool withdrawn)
    {
        withdrawn = false;
        var cursor = ItemContainersManager.Instance?.cursor;
        if (cursor.IsNullOrDestroyed()) return false;
        var contents = cursor.GetContent();
        if (contents == null) return false;
        if (contents.Count > 0)
        {
            ItemContainerEntry entry = contents[0];
            return Matches(entry, key) && Deposit(entry, cursor.TryRemoveItem);
        }

        if (AdvancedMaterials.StackCount(key) <= 0) return false;
        ItemData orb = OrbDrop.Carrier(key, physical: true);
        if (orb.IsNullOrDestroyed()) return false;

        // Check that native inventory/stash serialization preserves the marker before withdrawing.
        var saved = new ItemDataUnpacked(orb.GetID());
        if (!OrbDrop.IsPhysical(saved) || OrbDrop.KeyOf(saved) != key)
        {
            Main.logger_instance?.Warning("The Harbinger's Eye carrier did not preserve the orb marker.");
            return false;
        }
        if (!cursor.TryAddItem(orb, 1, Context.DEFAULT)) return false;
        if (!AdvancedMaterials.TryTakeFromStack(key, 1))
        {
            var held = cursor.GetContent();
            if (held.Count > 0 && Matches(held[0], key)) cursor.TryRemoveItem(held[0], 1, Context.DEFAULT);
            return false;
        }
        withdrawn = true;
        return true;
    }

    [HarmonyPatch]
    private static class TransferButton
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(InventoryPanelUI), nameof(InventoryPanelUI.StoreMaterialsButtonPress));
            yield return AccessTools.Method(typeof(Il2CppLE.UI.PanelSystem.InventoryPanel),
                nameof(Il2CppLE.UI.PanelSystem.InventoryPanel.StoreMaterialsButtonPress));
        }

        [HarmonyPostfix]
        private static void Postfix() => TransferCarried();
    }

    [HarmonyPatch(typeof(ItemContainerUI), nameof(ItemContainerUI.onPointerClick))]
    private static class QuickDeposit
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemContainerUI __instance, PointerEventData __0)
        {
            if (__instance.identifier != ContainerID.INVENTORY || __0.button != PointerEventData.InputButton.Right) return true;
            var manager = ItemContainersManager.Instance;
            if (manager.IsNullOrDestroyed() || manager.inventory.IsNullOrDestroyed() || manager.cursor.IsNullOrDestroyed()
                || manager.cursor.HasContent()) return true;
            ItemContainerEntry entry = manager.inventory.GetEntryAt(__instance.GetCursorPosInContainer(__0));
            if (entry.IsNullOrDestroyed() || !OrbDrop.IsPhysical(entry.data)) return true;
            Deposit(entry, manager.inventory.TryRemoveItem);
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemContainersManager), nameof(ItemContainersManager.TryQuickStoreCraftingItem))]
    private static class QuickStoreShortcut
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemContainersManager __instance, ItemContainerEntry __0, ref bool __result)
        {
            if (__0.IsNullOrDestroyed() || !OrbDrop.IsPhysical(__0.data)) return true;
            var inventory = __instance.inventory;
            ItemContainerEntry entry = inventory.IsNullOrDestroyed() ? null : inventory.GetEntryAt(__0.Position);
            __result = !entry.IsNullOrDestroyed() && entry.Pointer == __0.Pointer && Deposit(entry, inventory.TryRemoveItem);
            return false;
        }
    }

    [HarmonyPatch]
    private static class StackRules
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ItemList), nameof(ItemList.IsStackableItem), new[] { typeof(ItemData) });
            yield return AccessTools.Method(typeof(ItemData), nameof(ItemData.ItemTypeIsStackableInContainers));
        }

        [HarmonyPrefix]
        private static bool Prefix(ItemData __0, ref bool __result) => RejectOrb(__0, ref __result);
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.IsSameTypeAndSubtype))]
    private static class MatchRules
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, ItemData __0, ref bool __result)
        {
            if (!OrbDrop.IsOrb(__instance) && !OrbDrop.IsOrb(__0)) return true;
            __result = !__0.IsNullOrDestroyed() && __instance.Pointer == __0.Pointer;
            return false;
        }
    }

    [HarmonyPatch]
    private static class BlockKeyStorage
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(KeysItemContainer), nameof(KeysItemContainer.TryAddItem),
                new[] { typeof(ItemData), typeof(int), typeof(Context) });
            yield return AccessTools.Method(typeof(KeysItemContainer), nameof(KeysItemContainer.TryAddItem),
                new[] { typeof(ItemData), typeof(int), typeof(UnityEngine.Vector2Int), typeof(Context) });
            yield return AccessTools.Method(typeof(SpecificSubtypeOneItemContainer),
                nameof(SpecificSubtypeOneItemContainer.itemMatchesSubTypeRequirements));
            yield return AccessTools.Method(typeof(SpecificSubtypeOneItemContainer),
                nameof(SpecificSubtypeOneItemContainer.CanReceiveItem), new[] { typeof(ItemData), typeof(int) });
            yield return AccessTools.Method(typeof(ItemContainersManager), nameof(ItemContainersManager.TryStoreMaterial),
                new[] { typeof(ItemData), typeof(int), typeof(IItemContainer) });
        }

        [HarmonyPrefix]
        private static bool Prefix(ItemData __0, ref bool __result) => RejectOrb(__0, ref __result);
    }

    [HarmonyPatch]
    private static class BlockMaterialTransfer
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ItemContainersManager), nameof(ItemContainersManager.TryStoreMaterialFromInventory));
            yield return AccessTools.Method(typeof(ItemContainersManager), nameof(ItemContainersManager.TryStoreMaterial),
                new[] { typeof(ItemContainerEntry), typeof(IItemContainer), typeof(IItemContainer) });
        }

        [HarmonyPrefix]
        private static bool Prefix(ItemContainerEntry __0, ref bool __result) => RejectOrb(__0?.data, ref __result);
    }

    private static bool RejectOrb(ItemData item, ref bool result)
    {
        if (!OrbDrop.IsOrb(item)) return true;
        result = false;
        return false;
    }
}
