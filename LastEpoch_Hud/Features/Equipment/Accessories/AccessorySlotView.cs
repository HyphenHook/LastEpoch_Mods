using Il2Cpp;
using System;
using Il2CppLE.AssetBundles;
using Il2CppLE.AssetManagement;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Accessories;

/// <summary>One equipment slot on the accessory box: its frame, its item icon, hovering, tooltip and clicks.</summary>
internal sealed class AccessorySlotView
{
    private static AccessorySlotView hovered;
    private static TooltipItemManager tooltip;
    private static ItemDataUnpacked shown;

    private readonly int index;
    private readonly string slotName;
    private readonly string iconName;
    private readonly Func<ItemDataUnpacked, Sprite> customIcon;
    private readonly Func<Sprite> emptySilhouette;
    private Image slot;
    private Image icon;
    private Image empty;
    private LoadRef<Sprite> iconLoad;
    private long iconFor;

    /// <param name="index">The slot's place in the accessory store (0 Charm, 1 to 3 Jewels).</param>
    /// <param name="customIcon">Draws the item itself; null shows the game's own inventory icon.</param>
    /// <param name="emptySilhouette">A faint picture shown while the slot is empty; null shows none.</param>
    internal AccessorySlotView(int index, string slotName, string iconName,
        Func<ItemDataUnpacked, Sprite> customIcon = null, Func<Sprite> emptySilhouette = null)
    {
        this.index = index;
        this.slotName = slotName;
        this.iconName = iconName;
        this.customIcon = customIcon;
        this.emptySilhouette = emptySilhouette;
    }

    internal bool IsBuilt => slot != null;

    internal void Build(RectTransform box, int layer, float side, Vector2 position)
    {
        slot = R.MakeImage(box, slotName, Color.white);
        slot.gameObject.layer = layer;
        var slotRect = slot.rectTransform;
        slotRect.anchorMin = slotRect.anchorMax = new Vector2(0f, 1f);
        slotRect.pivot = new Vector2(0f, 1f);
        slotRect.sizeDelta = new Vector2(side, side);
        slotRect.anchoredPosition = position;
        slot.sprite = OrbArt.Named("charm_equipment_slot");
        if (emptySilhouette != null)
        {
            empty = R.MakeImage(slotRect, "Empty Jewel Silhouette", new Color(.65f, .65f, .65f, .3f));
            empty.gameObject.layer = layer;
            empty.sprite = emptySilhouette();
            empty.preserveAspect = true;
            empty.raycastTarget = false;
            empty.rectTransform.anchorMin = empty.rectTransform.anchorMax = new Vector2(.5f, .5f);
            empty.rectTransform.anchoredPosition = Vector2.zero;
            empty.rectTransform.sizeDelta = new Vector2(side * .70f, side * .70f);
        }
        icon = R.MakeImage(slotRect, iconName, Color.white);
        icon.gameObject.layer = layer;
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
        icon.rectTransform.sizeDelta = new Vector2(side * 0.83f, side * 0.83f);
        var trigger = slot.gameObject.AddComponent<EventTrigger>();
        Add(trigger, EventTriggerType.PointerEnter, _ => hovered = this);
        Add(trigger, EventTriggerType.PointerExit, _ => { ResetHover(); });
        Add(trigger, EventTriggerType.PointerClick, data =>
        {
            var pointer = data.TryCast<PointerEventData>();
            if (pointer?.button == PointerEventData.InputButton.Left) AccessoryStore.Move(index);
            else if (pointer?.button == PointerEventData.InputButton.Right)
            {
                if (AccessoryStore.ShiftHeld) AccessoryStore.DropEquipped(index);
                else AccessoryStore.UnequipToInventory(index);
            }
        });
        Add(trigger, EventTriggerType.BeginDrag, data =>
        {
            var pointer = data.TryCast<PointerEventData>();
            if (pointer?.button != PointerEventData.InputButton.Left) return;
            pointer.eligibleForClick = false;
            AccessoryStore.Move(index, takeOnly: true);
        });
        Add(trigger, EventTriggerType.Drop, data =>
        {
            var pointer = data.TryCast<PointerEventData>();
            if (pointer?.button == PointerEventData.InputButton.Left && pointer.pointerDrag != slot.gameObject)
                AccessoryStore.Move(index);
        });
    }

    /// <summary>Lets go of the slot after its box was destroyed.</summary>
    internal void Release()
    {
        iconLoad?.Dispose();
        iconLoad = null;
        iconFor = 0;
        slot = null;
        icon = null;
        empty = null;
        if (hovered == this) ResetHover();
    }

    /// <summary>Shows what is equipped, and the tooltip while the mouse is over the slot.</summary>
    internal void Refresh(ItemDataUnpacked item)
    {
        if (slot == null) return;
        if (empty != null) empty.enabled = item == null;
        icon.enabled = item != null;
        if (customIcon != null || item == null)
        {
            iconLoad?.Dispose();
            iconLoad = null;
            iconFor = 0;
        }
        else if (iconLoad == null || iconFor != (long)item.Pointer)
        {
            iconLoad?.Dispose();
            iconFor = (long)item.Pointer;
            SoftRef<Sprite> soft = item.GetItemSpriteFromData(ItemUIContext.Default);
            iconLoad = soft == null || !soft ? null : SoftRefExtensions.CreateLoadRef(soft, "LastEpoch_Hud", 0);
        }
        icon.sprite = item != null && customIcon != null ? customIcon(item)
            : iconLoad != null && iconLoad.Status == AssetStatus.Loaded ? iconLoad.AssetOrNull : null;
        slot.color = hovered == this ? new Color(1f, 0.91f, 0.72f) : Color.white;
        if (hovered != this || item == shown) return;
        CloseTooltip();
        if (item == null) return;
        tooltip = UnityEngine.Object.FindObjectOfType<TooltipItemManager>();
        var rect = slot.rectTransform;
        if (tooltip.IsNullOrDestroyed()) return;
        tooltip.OpenTooltip(item, TooltipItemManager.SlotType.CRAFTING, Vector2.zero,
            rect.position, slot.gameObject, rect.rect.size);
        shown = item;
    }

    /// <summary>The inventory is closed or gone: nothing is hovered any more.</summary>
    internal static void ResetHover()
    {
        hovered = null;
        CloseTooltip();
    }

    private static void CloseTooltip()
    {
        if (!tooltip.IsNullOrDestroyed() && shown != null
            && tooltip.ActiveItem?.individualID == shown.individualID)
            TooltipItemManager.ForceClose();
        tooltip = null;
        shown = null;
    }

    private static void Add(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> handler)
    {
        var entry = new EventTrigger.Entry { eventID = type, callback = new EventTrigger.TriggerEvent() };
        entry.callback.AddListener((UnityEngine.Events.UnityAction<BaseEventData>)(data => handler(data)));
        trigger.triggers.Add(entry);
    }
}
