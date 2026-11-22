using System;
using LastEpoch_Hud.Scripts.ModUI.Pages;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

/// <summary>Header settings panel (font size slider, light/dark mode); fields replaced on each HUD build.</summary>
internal static class HudSettingsPanel
{
    private static GameObject _panel;
    private static Slider _fontSlider;
    private static Text _fontValue;
    private static Button _lightModeButton;
    private static Button _darkModeButton;
    private static GameObject _window;
    private static Font _font;
    private static Action _onThemeChanged;

    public static void Build(GameObject window, Font font, Action onThemeChanged)
    {
        _window = window;
        _font = font;
        _onThemeChanged = onThemeChanged;

        _panel = Node(window, "LEHUD_SettingsPanel");
        RectTransform rect = _panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -HudTheme.HeaderHeight - 12f);
        rect.sizeDelta = new Vector2(420f, 250f);
        Image image = _panel.AddComponent<Image>();
        image.color = HudTheme.Surface;
        HudStyler.AddPrimaryBorder(_panel);

        Text title = TextNode(_panel, "Title", "Settings", HudTheme.CardTitleFontSize);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(22f, -54f);
        titleRect.offsetMax = new Vector2(-22f, -8f);
        title.alignment = TextAnchor.MiddleLeft;

        GameObject divider = Node(_panel, "TitleDivider");
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.anchoredPosition = new Vector2(0f, -58f);
        dividerRect.sizeDelta = new Vector2(-44f, HudTheme.BorderWidth);
        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = HudTheme.CardDivider;
        dividerImage.raycastTarget = false;

        Text fontLabel = TextNode(_panel, "FontSizeLabel", "Font Size", HudTheme.BodyFontSize);
        RectTransform fontLabelRect = fontLabel.GetComponent<RectTransform>();
        fontLabelRect.anchorMin = new Vector2(0f, 1f);
        fontLabelRect.anchorMax = new Vector2(1f, 1f);
        fontLabelRect.pivot = new Vector2(0.5f, 1f);
        fontLabelRect.offsetMin = new Vector2(22f, -98f);
        fontLabelRect.offsetMax = new Vector2(-110f, -66f);
        fontLabel.alignment = TextAnchor.MiddleLeft;

        _fontValue = TextNode(_panel, "FontSizeValue", "100%", HudTheme.ValueFontSize);
        RectTransform valueRect = _fontValue.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(1f, 1f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 1f);
        valueRect.anchoredPosition = new Vector2(-22f, -66f);
        valueRect.sizeDelta = new Vector2(82f, 32f);
        _fontValue.alignment = TextAnchor.MiddleRight;

        _fontSlider = BuildSlider(_panel);

        Text modeLabel = TextNode(_panel, "ModeLabel", "Appearance", HudTheme.BodyFontSize);
        RectTransform modeRect = modeLabel.GetComponent<RectTransform>();
        modeRect.anchorMin = new Vector2(0f, 1f);
        modeRect.anchorMax = new Vector2(1f, 1f);
        modeRect.pivot = new Vector2(0.5f, 1f);
        modeRect.offsetMin = new Vector2(22f, -166f);
        modeRect.offsetMax = new Vector2(-22f, -134f);
        modeLabel.alignment = TextAnchor.MiddleLeft;

