using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Services.Models.Items;
using Il2CppLE.Services.Visuals;
using Il2CppLE.UI.Tooltips;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

internal static class JewelVisuals
{
    private static readonly string[] Assets = { "jewel_cobalt", "jewel_ruby", "jewel_emerald", "jewel_prismatic" };
    private static readonly HashSet<Sprite> Owned = new();
    internal static Sprite IconFor(ItemData item)
    {
        if (!JewelBaseType.IsJewel(item)) return null;
        var sprite = OrbArt.Named(Assets[item.subType]);
        if (sprite != null) Owned.Add(sprite);
        return sprite;
    }
    private static void Apply(Image image, Sprite sprite)
    {
        if (image.IsNullOrDestroyed()) return;
        if (sprite != null)
        {
            image.overrideSprite = sprite;
            image.color = Color.white;
        }
        else if (image.overrideSprite != null && Owned.Contains(image.overrideSprite)) image.overrideSprite = null;
    }

    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItemSprite))]
    private static class InventoryIcon
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryItemUI __instance, ItemData __0) => Apply(__instance.contentImage, IconFor(__0));
    }

    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetImageSpritesAndColours))]
    private static class InventoryRefresh
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryItemUI __instance) => Apply(__instance.contentImage,
            IconFor(__instance.EntryRef.IsNullOrDestroyed() ? null : __instance.EntryRef.data));
    }

    [HarmonyPatch(typeof(ClientVisualsService), "GetItemVisual")]
    private static class WorldVisual
    {
        [HarmonyPrefix]
        private static void Prefix(ref ItemVisualKey __0)
        {
            if (!JewelBaseType.Registered || (int)__0.EquipmentType != JewelBaseType.Id) return;
            __0.EquipmentType = EquipmentType.RING;
            __0.SubType = 0;
            __0.UniqueID = 0;
        }
    }

    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetItemTooltipInfo))]
    private static class ModernContent
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, ItemTooltipInfo __result)
        {
            if (__result == null || !JewelBaseType.IsJewel(__0)) return;
            __result.Name = JewelBaseType.Names[__0.subType];
            __result.PrimaryType = "Jewel";
            __result.SecondaryType = "";
        }
    }

    [HarmonyPatch(typeof(ItemTooltip), nameof(ItemTooltip.SetTooltipInfo))]
    private static class ModernIcon
    {
        [HarmonyPrefix]
        private static void Prefix(ItemTooltip __instance, ItemTooltipInfo __0) => Apply(__instance._icon, IconFor(__0?.Item));
        [HarmonyPostfix]
        private static void Postfix(ItemTooltip __instance, ItemTooltipInfo __0) => Apply(__instance._icon, IconFor(__0?.Item));
    }

    [HarmonyPatch]
    private static class LegacyContent
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipDefaultContent));
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipFullContent));
        }
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, UITooltipItem.ItemTooltipInfo __result)
        {
            if (__result == null || !JewelBaseType.IsJewel(__0)) return;
            __result.nameText = JewelBaseType.Names[__0.subType];
            __result.typeText = "Jewel";
        }
    }

    [HarmonyPatch(typeof(UITooltipItem), nameof(UITooltipItem.SetAsItemTooltip))]
    private static class LegacyIcons
    {
        [HarmonyPrefix]
        private static void Prefix(UITooltipItem __instance, ItemDataUnpacked __3)
        {
            var sprite = IconFor(__3);
            Apply(__instance.itemImage, sprite);
            Apply(__instance.smallItemImage, sprite);
            Apply(__instance.mediumItemImage, sprite);
            Apply(__instance.tallMediumItemImage, sprite);
            Apply(__instance.largeItemImage, sprite);
            Apply(__instance.spearItemImage, sprite);
            Apply(__instance.wideItemImage, sprite);
        }
    }
}