using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

/// <summary>The Advance Materials inventory tab for stored crafting orbs.</summary>
internal static class OrbInventoryTab
{
    private static InventoryPanelUI panel;
    private static GameObject page;
    private static RectTransform row;
    private static bool buildFailed;
    private static readonly List<(AdvancedMaterial Material, Image Frame, Image Icon, TextMeshProUGUI Quantity)> slots = new();
    private static TooltipItemManager tooltip;
    private static string hoveredKey;

    internal static void Tick()
    {
        var current = Refs_Manager.InventoryPanelUI;
        if (panel != current)
        {
            CloseTooltip();
            panel = current;
            page = null;
            row = null;
            slots.Clear();
            buildFailed = false;
        }
        if (panel.IsNullOrDestroyed() || !panel.gameObject.activeInHierarchy)
        {
            if (hoveredKey != null) CloseTooltip();
            return;
        }
        if (buildFailed) return;
        try
        {
            if (page == null) TryBuildTab(panel.itemTabsController);
            if (page != null && page.activeInHierarchy) UpdateSlots();
            else if (hoveredKey != null) CloseTooltip();
        }
        catch (Exception ex)
        {
            buildFailed = true;
            if (page != null) page.SetActive(false);
            CloseTooltip();
            Main.logger_instance?.Warning("Inventory orb stacks: " + ex.Message);
        }
    }

    private static void TryBuildTab(TabUIController tabs)
    {
        if (tabs.IsNullOrDestroyed() || !tabs.initialilized || tabs.tabElements == null || tabs.tabContents == null) return;
        int id = tabs.tabContents.Count;
        int woven = InventoryPanelUI.WOVEN_ECHOES_TAB_ID;
        int resources = InventoryPanelUI.KEYS_TAB_ID;
        if (woven < 0 || woven >= id || resources < 0 || resources >= id || tabs.tabElements.Count != id) return;
        TabUIElement template = tabs.tabElements[resources];
        TabUIElement neighbor = tabs.tabElements[woven];
        GameObject sourcePage = tabs.tabContents[resources];
        if (template.IsNullOrDestroyed() || neighbor.IsNullOrDestroyed() || sourcePage == null) return;

        page = new GameObject("Advance Materials Tab");
        page.SetActive(false);
        RectTransform content = page.AddComponent<RectTransform>();
        RectTransform sourceRect = sourcePage.GetComponent<RectTransform>();
        content.SetParent(sourceRect.parent, false);
        content.anchorMin = sourceRect.anchorMin;
        content.anchorMax = sourceRect.anchorMax;
        content.pivot = sourceRect.pivot;
        content.sizeDelta = sourceRect.sizeDelta;
        content.anchoredPosition = sourceRect.anchoredPosition;
        content.SetSiblingIndex(tabs.tabContents[woven].transform.GetSiblingIndex() + 1);

        RectTransform resourceBody = sourcePage.transform.Find("Content")?.GetComponent<RectTransform>();
        Transform inventoryBackdrop = tabs.tabContents[woven].transform.Find("Content/panel-metallic-template-simplified");
        RectTransform body = CopyResourceBody(content, resourceBody, inventoryBackdrop);
        var resourceContainer = sourcePage.GetComponentInChildren<KeysItemContainerUI>(true);
        var quantityStyle = resourceContainer?.GetComponentInChildren<InventoryItemUI>(true)?.quantityText;
        BuildStacks(body, template.GetComponentInChildren<TextMeshProUGUI>(true), quantityStyle);

        // Clone under the inactive page so the copied button cannot enable with the Resources tab ID.
        GameObject tabObject = UnityEngine.Object.Instantiate(template.gameObject, content, false);
        tabObject.SetActive(false);
        tabObject.name = "Advance Materials Tab Button";
        var tab = tabObject.GetComponent<TabUIElement>();
        tab.controller = tabs;
        tab.elementID = id;
        tab.functionsToRun = new UnityEngine.Events.UnityEvent();
        tab.activeOrnament?.SetActive(false);
        tab.newContentNotificationIcon?.SetActive(false);
        LayoutElement tabLayout = tabObject.GetComponent<LayoutElement>();
        if (tabLayout != null)
        {
            tabLayout.minWidth = 140f;
            tabLayout.preferredWidth = 158f;
        }
        Transform labelRoot = tabObject.transform.Find("Label");
        if (labelRoot != null)
        {
            foreach (Behaviour behaviour in labelRoot.GetComponents<Behaviour>())
            {
                if (behaviour.GetIl2CppType().Name == "LocalizeStringEvent") behaviour.enabled = false;
            }
            var label = labelRoot.GetComponent<TextMeshProUGUI>();
            label.text = "Advance Materials";
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(label.fontSize, 12f);
            label.fontSizeMax = label.fontSize;
        }
        Transform slerp = tabObject.transform.Find("Key Slerp Target");
        if (slerp != null) slerp.gameObject.SetActive(false);
        tabObject.transform.SetParent(template.transform.parent, false);
        tabObject.transform.SetSiblingIndex(neighbor.transform.GetSiblingIndex() + 1);
        tabs.tabContents.Add(page);
        tabs.tabElements.Add(tab);
        tabObject.SetActive(true);
        tab.SetDisabled(false);
    }

