using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.Crafting;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_AdvancedForgeWindow
{
    // ---------------------------------------------------------------- hover: the orbs' own tooltip and the item's

    private static int hoveredNow = -1; // an orb's index, -2 for the item, -1 for nothing
    private const int FirstEchoHover = -3;
    private static bool tooltipOpen;

    private static UnityEngine.Camera PointerCamera()
    {
        Canvas canvas = root.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static void CloseTooltips()
    {
        hoveredNow = -1;
        if (tooltipOpen)
        {
            tooltipOpen = false;
            try
            {
                TooltipItemManager.ForceClose();
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Opens the game's own item tooltip for the item, beside the rectangle.</summary>
    private static void OpenGameTooltip(ItemData data, RectTransform at)
    {
        TooltipItemManager manager = Object.FindObjectOfType<TooltipItemManager>();
        ItemDataUnpacked unpacked = data?.TryCast<ItemDataUnpacked>();
        if (manager != null && unpacked != null)
        {
            manager.OpenTooltip(unpacked, ForgeTooltipSlot, Vector2.zero, at.position, at.gameObject, at.rect.size);
            tooltipOpen = true;
        }
    }

    private static void TickHover()
    {
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = PointerCamera();
        int hoveredEcho = TickEchoCards(mouse, cam);
        TickMemoryCards(Time.unscaledTime);
        int target = -1;
        for (int i = 0; target < 0 && i < orbViews.Count; i++)
        {
            if (orbViews[i].Rect != null && RectTransformUtility.RectangleContainsScreenPoint(orbViews[i].Rect, mouse, cam))
            {
                target = i;
                break;
            }
        }
        if (target < 0 && !animating && item != null && itemRect != null && RectTransformUtility.RectangleContainsScreenPoint(itemRect, mouse, cam))
        {
            target = -2;
        }
        if (target == -1 && hoveredEcho >= 0) target = FirstEchoHover - hoveredEcho;
        AffixCard.Tick(memoryViews, animating || MemoryClosing, cam);
        TintForge(forgeRect != null && RectTransformUtility.RectangleContainsScreenPoint(forgeRect, mouse, cam));
        if (target != hoveredNow)
        {
            if (tooltipOpen)
            {
                CloseTooltips();
            }
            hoveredNow = target;
            try
            {
                if (target >= 0)
                {
                    // an orb is shown with the game's own tooltip, through the item it stands in for on the ground
                    OpenGameTooltip(Drop.OrbDrop.TooltipCarrier(orbViews[target].Material.Key), orbViews[target].Rect);
                }
                else if (target == -2)
                {
                    OpenGameTooltip(item, itemRect);
                }
                else if (target <= FirstEchoHover)
                {
                    int index = FirstEchoHover - target;
                    OpenGameTooltip(echoPreviews[index], echoCards[index].Rect);
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Advanced Forge tooltip failed: " + ex.Message);
            }
        }
        ApplyOrbStates();
        PulseOrbs();
    }
}
