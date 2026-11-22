using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>What an affix card shows.</summary>
internal sealed class AffixCardData
{
    public string Text = "";
    public int Tier; // displayed tier, 1 to 8; 0 shows none
    public int Group = -1; // AffixCard.Prefix, Suffix, Sealed, Primordial or Corrupted; -1 for none
    public bool Encapsulated, Reminded;
    public bool Static; // not clickable, and dimmed
    public bool ReadOnly; // not clickable, but shown as usual
    public bool On; // chosen
}

/// <summary>The parts of one drawn affix card, kept so its look can follow the pointer.</summary>
internal sealed class AffixCardView
{
    public RectTransform Rect;
    public Image Bg, Bar, Diamond;
    public readonly Image[] Edge = new Image[4];
    public TextMeshProUGUI Name, Tier;
    public Color Accent;
    public bool Encapsulated, Reminded, Static, On, Hover;
}

/// <summary>
/// The one way every custom window draws an affix: a card of one fixed size with the tier small on the left, a coloured
/// bar for what kind of affix it is, and the wording, all in capitals, which shrinks to fit the card and is never cut.
/// The window decides where cards go and what a click does; how a card looks lives here and nowhere else.
/// </summary>
internal static class AffixCard
{
    public const float Width = 226f, Height = 38f, Gap = 3f; // every card, in every window
    public const float TierSize = 8f, TextMax = 12f, TextMin = 6f;

    public const int Prefix = 0, Suffix = 1, Sealed = 2, Primordial = 3, Corrupted = 4;

    private static readonly string[] GroupNames = { "Prefixes", "Suffixes", "Sealed", "Primordial", "Corrupted" };

    private static readonly Color[] GroupColours =
    {
        new(0.93f, 0.76f, 0.38f), // prefix: gold
        new(0.42f, 0.82f, 0.74f), // suffix: teal
        new(0.68f, 0.52f, 0.92f), // sealed: violet
        new(0.95f, 0.50f, 0.30f), // primordial: ember
        new(0.82f, 0.28f, 0.50f), // corrupted: crimson
    };

    public static readonly Color Encapsulate = new(0.45f, 0.80f, 1f);
    private static readonly Color TierGrey = new(0.52f, 0.50f, 0.47f);
    private static readonly Color TierGreyLit = new(0.72f, 0.70f, 0.66f);
    private static readonly Color Base = new(0.035f, 0.03f, 0.026f, 0.82f);
    private static readonly Color Hovered = new(0.085f, 0.07f, 0.05f, 0.9f);
    private static readonly Color Chosen = new(0.13f, 0.10f, 0.055f, 0.94f);
    private static readonly Color ChosenHovered = new(0.17f, 0.13f, 0.065f, 0.96f);
    private static readonly Color MemoryAccent = new(0.79f, 0.57f, 1f);
    private static readonly Color MemoryBase = new(0.073f, 0.041f, 0.103f, 0.92f);
    private static readonly Color EncapsulateBase = new(0.035f, 0.07f, 0.095f, 0.92f);
    private static readonly Color EdgeBase = new(0.55f, 0.50f, 0.44f, 1f);
    private static readonly Color EdgeHover = new(0.9f, 0.8f, 0.6f, 1f);
    private static readonly Color EdgeChosen = new(1f, 0.82f, 0.4f, 1f);

    public static string GroupName(int group) => GroupNames[group];

    public static Color GroupColour(int group) => group < 0 ? EdgeBase : GroupColours[group];

    // the forge's own border pieces, thinned for a card
    private sealed class EdgeArt
    {
        public Sprite Sprite;
        public Image.Type Type;
        public float Thickness;
    }

    private static readonly EdgeArt[] edges = new EdgeArt[4];