    private static RectTransform CopyResourceBody(RectTransform parent, RectTransform source, Transform inventoryBackdrop)
    {
        var body = new GameObject("Content").AddComponent<RectTransform>();
        body.SetParent(parent, false);
        Transform backdrop = null;
        if (source != null)
        {
            body.anchorMin = source.anchorMin;
            body.anchorMax = source.anchorMax;
            body.pivot = source.pivot;
            body.sizeDelta = source.sizeDelta;
            body.anchoredPosition = source.anchoredPosition;

            backdrop = source.Find("panel-metallic-template-simplified");
            if (backdrop == null)
            {
                for (int i = 0; i < source.childCount; i++)
                {
                    Transform child = source.GetChild(i);
                    if (child.name.Contains("metallic", StringComparison.OrdinalIgnoreCase)
                        || child.name.Contains("background", StringComparison.OrdinalIgnoreCase)
                        || child.name.Equals("BG", StringComparison.OrdinalIgnoreCase))
                    {
                        backdrop = child;
                        break;
                    }
                }
            }
        }
        else
        {
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(5f, 41f);
            body.offsetMax = new Vector2(-5f, -5f);
        }
        if (backdrop == null) backdrop = inventoryBackdrop;
        if (backdrop != null)
        {
            // Reuse the decorative layers independently of the native item container.
            GameObject copy = UnityEngine.Object.Instantiate(backdrop.gameObject, body, false);
            copy.SetActive(true);
            foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            return body;
        }
        Image fallback = R.MakeImage(body, "Resources Background", new Color(0.087f, 0.075f, 0.060f, 0.98f));
        fallback.raycastTarget = false;
        fallback.rectTransform.anchorMin = Vector2.zero;
        fallback.rectTransform.anchorMax = Vector2.one;
        fallback.rectTransform.offsetMin = fallback.rectTransform.offsetMax = Vector2.zero;
        return body;
    }

    private static void BuildStacks(RectTransform parent, TextMeshProUGUI labelStyle, TextMeshProUGUI quantityStyle)
    {
        var node = new GameObject("AdvancedForgeOrbStacks");
        row = node.AddComponent<RectTransform>();
        row.SetParent(parent, false);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0f, 37f);
        const float box = 67f;
        int count = AdvancedMaterials.All.Count;
        float stride = Mathf.Clamp((parent.rect.width - 24f) / count, box + 8f, 112f);
        row.sizeDelta = new Vector2(stride * count, 110f);

