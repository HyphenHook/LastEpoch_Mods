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
    // ---------------------------------------------------------------- the item slot

    private static RectTransform slotRect; // the whole ornate frame
    private static RectTransform itemRect; // what the frame leaves for the item

    private static void SlotBox(ItemData data)
    {
        Image frame = CraftingWidgets.MakeImage(Content, "SlotFrame", Color.white);
        frame.sprite = OrbArt.Named("forge_slot_frame");
        frame.preserveAspect = false;
        RectTransform br = frame.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0, 1);
        br.pivot = new Vector2(0, 1);
        br.anchoredPosition = new Vector2(CenterX - FrameW / 2, -SlotY);
        br.sizeDelta = new Vector2(FrameW, FrameH);
        dynamic.Add(frame.gameObject);
        slotRect = br;
        slotGlow = null;
        glimmers.Clear();
        if (data != null)
        {
            AmbientBelow(frame.transform);
        }

        var holder = new GameObject("Item");
        RectTransform hr = holder.AddComponent<RectTransform>();
        holder.transform.SetParent(frame.transform, false);
        hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
        hr.pivot = new Vector2(0.5f, 0.5f);
        hr.anchoredPosition = new Vector2(0, FrameH * (0.5f - ItemCentreFromTop));
        hr.sizeDelta = new Vector2(ItemW, ItemH);
        itemRect = hr;
        if (data != null)
        {
            ItemIconSource shown = IconFor(data);
            if (shown != null)
            {
                if (shown.Back != null)
                {
                    Image back = CraftingWidgets.MakeImage(holder.transform, "ItemBack", shown.BackColor);
                    back.sprite = shown.Back;
                    back.raycastTarget = false;
                    CraftingWidgets.Stretch(back.rectTransform, 2);
                }
                if (shown.Content != null)
                {
                    Image content = CraftingWidgets.MakeImage(holder.transform, "ItemIcon", Color.white);
                    content.sprite = shown.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    CraftingWidgets.Stretch(content.rectTransform, 10);
                }
            }
        }
        if (data != null)
        {
            AmbientAbove(frame.transform);
        }
        Button button = frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)ClickSlot);
    }
}
