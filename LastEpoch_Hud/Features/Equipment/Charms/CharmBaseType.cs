using LastEpoch_Hud.Scripts.Mods.Items.Jewels;
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Infrastructure.Game.Catalogs;
using Il2CppLE.Services.Models.Items;
using Il2CppLE.Services.Visuals;

using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Charms;

internal static class CharmBaseType
{
    internal const byte Id = Core.Items.EquipmentTypeRules.CharmBaseType;
    internal static bool Registered { get; private set; }
    private static bool failed;
    private static bool migrating;

    internal static string InspectEquipment()
    {
        if (!Registered) return "Charm base type is not registered.";
        var sample = new ItemDataUnpacked { itemType = Id, subType = 0, rarity = 2, forgingPotential = 0 };
        sample.RollIndividualID();
        sample.RefreshIDAndValues();
        var copy = new ItemDataUnpacked(sample.GetID());
        var category = Refs_Manager.item_list.GetEquipmentBaseItem(Id);
        return $"Charm base: {Id}; catalog name: {category.BaseTypeName}\n"
            + $"Catalog equipment: {ItemList.isEquipment(Id)}; item equipment: {copy.isEquipment()}\n"
            + $"Regular Charm Forge eligibility: {copy.isForgeableItem()}\n"
            + $"Forging potential supported: {ItemList.itemTypeCanHaveForgingPotential(Id)}\n"
            + $"Serialized base type: {copy.itemType}; affix slots: {category.maximumAffixes}\n"
            + "Regular Forge: corruption only. Exclusive Charm affix pool is not configured.";
    }

    internal static void Ensure()
    {
        if (Registered || failed) return;
        // Keep the saved POC item on ID 42 and the production category on ID 43.
        if (!EquipmentCatalogRegistry.CanAppend(Id)) return;
        try
        {
            var list = Refs_Manager.item_list;
            var original = list.EquippableItems;
            if (original.Length != Id || (list.nonEquippableItems != null
                && Id < list.nonEquippableItems.Length && list.nonEquippableItems[Id] != null))
                throw new InvalidOperationException("Charm base ID 43 is unavailable; catalog left unchanged.");
            var subtypes = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentItem>();
            var category = new ItemList.BaseEquipmentItem
            {
                BaseTypeName = "Charm", displayName = "Charm", baseTypeID = Id,
                type = (EquipmentType)Id, gridSize = new Vector2Int(1, 1), maximumAffixes = 4,
                affixEffectModifier = 1f, goldCostModifier = 1f, rerollChance = 0f,
                subItems = subtypes, filteredSubItems = subtypes,
                iconSprite = original[(int)EquipmentType.RING].iconSprite,
            };
            EquipmentCatalogRegistry.Append(Id, category);
            if (!ItemList.nonIdolEquipmentTypes.Contains((EquipmentType)Id))
                ItemList.nonIdolEquipmentTypes.Add((EquipmentType)Id);
            Registered = true;
            Main.logger_instance?.Msg("Registered dedicated Charm equipment base type 43 (1x1).");
        }
        catch (Exception ex) { failed = true; ErrorLog.Report(ex, "Charm base type registration"); }
    }

    internal static bool Migrate(ItemDataUnpacked item)
    {
        if (!Registered || item.IsNullOrDestroyed() || item.uniqueID != 506 || item.rarity != 7
            || (item.itemType != (byte)EquipmentType.RING && item.itemType != (byte)EquipmentType.RELIC)) return false;
        item.itemType = Id;
        item.subType = 0;
        item.RefreshIDAndValues();
        return true;
    }

    [HarmonyPatch(typeof(ItemDataUnpacked), nameof(ItemDataUnpacked.AfterIDChange))]
    private static class MigrateLoadedItem
    {
        [HarmonyPrefix]
        private static void Prefix(ItemDataUnpacked __instance)
        {
            if (migrating || !Registered || Refs_Manager.item_list.EquippableItems[Id].subItems.Count == 0) return;
            migrating = true;
            try
            {
                Migrate(__instance);
                if (__instance.itemType == Id && __instance.forgingPotential != 0)
                {
                    __instance.forgingPotential = 0;
                    __instance.RefreshIDAndValues();
                }
            }
            finally { migrating = false; }
        }
    }

