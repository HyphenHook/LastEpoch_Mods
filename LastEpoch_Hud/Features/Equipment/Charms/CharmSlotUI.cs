using System;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppLE.AssetManagement;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using LastEpoch_Hud.Scripts.Mods.Items.Jewels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Charms;

internal static class CharmSlotUI
{
    private static InventoryPanelUI panel;
    private static Image column;
    private static readonly Image[] slots = new Image[CharmEquipment.SlotCount];
    private static readonly Image[] icons = new Image[CharmEquipment.SlotCount];
    private static readonly LoadRef<Sprite>[] iconLoads = new LoadRef<Sprite>[CharmEquipment.SlotCount];
    private static readonly long[] iconFor = new long[CharmEquipment.SlotCount];
    private static TooltipItemManager tooltip;
    private static ItemDataUnpacked shown;
    private static int hovered = -1;
    private static float panelSearchAt;
    private static float retryAt;
    private static string waitReason = "waiting for the inventory panel";

    internal static string Inspect() => column == null ? waitReason
        : column.gameObject.activeInHierarchy ? "visible, one Charm left and three Jewels right"
        : "created, inventory closed";

    internal static void Tick(bool available)
    {
        var current = Refs_Manager.InventoryPanelUI;
        if (current.IsNullOrDestroyed() && Time.unscaledTime >= panelSearchAt)
        {
            panelSearchAt = Time.unscaledTime + 1f;
            current = UnityEngine.Object.FindObjectOfType<InventoryPanelUI>();
        }
        if (panel != current)
        {
            CloseTooltip();
            for (int i = 0; i < CharmEquipment.SlotCount; i++)
            {
                iconLoads[i]?.Dispose();
                iconLoads[i] = null;
                iconFor[i] = 0;
                slots[i] = null;
                icons[i] = null;
            }
            if (column != null) UnityEngine.Object.Destroy(column.gameObject);
            panel = current;
            column = null;
            hovered = -1;
        }

        bool visible = available && !panel.IsNullOrDestroyed() && panel.gameObject.activeInHierarchy;
        if (column == null)
            waitReason = !available ? "waiting for an offline character"
                : panel.IsNullOrDestroyed() ? "no live inventory panel"
                : !panel.gameObject.activeInHierarchy ? "inventory panel closed" : waitReason;
        if (visible && column == null && Time.unscaledTime >= retryAt)
        {
            retryAt = Time.unscaledTime + 1f;
            Build();
        }
        if (column == null) return;
        column.gameObject.SetActive(visible);
        if (!visible)
        {
            hovered = -1;
            CloseTooltip();
            return;
        }
        for (int i = 0; i < CharmEquipment.SlotCount; i++)
        {
            var item = CharmEquipment.EquippedAt(i);
            icons[i].enabled = item != null;
            bool jewel = item != null && CharmEquipment.IsJewel(item);
            if (item != null && !jewel && (iconLoads[i] == null || iconFor[i] != (long)item.Pointer))
            {
                iconLoads[i]?.Dispose();
                iconFor[i] = (long)item.Pointer;
                SoftRef<Sprite> soft = item.GetItemSpriteFromData(ItemUIContext.Default);
                iconLoads[i] = soft == null || !soft ? null : SoftRefExtensions.CreateLoadRef(soft, "LastEpoch_Hud", 0);
            }
            if (item == null || jewel)
            {
                iconLoads[i]?.Dispose();
                iconLoads[i] = null;
                iconFor[i] = 0;
            }
            icons[i].sprite = jewel ? JewelVisuals.IconFor(item)
                : iconLoads[i] != null && iconLoads[i].Status == AssetStatus.Loaded ? iconLoads[i].AssetOrNull : null;
            slots[i].color = hovered == i ? new Color(1f, 0.91f, 0.72f) : Color.white;
        }
        if (hovered < 0) return;
        var hoveredItem = CharmEquipment.EquippedAt(hovered);
        if (hoveredItem == shown) return;
        CloseTooltip();
        if (hoveredItem == null) return;
        tooltip = UnityEngine.Object.FindObjectOfType<TooltipItemManager>();
        var rect = slots[hovered].rectTransform;
        if (tooltip.IsNullOrDestroyed()) return;
        tooltip.OpenTooltip(hoveredItem, TooltipItemManager.SlotType.CRAFTING, Vector2.zero,
            rect.position, slots[hovered].gameObject, rect.rect.size);
        shown = hoveredItem;
    }

