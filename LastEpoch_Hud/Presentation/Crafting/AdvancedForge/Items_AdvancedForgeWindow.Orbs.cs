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
    // ---------------------------------------------------------------- the orbs and the Forge button

    private sealed class OrbView
    {
        public RectTransform Rect;
        public Image Frame, Art, Glow;
        public TextMeshProUGUI CountText;
        public AdvancedMaterial Material;
        public int Count;
        public bool Have => Count > 0;
    }

    private static readonly List<OrbView> orbViews = new();
    private static string selectedKey;
    private static RectTransform forgeRect;
    private static Image forgeImage;
    private static TextMeshProUGUI forgeLabel;
    private static Image forgeGlow;
    private static float forgeBlend; // 0 at rest, 1 under the pointer
    private static float nextOrbCountRefresh;
    private static TMP_FontAsset countFontCache;

    /// <summary>The font the game draws the count of a stack of items in.</summary>
    private static TMP_FontAsset CountFont()
    {
        if (countFontCache == null)
        {
            try
            {
                InventoryItemUI any = Object.FindObjectsOfType<InventoryItemUI>(true).FirstOrDefault(u => u != null && !u.quantityText.IsNullOrDestroyed());
                countFontCache = any?.quantityText.font;
            }
            catch (Exception)
            {
            }
        }
        return countFontCache;
    }

    private static void OrbRow()
    {
        orbViews.Clear();
        int n = AdvancedMaterials.All.Count;
        float total = n * OrbSize + (n - 1) * 14;
        float x = CenterX - total / 2;
        Label("Orbs", CenterX - 150, OrbY - 26, 300, 20, 15, Gold, TextAlignmentOptions.Center);
        foreach (AdvancedMaterial m in AdvancedMaterials.All)
        {
            int have = AdvancedMaterials.Count(m.Key);
            Image frame = CraftingWidgets.MakeImage(Content, "Orb_" + m.Key, Color.white);
            frame.sprite = OrbArt.Named("orb_slot_empty");
            RectTransform br = frame.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0, 1);
            br.pivot = new Vector2(0, 1);
            br.anchoredPosition = new Vector2(x, -OrbY);
            br.sizeDelta = new Vector2(OrbSize, OrbSize);
            dynamic.Add(frame.gameObject);

            Image glow = CraftingWidgets.MakeImage(frame.transform, "Glow", new Color(1f, 1f, 1f, 0.5f));
            glow.sprite = OrbArt.Named("glow_violet");
            glow.raycastTarget = false;
            RectTransform gr = glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = Vector2.zero;
            gr.sizeDelta = new Vector2(OrbSize * 0.92f, OrbSize * 0.92f);

            Image art = CraftingWidgets.MakeImage(frame.transform, "Art", Color.white);
            art.preserveAspect = true;
            art.raycastTarget = false;
            RectTransform ar = art.rectTransform;
            ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
            ar.pivot = new Vector2(0.5f, 0.5f);
            ar.anchoredPosition = Vector2.zero;
            ar.sizeDelta = new Vector2(OrbSize * 0.56f, OrbSize * 0.56f);

            // the count sits at the bottom right of the orb itself (the orb fills the middle 56% of the slot), a little way off it
            float orbRight = OrbSize / 2 + OrbSize * 0.28f, orbBottom = OrbSize / 2 + OrbSize * 0.28f;
            TextMeshProUGUI count = Label(have.ToString("N0"), orbRight + 2 - 30, orbBottom + 1 - 14, 30, 14, 10,
                have > 0 ? Color.white : Dim, TextAlignmentOptions.BottomRight, frame.transform).GetComponent<TextMeshProUGUI>();
            TMP_FontAsset countFont = CountFont();
            if (countFont != null)
            {
                count.font = countFont;
            }
            string key = m.Key;
            Button b = frame.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener((UnityEngine.Events.UnityAction)(() => SelectOrb(key)));
            orbViews.Add(new OrbView { Rect = br, Frame = frame, Art = art, Glow = glow, Material = m, Count = have, CountText = count });
            x += OrbSize + 14;
        }
        if (selectedKey != null && AdvancedMaterials.Count(selectedKey) < 1)
        {
            selectedKey = null;
        }
        ApplyOrbStates();
    }

    private static void TickOrbCounts()
    {
        float now = Time.unscaledTime;
        if (animating || now < nextOrbCountRefresh) return;
        nextOrbCountRefresh = now + 0.2f;
        foreach (OrbView view in orbViews)
        {
            int count = AdvancedMaterials.Count(view.Material.Key);
            if (count == view.Count) continue;
            view.Count = count;
            view.CountText.text = count.ToString("N0");
            view.CountText.color = view.Have ? Color.white : Dim;
            if (!view.Have && selectedKey == view.Material.Key)
            {
                selectedKey = null;
                dirty = true;
            }
        }
    }

    /// <summary>
    /// An orb slot is empty, hovered (a violet ring) or selected (a violet sphere that pulses behind the orb). An orb
    /// that has run out is grey and cannot be selected.
    /// </summary>
    private static void ApplyOrbStates()
    {
        for (int i = 0; i < orbViews.Count; i++)
        {
            OrbView v = orbViews[i];
            if (v.Frame == null)
            {
                continue;
            }
            bool selected = v.Have && selectedKey == v.Material.Key;
            bool hover = v.Have && !selected && hoveredNow == i;
            v.Frame.sprite = OrbArt.Named(selected ? "orb_slot_filled" : hover ? "orb_slot_hover" : "orb_slot_empty");
            v.Art.sprite = OrbArt.Get(v.Material.Key, v.Material.Tint, v.Material.Glow);
            v.Art.color = v.Have ? Color.white : new Color(0.48f, 0.48f, 0.48f);
            v.Glow.enabled = selected;
        }
    }

    private static void PulseOrbs()
    {
        float t = Time.unscaledTime;
        foreach (OrbView v in orbViews)
        {
            if (v.Glow != null && v.Glow.enabled)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 3.2f);
                v.Glow.color = new Color(1f, 1f, 1f, 0.28f + 0.42f * k);
                v.Glow.rectTransform.localScale = Vector3.one * (0.94f + 0.14f * k);
            }
        }
    }

    private static void SelectOrb(string key)
    {
        if (animating || MemoryClosing || AdvancedMaterials.Count(key) < 1)
        {
            return; // an orb that has run out cannot be selected
        }
        selectedKey = selectedKey == key ? null : key;
        status = "";
        dirty = true;
    }

    private static string OrbRestriction(string key) => key switch
    {
        AdvancedMaterials.EncapsulationOrb => Items_Encapsulation.WhyNot(item),
        AdvancedMaterials.ReminiscenceOrb => Items_Reminiscence.WhyNot(item),
        AdvancedMaterials.EchoOrb => Items_EchoOrb.WhyNot(item),
        AdvancedMaterials.ReverieOrb => Items_Reminiscence.ReverieRestriction(item),
        AdvancedMaterials.ChaosOrb => Items_ChaosReroll.WhyNot(item),
        AdvancedMaterials.AttunementOrb => Items_Attunement.WhyNot(item),
        _ => "Select an orb first.",
    };

    private static bool ForgeReady() => selectedKey != null && item != null
        && AdvancedMaterials.Count(selectedKey) > 0 && OrbRestriction(selectedKey).Length == 0;

    private static void ForgeButton(string caption = "Forge", bool memoryAction = false, bool echoAction = false)
    {
        float y = SlotY + FrameH + 4;
        Image img = CraftingWidgets.MakeImage(Content, "ForgeButton", Color.white);
        img.sprite = OrbArt.Named(memoryAction || echoAction ? "memory_button_plate" : "forge_button_frame");
        if (echoAction) img.color = new Color(0.61f, 0.95f, 1f);
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(CenterX - ForgeW / 2, -y);
        r.sizeDelta = new Vector2(ForgeW, ForgeH);
        dynamic.Add(img.gameObject);
        forgeRect = r;
        forgeImage = img;
        GameObject label = Label(caption, 0, 0, ForgeW, ForgeH, memoryAction || echoAction ? 14f : 20f, Gold, TextAlignmentOptions.Center, img.transform);
        forgeLabel = label.GetComponent<TextMeshProUGUI>();
        // a violet wash over the frame, which comes up when the pointer is on the button
        Image glow = CraftingWidgets.MakeImage(img.transform, "HoverWash", new Color(0.78f, 0.55f, 1f, 0f));
        glow.sprite = img.sprite;
        glow.raycastTarget = false;
        CraftingWidgets.Stretch(glow.rectTransform, 0);
        glow.transform.SetSiblingIndex(0);
        forgeGlow = glow;
        forgeBlend = 0f;
        RectTransform lr = label.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = memoryAction || echoAction ? new Vector2(23f, 13f) : Vector2.zero;
        lr.offsetMax = memoryAction || echoAction ? new Vector2(-23f, -15f) : Vector2.zero;
        if (memoryAction || echoAction)
        {
            forgeLabel.enableWordWrapping = false;
            forgeLabel.enableAutoSizing = true;
            forgeLabel.fontSizeMin = 10f;
            forgeLabel.fontSizeMax = 14f;
        }
        Button b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener((UnityEngine.Events.UnityAction)ForgeClicked);
        TintForge(false);
    }

    private static void RefuseMemoryButton()
    {
        const float width = 158f, height = 48f;
        Image face = CraftingWidgets.MakeImage(Content, "RefuseUnveil", Color.white);
        face.sprite = OrbArt.Named("memory_button_plate");
        RectTransform r = face.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(CenterX + ForgeW * 0.5f + 12f, -(SlotY + FrameH + 15f));
        r.sizeDelta = new Vector2(width, height);
        dynamic.Add(face.gameObject);
        Label("Refuse Unveil", 12f, 9f, width - 24f, height - 16f, 11f, Gold, TextAlignmentOptions.Center, face.transform);
        Button button = face.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(0.78f, 0.74f, 0.83f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.63f, 0.53f, 0.75f);
        button.colors = colors;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            if (animating || MemoryClosing) return;
            memoryRefuseAt = Time.unscaledTime;
            status = "";
        }));
    }

    private static void TintForge(bool hover)
    {
        if (forgeImage == null)
        {
            return;
        }
        bool ready = !animating && !MemoryClosing && (Items_Reminiscence.ObscuredOf(item) != null || ForgeReady());
        float target = ready && hover ? 1f : 0f;
        forgeBlend = Mathf.MoveTowards(forgeBlend, target, Time.unscaledDeltaTime * 7f);
        float dim = ready ? 1f : 0.5f;
        forgeImage.color = selectedKey == AdvancedMaterials.EchoOrb && Items_Reminiscence.ObscuredOf(item) == null
            ? new Color(0.61f * dim, 0.95f * dim, dim, ready ? 1f : 0.9f)
            : new Color(dim, dim, dim, ready ? 1f : 0.9f);
        if (forgeGlow != null)
        {
            forgeGlow.color = new Color(0.78f, 0.55f, 1f, 0.34f * forgeBlend);
        }
        if (forgeLabel != null)
        {
            Color rest = ready ? new Color(1f, 0.9f, 0.55f) : new Color(0.6f, 0.58f, 0.55f);
            forgeLabel.color = Color.Lerp(rest, new Color(1f, 1f, 0.92f), forgeBlend);
        }
    }


}
