using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

/// <summary>Visual tokens, palettes and saved theme preferences for the HUD. Preferences load from PlayerPrefs once per game session (LoadPreferences); setters update memory and PlayerPrefs; never reset on HUD re-bind.</summary>
public static class HudTheme
{
    private readonly struct Palette
    {
        public readonly Color Backdrop,
            Background,
            Surface,
            Hover,
            Pressed,
            Selection;
        public readonly Color Accent,
            AccentSoft,
            AccentMuted,
            CardDivider;
        public readonly Color Text,
            TextMuted,
            Handle,
            Disabled,
            ForceDropSetText,
            ForceDropCorruptionText,
            ForceDropIdolText;

        public Palette(
            Color backdrop,
            Color background,
            Color surface,
            Color hover,
            Color pressed,
            Color selection,
            Color accent,
            Color accentSoft,
            Color accentMuted,
            Color cardDivider,
            Color text,
            Color textMuted,
            Color handle,
            Color disabled,
            Color forceDropSetText,
            Color forceDropCorruptionText,
            Color forceDropIdolText
        )
        {
            Backdrop = backdrop;
            Background = background;
            Surface = surface;
            Hover = hover;
            Pressed = pressed;
            Selection = selection;
            Accent = accent;
            AccentSoft = accentSoft;
            AccentMuted = accentMuted;
            CardDivider = cardDivider;
            Text = text;
            TextMuted = textMuted;
            Handle = handle;
            Disabled = disabled;
            ForceDropSetText = forceDropSetText;
            ForceDropCorruptionText = forceDropCorruptionText;
            ForceDropIdolText = forceDropIdolText;
        }
    }

    private static readonly Palette DarkPalette = new(
        Rgba(0, 0, 0, 166),
        Rgba(21, 21, 21, 255), // #151515 main background
        Rgba(14, 14, 16, 255), // #0E0E10 cards and controls
        Rgba(35, 35, 39, 255),
        Rgba(52, 43, 34, 255),
        Rgba(40, 40, 44, 255), // selected items are visibly lighter
        Rgba(213, 180, 122, 255), // #D5B47A
        Rgba(213, 180, 122, 166),
        Rgba(213, 180, 122, 205),
        Rgba(255, 255, 255, 102),
        Rgba(250, 250, 250, 255),
        Rgba(250, 250, 250, 184),
        Color.white,
        Rgba(85, 83, 80, 160),
        new Color(.42f, .90f, .44f),
        new Color(.80f, .56f, 1f),
        new Color(.25f, .90f, 1f)
    );

    private static readonly Palette LightPalette = new(
        Rgba(0, 0, 0, 120),
        Rgba(241, 241, 243, 255),
        Rgba(255, 255, 255, 255),
        Rgba(235, 228, 218, 255),
        Rgba(222, 207, 184, 255),
        Rgba(231, 221, 206, 255),
        Rgba(151, 101, 31, 255),
        Rgba(151, 101, 31, 166),
        Rgba(151, 101, 31, 205),
        Rgba(0, 0, 0, 72),
        Rgba(24, 24, 27, 255),
        Rgba(63, 63, 70, 210),
        Rgba(55, 55, 60, 255),
        Rgba(145, 145, 150, 170),
        new Color(.12f, .50f, .16f),
        new Color(.47f, .22f, .68f),
        new Color(.04f, .45f, .58f)
    );

    private static bool preferencesLoaded;
    private static bool lightMode;
    private static float fontScale = 1f;

    private static Palette Current => lightMode ? LightPalette : DarkPalette;
    public static bool LightMode => lightMode;
    public static float FontScale => fontScale;

    public static Color Backdrop => Current.Backdrop;
    public static Color Background => Current.Background;
    public static Color Header => Current.Background;
    public static Color Sidebar => Current.Background;
    public static Color Surface => Current.Surface;
    public static Color SurfaceRaised => Current.Surface;
    public static Color SurfaceHover => Current.Hover;
    public static Color SurfacePressed => Current.Pressed;
    public static Color Selection => Current.Selection;
    public static Color Accent => Current.Accent;
    public static Color AccentBright => Current.Accent;
    public static Color AccentSoft => Current.AccentSoft;
    public static Color AccentMuted => Current.AccentMuted;
    public static Color Border => Current.Accent;
    public static Color CardDivider => Current.CardDivider;
    public static Color TextPrimary => Current.Text;
    public static Color TextSecondary => Current.Text;
    public static Color TextMuted => Current.TextMuted;
    public static Color ControlHandle => Current.Handle;
    public static Color ControlTrack => Current.Accent;
    public static Color ControlBox => Current.Surface;
    public static Color ControlCheck => Current.Accent;
    public static Color InputBackground => Current.Surface;
    public static Color ItemSelection => Current.Selection;
    public static Color ControlDisabled => Current.Disabled;
    public static Color ForceDropSetText => Current.ForceDropSetText;
    public static Color ForceDropCorruptionText => Current.ForceDropCorruptionText;
    public static Color ForceDropIdolText => Current.ForceDropIdolText;
    public static Color Transparent => Color.clear;

    // Unity ColorBlocks multiply the target graphic by the selected state color.
    // Keep selectable graphics neutral so semantic ButtonColors render exactly.
    public static Color SelectableTint => Color.white;

    public static readonly Vector2 WindowAnchorMin = new(0.08f, 0.08f);
    public static readonly Vector2 WindowAnchorMax = new(0.92f, 0.92f);

