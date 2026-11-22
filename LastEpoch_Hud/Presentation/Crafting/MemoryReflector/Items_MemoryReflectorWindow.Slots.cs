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
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_MemoryReflectorWindow
{
    private static Color ItemColour(ItemData item) => R.ItemColor(item);

    // ---------------------------------------------------------------- item slots

    private static readonly RectTransform[] slotRects = new RectTransform[2];
    private static readonly ItemData[] slotItems = new ItemData[2];
    private static readonly CanvasGroup[] slotGroups = new CanvasGroup[2];
    private static int hovered = -1;

    private static void SlotBox(ItemData item, int id, float x, float y, Action click)
    {
        Image box = CraftingWidgets.MakeImage(Content, "Slot", new Color(0.05f, 0.04f, 0.035f, 0.97f));
        RectTransform br = box.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0, 1);
        br.pivot = new Vector2(0, 1);
        br.anchoredPosition = new Vector2(x, -y);
        br.sizeDelta = new Vector2(IconW, IconH);
        dynamic.Add(box.gameObject);
        slotGroups[id] = box.gameObject.AddComponent<CanvasGroup>();
        if (item != null)
        {
            ItemIconSource icon = IconFor(item, id);
            if (icon == null || icon.Back == null)
            {
                box.color = Color.Lerp(new Color(0.05f, 0.04f, 0.035f, 0.97f), ItemColour(item), 0.2f);
            }
            if (icon != null)
            {
                if (icon.Back != null)
                {
                    Image back = CraftingWidgets.MakeImage(box.transform, "ItemBack", icon.BackColor);
                    back.sprite = icon.Back;
                    back.raycastTarget = false;
                    CraftingWidgets.Stretch(back.rectTransform, 4);
                }
                if (icon.Content != null)
                {
                    Image content = CraftingWidgets.MakeImage(box.transform, "ItemIcon", Color.white);
                    content.sprite = icon.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    CraftingWidgets.Stretch(content.rectTransform, 6);
                }
            }
            slotRects[id] = br;
            slotItems[id] = item;
        }
        DressWovenSlot(box, id, item);
        Button button = box.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(click);
    }
}
