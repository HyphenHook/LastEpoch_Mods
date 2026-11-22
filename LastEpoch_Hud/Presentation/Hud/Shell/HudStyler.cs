using System.Collections.Generic;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

/// <summary>Applies HudTheme tokens to runtime-built HUD controls. Font baselines clear on HUD re-bind (ResetFontBaselines); cleared card rows drop theirs (ForgetFontBaselines).</summary>
internal static class HudStyler
{
    private static readonly Dictionary<int, LegacyFontMetrics> _legacyFontSizes = new();
    private static readonly Dictionary<int, TmpFontMetrics> _tmpFontSizes = new();

    // Selectable color transitions tint their target Graphic, which makes an
    // Outline attached to that same Graphic look muted. Independent edge
    // images retain the exact primary color in every interaction state.
    public static void AddPrimaryBorder(
        GameObject target,
        float thickness = HudTheme.ControlBorderWidth
    )
    {
        if (target.IsNullOrDestroyed())
        {
            return;
        }
        float half = thickness * 0.5f;
        AddBorderEdge(
            target,
            "LEHUD_BorderTop",
            new Vector2(0f, 1f),
            Vector2.one,
            new Vector2(0f, -half),
            new Vector2(0f, thickness)
        );
        AddBorderEdge(
            target,
            "LEHUD_BorderBottom",
            Vector2.zero,
            new Vector2(1f, 0f),
            new Vector2(0f, half),
            new Vector2(0f, thickness)
        );
        AddBorderEdge(
            target,
            "LEHUD_BorderLeft",
            Vector2.zero,
            new Vector2(0f, 1f),
            new Vector2(half, 0f),
            new Vector2(thickness, 0f)
        );
        AddBorderEdge(
            target,
            "LEHUD_BorderRight",
            new Vector2(1f, 0f),
            Vector2.one,
            new Vector2(-half, 0f),
            new Vector2(thickness, 0f)
        );
    }

    public static void ApplySlider(Slider slider)
    {
        if (slider.IsNullOrDestroyed())
        {
            return;
        }
        GameObject background = Prefab.Child(slider.gameObject, "Background");
        Image backgroundImage = background.IsNullOrDestroyed()
            ? null
            : background.GetComponent<Image>();
        if (!backgroundImage.IsNullOrDestroyed())
        {
            backgroundImage.color = HudTheme.ControlTrack;
        }
        if (!slider.fillRect.IsNullOrDestroyed())
        {
            Image fill = slider.fillRect.GetComponent<Image>();
            if (!fill.IsNullOrDestroyed())
            {
                fill.color = HudTheme.Accent;
            }
        }
        if (!slider.targetGraphic.IsNullOrDestroyed())
        {
            slider.targetGraphic.color = HudTheme.ControlHandle;
        }
    }

    public static void ApplyDropdown(Dropdown dropdown)
    {
        if (dropdown.IsNullOrDestroyed())
        {
            return;
        }
        Image image = dropdown.GetComponent<Image>();
        if (!image.IsNullOrDestroyed())
        {
            image.color = HudTheme.SurfaceRaised;
        }
        dropdown.colors = HudTheme.ButtonColors(HudTheme.SurfaceRaised, HudTheme.Selection);
        if (!dropdown.template.IsNullOrDestroyed())
        {
            foreach (Image childImage in dropdown.template.GetComponentsInChildren<Image>(true))
            {
                string name = childImage.gameObject.name;
                if (name.Contains("Checkmark", System.StringComparison.OrdinalIgnoreCase))
                {
                    childImage.color = HudTheme.Accent;
                }
                else if (name.Contains("Handle", System.StringComparison.OrdinalIgnoreCase))
                {
                    childImage.color = HudTheme.AccentMuted;
                }
                else
                {
                    childImage.color = HudTheme.ControlBox;
                }
            }
            foreach (Toggle option in dropdown.template.GetComponentsInChildren<Toggle>(true))
            {
                if (!option.targetGraphic.IsNullOrDestroyed())
                {
                    option.targetGraphic.color = HudTheme.ControlBox;
                }
                if (!option.graphic.IsNullOrDestroyed())
                {
                    option.graphic.color = HudTheme.Accent;
                }
                option.colors = HudTheme.ButtonColors(HudTheme.ControlBox, HudTheme.SurfaceHover);
            }
            foreach (Text optionText in dropdown.template.GetComponentsInChildren<Text>(true))
            {
                optionText.color = HudTheme.TextPrimary;
            }
        }
    }

