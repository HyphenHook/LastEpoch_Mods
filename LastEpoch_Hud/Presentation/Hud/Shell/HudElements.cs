using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

/// <summary>Shared construction primitives for runtime-built HUD controls.</summary>
internal static class HudElements
{
    public static GameObject Node(GameObject parent, string name)
    {
        var node = new GameObject(name);
        node.layer = parent.layer;
        node.AddComponent<RectTransform>().SetParent(parent.transform, false);
        return node;
    }

    public static Text Text(
        GameObject parent,
        string name,
        string caption,
        Font font,
        int size,
        HorizontalWrapMode horizontalOverflow = HorizontalWrapMode.Overflow,
        bool localize = true
    )
    {
        var text = Node(parent, name).AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Normal;
        text.color = HudTheme.TextPrimary;
        text.horizontalOverflow = horizontalOverflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        if (localize)
            LocaleRegistry.Apply(text, caption);
        else
            text.text = caption;
        return text;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>First font used by any Text under root, or null.</summary>
    public static Font FindFont(GameObject root)
    {
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (!text.font.IsNullOrDestroyed())
            {
                return text.font;
            }
        }
        return null;
    }
}
