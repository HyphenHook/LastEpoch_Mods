using System;
using System.Collections.Generic;
using System.Globalization;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

// Shared renderer for the large single-card slider pages. Page files provide
// only labels, ranges, units, and save bindings; all visual construction lives here.
internal sealed class HudSliderCard : IHudSearchPage
{
    internal sealed class Definition
    {
        public string Id;
        public string Label;
        public string Unit;
        public float Minimum;
        public float Maximum;
        public bool WholeNumbers;
        public Func<float> Read;
        public Action<float> Write;
        public Func<bool> ReadEnabled;
        public Action<bool> WriteEnabled;
    }

    private sealed class Row
    {
        public GameObject Root;
        public Definition Definition;
        public Slider Slider;
        public TMP_InputField Input;
        public Toggle Toggle;
    }

    private readonly List<Row> rows = new();
    private readonly GameObject root;
    private readonly Font font;
    private readonly Sprite handleSprite;
    private readonly TMP_InputField inputTemplate;
    private readonly ScrollRect scroll;
    private readonly RectTransform contentRect;
    private readonly string title;
    private readonly List<HudSearchEntry> searchEntries = new();
    private bool refreshing;
    private bool searchActive;

    public HudPageId PageId { get; }
    public IReadOnlyList<HudSearchEntry> SearchEntries => searchEntries;

    private HudSliderCard(
        GameObject parent,
        GameObject hud,
        Font inheritedFont,
        HudPageId pageId,
        string rootName,
        string title,
        IReadOnlyList<Definition> definitions
    )
    {
        font = inheritedFont;
        this.title = title;
        PageId = pageId;
        inputTemplate = FindInputTemplate(hud);
        handleSprite = FindHandleSprite(hud);

        root = Node(parent, rootName);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = new Vector2(
            HudTheme.SidebarWidth + HudTheme.ContentPadding,
            HudTheme.ContentPadding
        );
        rootRect.offsetMax = new Vector2(
            -HudTheme.ContentPadding,
            -HudTheme.HeaderHeight - HudTheme.ContentPadding
        );

        var card = Node(root, "Card");
        Stretch(card.GetComponent<RectTransform>());
        card.GetComponent<RectTransform>().offsetMin = new Vector2(
            HudTheme.CardHorizontalInset,
            0f
        );
        card.GetComponent<RectTransform>().offsetMax = new Vector2(
            -HudTheme.CardHorizontalInset,
            0f
        );
        var cardImage = card.AddComponent<Image>();
        cardImage.color = HudTheme.Surface;
        var cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = HudTheme.Border;
        cardOutline.effectDistance = new Vector2(HudTheme.BorderWidth, -HudTheme.BorderWidth);
        cardOutline.useGraphicAlpha = false;

        var titleText = TextNode(card, "Title", title, HudTheme.SliderCardTitleFontSize);
        var titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(HudTheme.SliderCardPadding, -76f);
        titleRect.offsetMax = new Vector2(-HudTheme.SliderCardPadding, -20f);
        titleText.alignment = TextAnchor.MiddleLeft;

        var divider = Node(card, "TitleDivider");
        var dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.anchoredPosition = new Vector2(0f, -78f);
        dividerRect.sizeDelta = new Vector2(-HudTheme.SliderCardPadding * 2f, HudTheme.BorderWidth);
        var dividerImage = divider.AddComponent<Image>();
        dividerImage.color = HudTheme.CardDivider;
        dividerImage.raycastTarget = false;

        var scrollObject = Node(card, "Scroll");
        var scrollTransform = scrollObject.GetComponent<RectTransform>();
        scrollTransform.anchorMin = Vector2.zero;
        scrollTransform.anchorMax = Vector2.one;
        scrollTransform.offsetMin = new Vector2(18f, 18f);
        scrollTransform.offsetMax = new Vector2(-18f, -88f);
        scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;

        var viewport = Node(scrollObject, "Viewport");
        var viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMax = new Vector2(-HudTheme.SliderScrollbarWidth - 10f, 0f);
        var viewportInput = viewport.AddComponent<Image>();
        viewportInput.color = HudTheme.Transparent;
        viewportInput.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        var content = Node(viewport, "Content");
        contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(
            (int)HudTheme.SliderCardPadding,
            (int)HudTheme.SliderCardPadding,
            14,
            22
        );
        layout.spacing = HudTheme.SliderCardRowGap;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.verticalScrollbar = BuildScrollbar(scrollObject);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalScrollbarSpacing = 0f;

        foreach (var definition in definitions)
            AddRow(content, definition);

        root.SetActive(false);
        searchEntries.Add(new HudSearchEntry { Card = title, Label = title });
        HudSearch.Register(this);
    }