    /// <summary>Takes the forge's border pieces for the cards. Safe to call again.</summary>
    public static void Prepare(Transform main)
    {
        string[] names = { "Framing/border-piece-top", "Framing/border-piece-bottom", "Framing/border-piece-left", "Framing/border-piece-right" };
        for (int i = 0; i < 4; i++)
        {
            Image img = main.Find(names[i])?.GetComponent<Image>();
            if (img == null || img.sprite == null)
            {
                continue;
            }
            Vector2 size = img.rectTransform.sizeDelta;
            float thick = i < 2 ? Mathf.Abs(size.y) : Mathf.Abs(size.x);
            edges[i] = new EdgeArt { Sprite = img.sprite, Type = img.type, Thickness = Mathf.Clamp(thick > 0 ? thick : 10, 2, 4) };
        }
    }

    /// <summary>Draws a card with its top left at (x, y) below the parent's top left.</summary>
    public static AffixCardView Build(Transform parent, GameObject labelTemplate, TMP_FontAsset bodyFont, AffixCardData data, float x, float y, List<GameObject> keep)
    {
        bool reminded = data.Reminded && !data.Encapsulated;
        Color accent = data.Encapsulated ? Encapsulate : reminded ? MemoryAccent : GroupColour(data.Group);
        Image bg = CraftingWidgets.MakeImage(parent, "AffixCard", data.Encapsulated ? EncapsulateBase : reminded ? MemoryBase : Base);
        RectTransform r = bg.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(Width, Height);
        keep?.Add(bg.gameObject);
        var view = new AffixCardView { Rect = r, Bg = bg, Accent = accent, Encapsulated = data.Encapsulated, Reminded = reminded, Static = data.Static, On = data.On };

        for (int side = 0; side < 4; side++)
        {
            EdgeArt e = edges[side];
            Image img = CraftingWidgets.MakeImage(bg.transform, "Edge", EdgeBase);
            img.raycastTarget = false;
            if (e != null)
            {
                img.sprite = e.Sprite;
                img.type = e.Type;
            }
            float t = e != null ? e.Thickness : 2f;
            RectTransform er = img.rectTransform;
            switch (side)
            {
                case 0: er.anchorMin = new Vector2(0, 1); er.anchorMax = new Vector2(1, 1); er.pivot = new Vector2(0.5f, 1); er.sizeDelta = new Vector2(0, t); break;
                case 1: er.anchorMin = new Vector2(0, 0); er.anchorMax = new Vector2(1, 0); er.pivot = new Vector2(0.5f, 0); er.sizeDelta = new Vector2(0, t); break;
                case 2: er.anchorMin = new Vector2(0, 0); er.anchorMax = new Vector2(0, 1); er.pivot = new Vector2(0, 0.5f); er.sizeDelta = new Vector2(t, 0); break;
                default: er.anchorMin = new Vector2(1, 0); er.anchorMax = new Vector2(1, 1); er.pivot = new Vector2(1, 0.5f); er.sizeDelta = new Vector2(t, 0); break;
            }
            er.anchoredPosition = Vector2.zero;
            view.Edge[side] = img;
        }

        // the colour of what kind of affix it is, down the left
        view.Bar = CraftingWidgets.Part(bg.transform, "Bar", accent, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(3, -10));

        // the tier: small, with a thin divider
        view.Tier = Text(bg.transform, labelTemplate, bodyFont, TextAlignmentOptions.Center, TierSize, 10, 0);
        RectTransform tr = view.Tier.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0);
        tr.anchorMax = new Vector2(0, 1);
        tr.pivot = new Vector2(0, 0.5f);
        tr.offsetMin = new Vector2(10, 0);
        tr.offsetMax = new Vector2(30, 0);
        view.Tier.enableAutoSizing = false;
        view.Tier.enableWordWrapping = false;
        view.Tier.overflowMode = TextOverflowModes.Overflow;
        view.Tier.text = data.Tier > 0 ? "T" + data.Tier : "";
        CraftingWidgets.Part(bg.transform, "Divider", new Color(0.55f, 0.46f, 0.32f, 0.55f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(1, -10));

        // the wording: a box of one size, and the text shrinks to fit it
        view.Name = Text(bg.transform, labelTemplate, bodyFont, TextAlignmentOptions.MidlineLeft, TextMax, 40, data.Encapsulated || reminded ? 24 : 8);
        view.Name.enableWordWrapping = true;
        view.Name.overflowMode = TextOverflowModes.Overflow;
        view.Name.enableAutoSizing = true;
        view.Name.fontSizeMin = TextMin;
        view.Name.fontSizeMax = TextMax;
        view.Name.text = data.Text;

        // a encapsulated affix has a diamond, in the middle of the card's height
        if (data.Encapsulated || reminded)
        {
            Image diamond = CraftingWidgets.MakeImage(bg.transform, "Diamond", reminded ? new Color(1f, 0.38f, 1f) : Color.white);
            diamond.sprite = OrbArt.Diamond();
            diamond.preserveAspect = true;
            diamond.raycastTarget = false;
            RectTransform dr = diamond.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(1, 0.5f);
            dr.pivot = new Vector2(1, 0.5f);
            dr.anchoredPosition = new Vector2(-6, 0);
            dr.sizeDelta = new Vector2(14, 14);
            view.Diamond = diamond;
        }
        if (data.Static || data.ReadOnly)
        {
            bg.raycastTarget = false;
        }
        Apply(view, 1f);
        return view;
    }