    private static void Build()
    {
        var weaponNode = panel.transform.Find("InventoryHolder/Equipped Item Slots (Paper Doll)/weapon");
        var weapon = weaponNode?.GetComponent<ItemContainerUIWithBorder>();
        if (weapon.IsNullOrDestroyed())
        {
            waitReason = "waiting for the left weapon slot";
            return;
        }
        var weaponRect = weapon.GetComponent<RectTransform>();
        var parent = weaponRect?.parent?.GetComponent<RectTransform>();
        if (parent == null) return;
        float side = weapon.slotSize.x > 0f ? weapon.slotSize.x : 44.18f;
        const float slotGap = 2f;
        float boxWidth = side * 2f + slotGap;
        float boxHeight = side * 3f + slotGap * 2f;
        var position = weaponRect.localPosition;
        var bounds = weaponRect.rect;
        float centerX = position.x + bounds.center.x;
        float centerY = position.y + bounds.yMax + 5f + boxHeight * 0.5f;
        var helmetRect = parent.Find("helmet")?.GetComponent<RectTransform>();
        if (helmetRect != null)
        {
            float helmetTop = helmetRect.localPosition.y + helmetRect.rect.yMax;
            centerY = helmetTop - boxHeight * 0.5f;
        }

        column = R.MakeImage(parent, "Charm and Jewel Equipment", Color.clear);
        column.gameObject.layer = parent.gameObject.layer;
        var boxRect = column.rectTransform;
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(boxWidth, boxHeight);
        boxRect.anchoredPosition = new Vector2(centerX, centerY) - parent.rect.center;
        column.raycastTarget = false;
        for (int i = 0; i < CharmEquipment.SlotCount; i++)
        {
            int index = i;
            slots[i] = R.MakeImage(boxRect, i == 0 ? "Charm Equipment Slot" : "Jewel Equipment Slot " + i, Color.white);
            slots[i].gameObject.layer = parent.gameObject.layer;
            var slotRect = slots[i].rectTransform;
            slotRect.anchorMin = slotRect.anchorMax = new Vector2(0f, 1f);
            slotRect.pivot = new Vector2(0f, 1f);
            slotRect.sizeDelta = new Vector2(side, side);
            slotRect.anchoredPosition = i == 0
                ? new Vector2(0f, -(boxHeight - side) * 0.5f)
                : new Vector2(side + slotGap, -(i - 1) * (side + slotGap));
            slots[i].sprite = OrbArt.Named("charm_equipment_slot");
            icons[i] = R.MakeImage(slotRect, i == 0 ? "Charm Item Icon" : "Jewel Item Icon", Color.white);
            icons[i].gameObject.layer = parent.gameObject.layer;
            icons[i].raycastTarget = false;
            icons[i].preserveAspect = true;
            icons[i].rectTransform.anchorMin = icons[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icons[i].rectTransform.anchoredPosition = Vector2.zero;
            icons[i].rectTransform.sizeDelta = new Vector2(side * 0.83f, side * 0.83f);
            var trigger = slots[i].gameObject.AddComponent<EventTrigger>();
            Add(trigger, EventTriggerType.PointerEnter, _ => hovered = index);
            Add(trigger, EventTriggerType.PointerExit, _ => { hovered = -1; CloseTooltip(); });
            Add(trigger, EventTriggerType.PointerClick, data =>
            {
                var pointer = data.TryCast<PointerEventData>();
                if (pointer?.button == PointerEventData.InputButton.Left) CharmEquipment.Move(index);
                else if (pointer?.button == PointerEventData.InputButton.Right) CharmEquipment.UnequipToInventory(index);
            });
            Add(trigger, EventTriggerType.BeginDrag, data =>
            {
                var pointer = data.TryCast<PointerEventData>();
                if (pointer?.button != PointerEventData.InputButton.Left) return;
                pointer.eligibleForClick = false;
                CharmEquipment.Move(index, takeOnly: true);
            });
            Add(trigger, EventTriggerType.Drop, data =>
            {
                var pointer = data.TryCast<PointerEventData>();
                if (pointer?.button == PointerEventData.InputButton.Left && pointer.pointerDrag != slots[index].gameObject)
                    CharmEquipment.Move(index);
            });
        }

    }

    private static void Add(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> handler)
    {
        var entry = new EventTrigger.Entry { eventID = type, callback = new EventTrigger.TriggerEvent() };
        entry.callback.AddListener((UnityEngine.Events.UnityAction<BaseEventData>)(data => handler(data)));
        trigger.triggers.Add(entry);
    }

    private static void CloseTooltip()
    {
        if (!tooltip.IsNullOrDestroyed() && shown != null
            && tooltip.ActiveItem?.individualID == shown.individualID)
            TooltipItemManager.ForceClose();
        tooltip = null;
        shown = null;
    }
}
