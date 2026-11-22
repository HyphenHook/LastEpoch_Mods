using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Infrastructure.Game.Catalogs;
using Il2CppLE.Services.Models.Items;
using Il2CppLE.Services.Visuals;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.BaseTypePoc;

// Independent experiment. Never borrows or edits the Charm definition or its save.
internal static class EquipmentBaseTypePoc
{
    private const string BaseName = "Resonator (Base Type POC)";
    private const string ItemName = "Copper Resonator";
    private static readonly string config = Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name, "EquipmentBaseTypePoc.enabled");
    private static bool requested;
    private static bool checkedConfig;
    private static bool finished;
    private static bool registered;
    private static byte baseId;
    private static string report = "Not enabled. Use Items > Test Drops > Run Equipment Base Type POC.";
    internal static string Inspect() => report;
    internal static bool Registered => registered;
    internal static void EnsureCatalog()
    {
        requested = true;
        checkedConfig = true;
        Tick();
    }

    internal static void Tick()
    {
        if (!checkedConfig) { requested = File.Exists(config); checkedConfig = true; }
        if (!requested || finished || Locales.current == Locales.Selected.Unknow
            || Refs_Manager.item_list.IsNullOrDestroyed()) return;
        finished = true;
        try
        {
            var list = Refs_Manager.item_list;
            var original = list.EquippableItems;
            int id = original.Length;
            int enumMax = Enum.GetValues(typeof(EquipmentType)).Cast<EquipmentType>().Max(x => (int)x);
            if (id <= enumMax || id > byte.MaxValue)
                throw new InvalidOperationException("No append-only base ID outside the native enum: " + id);
            if (list.nonEquippableItems != null && id < list.nonEquippableItems.Length
                && list.nonEquippableItems[id] != null)
                throw new InvalidOperationException("Append ID " + id + " is already a non-equipment category. Catalog left unchanged.");
            baseId = (byte)id;
            var implicits = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit>();
            implicits.Add(new ItemList.EquipmentImplicit
            {
                property = SP.Intelligence, type = BaseStats.ModType.ADDED,
                implicitValue = 5f, implicitMaxValue = 5f,
            });
            var subtypes = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentItem>();
            subtypes.Add(new ItemList.EquipmentItem
            {
                name = ItemName, displayName = ItemName, subTypeID = 0, levelRequirement = 1,
                cannotDrop = true, implicits = implicits,
                classRequirement = ItemList.ClassRequirement.None,
                subClassRequirement = ItemList.SubClassRequirement.None,
            });
            var category = new ItemList.BaseEquipmentItem
            {
                BaseTypeName = BaseName, displayName = BaseName, baseTypeID = id,
                type = (EquipmentType)id, gridSize = new Vector2Int(1, 1), maximumAffixes = 4,
                affixEffectModifier = 1f, rerollChance = 0f, goldCostModifier = 1f,
                subItems = subtypes, filteredSubItems = subtypes,
                iconSprite = original[(int)EquipmentType.RING].iconSprite,
            };
            EquipmentCatalogRegistry.Append(id, category);
            registered = true;
            report = "Registered new base ID " + baseId + " (not in EquipmentType enum), subtype 0.\n";
            Probe();
        }
        catch (Exception ex)
        {
            report += "\nPOC stopped: " + ex.Message;
            ErrorLog.Report(ex, "Equipment base type POC");
        }
    }

    internal static void Run()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(config));
        File.WriteAllText(config, "Enabled for repeatable item loading across restarts.");
        requested = true;
        checkedConfig = true;
        Tick();
        if (registered)
        {
            try { Probe(); }
            catch (Exception ex) { ErrorLog.Report(ex, "Equipment base type POC probe"); }
        }
        Main.logger_instance?.Msg(report);
    }

    private static ItemDataUnpacked MakeItem()
    {
        var item = new ItemDataUnpacked { itemType = baseId, subType = 0, rarity = 0 };
        item.RollIndividualID();
        item.RefreshIDAndValues();
        return item;
    }

    private static void Probe()
    {
        if (!registered) return;
        try
        {
            var item = MakeItem();
            var restored = new ItemDataUnpacked(item.GetID());
            var list = Refs_Manager.item_list;
            bool roundTrip = restored.itemType == baseId && restored.subType == 0 && restored.individualID == item.individualID;
            var size = list.GetItemSizeFromBaseType(baseId);
            report = $"POC base ID: {baseId}; native enum member: {Enum.IsDefined(typeof(EquipmentType), (int)baseId)}\n"
                + $"Serialization round trip: {roundTrip}\n"
                + $"Equipment classification: {ItemList.isEquipment(baseId)}\n"
                + $"Inventory dimensions: {size.x} x {size.y}\n"
                + $"Subtype: {list.GetEquipmentSubItem(baseId, 0).name}\n"
                + $"Implicit: +{list.GetItemImplicits(restored)[0].GetValue(255)} Intelligence\n"
                + "Native slot equip intentionally blocked. Inventory, hover, drop/pickup and restart require live testing.";
            if (!roundTrip || size.x != 1 || size.y != 1) throw new InvalidOperationException("Core POC checks failed.");
        }
        catch (Exception ex)
        {
            report += "\nProbe failed: " + ex.Message;
            throw;
        }
    }

    internal static void Give()
    {
        try
        {
            Run();
            if (!registered) return;
            var player = PlayerFinder.getPlayerData();
            if (player.IsNullOrDestroyed() || !player.IsOffline) return;
            var manager = ItemContainersManager.Instance;
            if (manager.IsNullOrDestroyed() || manager.inventory.IsNullOrDestroyed()) return;
            Probe();
            bool added = manager.inventory.TryAddItem(MakeItem(), 1, Context.DEFAULT);
            report += "\nDemo item added to inventory: " + added;
            Main.logger_instance?.Msg(report);
        }
        catch (Exception ex) { report += "\nGive failed: " + ex.Message; ErrorLog.Report(ex, "Give base type POC"); }
    }

    private static bool IsPoc(int type) => registered && type == baseId;

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
            if (!IsPoc(__0)) return true;
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
            if (!IsPoc(__0)) return true;
            __result = BaseName;
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
            if (!IsPoc(__0) || __1 != 0) return true;
            __result = ItemName;
            return false;
        }
    }

    [HarmonyPatch(typeof(ClientVisualsService), "GetItemVisual")]
    private static class Visual
    {
        [HarmonyPrefix]
        private static void Prefix(ref ItemVisualKey __0)
        {
            if (!IsPoc((int)__0.EquipmentType)) return;
            // Borrow an existing visual only; the actual item retains the new numeric base ID.
            __0.EquipmentType = EquipmentType.RING;
            __0.SubType = 0;
            __0.UniqueID = 0;
        }
    }

    [HarmonyPatch(typeof(OneSlotItemContainerWithCharacterRequirements), nameof(OneSlotItemContainerWithCharacterRequirements.TryAddItem))]
    private static class SlotGuard
    {
        [HarmonyPrefix]
        private static bool Prefix(OneSlotItemContainerWithCharacterRequirements __instance, ItemData __0, ref bool __result)
        {
            if (__0.IsNullOrDestroyed() || !IsPoc(__0.itemType)
                || __instance.id < ContainerID.EQ_HELMET || __instance.id > ContainerID.EQ_RELIC) return true;
            __result = false;
            return false;
        }
    }
}