    private static TextMeshProUGUI Text(Transform parent, GameObject labelTemplate, TMP_FontAsset font, TextAlignmentOptions align, float size, float left, float right)
    {
        GameObject go = Object.Instantiate(labelTemplate, parent, false);
        CraftingWidgets.StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(left, 2);
        r.offsetMax = new Vector2(-right, -2);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            t.font = font;
        }
        t.alignment = align;
        t.fontSize = size;
        t.fontStyle = FontStyles.UpperCase;
        t.raycastTarget = false;
        go.SetActive(true);
        return t;
    }

    /// <summary>The colours of a card in its present state, eased towards by the blend (1 is at once).</summary>
    public static void Apply(AffixCardView v, float blend)
    {
        Color bg = v.Encapsulated && !v.On && !v.Hover ? EncapsulateBase : v.On ? (v.Hover ? ChosenHovered : Chosen) : v.Hover ? Hovered : v.Reminded ? MemoryBase : Base;
        Color edge = v.Static ? new Color(0.3f, 0.28f, 0.25f, 0.8f) : v.On ? EdgeChosen : v.Hover ? EdgeHover : EdgeBase;
        Color text = v.Encapsulated ? new Color(0.62f, 0.9f, 1f) : v.Reminded ? MemoryAccent
            : v.Static ? new Color(0.5f, 0.48f, 0.45f)
            : v.On ? new Color(1f, 0.93f, 0.68f) : v.Hover ? new Color(0.98f, 0.95f, 0.88f) : new Color(0.84f, 0.80f, 0.72f);
        v.Bg.color = Color.Lerp(v.Bg.color, bg, blend);
        foreach (Image e in v.Edge)
        {
            if (e != null)
            {
                e.color = Color.Lerp(e.color, edge, blend);
            }
        }
        v.Name.color = Color.Lerp(v.Name.color, text, blend);
        v.Tier.color = Color.Lerp(v.Tier.color, v.On || v.Hover ? TierGreyLit : TierGrey, blend);
        Color bar = v.Accent;
        bar.a = Mathf.Lerp(v.Bar.color.a, v.Static ? 0.45f : v.On ? 1f : v.Hover ? 0.9f : 0.65f, blend);
        v.Bar.color = bar;
    }

    /// <summary>Follows the pointer over the cards of a window: the one under it lights up.</summary>
    public static void Tick(List<AffixCardView> views, bool blocked, UnityEngine.Camera camera)
    {
        if (views.Count == 0)
        {
            return;
        }
        Vector2 mouse = Input.mousePosition;
        float blend = Mathf.Clamp01(Time.unscaledDeltaTime * 16f);
        foreach (AffixCardView v in views)
        {
            if (v.Rect == null)
            {
                continue;
            }
            v.Hover = !v.Static && !blocked && RectTransformUtility.RectangleContainsScreenPoint(v.Rect, mouse, camera);
            Apply(v, blend);
        }
    }
}
