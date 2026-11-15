using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

/// <summary>
/// What an Encapsulation Orb looks like wherever the game draws the rune it stands in for: its own icon, and a tooltip of
/// its own in place of the rune's.
/// </summary>
internal static class OrbItemVisuals
{
    private static AdvancedMaterial Material => AdvancedMaterials.All.Find(m => m.Key == AdvancedMaterials.EncapsulationOrb);

    private static Sprite OrbSprite()
    {
        AdvancedMaterial m = Material;
        return OrbArt.Get(m.Key, m.Tint, m.Glow);
    }

    // ------------------------------------------------------------------ inventory and stash icons

    private static readonly List<InventoryItemUI> views = new();
    private static float nextRefresh;

    private static void ApplyIcon(InventoryItemUI ui)
    {
        if (ui.IsNullOrDestroyed() || ui.contentImage.IsNullOrDestroyed())
        {
            return;
        }
        ui.contentImage.overrideSprite = null;
        ui.contentImage.sprite = OrbSprite();
        ui.contentImage.color = Color.white;
    }

    [HarmonyPatch(typeof(InventoryItemUI), "SetItemSprite")]
    private static class InventoryIconPatch
    {
        [HarmonyPostfix]
        private static void Postfix(InventoryItemUI __instance)
        {
            try
            {
                if (__instance.IsNullOrDestroyed())
                {
                    return;
                }
                bool orb = !__instance.EntryRef.IsNullOrDestroyed() && OrbDrop.IsOrb(__instance.EntryRef.data);
                if (!orb)
                {
                    views.Remove(__instance);
                    return;
                }
                if (!views.Contains(__instance))
                {
                    views.Add(__instance);
                }
                ApplyIcon(__instance);
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>The game's own image loads can finish after the patch, so the icon is put back now and then.</summary>
    public static void Tick()
    {
        // the tooltip is only hidden, never closed, by the game when the pointer moves on: the plate goes with it
        if (plate != null && (owner.IsNullOrDestroyed() || !owner.gameObject.activeInHierarchy))
        {
            ClosePlate();
        }
        if (Time.unscaledTime < nextRefresh)
        {
            return;
        }
        nextRefresh = Time.unscaledTime + 0.25f;
        for (int i = views.Count - 1; i >= 0; i--)
        {
            InventoryItemUI ui = views[i];
            if (ui.IsNullOrDestroyed() || ui.EntryRef.IsNullOrDestroyed() || !OrbDrop.IsOrb(ui.EntryRef.data))
            {
                views.RemoveAt(i);
            }
            else if (ui.gameObject.activeInHierarchy)
            {
                ApplyIcon(ui);
            }
        }
    }

    // ------------------------------------------------------------------ the tooltip

    private static UITooltipItem owner;
    private static RectTransform plate;

    private static Image[] Images(UITooltipItem ui) => new[]
    {
        ui.itemImage, ui.smallItemImage, ui.mediumItemImage, ui.tallMediumItemImage, ui.largeItemImage, ui.spearItemImage, ui.wideItemImage,
    };

    private static void ClosePlate()
    {
        if (plate != null)
        {
            UnityEngine.Object.Destroy(plate.gameObject);
        }
        plate = null;
        if (!owner.IsNullOrDestroyed())
        {
            CanvasGroup g = owner.GetComponent<CanvasGroup>();
            if (g != null)
            {
                g.alpha = 1f;
            }
        }
        owner = null;
    }

    [HarmonyPatch(typeof(UITooltipItem), "SetItemImage",
        new[] { typeof(ItemDataUnpacked), typeof(UITooltipItem.ItemTooltipInfo), typeof(bool) })]
    private static class TooltipPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UITooltipItem __instance, ItemDataUnpacked __0, bool __2)
        {
            try
            {
                if (__2 || __instance.IsNullOrDestroyed())
                {
                    return; // the comparison tooltip is not the one being hovered
                }
                if (__instance == owner)
                {
                    ClosePlate();
                }
                if (!OrbDrop.IsOrb(__0?.TryCast<ItemData>()))
                {
                    return;
                }
                Sprite sprite = OrbSprite();
                foreach (Image image in Images(__instance))
                {
                    if (!image.IsNullOrDestroyed())
                    {
                        image.overrideSprite = null;
                        image.sprite = sprite;
                    }
                }
                ClosePlate();
                owner = __instance;
                TMP_FontAsset font = __instance.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
                plate = OrbTooltipPlate.Build(__instance.transform.parent, font, Material);
                CanvasGroup group = __instance.GetComponent<CanvasGroup>() ?? __instance.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f; // the rune's own tooltip is there but unseen
                Sync();
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Orb tooltip: " + ex.Message);
            }
        }
    }

    private static void Sync()
    {
        if (plate == null)
        {
            return;
        }
        if (owner.IsNullOrDestroyed() || !owner.gameObject.activeInHierarchy)
        {
            ClosePlate();
            return;
        }
        RectTransform source = owner.GetComponent<RectTransform>();
        plate.pivot = source.pivot;
        plate.position = source.position;
        owner.GetComponent<CanvasGroup>().alpha = 0f;
        plate.transform.SetAsLastSibling();
    }

    [HarmonyPatch(typeof(UITooltipItem), "OnUpdateTick")]
    private static class TooltipTickPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UITooltipItem __instance)
        {
            try
            {
                if (__instance == owner)
                {
                    Sync();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