        _lightModeButton = BuildChoice(
            _panel,
            "LightMode",
            "Light Mode",
            0f,
            22f,
            6f,
            () => SetLightMode(true)
        );
        _darkModeButton = BuildChoice(
            _panel,
            "DarkMode",
            "Dark Mode",
            0.5f,
            6f,
            22f,
            () => SetLightMode(false)
        );
        _panel.SetActive(false);
    }

    public static void Toggle()
    {
        if (_panel.IsNullOrDestroyed())
        {
            return;
        }
        bool show = !_panel.activeSelf;
        _panel.SetActive(show);
        if (!show)
        {
            return;
        }
        _panel.transform.SetAsLastSibling();
        Refresh();
    }

    public static void Refresh()
    {
        if (!_fontSlider.IsNullOrDestroyed())
        {
            _fontSlider.SetValueWithoutNotify(Mathf.Round(HudTheme.FontScale * 100f));
        }
        if (!_fontValue.IsNullOrDestroyed())
        {
            _fontValue.text = Mathf.RoundToInt(HudTheme.FontScale * 100f) + "%";
        }
        StyleChoice(_darkModeButton, !HudTheme.LightMode);
        StyleChoice(_lightModeButton, HudTheme.LightMode);
    }

    private static Slider BuildSlider(GameObject parent)
    {
        GameObject sliderObject = Node(parent, "FontSizeSlider");
        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -104f);
        rect.sizeDelta = new Vector2(-44f, 28f);

        GameObject track = Node(sliderObject, "Track");
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.sizeDelta = new Vector2(-8f, HudTheme.SliderTrackHeight);
        track.AddComponent<Image>().color = HudTheme.ControlTrack;

        GameObject handleArea = Node(sliderObject, "Handle Slide Area");
        Stretch(handleArea.GetComponent<RectTransform>());
        GameObject handle = Node(handleArea, "Handle");
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(HudTheme.SliderHandleSize, HudTheme.SliderHandleSize);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = HudTheme.ControlHandle;

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 80f;
        slider.maxValue = 140f;
        slider.wholeNumbers = true;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.value = Mathf.Round(HudTheme.FontScale * 100f);
        slider.colors = HudTheme.ButtonColors(HudTheme.ControlHandle, HudTheme.ControlHandle);
        SliderHook.Register(slider, OnFontSizeChanged);
        return slider;
    }

    private static Button BuildChoice(
        GameObject parent,
        string name,
        string caption,
        float anchorX,
        float leftInset,
        float rightInset,
        Action click
    )
    {
        GameObject obj = Node(parent, name);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchorX, 1f);
        rect.anchorMax = new Vector2(anchorX + 0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((leftInset - rightInset) * 0.5f, -180f);
        rect.sizeDelta = new Vector2(-leftInset - rightInset - 6f, 48f);
        Image image = obj.AddComponent<Image>();
        image.color = HudTheme.Surface;
        HudStyler.AddPrimaryBorder(obj);
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = HudTheme.ActionButtonColors(HudTheme.Surface, HudTheme.Selection);
        ButtonHook.Register(button, click);
        Text label = TextNode(obj, "Label", caption, HudTheme.ValueFontSize);
        Stretch(label.GetComponent<RectTransform>());
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private static void OnFontSizeChanged(float value)
    {
        float rounded = Mathf.Round(value / 5f) * 5f;
        if (Mathf.Abs(_fontSlider.value - rounded) > 0.01f)
        {
            _fontSlider.SetValueWithoutNotify(rounded);
        }
        HudTheme.SetFontScale(rounded / 100f);
        HudStyler.ApplyFontScale(_window);
        Refresh();
    }

    private static void SetLightMode(bool enabled)
    {
        HudTheme.SetLightMode(_window, enabled);
        HudStyler.NormalizeSelectableGraphics(_window);
        _onThemeChanged?.Invoke();
        MonolithTimelineEditor.RefreshSelection();
        Refresh();
    }

    private static void StyleChoice(Button button, bool selected)
    {
        if (button.IsNullOrDestroyed())
        {
            return;
        }
        Color normal = selected ? HudTheme.Selection : HudTheme.Surface;
        Image image = button.GetComponent<Image>();
        if (!image.IsNullOrDestroyed())
        {
            image.color = HudTheme.SelectableTint;
        }
        button.colors = HudTheme.ActionButtonColors(
            normal,
            selected ? HudTheme.Selection : HudTheme.SurfaceHover
        );
    }

    private static GameObject Node(GameObject parent, string name) =>
        HudElements.Node(parent, name);

    private static Text TextNode(GameObject parent, string name, string caption, int size) =>
        HudElements.Text(parent, name, caption, _font, size);

    private static void Stretch(RectTransform rect) => HudElements.Stretch(rect);
}
