using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The Advanced Crafting tab of the Crafting Materials panel: a second page next to the runes, glyphs and shards that
/// lists the mod's own crafting materials with their counts and lets the player use the selected one on the forge item.
/// </summary>
internal static class Items_AdvancedCraftingTab
{
    private const string TabsName = "AdvancedCraftingTabs";
    private const string PaneName = "AdvancedCraftingPane";
    private static readonly Color Gold = new(0.86f, 0.72f, 0.38f);
    private static readonly Color Dark = new(0.045f, 0.04f, 0.035f, 0.96f);

    private sealed class Ui
    {
        public GameObject Tabs, Pane;
        public Transform Content;
        public TextMeshProUGUI Name, Description, Status;
        public readonly Dictionary<string, (TextMeshProUGUI Count, Image Selected)> Slots = new();
        public string Selected = AdvancedMaterials.EncapsulationOrb;
    }

    private static Ui ui;

    [HarmonyPatch(typeof(CraftingMaterialsPanelUI), "OnOpen")]
    private static class OpenPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CraftingMaterialsPanelUI __instance) => Ensure(__instance);
    }

    private static void Ensure(CraftingMaterialsPanelUI panel)
    {
        try
        {
            if (panel.IsNullOrDestroyed())
            {
                return;
            }
            Transform root = panel.transform;
            if (ui == null || ui.Pane == null || ui.Tabs == null || root.Find(PaneName) == null)
            {
                Build(root);
            }
            if (ui != null)
            {
                Show(false);
            }
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Crafting tab: " + ex);
        }
    }

    // ---------------------------------------------------------------- building

    private static TextMeshProUGUI Label(Transform parent, string name, Transform fontSource, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = Object.Instantiate(fontSource.gameObject, parent, false);
        go.name = name;
        R.StripGameLogic(go);
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c != null && c.GetIl2CppType().Name == "UILocalization")
            {
                Object.Destroy(c);
            }
        }
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        return t;
    }

    private static GameObject Box(Transform parent, string name, float x, float y, float w, float h, Color fill)
    {
        Image img = R.MakeImage(parent, name, fill);
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        return img.gameObject;
    }

    private static void Stretch(RectTransform r, float pad)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(pad, pad);
        r.offsetMax = new Vector2(-pad, -pad);
    }

    private static void PlaceTop(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    /// <summary>A flat dark button with a thin gold line round it.</summary>
    private static GameObject MakeButton(Transform parent, string name, string text, float x, float y, float w, float h, Transform fontSource, Action click)
    {
        GameObject border = Box(parent, name, x, y, w, h, Gold * 0.7f);
        Image inner = R.MakeImage(border.transform, "Fill", Dark);
        Stretch(inner.rectTransform, 1.5f);
        inner.raycastTarget = false;
        TextMeshProUGUI label = Label(border.transform, "Text", fontSource, 17f, Gold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, 0);
        label.text = text;
        Button b = border.AddComponent<Button>();
        b.targetGraphic = border.GetComponent<Image>();
        b.onClick.AddListener(click);
        return border;
    }

    private static void Build(Transform root)
    {
        Transform content = root.Find("Content");
        Transform title = root.Find("Framing/Header/Title");
        if (content == null || title == null)
        {
            return;
        }
        foreach (string old in new[] { TabsName, PaneName })
        {
            Transform stale = root.Find(old);
            if (stale != null)
            {
                Object.Destroy(stale.gameObject);
            }
        }
        ui = new Ui { Content = content };

        // the two tabs, under the header
        ui.Tabs = new GameObject(TabsName);
        var tr = ui.Tabs.AddComponent<RectTransform>();
        ui.Tabs.transform.SetParent(root, false);
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0, -68);
        tr.sizeDelta = new Vector2(440, 30);
        MakeButton(ui.Tabs.transform, "BasicTab", "Runes, Glyphs and Shards", -114, 0, 210, 30, title, () => Show(false));
        MakeButton(ui.Tabs.transform, "AdvancedTab", "Advanced Crafting", 114, 0, 210, 30, title, () => Show(true));

        // the page that replaces the runes, glyphs and shards
        ui.Pane = new GameObject(PaneName);
        var pr = ui.Pane.AddComponent<RectTransform>();
        ui.Pane.transform.SetParent(root, false);
        R.CopyRect(content.GetComponent<RectTransform>(), pr);
        float x0 = -(AdvancedMaterials.All.Count - 1) * 48f; // slots are centred
        for (int i = 0; i < AdvancedMaterials.All.Count; i++)
        {
            BuildSlot(ui.Pane.transform, AdvancedMaterials.All[i], x0 + i * 96f, -10, title);
        }

        float infoTop = -140;
        ui.Name = Label(ui.Pane.transform, "Name", title, 24f, Gold, TextAlignmentOptions.Center);
        PlaceTop(ui.Name.rectTransform, 0, infoTop, 520, 34);
        ui.Description = Label(ui.Pane.transform, "Description", title, 17f, new Color(0.82f, 0.8f, 0.74f), TextAlignmentOptions.Top);
        PlaceTop(ui.Description.rectTransform, 0, infoTop - 40, 520, 110);
        MakeButton(ui.Pane.transform, "UseButton", "Use on the item in the forge", 0, infoTop - 160, 300, 38, title, UseSelected);
        ui.Status = Label(ui.Pane.transform, "Status", title, 18f, new Color(0.7f, 0.9f, 1f), TextAlignmentOptions.Top);
        PlaceTop(ui.Status.rectTransform, 0, infoTop - 210, 520, 70);
        ui.Pane.SetActive(false);
    }

    private static void BuildSlot(Transform parent, AdvancedMaterial m, float x, float y, Transform fontSource)
    {
        GameObject frame = Box(parent, "Slot_" + m.Key, x, y, 84, 84, Gold * 0.7f);
        Image inner = R.MakeImage(frame.transform, "Fill", Dark);
        Stretch(inner.rectTransform, 2);
        inner.raycastTarget = false;
        Image art = R.MakeImage(frame.transform, "Icon", m.Tint);
        art.raycastTarget = false;
        art.preserveAspect = true;
        art.sprite = BorrowIcon();
        Stretch(art.rectTransform, 10);
        Image selected = R.MakeImage(frame.transform, "Selected", new Color(1f, 0.85f, 0.4f, 0.22f));
        selected.raycastTarget = false;
        Stretch(selected.rectTransform, 2);
        TextMeshProUGUI count = Label(frame.transform, "Count", fontSource, 18f, Color.white, TextAlignmentOptions.BottomRight);
        Stretch(count.rectTransform, 6);
        Button b = frame.AddComponent<Button>();
        b.targetGraphic = frame.GetComponent<Image>();
        string key = m.Key;
        b.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            ui.Selected = key;
            Refresh();
        }));
        ui.Slots[m.Key] = (count, selected);
    }

    private static Sprite borrowed;

    /// <summary>A rune's own icon from the panel, until the orb has art of its own.</summary>
    private static Sprite BorrowIcon()
    {
        if (borrowed != null)
        {
            return borrowed;
        }
        try
        {
            foreach (Image img in Object.FindObjectsOfType<Image>(true))
            {
                if (img != null && img.sprite != null && img.gameObject.name == "Icon"
                    && img.transform.parent != null && img.transform.parent.parent != null
                    && img.transform.parent.parent.name == "ItemSlot")
                {
                    borrowed = img.sprite;
                    break;
                }
            }
        }
        catch (Exception)
        {
        }
        return borrowed;
    }

    // ---------------------------------------------------------------- behaviour

    private static void Show(bool advanced)
    {
        if (ui == null || ui.Content == null || ui.Pane == null)
        {
            return;
        }
        foreach (string name in new[] { "Runes and Glyphs", "Shards" })
        {
            Transform t = ui.Content.Find(name);
            if (t != null)
            {
                t.gameObject.SetActive(!advanced);
            }
        }
        ui.Pane.SetActive(advanced);
        ui.Status.text = "";
        Refresh();
    }

    private static void Refresh()
    {
        if (ui == null)
        {
            return;
        }
        foreach (AdvancedMaterial m in AdvancedMaterials.All)
        {
            if (ui.Slots.TryGetValue(m.Key, out var slot) && slot.Count != null)
            {
                slot.Count.text = AdvancedMaterials.Count(m.Key).ToString();
                slot.Selected.enabled = ui.Selected == m.Key;
            }
        }
        AdvancedMaterial current = AdvancedMaterials.All.Find(m => m.Key == ui.Selected);
        if (current != null)
        {
            ui.Name.text = current.Name;
            ui.Description.text = current.Description;
        }
    }

    private static void UseSelected()
    {
        try
        {
            if (ui.Selected == AdvancedMaterials.EncapsulationOrb)
            {
                bool ok = Items_Encapsulation.Use(out string message);
                ui.Status.color = ok ? new Color(0.7f, 0.95f, 0.75f) : new Color(1f, 0.55f, 0.5f);
                ui.Status.text = message;
            }
            Refresh();
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Crafting use: " + ex.Message);
        }
    }
}
