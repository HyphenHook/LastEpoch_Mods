using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Mods.Character;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_RecombinatorWindow
{
    private static readonly RectTransform[] slotRects = new RectTransform[3];
    private static readonly ItemData[] slotItems = new ItemData[3];
    private static readonly RectTransform[] slotBoxes = new RectTransform[3];
    private static readonly Vector2[] slotBoxBase = new Vector2[3];
    private static readonly CanvasGroup[] slotGroups = new CanvasGroup[3];

    /// <summary>A forge-style item slot: the item's own icon and background, the forge's border, a click action.</summary>
    private static void SlotBox(ItemData item, int id, float x, float y, Action click)
    {
        // the ornate frame of the kit, which the item box sits in
        Rect fr = RecombKit.Frame(id);
        Image frame = CraftingWidgets.MakeImage(Content, "SlotFrame", Color.white);
        string fossilFrame = id == 0 ? "primordial_frame_left" : id == 1 ? "primordial_frame_right" : "primordial_frame_result";
        Sprite skin = PrimordialSkin ? OrbArt.Named(fossilFrame) : null;
        frame.sprite = skin != null ? skin : RecombArt.Base(RecombKit.FrameComponent(id));
        frame.raycastTarget = false;
        RectTransform frr = frame.rectTransform;
        frr.anchorMin = frr.anchorMax = new Vector2(0, 1);
        frr.pivot = new Vector2(0, 1);
        frr.anchoredPosition = new Vector2(fr.x, -fr.y);
        frr.sizeDelta = new Vector2(fr.width, fr.height);
        dynamic.Add(frame.gameObject);

        Image box = CraftingWidgets.MakeImage(Content, "Slot", new Color(0.05f, 0.04f, 0.035f, 0.97f));
        RectTransform br = box.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0, 1);
        br.pivot = new Vector2(0, 1);
        br.anchoredPosition = new Vector2(x, -y);
        br.sizeDelta = id == 2 ? new Vector2(IconW, IconH) : new Vector2(RecombKit.InW, RecombKit.ItemH);
        dynamic.Add(box.gameObject);
        slotGroups[id] = box.gameObject.AddComponent<CanvasGroup>();
        slotBoxes[id] = br;
        slotBoxBase[id] = br.anchoredPosition;

        if (item != null)
        {
            ItemIconSource icon = IconFor(item, id);
            if (icon == null || icon.Back == null)
            {
                // only until the game's own background has been read
                box.color = Color.Lerp(new Color(0.05f, 0.04f, 0.035f, 0.97f), ItemColor(item), 0.2f);
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
                    Image content = CraftingWidgets.MakeImage(box.transform, "ItemIcon", icon.ContentColor);
                    content.sprite = icon.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    CraftingWidgets.Stretch(content.rectTransform, 14);
                }
            }
            slotRects[id] = br;
            slotItems[id] = item;
        }
        Button button = box.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(click);
        frame.transform.SetAsLastSibling(); // each frame covers the item's own edges
        if (id < 2 && item != null)
        {
            AddInputFlames(id, x, y);
        }
    }

    /// <summary>
    /// A solid frame for an item slot, drawn over the item art so it truly borders it: a rim in the item's rarity
    /// colour, lit on the top and left and shadowed on the bottom and right, a dark inner line, and a stud at each corner.
    /// </summary>
    private static void SlotFrame(Image box, Color? rarity)
    {
        if (edges[0] != null && edges[0].Sprite != null)
        {
            // the forge's own frame pieces, over the item, with only a thin line of the rarity colour just inside
            ForgeBorder(box.transform, null);
            float th = Mathf.Min(edges[0].Thickness, 12f);
            Color line = rarity ?? new Color(0.5f, 0.4f, 0.24f);
            line.a = 0.9f;
            Transform ft = box.transform;
            CraftingWidgets.Part(ft, "RarityTop", line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -th), new Vector2(-2 * th, 2));
            CraftingWidgets.Part(ft, "RarityBottom", line, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, th), new Vector2(-2 * th, 2));
            CraftingWidgets.Part(ft, "RarityLeft", line, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(th, 0), new Vector2(2, -2 * th));
            CraftingWidgets.Part(ft, "RarityRight", line, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-th, 0), new Vector2(2, -2 * th));
            return;
        }
        Color c = rarity ?? new Color(0.50f, 0.40f, 0.24f);
        c = new Color(Mathf.Min(1f, c.r * 1.1f), Mathf.Min(1f, c.g * 1.1f), Mathf.Min(1f, c.b * 1.1f), 1f);
        Color lit = Color.Lerp(c, Color.white, 0.28f);
        Color dim = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f);
        const float t = 5f;
        Transform tf = box.transform;
        Vector2 mid = new(0.5f, 0.5f);
        CraftingWidgets.Part(tf, "RimTop", lit, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, t));
        CraftingWidgets.Part(tf, "RimLeft", lit, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(t, 0));
        CraftingWidgets.Part(tf, "RimBottom", dim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, t));
        CraftingWidgets.Part(tf, "RimRight", dim, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(t, 0));
        // a dark line just inside the rim, so the art sits in a recess
        Color shadow = new(0f, 0f, 0f, 0.7f);
        CraftingWidgets.Part(tf, "InTop", shadow, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -t), new Vector2(-2 * t, 2));
        CraftingWidgets.Part(tf, "InBottom", shadow, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, t), new Vector2(-2 * t, 2));
        CraftingWidgets.Part(tf, "InLeft", shadow, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(t, 0), new Vector2(2, -2 * t));
        CraftingWidgets.Part(tf, "InRight", shadow, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-t, 0), new Vector2(2, -2 * t));
        foreach (Vector2 corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
        {
            CraftingWidgets.Part(tf, "Stud", dim, corner, corner, mid, Vector2.zero, new Vector2(10, 10), 45f);
            CraftingWidgets.Part(tf, "StudCore", lit, corner, corner, mid, Vector2.zero, new Vector2(5, 5), 45f);
        }
    }

    /// <summary>The forge's own frame pieces (its sprites), laid round a slot.</summary>
    private static void ForgeBorder(Transform parent, Color? rarity = null)
    {
        for (int side = 0; side < 4; side++) // top, bottom, left, right
        {
            EdgeSprite e = edges[side];
            if (e == null || e.Sprite == null)
            {
                continue;
            }
            Color edge = e.Color;
            if (rarity.HasValue)
            {
                Color rc = rarity.Value;
                edge = new Color(Mathf.Min(1f, rc.r * 1.3f), Mathf.Min(1f, rc.g * 1.3f), Mathf.Min(1f, rc.b * 1.3f), 1f);
            }
            Image img = CraftingWidgets.MakeImage(parent, "Border", edge);
            img.sprite = e.Sprite;
            img.type = e.Type;
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            float t = e.Thickness;
            switch (side)
            {
                case 0:
                    r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, t);
                    break;
                case 1:
                    r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); r.sizeDelta = new Vector2(0, t);
                    break;
                case 2:
                    r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f); r.sizeDelta = new Vector2(t, 0);
                    break;
                default:
                    r.anchorMin = new Vector2(1, 0); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 0.5f); r.sizeDelta = new Vector2(t, 0);
                    break;
            }
            r.anchoredPosition = Vector2.zero;
        }
    }

    private sealed class EdgeSprite
    {
        public Sprite Sprite;
        public Image.Type Type;
        public Color Color;
        public float Thickness;
    }

    private static readonly EdgeSprite[] edges = new EdgeSprite[4];
    private static Sprite dividerSprite;
    private static float dividerHeight = 2;

    /// <summary>Reads the forge's frame and divider sprites, once.</summary>
    private static void CaptureForgeArt(Transform main)
    {
        string[] names = { "Framing/border-piece-top", "Framing/border-piece-bottom", "Framing/border-piece-left", "Framing/border-piece-right" };
        for (int i = 0; i < 4; i++)
        {
            Transform t = main.Find(names[i]);
            Image img = t != null ? t.GetComponent<Image>() : null;
            if (img == null)
            {
                continue;
            }
            Vector2 size = img.rectTransform.sizeDelta;
            float thick = i < 2 ? Mathf.Abs(size.y) : Mathf.Abs(size.x);
            edges[i] = new EdgeSprite { Sprite = img.sprite, Type = img.type, Color = img.color, Thickness = Mathf.Clamp(thick > 0 ? thick : 10, 6, 12) };
        }
        Transform sep = main.Find("BottomVisuals/seperator");
        Image sepImg = sep != null ? sep.GetComponent<Image>() : null;
        if (sepImg != null)
        {
            dividerSprite = sepImg.sprite;
            dividerHeight = Mathf.Clamp(Mathf.Abs(sepImg.rectTransform.sizeDelta.y), 2, 8);
        }
    }

    private static void Divider(float x, float y, float w)
    {
        Image img = CraftingWidgets.MakeImage(Content, "Divider", dividerSprite != null ? Color.white : new Color(0.5f, 0.4f, 0.22f, 0.8f));
        if (dividerSprite != null)
        {
            img.sprite = dividerSprite;
        }
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, dividerSprite != null ? dividerHeight : 2);
        dynamic.Add(img.gameObject);
    }

}
