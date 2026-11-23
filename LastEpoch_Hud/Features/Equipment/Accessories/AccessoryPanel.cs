using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Accessories;

/// <summary>
/// The box on the inventory panel that holds the accessory slots: one Charm slot on the left and three Jewel slots on
/// the right. It only provides the box and where each slot goes; <c>CharmSlotUI</c> and <c>JewelSlotUI</c> fill it.
/// </summary>
internal static class AccessoryPanel
{
    private const float SlotGap = 2f;
    private static InventoryPanelUI panel;
    private static Image column;
    private static float panelSearchAt;
    private static float retryAt;
    private static string waitReason = "waiting for the inventory panel";
    private static float side;
    private static float boxHeight;

    /// <summary>Counts every time the box is rebuilt, so the slot views know to rebuild their slots in it.</summary>
    internal static int Generation { get; private set; }
    internal static RectTransform Box => column == null ? null : column.rectTransform;
    internal static int Layer => column == null ? 0 : column.gameObject.layer;
    internal static float Side => side;
    internal static bool Visible => column != null && column.gameObject.activeSelf;

    internal static string Inspect() => column == null ? waitReason
        : column.gameObject.activeInHierarchy ? "visible, one Charm left and three Jewels right"
        : "created, inventory closed";

    /// <summary>The Charm slot is vertically centred on the box.</summary>
    internal static Vector2 CharmPosition => new(0f, -(boxHeight - side) * 0.5f);

    /// <summary>Jewel slots 1 to 3, top to bottom. The column is lifted one slot plus a gap, so the bottom Jewel slot is level with the Charm slot.</summary>
    internal static Vector2 JewelPosition(int jewel) => new(side + SlotGap, side + SlotGap - (jewel - 1) * (side + SlotGap));

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
            if (column != null) UnityEngine.Object.Destroy(column.gameObject);
            panel = current;
            column = null;
            Generation++;
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
        if (column != null) column.gameObject.SetActive(visible);
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
        side = weapon.slotSize.x > 0f ? weapon.slotSize.x : 44.18f;
        float boxWidth = side * 2f + SlotGap;
        boxHeight = side * 3f + SlotGap * 2f;
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
        Generation++;
    }
}