    public static HudSliderCard Build(
        GameObject parent,
        GameObject hud,
        Font font,
        HudPageId pageId,
        string title,
        IReadOnlyList<Definition> definitions
    )
    {
        string rootName = HudPageRoutes.SearchRoot(pageId);
        if (rootName == null || parent.IsNullOrDestroyed() || hud.IsNullOrDestroyed())
        {
            return null;
        }

        return new HudSliderCard(parent, hud, font, pageId, rootName, title, definitions);
    }

    public void Show()
    {
        if (root.IsNullOrDestroyed())
            return;
        root.SetActive(true);
        Refresh();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
    }

    public void Hide()
    {
        if (!root.IsNullOrDestroyed())
            root.SetActive(false);
    }

    public void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ClearSearch();
            return;
        }
        searchActive = true;
        bool cardMatch = Matches(query, title);
        foreach (var row in rows)
            if (!row.Root.IsNullOrDestroyed())
                row.Root.SetActive(cardMatch || Matches(query, row.Definition.Label));
        RebuildAfterSearch();
    }

    public void ClearSearch()
    {
        if (!searchActive)
            return;
        searchActive = false;
        foreach (var row in rows)
            if (!row.Root.IsNullOrDestroyed())
                row.Root.SetActive(true);
        RebuildAfterSearch();
    }

    private void AddRow(GameObject content, Definition definition)
    {
        var rowObject = Node(content, "Row_" + definition.Id);
        var rowElement = rowObject.AddComponent<LayoutElement>();
        rowElement.minHeight = HudTheme.SliderCardRowHeight;
        rowElement.preferredHeight = HudTheme.SliderCardRowHeight;

        var label = TextNode(rowObject, "Label", definition.Label, HudTheme.BodyFontSize);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.offsetMin = new Vector2(4f, -38f);
        labelRect.offsetMax = new Vector2(definition.ReadEnabled == null ? -150f : -200f, -4f);
        label.alignment = TextAnchor.MiddleLeft;

        var slider = BuildSlider(rowObject, definition);
        var input = BuildInput(rowObject);
        var toggle = definition.ReadEnabled == null ? null : BuildToggle(rowObject);
        if (!input.IsNullOrDestroyed() && !toggle.IsNullOrDestroyed())
        {
            var inputRect = input.GetComponent<RectTransform>();
            inputRect.anchoredPosition = new Vector2(-HudTheme.ToggleSize - 16f, -4f);
            inputRect.sizeDelta = new Vector2(HudTheme.SliderValueWidth - 14f, 34f);
        }
        var row = new Row
        {
            Root = rowObject,
            Definition = definition,
            Slider = slider,
            Input = input,
            Toggle = toggle,
        };
        searchEntries.Add(new HudSearchEntry { Card = title, Label = definition.Label });
        rows.Add(row);

        SliderHook.Register(slider, value => SliderChanged(row, value));
        if (!toggle.IsNullOrDestroyed())
        {
            ToggleHook.Register(
                toggle,
                enabled =>
                {
                    if (!refreshing)
                        row.Definition.WriteEnabled?.Invoke(enabled);
                }
            );
        }
        if (!input.IsNullOrDestroyed())
        {
            input.onEndEdit = new TMP_InputField.SubmitEvent();
            input.onEndEdit.AddListener((UnityAction<string>)(text => CommitInput(row, text)));
        }
    }

    private Toggle BuildToggle(GameObject parent)
    {
        var box = Node(parent, "EnabledBox");
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(1f, 1f);
        boxRect.anchorMax = new Vector2(1f, 1f);
        boxRect.pivot = new Vector2(1f, 1f);
        boxRect.anchoredPosition = new Vector2(-4f, -9f);
        boxRect.sizeDelta = new Vector2(HudTheme.ToggleSize, HudTheme.ToggleSize);
        var boxImage = box.AddComponent<Image>();
        boxImage.color = HudTheme.ControlBox;
        HudStyler.AddPrimaryBorder(box);

        var check = Node(box, "Checkmark");
        var checkRect = check.GetComponent<RectTransform>();
        Stretch(checkRect);
        checkRect.offsetMin = new Vector2(5f, 5f);
        checkRect.offsetMax = new Vector2(-5f, -5f);
        var checkImage = check.AddComponent<Image>();
        checkImage.color = HudTheme.ControlCheck;

        var toggle = parent.AddComponent<Toggle>();
        toggle.targetGraphic = boxImage;
        toggle.graphic = checkImage;
        toggle.colors = HudTheme.ButtonColors(HudTheme.ControlBox, HudTheme.SurfaceHover);
        return toggle;
    }

    private Slider BuildSlider(GameObject parent, Definition definition)
    {
        var sliderObject = Node(parent, "LEHUD_Slider_" + definition.Id);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 9f);
        sliderRect.sizeDelta = new Vector2(-8f, 24f);

        var track = Node(sliderObject, "Track");
        var trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(-6f, HudTheme.SliderTrackHeight);
        var trackImage = track.AddComponent<Image>();
        trackImage.color = HudTheme.ControlTrack;

        var handleArea = Node(sliderObject, "Handle Slide Area");
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        Stretch(handleAreaRect);
        handleAreaRect.offsetMin = new Vector2(4f, 0f);
        handleAreaRect.offsetMax = new Vector2(-4f, 0f);

        var handle = Node(handleArea, "Handle");
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(HudTheme.SliderHandleSize, HudTheme.SliderHandleSize);
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = HudTheme.ControlHandle;
        if (!handleSprite.IsNullOrDestroyed())
        {
            handleImage.sprite = handleSprite;
            handleImage.preserveAspect = true;
        }

        var slider = sliderObject.AddComponent<Slider>();
        slider.minValue = definition.Minimum;
        slider.maxValue = definition.Maximum;
        slider.wholeNumbers = definition.WholeNumbers;
        slider.direction = Slider.Direction.LeftToRight;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.colors = HudTheme.ButtonColors(HudTheme.ControlHandle, HudTheme.ControlHandle);
        return slider;
    }

    private Scrollbar BuildScrollbar(GameObject parent)
    {
        var scrollbarObject = Node(parent, "Scrollbar");
        var scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(-2f, 0f);
        scrollbarRect.sizeDelta = new Vector2(HudTheme.SliderScrollbarWidth, -8f);
        var background = scrollbarObject.AddComponent<Image>();
        background.color = HudTheme.Surface;

        var slidingArea = Node(scrollbarObject, "Sliding Area");
        var slidingRect = slidingArea.GetComponent<RectTransform>();
        Stretch(slidingRect);
        slidingRect.offsetMin = new Vector2(2f, 2f);
        slidingRect.offsetMax = new Vector2(-2f, -2f);

        var handle = Node(slidingArea, "Handle");
        var handleRect = handle.GetComponent<RectTransform>();
        Stretch(handleRect);
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = HudTheme.Accent;

        var scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.colors = HudTheme.ButtonColors(HudTheme.Accent, HudTheme.Accent);
        scrollbar.size = 0.2f;
        return scrollbar;
    }

    private TMP_InputField BuildInput(GameObject parent)
    {
        if (inputTemplate.IsNullOrDestroyed())
        {
            Main.logger_instance?.Warning(
                "HudSliderCard: no TMP input template found; numeric entry unavailable"
            );
            return null;
        }

        var clone = UnityEngine.Object.Instantiate(
            inputTemplate.gameObject,
            parent.transform,
            false
        );
        clone.name = "ValueInput";
        var input = clone.GetComponent<TMP_InputField>();
        var rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-4f, -4f);
        rect.sizeDelta = new Vector2(HudTheme.SliderValueWidth, 34f);
        rect.localScale = Vector3.one;

        var layout = clone.GetComponent<LayoutElement>();
        if (!layout.IsNullOrDestroyed())
            layout.ignoreLayout = true;
        var background = clone.GetComponent<Image>();
        if (!background.IsNullOrDestroyed())
        {
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = HudTheme.ControlBox;
        }
        HudStyler.AddPrimaryBorder(clone, 1f);
        input.colors = HudTheme.ButtonColors(HudTheme.ControlBox, HudTheme.Selection);
        input.onValueChanged.RemoveAllListeners();
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.characterLimit = 12;
        input.readOnly = false;
        input.interactable = true;
        if (!input.placeholder.IsNullOrDestroyed())
            input.placeholder.gameObject.SetActive(false);
        if (!input.textViewport.IsNullOrDestroyed())
        {
            input.textViewport.anchorMin = Vector2.zero;
            input.textViewport.anchorMax = Vector2.one;
            input.textViewport.offsetMin = new Vector2(8f, 0f);
            input.textViewport.offsetMax = new Vector2(-10f, 0f);
        }
        if (!input.textComponent.IsNullOrDestroyed())
        {
            input.textComponent.color = HudTheme.TextPrimary;
            input.textComponent.fontSize = HudTheme.BodyFontSize;
            input.textComponent.enableAutoSizing = true;
            input.textComponent.fontSizeMin = 12f;
            input.textComponent.fontSizeMax = HudTheme.BodyFontSize;
            input.textComponent.horizontalAlignment = HorizontalAlignmentOptions.Right;
            input.textComponent.verticalAlignment = VerticalAlignmentOptions.Middle;
            input.textComponent.margin = Vector4.zero;
        }
        clone.SetActive(true);
        return input;
    }

    private void Refresh()
    {
        refreshing = true;
        try
        {
            foreach (var row in rows)
            {
                if (!row.Toggle.IsNullOrDestroyed())
                    row.Toggle.isOn = row.Definition.ReadEnabled?.Invoke() ?? false;
                float value = row.Definition.Read?.Invoke() ?? 0f;
                value = Mathf.Clamp(value, row.Definition.Minimum, row.Definition.Maximum);
                row.Slider.value = value;
                SetInputText(row, row.Slider.value);
            }
        }
        finally
        {
            refreshing = false;
        }
    }

    private void SliderChanged(Row row, float value)
    {
        if (refreshing)
            return;
        value = Mathf.Clamp(value, row.Definition.Minimum, row.Definition.Maximum);
        row.Definition.Write?.Invoke(value);
        SetInputText(row, value);
    }

    private void CommitInput(Row row, string text)
    {
        if (row == null || row.Slider.IsNullOrDestroyed())
            return;
        if (TryParse(text, out float value))
            row.Slider.value = Mathf.Clamp(value, row.Definition.Minimum, row.Definition.Maximum);
        SetInputText(row, row.Slider.value);
    }

    private static bool TryParse(string text, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        string cleaned = text.Replace("%", "").Replace("x", "").Replace("X", "").Trim();
        return float.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || float.TryParse(cleaned, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static void SetInputText(Row row, float value)
    {
        if (row.Input.IsNullOrDestroyed() || row.Input.isFocused)
            return;
        string number =
            Mathf.Abs(value - Mathf.Round(value)) < 0.001f
                ? Mathf.Round(value).ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        row.Input.SetTextWithoutNotify(number + row.Definition.Unit);
    }

    private bool Matches(string query, string label)
    {
        if (!HudNavigation.TryGetPage(PageId, out var section, out var page))
            return false;
        return HudSearch.Score(query, label, title, page.Label, section.Label) >= 0;
    }

    private void RebuildAfterSearch()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
    }

    private static TMP_InputField FindInputTemplate(GameObject hud)
    {
        foreach (var candidate in hud.GetComponentsInChildren<TMP_InputField>(true))
            if (!candidate.IsNullOrDestroyed() && candidate.name == "InputField")
                return candidate;
        foreach (var candidate in hud.GetComponentsInChildren<TMP_InputField>(true))
            if (!candidate.IsNullOrDestroyed())
                return candidate;
        return null;
    }

    private static Sprite FindHandleSprite(GameObject hud)
    {
        foreach (var slider in hud.GetComponentsInChildren<Slider>(true))
        {
            if (slider.IsNullOrDestroyed() || slider.handleRect.IsNullOrDestroyed())
                continue;
            var image = slider.handleRect.GetComponent<Image>();
            if (!image.IsNullOrDestroyed() && !image.sprite.IsNullOrDestroyed())
                return image.sprite;
        }
        return null;
    }

    private static GameObject Node(GameObject parent, string name) =>
        HudElements.Node(parent, name);

    private Text TextNode(GameObject parent, string name, string caption, int size) =>
        HudElements.Text(parent, name, caption, font, size);

    private static void Stretch(RectTransform rect) => HudElements.Stretch(rect);
}