    public static void ApplyFontScale(GameObject root)
    {
        if (root.IsNullOrDestroyed())
        {
            return;
        }
        float fontScale = HudTheme.FontScale;
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            int id = text.GetInstanceID();
            if (!_legacyFontSizes.TryGetValue(id, out LegacyFontMetrics baseline))
            {
                baseline = new LegacyFontMetrics(text);
                _legacyFontSizes[id] = baseline;
            }
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(baseline.Size * fontScale));
            text.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(baseline.Minimum * fontScale));
            text.resizeTextMaxSize = Mathf.Max(8, Mathf.RoundToInt(baseline.Maximum * fontScale));
        }
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            int id = text.GetInstanceID();
            if (!_tmpFontSizes.TryGetValue(id, out TmpFontMetrics baseline))
            {
                baseline = new TmpFontMetrics(text);
                _tmpFontSizes[id] = baseline;
            }
            text.fontSize = Mathf.Max(8f, baseline.Size * fontScale);
            text.fontSizeMin = Mathf.Max(8f, baseline.Minimum * fontScale);
            text.fontSizeMax = Mathf.Max(8f, baseline.Maximum * fontScale);
        }
        Canvas.ForceUpdateCanvases();
    }

    public static void ResetFontBaselines()
    {
        _legacyFontSizes.Clear();
        _tmpFontSizes.Clear();
    }

    public static void ForgetFontBaselines(GameObject root)
    {
        if (root.IsNullOrDestroyed())
        {
            return;
        }
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            _legacyFontSizes.Remove(text.GetInstanceID());
        }
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            _tmpFontSizes.Remove(text.GetInstanceID());
        }
    }

    // ColorBlock values are the final visual colors. Keeping a tinted base
    // Graphic would multiply the two colors and make #0E0E10 appear black.
    public static void NormalizeSelectableGraphics(GameObject root)
    {
        if (root.IsNullOrDestroyed())
        {
            return;
        }
        foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
        {
            bool themed =
                selectable is Slider
                || HudTheme.IsThemedSelectableColor(selectable.colors.normalColor);
            if (themed && !selectable.targetGraphic.IsNullOrDestroyed())
            {
                selectable.targetGraphic.color = HudTheme.SelectableTint;
            }
        }
    }

    private static void AddBorderEdge(
        GameObject target,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta
    )
    {
        GameObject edge = Prefab.Child(target, name);
        if (edge.IsNullOrDestroyed())
        {
            edge = new GameObject(name);
            edge.layer = target.layer;
            edge.AddComponent<RectTransform>().SetParent(target.transform, false);
            Image image = edge.AddComponent<Image>();
            image.color = HudTheme.Border;
            image.raycastTarget = false;
        }
        RectTransform rect = edge.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        Image edgeImage = edge.GetComponent<Image>();
        edgeImage.color = HudTheme.Border;
        edgeImage.raycastTarget = false;
        edge.transform.SetAsLastSibling();
    }

    private readonly struct LegacyFontMetrics
    {
        public readonly int Size,
            Minimum,
            Maximum;

        public LegacyFontMetrics(Text text)
        {
            Size = text.fontSize;
            Minimum = text.resizeTextMinSize;
            Maximum = text.resizeTextMaxSize;
        }
    }

    private readonly struct TmpFontMetrics
    {
        public readonly float Size,
            Minimum,
            Maximum;

        public TmpFontMetrics(TMP_Text text)
        {
            Size = text.fontSize;
            Minimum = text.fontSizeMin;
            Maximum = text.fontSizeMax;
        }
    }
}