        TMP_FontAsset font = labelStyle == null ? null : labelStyle.font;
        for (int i = 0; i < count; i++)
        {
            AdvancedMaterial material = AdvancedMaterials.All[i];
            Image frame = R.MakeImage(row, material.Key, Color.white);
            frame.sprite = OrbArt.Named("orb_slot_empty");
            RectTransform boxRect = frame.rectTransform;
            boxRect.sizeDelta = new Vector2(box, box);
            boxRect.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * stride, -6f);
            EventTrigger input = frame.gameObject.AddComponent<EventTrigger>();
            AddInteraction(input, EventTriggerType.PointerClick, material.Key);
            AddInteraction(input, EventTriggerType.BeginDrag, material.Key);

            Image icon = R.MakeImage(boxRect, "Orb", Color.white);
            icon.sprite = OrbArt.Get(material.Key, material.Tint, material.Glow);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(box * 0.56f, box * 0.56f);

            TextMeshProUGUI quantity = Text(boxRect, "0", quantityStyle == null ? font : quantityStyle.font,
                10f, TextAlignmentOptions.BottomRight);
            var counter = quantity.rectTransform;
            counter.anchorMin = Vector2.zero;
            counter.anchorMax = Vector2.one;
            counter.offsetMin = new Vector2(2f, 0f);
            counter.offsetMax = new Vector2(-4f, -1f);
            slots.Add((material, frame, icon, quantity));
        }
    }

    private static void AddInteraction(EventTrigger input, EventTriggerType kind, string key)
    {
        var entry = new EventTrigger.Entry { eventID = kind, callback = new EventTrigger.TriggerEvent() };
        entry.callback.AddListener((UnityEngine.Events.UnityAction<BaseEventData>)(data =>
        {
            var pointer = data.TryCast<PointerEventData>();
            if (pointer != null && pointer.button != PointerEventData.InputButton.Left) return;
            MoveOrb(key, kind == EventTriggerType.BeginDrag);
        }));
        input.triggers.Add(entry);
    }

    private static void MoveOrb(string key, bool dragging)
    {
        try
        {
            var cursor = ItemContainersManager.Instance?.cursor;
            if (cursor.IsNullOrDestroyed() || (dragging && cursor.HasContent())) return;
            if (!OrbInventoryItems.MoveStackOrb(key, out bool withdrawn)) return;
            CloseTooltip();
            if (withdrawn)
            {
                panel.itemTabsController?.Select(InventoryPanelUI.ITEM_TAB_ID, false, true);
            }
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Could not move an orb: " + ex.Message);
        }
    }

    private static TextMeshProUGUI Text(Transform parent, string value, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
    {
        var node = new GameObject("OrbStackLabel");
        node.AddComponent<RectTransform>().SetParent(parent, false);
        var text = node.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void UpdateSlots()
    {
        Canvas canvas = row.GetComponentInParent<Canvas>();
        UnityEngine.Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        string hit = null;
        RectTransform anchor = null;
        foreach (var slot in slots)
        {
            int amount = AdvancedMaterials.StackCount(slot.Material.Key);
            string count = amount.ToString();
            if (slot.Quantity.text != count) slot.Quantity.text = count;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(slot.Frame.rectTransform, Input.mousePosition, camera);
            slot.Frame.sprite = OrbArt.Named(over ? "orb_slot_hover" : "orb_slot_empty");
            slot.Icon.color = amount > 0 ? Color.white : new Color(0.64f, 0.64f, 0.68f, 0.44f);
            slot.Quantity.color = amount > 0 ? Color.white : new Color(0.60f, 0.57f, 0.54f);
            if (over)
            {
                hit = slot.Material.Key;
                anchor = slot.Frame.rectTransform;
            }
        }

        if (hit == hoveredKey) return;
        CloseTooltip();
        if (hit == null) return;
        tooltip = OrbItemVisuals.OpenOrbTooltip(hit, anchor);
        if (!tooltip.IsNullOrDestroyed()) hoveredKey = hit;
    }

    private static void CloseTooltip()
    {
        if (!tooltip.IsNullOrDestroyed() && hoveredKey != null && OrbDrop.KeyOf(tooltip.ActiveItem) == hoveredKey)
        {
            TooltipItemManager.ForceClose();
        }
        tooltip = null;
        hoveredKey = null;
    }
}
