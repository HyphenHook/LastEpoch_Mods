using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Infrastructure.Game.Catalogs;
using LastEpoch_Hud.Scripts.Core.Jewels;

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
            var subtypes = BuildSubtypes();
            var category = new ItemList.BaseEquipmentItem
            {
                BaseTypeName = "Jewels", displayName = "Jewels", baseTypeID = Id,
                type = (EquipmentType)Id, gridSize = new Vector2Int(1, 1), maximumAffixes = 6,
                affixEffectModifier = 0f, minimumDropLevel = 1f, goldCostModifier = 1f,
                minimumSellValue = 1, rerollChance = 1f, subItems = subtypes, filteredSubItems = subtypes,
                iconSprite = original[(int)EquipmentType.RING].iconSprite,
            };
            EquipmentCatalogRegistry.Append(Id, category);
            CalibrateAffixEffect(category);
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

    // The game's affix effect is the bonus ABOVE one: the multiplier is 1 + this, so a stored 1 doubled every affix on a
    // Jewel. A Jewel's affix values are exactly what Data/JewelAffixes.csv says: nothing about the item (its potential,
    // rarity or type bonus) scales them, so the bonus is always 0. Attunement still applies, on top of this.
    [HarmonyPatch(typeof(ItemList), nameof(ItemList.getAffixEffectModifier), new[] { typeof(ItemData) })]
    private static class NeutralAffixEffect
    {
        [HarmonyPostfix]
        private static void Postfix(ItemData __0, ref float __result)
        {
            if (IsJewel(__0)) __result = 0f;
        }
    }

    [HarmonyPatch(typeof(ItemList), nameof(ItemList.getAffixEffectModifier), new[] { typeof(int), typeof(int) })]
    private static class NeutralAffixEffectByType
    {
        [HarmonyPostfix]
        private static void Postfix(int __0, ref float __result)
        {
            if (Registered && __0 == Id) __result = 0f;
        }
    }

    // Reports the affix effect bonus the game reads for a Jewel. It must be 0 (a multiplier of 1).
    private static void CalibrateAffixEffect(ItemList.BaseEquipmentItem category)
    {
        try
        {
            var sample = new ItemDataUnpacked { itemType = Id, subType = 0, rarity = 4 };
            float bonus = ItemList.get().getAffixEffectModifier(sample);
            Main.logger_instance?.Msg("Jewel affix effect bonus: " + bonus + " (stored " + category.affixEffectModifier
                + "); 0 means values are exactly as the table says.");
        }
        catch (Exception ex) { Main.logger_instance?.Warning("Jewel affix effect check failed: " + ex.Message); }
    }

    private static void AddType(Il2CppSystem.Collections.Generic.List<EquipmentType> types)
    {
        if (types != null && !types.Contains((EquipmentType)Id)) types.Add((EquipmentType)Id);
    }

    private static ItemList.EquipmentImplicit Implicit(SP property, BaseStats.ModType type, float min, float max) =>
        new() { property = property, type = type, implicitValue = min, implicitMaxValue = max };

    // The four subtypes and their implicits come from Data/JewelImplicits.csv (embedded at compile time). A missing or
    // unreadable table falls back to the built-in defaults below, so the Jewels always register.
    private static Il2CppSystem.Collections.Generic.List<ItemList.EquipmentItem> BuildSubtypes()
    {
        var rows = LoadImplicitRows();
        var subtypes = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentItem>();
        for (int index = 0; index < Names.Length; index++)
        {
            var implicits = new List<ItemList.EquipmentImplicit>();
            foreach (var row in rows)
                if (row.Subtype == index) implicits.Add(ToImplicit(row));
            subtypes.Add(Subtype(index, implicits.ToArray()));
        }
        return subtypes;
    }

    private static ItemList.EquipmentImplicit ToImplicit(JewelImplicitRow row) => new()
    {
        property = (SP)Enum.Parse(typeof(SP), row.Property, true),
        type = (BaseStats.ModType)Enum.Parse(typeof(BaseStats.ModType), row.ModType, true),
        tags = row.Tags.Length == 0 ? AT.None : (AT)Enum.Parse(typeof(AT), row.Tags, true),
        specialTag = (byte)row.SpecialTag,
        implicitValue = row.Min,
        implicitMaxValue = row.Max,
    };

    private static List<JewelImplicitRow> LoadImplicitRows()
    {
        string text = EmbeddedData.Read("JewelImplicits.csv");
        if (text == null)
        {
            Main.logger_instance?.Warning("JewelImplicits.csv is not in the mod; the Jewels use their built-in implicits.");
            return DefaultImplicitRows();
        }
        var errors = new List<string>();
        var rows = JewelImplicitTable.Parse(text, errors);
        try
        {
            foreach (var row in rows) ToImplicit(row); // every name must be one the game knows
            if (!JewelImplicitTable.CoversEverySubtype(rows)) errors.Add("every Jewel subtype needs at least one implicit");
        }
        catch (Exception ex) { errors.Add("a Property, ModType or Tags name is not one the game knows (" + ex.Message + ")"); }
        foreach (string error in errors) Main.logger_instance?.Warning("Jewel implicits: " + error);
        if (errors.Count != 0 && (rows.Count == 0 || !JewelImplicitTable.CoversEverySubtype(rows) || errors.Exists(e => e.StartsWith("a Property"))))
        {
            Main.logger_instance?.Warning("Jewel implicits: the table could not be used, so the built-in implicits are used.");
            return DefaultImplicitRows();
        }
        Main.logger_instance?.Msg("Jewel implicits: loaded " + rows.Count + " rows from JewelImplicits.csv.");
        return rows;
    }

    private static List<JewelImplicitRow> DefaultImplicitRows() => new()
    {
        new() { Subtype = 0, Property = "Mana", ModType = "ADDED", Tags = "None", Min = 44, Max = 60 },
        new() { Subtype = 0, Property = "Mana", ModType = "INCREASED", Tags = "None", Min = .12f, Max = .24f },
        new() { Subtype = 1, Property = "Health", ModType = "ADDED", Tags = "None", Min = 60, Max = 100 },
        new() { Subtype = 1, Property = "Health", ModType = "INCREASED", Tags = "None", Min = .14f, Max = .26f },
        new() { Subtype = 2, Property = "Damage", ModType = "INCREASED", Tags = "None", Min = .12f, Max = .16f },
        new() { Subtype = 3, Property = "AllAttributes", ModType = "ADDED", Tags = "None", Min = 6, Max = 20 },
        new() { Subtype = 3, Property = "AllAttributes", ModType = "INCREASED", Tags = "None", Min = .06f, Max = .10f },
    };

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


}
