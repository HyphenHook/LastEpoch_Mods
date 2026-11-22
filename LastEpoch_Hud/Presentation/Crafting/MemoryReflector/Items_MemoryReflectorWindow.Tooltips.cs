using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_MemoryReflectorWindow
{
    private static UnityEngine.Camera PointerCamera()
    {
        Canvas canvas = root.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static void TickRows() => AffixCard.Tick(rows, animating, PointerCamera());

    private static void TickHover()
    {
        int now = -1;
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = PointerCamera();
        for (int i = 0; i < slotRects.Length; i++)
        {
            RectTransform r = slotRects[i];
            if (r != null && slotItems[i] != null && !animating && RectTransformUtility.RectangleContainsScreenPoint(r, mouse, cam))
            {
                now = i;
                break;
            }
        }
        if (now == hovered)
        {
            return;
        }
        CloseTooltip();
        hovered = now;
        if (now >= 0)
        {
            try
            {
                TooltipItemManager manager = Object.FindObjectOfType<TooltipItemManager>();
                ItemDataUnpacked data = slotItems[now].TryCast<ItemDataUnpacked>();
                RectTransform r = slotRects[now];
                if (manager != null && data != null)
                {
                    manager.OpenTooltip(data, TooltipItemManager.SlotType.CRAFTING, Vector2.zero, r.position, r.gameObject, r.rect.size);
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Memory Reflector tooltip failed: " + ex.Message);
            }
        }
    }

    private static void CloseTooltip()
    {
        if (hovered < 0)
        {
            return;
        }
        hovered = -1;
        try
        {
            TooltipItemManager.ForceClose();
        }
        catch (Exception)
        {
        }
    }
}
