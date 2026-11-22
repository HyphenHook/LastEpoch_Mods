using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Infrastructure.Game.Catalogs;

using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

internal static class JewelBaseType
{
    internal const byte Id = 44;
    internal static readonly string[] Names = { "Cobalt Jewel", "Ruby Jewel", "Emerald Jewel", "Prismatic Jewel" };
    internal static bool Registered { get; private set; }
    internal static string Status { get; private set; } = "Waiting for the Jewel catalog.";
    private static bool failed;

    internal static bool IsJewel(ItemData item) => Registered && !item.IsNullOrDestroyed()
        && item.itemType == Id && item.subType < Names.Length;

    internal static void Ensure()
    {
        if (Registered || failed) return;
        if (!EquipmentCatalogRegistry.CanAppend(Id)) return;
        try
        {
            var list = Refs_Manager.item_list;
            var original = list.EquippableItems;
            if (original.Length != Id || (list.nonEquippableItems != null
                && Id < list.nonEquippableItems.Length && list.nonEquippableItems[Id] != null))
                throw new InvalidOperationException("Jewel base type ID 44 is occupied; the catalog was not replaced.");
            var subtypes = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentItem>();
            subtypes.Add(Subtype(0, Implicit(SP.Mana, BaseStats.ModType.ADDED, 44, 60),
                Implicit(SP.Mana, BaseStats.ModType.INCREASED, .12f, .24f)));
            subtypes.Add(Subtype(1, Implicit(SP.Health, BaseStats.ModType.ADDED, 60, 100),
                Implicit(SP.Health, BaseStats.ModType.INCREASED, .14f, .26f)));
            subtypes.Add(Subtype(2, Implicit(SP.Damage, BaseStats.ModType.INCREASED, .12f, .16f)));
            subtypes.Add(Subtype(3, Implicit(SP.AllAttributes, BaseStats.ModType.ADDED, 6, 20),
                Implicit(SP.AllAttributes, BaseStats.ModType.INCREASED, .06f, .10f)));
            var category = new ItemList.BaseEquipmentItem
            {
                BaseTypeName = "Jewels", displayName = "Jewels", baseTypeID = Id,
                type = (EquipmentType)Id, gridSize = new Vector2Int(1, 1), maximumAffixes = 6,
                affixEffectModifier = 1f, minimumDropLevel = 1f, goldCostModifier = 1f,
                minimumSellValue = 1, rerollChance = 1f, subItems = subtypes, filteredSubItems = subtypes,
                iconSprite = original[(int)EquipmentType.RING].iconSprite,
            };
            EquipmentCatalogRegistry.Append(Id, category);
            // highestEquipmentBaseTypeID is a native const, not writable catalog state.
            // The extended catalog and classification patches register this custom ID.
            AddType(ItemList.nonIdolEquipmentTypes);
            AddType(ItemList.droppableNonIdolEquipmentTypes);
            AddType(ItemList.droppableJewelleryEquipmentTypes);
            Registered = true;
            Status = "Jewels registered: base 44; four subtypes; 1x1; six total affix slots.";
            Main.logger_instance?.Msg(Status);
        }
        catch (Exception ex)
        {
            failed = true;
            Status = "Jewel registration failed: " + ex.Message;
            ErrorLog.Report(ex, "Jewel base type registration");
        }
    }

    private static void AddType(Il2CppSystem.Collections.Generic.List<EquipmentType> types)
    {
        if (types != null && !types.Contains((EquipmentType)Id)) types.Add((EquipmentType)Id);
    }

    private static ItemList.EquipmentImplicit Implicit(SP property, BaseStats.ModType type, float min, float max) =>
        new() { property = property, type = type, implicitValue = min, implicitMaxValue = max };

    private static ItemList.EquipmentItem Subtype(int index, params ItemList.EquipmentImplicit[] implicits)
    {
        var values = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit>();
        foreach (var implicitValue in implicits) values.Add(implicitValue);
        return new ItemList.EquipmentItem
        {
            name = Names[index], displayName = Names[index], subTypeID = index,
            levelRequirement = 1, cannotDrop = false, implicits = values,
            classRequirement = ItemList.ClassRequirement.None,
            subClassRequirement = ItemList.SubClassRequirement.None,
        };
    }

    [HarmonyPatch]
    private static class Classification
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (string name in new[] { "isEquipment", "isEquippable", "ItemTypeIsEquipment", "itemTypeCanHaveForgingPotential" })
                yield return AccessTools.Method(typeof(ItemList), name, new[] { typeof(int) });
        }
        [HarmonyPrefix]
        private static bool Prefix(int __0, ref bool __result)
        {
            if (!Registered || __0 != Id) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.isEquipment))]
    private static class ItemClassification
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemData __instance, ref bool __result)
        {
            if (!IsJewel(__instance)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    private static class NamesPatch
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
            if (!Registered || __0 != Id) return true;
            __result = "Jewel";
            return false;
        }
    }

    [HarmonyPatch]
    private static class SubtypeNames
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (string name in new[] { "GetItemName", "GetItemDisplayName", "GetDefaultSubTypeName" })
                yield return AccessTools.Method(typeof(ItemList), name, new[] { typeof(int), typeof(int) });
        }
        [HarmonyPrefix]
        private static bool Prefix(int __0, int __1, ref string __result)
        {
            if (!Registered || __0 != Id || __1 < 0 || __1 >= Names.Length) return true;
            __result = Names[__1];
            return false;
        }
    }

    [HarmonyPatch(typeof(OneSlotItemContainerWithCharacterRequirements), nameof(OneSlotItemContainerWithCharacterRequirements.TryAddItem))]
    private static class NativeEquipmentGuard
    {
        [HarmonyPrefix]
        private static bool Prefix(OneSlotItemContainerWithCharacterRequirements __instance, ItemData __0, ref bool __result)
        {
            if (!IsJewel(__0) || __instance.id < ContainerID.EQ_HELMET || __instance.id > ContainerID.EQ_RELIC) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(OneSlotItemContainer), nameof(OneSlotItemContainer.CanAddItemRarity))]
    private static class NativeSlotGuard
    {
        [HarmonyPostfix]
        private static void Postfix(OneSlotItemContainer __instance, ItemData __0, ref bool __result)
        {
            if (IsJewel(__0) && __instance.id >= ContainerID.EQ_HELMET && __instance.id <= ContainerID.EQ_RELIC)
                __result = false;
        }
    }
}
