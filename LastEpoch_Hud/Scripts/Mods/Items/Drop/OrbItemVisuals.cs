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

    // The game's own item tooltip is used, with its frame, fonts and layout: only what it says is changed.
    private static UITooltipItem owner;

    private static Image[] Images(UITooltipItem ui) => new[]
    {
        ui.itemImage, ui.smallItemImage, ui.mediumItemImage, ui.tallMediumItemImage, ui.largeItemImage, ui.spearItemImage, ui.wideItemImage,
    };

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (!text.IsNullOrDestroyed() && text.text != value)
        {
            text.text = value;
        }
    }

    private static void Rewrite(UITooltipItem ui)
    {
        AdvancedMaterial m = Material;
        Set(ui.nameText, "<cspace=1.65><color=#8FD8FFFF>" + m.Name + "</color></cspace>");
        Set(ui.typeText, "Advanced Crafting Orb");
        var body = new System.Text.StringBuilder();
        body.Append("<color=#8FD8FF>").Append(m.Effect).Append("</color>");
        foreach (string rule in m.Rules)
        {
            body.Append("\n").Append("<color=#C8C0AA>").Append(rule).Append("</color>");
        }
        Set(ui.implicitText, body.ToString());
        Set(ui.loreText, m.Flavour.Length > 0 ? "<i>" + m.Flavour + "</i>" : "");
        if (!ui.goldValueHolder.IsNullOrDestroyed() && ui.goldValueHolder.activeSelf)
        {
            ui.goldValueHolder.SetActive(false); // an orb has no vendor price
        }
        foreach (Image image in Images(ui))
        {
            if (!image.IsNullOrDestroyed() && image.sprite != OrbSprite())
            {
                image.overrideSprite = null;
                image.sprite = OrbSprite();
            }
        }
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
                if (OrbDrop.IsOrb(__0?.TryCast<ItemData>()))
                {
                    owner = __instance;
                    Rewrite(__instance);
                }
                else if (__instance == owner)
                {
                    owner = null;
                    if (!__instance.goldValueHolder.IsNullOrDestroyed())
                    {
                        __instance.goldValueHolder.SetActive(true);
                    }
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Orb tooltip: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(UITooltipItem), "OnUpdateTick")]
    private static class TooltipTickPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UITooltipItem __instance)
        {
            try
            {
                if (__instance == owner && __instance.gameObject.activeInHierarchy)
                {
                    Rewrite(__instance); // the game fills the tooltip in again as it updates
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
