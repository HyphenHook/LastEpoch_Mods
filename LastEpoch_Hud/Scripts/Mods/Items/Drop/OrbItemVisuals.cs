using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Tooltips;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

/// <summary>Orb icons and tooltips for inventory, material stacks and ground drops.</summary>
internal static class OrbItemVisuals
{
    private static readonly Dictionary<GroundItemLabel, RectTransform> groundLabels = new();
    private static GroundItemLabel groundHover;

    internal static void TrackGroundLabel(GroundItemLabel label, RectTransform textRect)
    {
        if (textRect == null)
        {
            groundLabels.Remove(label);
        }
        else
        {
            groundLabels[label] = textRect;
        }
    }

    private static void TickGroundHover()
    {
        GroundItemLabel hit = null;
        RectTransform anchor = null;
        string key = null;
        foreach (var entry in groundLabels)
        {
            GroundItemLabel label = entry.Key;
            RectTransform rect = entry.Value;
            if (label.IsNullOrDestroyed() || rect.IsNullOrDestroyed() || !label.gameObject.activeInHierarchy)
            {
                continue;
            }
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            UnityEngine.Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera))
            {
                continue;
            }
            key = OrbDrop.KeyOf(label.getItemData()?.TryCast<ItemData>());
            if (key != null)
            {
                hit = label;
                anchor = rect;
                break;
            }
        }
        if (hit == groundHover)
        {
            return;
        }
        if (!groundHover.IsNullOrDestroyed() && hit == null)
        {
            TooltipItemManager.ForceClose();
        }
        groundHover = hit;
        if (hit != null) OpenOrbTooltip(key, anchor);
    }

    internal static TooltipItemManager OpenOrbTooltip(string key, RectTransform anchor)
    {
        ItemDataUnpacked data = OrbDrop.TooltipCarrier(key)?.TryCast<ItemDataUnpacked>();
        if (data.IsNullOrDestroyed() || anchor == null) return null;
        TooltipItemManager manager = UnityEngine.Object.FindObjectOfType<TooltipItemManager>();
        if (!manager.IsNullOrDestroyed())
        {
            manager.OpenTooltip(data, TooltipItemManager.SlotType.CRAFTING, Vector2.zero,
                anchor.position, anchor.gameObject, anchor.rect.size);
        }
        return manager;
    }

    // ------------------------------------------------------------------ inventory and stash icons

    private static readonly List<InventoryItemUI> views = new();
    private static float nextRefresh;

    private static void BindInventoryIcon(InventoryItemUI ui, ItemData data)
    {
        if (ui.IsNullOrDestroyed()) return;
        string key = OrbDrop.KeyOf(data);
        if (key == null)
        {
            if (views.Remove(ui) && !ui.contentImage.IsNullOrDestroyed()) ui.contentImage.overrideSprite = null;
            return;
        }
        if (!views.Contains(ui)) views.Add(ui);
        ApplyIcon(ui, key);
    }

    private static void ApplyIcon(InventoryItemUI ui, string key)
    {
        if (ui.contentImage.IsNullOrDestroyed()) return;
        AdvancedMaterial material = AdvancedMaterials.All.Find(m => m.Key == key);
        if (material == null) return;
        Sprite sprite = OrbArt.Get(material.Key, material.Tint, material.Glow);
        // The carrier's asynchronous image load writes sprite, so keep the orb on the override layer.
        if (ui.contentImage.overrideSprite != sprite) ui.contentImage.overrideSprite = sprite;
        ui.contentImage.color = Color.white;
    }

    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItemSprite))]
    private static class InventoryIconPatch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryItemUI __instance, ItemData __0) => BindInventoryIcon(__instance, __0);
    }

    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.Initialize))]
    private static class InventoryIconInitialized
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryItemUI __instance, ItemContainerEntry __1) =>
            BindInventoryIcon(__instance, __1.IsNullOrDestroyed() ? null : __1.data);
    }

    /// <summary>The game's own image loads can finish after the patch, so the icon is put back now and then.</summary>
    public static void Tick()
    {
        if (Time.unscaledTime < nextRefresh)
        {
            return;
        }
        nextRefresh = Time.unscaledTime + 0.25f;
        AdvancedMaterials.InitializeStorage();
        OrbInventoryTab.Tick();
        List<GroundItemLabel> staleLabels = null;
        foreach (var label in groundLabels.Keys)
        {
            if (label.IsNullOrDestroyed())
            {
                (staleLabels ??= new()).Add(label);
            }
        }
        if (staleLabels != null)
        {
            foreach (var label in staleLabels) groundLabels.Remove(label);
        }
        if (groundLabels.Count > 0 || !groundHover.IsNullOrDestroyed())
        {
            try
            {
                TickGroundHover();
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Orb ground hover: " + ex.Message);
            }
        }
        for (int i = views.Count - 1; i >= 0; i--)
        {
            InventoryItemUI ui = views[i];
            if (ui.IsNullOrDestroyed())
            {
                views.RemoveAt(i);
                continue;
            }
            string key = ui.EntryRef.IsNullOrDestroyed() ? null : OrbDrop.KeyOf(ui.EntryRef.data);
            if (key == null)
            {
                if (!ui.contentImage.IsNullOrDestroyed()) ui.contentImage.overrideSprite = null;
                views.RemoveAt(i);
            }
            else if (ui.gameObject.activeInHierarchy) ApplyIcon(ui, key);
        }
    }

    private const string OrbType = "Advanced Crafting Orb";
    private static readonly HashSet<Sprite> tooltipOrbSprites = new();

    private static AdvancedMaterial MaterialOf(ItemData item)
    {
        string key = OrbDrop.KeyOf(item);
        return key == null ? null : AdvancedMaterials.All.Find(m => m.Key == key);
    }

    private static string OrbDescription(AdvancedMaterial material)
    {
        string effect = material.Key == AdvancedMaterials.ReminiscenceOrb ? material.Description : material.Effect;
        string rules = string.Join("\n", material.Rules);
        return "<color=#FFB75A>" + effect + "</color>"
            + (rules.Length == 0 ? "" : "\n<color=#C8C0AA>" + rules + "</color>");
    }

    private static string OrbLore(AdvancedMaterial material) =>
        string.IsNullOrEmpty(material.Flavour) ? "" : "<i>" + material.Flavour + "</i>";

    private static Sprite OrbTooltipSprite(AdvancedMaterial material)
    {
        if (material == null) return null;
        Sprite sprite = OrbArt.Get(material.Key, material.Tint, material.Glow);
        if (sprite != null) tooltipOrbSprites.Add(sprite);
        return sprite;
    }

    private static void BindTooltipIcon(Image image, Sprite sprite)
    {
        if (image.IsNullOrDestroyed()) return;
        if (sprite != null)
        {
            // Native asynchronous icon loads write Image.sprite, which stays underneath the override.
            image.overrideSprite = sprite;
        }
        else if (image.overrideSprite != null && tooltipOrbSprites.Contains(image.overrideSprite))
        {
            image.overrideSprite = null;
        }
    }

    private static void BindLegacyIcons(UITooltipItem ui, ItemDataUnpacked item)
    {
        Sprite sprite = OrbTooltipSprite(MaterialOf(item));
        BindTooltipIcon(ui.itemImage, sprite);
        BindTooltipIcon(ui.smallItemImage, sprite);
        BindTooltipIcon(ui.mediumItemImage, sprite);
        BindTooltipIcon(ui.tallMediumItemImage, sprite);
        BindTooltipIcon(ui.largeItemImage, sprite);
        BindTooltipIcon(ui.spearItemImage, sprite);
        BindTooltipIcon(ui.wideItemImage, sprite);
    }

    [HarmonyPatch]
    private static class LegacyOrbContent
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipDefaultContent));
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipFullContent));
        }

        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, UITooltipItem.ItemTooltipInfo __result)
        {
            if (__result == null) return;
            AdvancedMaterial material = MaterialOf(__0);
            if (material == null) return;

            string color = ColorUtility.ToHtmlStringRGB(OrbDrop.LootColor(material.Key));
            __result.nameText = "<cspace=1.65><color=#" + color + ">" + material.Name + "</color></cspace>";
            __result.typeText = OrbType;
            __result.implicitText = OrbDescription(material);
            __result.loreText = OrbLore(material);
            __result.valueText = "";
            __result.secondaryValueText = "";
        }
    }

    [HarmonyPatch(typeof(UITooltipItem), nameof(UITooltipItem.SetAsItemTooltip))]
    private static class LegacyOrbIconBeforeShow
    {
        [HarmonyPrefix]
        private static void Prefix(UITooltipItem __instance, ItemDataUnpacked __3) => BindLegacyIcons(__instance, __3);
    }

    [HarmonyPatch(typeof(UITooltipItem), "SetItemImage",
        new[] { typeof(ItemDataUnpacked), typeof(UITooltipItem.ItemTooltipInfo), typeof(bool) })]
    private static class LegacyOrbIconAfterSetup
    {
        [HarmonyPostfix]
        private static void Postfix(UITooltipItem __instance, ItemDataUnpacked __0, bool __2)
        {
            if (!__2) BindLegacyIcons(__instance, __0);
        }
    }

    [HarmonyPatch(typeof(ItemTooltipContentBuilder), nameof(ItemTooltipContentBuilder.GetItemTooltipInfo))]
    private static class OrbContent
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, ItemTooltipInfo __result)
        {
            if (__result == null) return;
            AdvancedMaterial material = MaterialOf(__0);
            if (material == null) return;

            string lore = OrbLore(material);
            __result.Name = material.Name;
            __result.PrimaryType = OrbType;
            __result.SecondaryType = "";
            __result.Description = OrbDescription(material) + (lore.Length == 0 ? "" : "\n\n" + lore);
            __result.VendorInfo = null;
        }
    }

    [HarmonyPatch(typeof(ItemTooltip), nameof(ItemTooltip.SetTooltipInfo))]
    private static class OrbIcon
    {
        [HarmonyPrefix]
        private static void Prefix(ItemTooltip __instance, ItemTooltipInfo __0) =>
            BindTooltipIcon(__instance._icon, OrbTooltipSprite(MaterialOf(__0?.Item)));

        [HarmonyPostfix]
        private static void Postfix(ItemTooltip __instance, ItemTooltipInfo __0) =>
            BindTooltipIcon(__instance._icon, OrbTooltipSprite(MaterialOf(__0?.Item)));
    }
}