    [HarmonyPatch]
    private static class Classification
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (string name in new[] { "isEquipment", "isEquippable", "ItemTypeIsEquipment" })
                yield return AccessTools.Method(typeof(ItemList), name, new[] { typeof(int) });
        }
        [HarmonyPrefix]
        private static bool Prefix(int __0, ref bool __result)
        {
            // One patch covers both accessory base types; a second class on the same native methods crashed the game.
            if (!(Registered && __0 == Id) && !(JewelBaseType.Registered && __0 == JewelBaseType.Id)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemList), nameof(ItemList.itemTypeCanHaveForgingPotential))]
    private static class NoForgingPotential
    {
        [HarmonyPrefix]
        private static bool Prefix(int __0, ref bool __result)
        {
            if (JewelBaseType.Registered && __0 == JewelBaseType.Id)
            {
                __result = true;
                return false;
            }
            if (__0 != Id) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.SetForgingPotential))]
    private static class RejectForgingPotential
    {
        [HarmonyPrefix]
        private static void Prefix(ItemData __instance, ref int __0)
        {
            if (__instance.itemType == Id) __0 = 0;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.RefreshIDAndValues))]
    private static class PersistZeroForgingPotential
    {
        [HarmonyPrefix]
        private static void Prefix(ItemData __instance)
        {
            if (__instance.itemType == Id) __instance.forgingPotential = 0;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.isEquipment))]
    private static class ItemClassification
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, ref bool __result)
        {
            if (!Registered || __instance.itemType != Id) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.isForgeableItem))]
    private static class ForgeEligibility
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, ref bool __result)
        {
            if (!Registered || __instance.itemType != Id) return true;
            // Evaluate the native equipment rules on a copy: rarity and corruption
            // remain intact and the original item's identity is never changed.
            var copy = new ItemDataUnpacked(__instance.GetID());
            copy.itemType = (byte)EquipmentType.RING;
            copy.subType = 0;
            __result = copy.isForgeableItem();
            return false;
        }
    }

    private static ItemDataUnpacked NativeJewelleryCopy(ItemData item)
    {
        var copy = new ItemDataUnpacked(item.GetID());
        copy.itemType = (byte)EquipmentType.RING;
        copy.subType = 0;
        return copy;
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.CanBeCorrupted))]
    private static class CorruptionEligibility
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, ref bool __result)
        {
            if (!Registered || __instance.itemType != Id) return true;
            __result = NativeJewelleryCopy(__instance).CanBeCorrupted();
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.GetCorruptableItemCategory))]
    private static class CorruptionCategory
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, ref ItemData.CorruptableItemCategory __result)
        {
            if (!Registered || __instance.itemType != Id) return true;
            __result = NativeJewelleryCopy(__instance).GetCorruptableItemCategory();
            return false;
        }
    }

    [HarmonyPatch(typeof(OneSlotItemContainer), nameof(OneSlotItemContainer.CanAddItemType))]
    private static class ForgeSlotType
    {
        [HarmonyPrefix]
        private static bool Prefix(OneSlotItemContainer __instance, int __0, ref bool __result)
        {
            if (!Registered || __0 != Id || __instance.id != ContainerID.CRAFTING_MAIN) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    private static class Names
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (string name in new[] { "GetBaseTypeName", "GetBaseTypeDisplayName" })
                yield return AccessTools.Method(typeof(ItemList), name, new[] { typeof(int) });
        }
        [HarmonyPrefix]
        private static bool Prefix(int __0, ref string __result)
        {
            if (Registered && __0 == Id) __result = "Charm";
            else if (JewelBaseType.Registered && __0 == JewelBaseType.Id) __result = "Jewel";
            else return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ClientVisualsService), "GetItemVisual")]
    private static class Visual
    {
        [HarmonyPrefix]
        private static void Prefix(ref ItemVisualKey __0)
        {
            if (!Registered || (int)__0.EquipmentType != Id) return;
            __0.EquipmentType = EquipmentType.RING;
            __0.SubType = 0;
            __0.UniqueID = 0;
        }
    }
}
