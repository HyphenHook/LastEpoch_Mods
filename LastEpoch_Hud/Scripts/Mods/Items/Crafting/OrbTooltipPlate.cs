using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>The tooltip of an advanced crafting orb: a dark plate edged in the orb's light, used in the Advanced Forge and on the ground.</summary>
internal static class OrbTooltipPlate
{
    public const float Width = 300;

    private static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, string text, float x, float y, float w, float h, float size, Color colour, TextAlignmentOptions align)
    {
        var go = new GameObject("Text");
        var r = go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            t.font = font;
        }
        t.fontStyle = FontStyles.Normal;
        t.text = text;
        t.fontSize = size;
        t.color = colour;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Builds the plate under the parent, top left at the parent's top left. The caller places it.</summary>
    public static RectTransform Build(Transform parent, TMP_FontAsset font, AdvancedMaterial m)
    {
        const float pad = 18;
        float inner = Width - 2 * pad;
        Image plate = R.MakeImage(parent, "OrbTooltip", new Color(0.03f, 0.027f, 0.025f, 0.985f));
        plate.raycastTarget = false;
        RectTransform pr = plate.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0, 1);
        pr.pivot = new Vector2(0, 1);
        Color edge = Color.Lerp(m.Glow, new Color(0.6f, 0.5f, 0.3f), 0.5f);
        Transform t = plate.transform;
        R.Part(t, "EdgeTop", edge, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));
        R.Part(t, "EdgeBottom", edge, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 2));
        R.Part(t, "EdgeLeft", edge, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(2, 0));
        R.Part(t, "EdgeRight", edge, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(2, 0));

        Image art = R.MakeImage(t, "Art", Color.white);
        art.sprite = OrbArt.Get(m.Key, m.Tint, m.Glow);
        art.preserveAspect = true;
        art.raycastTarget = false;
        RectTransform ar = art.rectTransform;
        ar.anchorMin = ar.anchorMax = new Vector2(0, 1);
        ar.pivot = new Vector2(0, 1);
        ar.anchoredPosition = new Vector2(pad, -pad);
        ar.sizeDelta = new Vector2(46, 46);

        Text(t, font, m.Name, pad + 58, pad + 2, inner - 58, 24, 17, Color.Lerp(m.Glow, Color.white, 0.4f), TextAlignmentOptions.Left);
        Text(t, font, "Advanced Crafting Orb", pad + 58, pad + 28, inner - 58, 18, 12, new Color(0.62f, 0.6f, 0.56f), TextAlignmentOptions.Left);
        float y = pad + 46 + 14;
        R.Part(t, "Rule", edge * 0.7f, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -y), new Vector2(inner, 1));
        y += 14;
        Text(t, font, m.Effect, pad, y, inner, 22, 14, m.Glow, TextAlignmentOptions.Center);
        y += 30;
        foreach (string rule in m.Rules)
        {
            Text(t, font, rule, pad, y, inner, 20, 12, new Color(0.8f, 0.77f, 0.7f), TextAlignmentOptions.Center);
            y += 24;
        }
        y += 6;
        if (m.Flavour.Length > 0)
        {
            R.Part(t, "Rule2", edge * 0.7f, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -y), new Vector2(inner, 1));
            y += 14;
            Text(t, font, "<i>" + m.Flavour + "</i>", pad, y, inner, 22, 12, new Color(0.74f, 0.62f, 0.4f), TextAlignmentOptions.Center);
            y += 28;
        }
        Text(t, font, "Owned: " + AdvancedMaterials.Count(m.Key).ToString("N0"), pad, y, inner, 20, 12, new Color(0.62f, 0.6f, 0.56f), TextAlignmentOptions.Center);
        y += 20 + pad;
        pr.sizeDelta = new Vector2(Width, y);
        plate.transform.SetAsLastSibling();
        return pr;
    }
}