    public const float HeaderHeight = 72f;
    public const float SidebarWidth = 270f;
    public const float ContentPadding = 24f;
    public const float CardGap = 18f;
    public const float CardHorizontalInset = 34f;
    public const float BorderWidth = 1f;
    public const float ControlBorderWidth = 2f;
    public const float SectionHeight = 52f;
    public const float PageHeight = 44f;
    public const float NavigationIndent = 44f;
    public const float RowHeight = 46f;
    public const float ToggleSize = 22f;
    public const float SliderCardPadding = 34f;
    public const float SliderCardRowHeight = 82f;
    public const float SliderCardRowGap = 12f;
    public const float SliderTrackHeight = 6f;
    public const float SliderHandleSize = 18f;
    public const float SliderValueWidth = 126f;
    public const float SliderScrollbarWidth = 12f;
    public const float ActionButtonHeight = 54f;
    public const float ActionButtonGap = 12f;
    public const float SearchAnchorX = 0.59f;
    public const float SearchWidth = 520f;
    public const float SearchHeight = 44f;
    public const float SearchButtonWidth = 92f;
    public const float SearchResultsWidth = 590f;
    public const float SearchResultHeight = 58f;
    public const float SearchCornerRadius = 8f;

    public const int BrandFontSize = 34;
    public const int SectionFontSize = 24;
    public const int PageFontSize = 20;
    public const int CardTitleFontSize = 25;
    public const int BodyFontSize = 17;
    public const int ValueFontSize = 16;
    public const int SliderCardTitleFontSize = 32;

    public static ColorBlock ButtonColors(Color normal, Color selected)
    {
        return new ColorBlock
        {
            normalColor = normal,
            highlightedColor = SurfaceHover,
            pressedColor = SurfacePressed,
            selectedColor = selected,
            disabledColor = ControlDisabled,
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };
    }

    public static ColorBlock ActionButtonColors(Color normal, Color selected)
    {
        var colors = ButtonColors(normal, selected);
        colors.highlightedColor = SurfaceHover;
        colors.pressedColor = AccentMuted;
        colors.fadeDuration = 0.04f;
        return colors;
    }

    public static void LoadPreferences()
    {
        if (preferencesLoaded)
            return;
        preferencesLoaded = true;
        lightMode = PlayerPrefs.GetInt("LEHUD.LightMode", 0) != 0;
        fontScale = Mathf.Clamp(PlayerPrefs.GetFloat("LEHUD.FontScale", 1f), 0.8f, 1.4f);
    }

    public static void SetLightMode(GameObject root, bool enabled)
    {
        LoadPreferences();
        if (lightMode == enabled)
            return;
        Palette previous = Current;
        lightMode = enabled;
        ApplyPalette(root, previous, Current);
        PlayerPrefs.SetInt("LEHUD.LightMode", enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetFontScale(float scale)
    {
        LoadPreferences();
        fontScale = Mathf.Clamp(Mathf.Round(scale * 20f) / 20f, 0.8f, 1.4f);
        PlayerPrefs.SetFloat("LEHUD.FontScale", fontScale);
        PlayerPrefs.Save();
    }

    public static bool IsThemedSelectableColor(Color normal) =>
        Same(normal, Current.Surface)
        || Same(normal, Current.Background)
        || Same(normal, Current.Selection)
        || Same(normal, Current.Hover)
        || Same(normal, Current.Accent)
        || Same(normal, Current.AccentMuted);

    private static void ApplyPalette(GameObject root, Palette from, Palette to)
    {
        if (root.IsNullOrDestroyed())
            return;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
            image.color = Map(image.color, from, to);
        foreach (var text in root.GetComponentsInChildren<Text>(true))
            text.color = Map(text.color, from, to);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            text.color = Map(text.color, from, to);
        foreach (var outline in root.GetComponentsInChildren<Outline>(true))
            outline.effectColor = Map(outline.effectColor, from, to);
        foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
        {
            var colors = selectable.colors;
            colors.normalColor = Map(colors.normalColor, from, to);
            colors.highlightedColor = Map(colors.highlightedColor, from, to);
            colors.pressedColor = Map(colors.pressedColor, from, to);
            colors.selectedColor = Map(colors.selectedColor, from, to);
            colors.disabledColor = Map(colors.disabledColor, from, to);
            selectable.colors = colors;
        }
    }

    private static Color Map(Color value, Palette from, Palette to)
    {
        if (Same(value, from.Backdrop))
            return to.Backdrop;
        if (Same(value, from.Background))
            return to.Background;
        if (Same(value, from.Surface))
            return to.Surface;
        if (Same(value, from.Hover))
            return to.Hover;
        if (Same(value, from.Pressed))
            return to.Pressed;
        if (Same(value, from.Selection))
            return to.Selection;
        if (Same(value, from.Accent))
            return to.Accent;
        if (Same(value, from.AccentSoft))
            return to.AccentSoft;
        if (Same(value, from.AccentMuted))
            return to.AccentMuted;
        if (Same(value, from.CardDivider))
            return to.CardDivider;
        if (Same(value, from.Text))
            return to.Text;
        if (Same(value, from.TextMuted))
            return to.TextMuted;
        if (Same(value, from.Handle))
            return to.Handle;
        if (Same(value, from.Disabled))
            return to.Disabled;
        if (Same(value, from.ForceDropSetText))
            return to.ForceDropSetText;
        if (Same(value, from.ForceDropCorruptionText))
            return to.ForceDropCorruptionText;
        if (Same(value, from.ForceDropIdolText))
            return to.ForceDropIdolText;
        return value;
    }

    private static bool Same(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.004f
        && Mathf.Abs(a.g - b.g) < 0.004f
        && Mathf.Abs(a.b - b.b) < 0.004f
        && Mathf.Abs(a.a - b.a) < 0.004f;

    private static Color Rgba(byte r, byte g, byte b, byte a) => new Color32(r, g, b, a);
}
