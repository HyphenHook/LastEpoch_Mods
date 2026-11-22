using System;
using System.Collections.Generic;
using System.Globalization;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Keybind;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

// Shared renderer for pages made from several vertically stacked cards. The
// page owns one scrollbar; cards only describe their controls and bindings.
internal sealed class HudFormPage : IHudSearchPage
{
    private const float DimmedAlpha = 0.45f;

    internal sealed class Card
    {
        internal GameObject Root;
        internal GameObject Body;
        internal Text Indicator;
        internal string Title;
        internal readonly List<SearchItem> SearchItems = new();
        internal bool Expanded = true;
        internal bool? ExpandedBeforeSearch;
    }

    internal sealed class SearchItem
    {
        public string Label;
        public GameObject Root;
        public bool Conditional;
        public HudSearchEntry Entry;
    }

    private sealed class ActionSearchGroup
    {
        public GameObject Root;
        public readonly List<GameObject> Rows = new();
        public readonly List<SearchItem> Items = new();
    }

    private sealed class ToggleBinding
    {
        public Toggle Control;
        public Func<bool> Read;
    }

    private sealed class SliderBinding
    {
        public Slider Control;
        public TMP_InputField Input;
        public Func<float> Read;
        public Action<float> Write;
        public float Minimum;
        public float Maximum;
        public string Unit;
    }

    private sealed class DropdownBinding
    {
        public Dropdown Control;
        public Func<int> Read;
    }

    private sealed class VisibilityBinding
    {
        public GameObject Root;
        public Func<bool> Read;
    }

    private sealed class KeybindBinding
    {
        public Text Display;
        public KeybindSetting Setting;
    }

    private readonly GameObject root;
    private readonly GameObject _hud;
    private readonly GameObject content;
    private readonly Font font;
    private readonly TMP_InputField inputTemplate;
    private readonly Sprite handleSprite;
    private readonly ScrollRect scroll;
    private readonly RectTransform contentRect;
    private readonly List<Card> cards = new();
    private readonly List<ActionSearchGroup> actionSearchGroups = new();
    private readonly List<HudSearchEntry> searchEntries = new();
    private readonly Dictionary<GameObject, SearchItem> searchItemsByRoot = new();
    private readonly List<ToggleBinding> toggles = new();
    private readonly List<SliderBinding> sliders = new();
    private readonly List<DropdownBinding> dropdowns = new();
    private readonly List<VisibilityBinding> visibility = new();
    private readonly List<KeybindBinding> keybinds = new();
    private Dropdown _dropdownTemplate;
    private bool refreshing;
    private bool searchActive;

    private HudFormPage(
        GameObject parent,
        GameObject hud,
        Font inheritedFont,
        HudPageId pageId,
        string rootName
    )
    {
        font = inheritedFont;
        _hud = hud;
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

        var scrollObject = Node(root, "Scroll");
        Stretch(scrollObject.GetComponent<RectTransform>());
        scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 38f;
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

        content = Node(viewport, "Content");
        contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(
            (int)HudTheme.CardHorizontalInset,
            (int)HudTheme.CardHorizontalInset,
            1,
            1
        );
        layout.spacing = HudTheme.CardGap;
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
        root.SetActive(false);
        HudSearch.Register(this);
    }

    public HudPageId PageId { get; }
    public IReadOnlyList<HudSearchEntry> SearchEntries => searchEntries;

    public static HudFormPage Build(GameObject parent, GameObject hud, Font font, HudPageId pageId)
    {
        string rootName = HudPageRoutes.SearchRoot(pageId);
        if (rootName == null || parent.IsNullOrDestroyed() || hud.IsNullOrDestroyed())
        {
            return null;
        }

        return new HudFormPage(parent, hud, font, pageId, rootName);
    }

    public Card AddCard(string id, string title)
    {
        var cardObject = Node(content, "Card_" + id);
        var image = cardObject.AddComponent<Image>();
        image.color = HudTheme.Surface;
        var outline = cardObject.AddComponent<Outline>();
        outline.effectColor = HudTheme.Border;
        outline.effectDistance = new Vector2(HudTheme.BorderWidth, -HudTheme.BorderWidth);
        outline.useGraphicAlpha = false;

        var layout = cardObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(
            (int)HudTheme.SliderCardPadding,
            (int)HudTheme.SliderCardPadding,
            18,
            26
        );
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = cardObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var header = Node(cardObject, "Header");
        var headerElement = header.AddComponent<LayoutElement>();
        headerElement.minHeight = 62f;
        headerElement.preferredHeight = 62f;
        var headerImage = header.AddComponent<Image>();
        headerImage.color = HudTheme.Transparent;
        var headerButton = header.AddComponent<Button>();
        headerButton.targetGraphic = headerImage;
        headerButton.colors = HudTheme.ButtonColors(HudTheme.Transparent, HudTheme.SurfaceHover);

        var titleText = TextNode(header, "Title", title, HudTheme.SliderCardTitleFontSize);
        var titleRect = titleText.GetComponent<RectTransform>();
        Stretch(titleRect);
        titleRect.offsetMin = new Vector2(0f, 5f);
        titleRect.offsetMax = new Vector2(-48f, 0f);
        titleText.alignment = TextAnchor.MiddleLeft;

        var indicator = TextNode(header, "Indicator", "-", HudTheme.SliderCardTitleFontSize);
        var indicatorRect = indicator.GetComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(1f, 0f);
        indicatorRect.anchorMax = Vector2.one;
        indicatorRect.pivot = new Vector2(1f, 0.5f);
        indicatorRect.offsetMin = new Vector2(-42f, 0f);
        indicatorRect.offsetMax = Vector2.zero;
        indicator.alignment = TextAnchor.MiddleCenter;

        var divider = Node(header, "TitleDivider");
        var dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = Vector2.zero;
        dividerRect.anchorMax = new Vector2(1f, 0f);
        dividerRect.pivot = new Vector2(0.5f, 0f);
        dividerRect.anchoredPosition = Vector2.zero;
        dividerRect.sizeDelta = new Vector2(0f, HudTheme.BorderWidth);
        var dividerImage = divider.AddComponent<Image>();
        dividerImage.color = HudTheme.CardDivider;
        dividerImage.raycastTarget = false;

        var body = Node(cardObject, "Body");
        var bodyLayout = body.AddComponent<VerticalLayoutGroup>();
        bodyLayout.padding = new RectOffset(0, 0, 8, 0);
        bodyLayout.spacing = 10f;
        bodyLayout.childAlignment = TextAnchor.UpperLeft;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;
        var bodyFitter = body.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var card = new Card
        {
            Root = cardObject,
            Body = body,
            Indicator = indicator,
            Title = title,
        };
        cards.Add(card);
        searchEntries.Add(new HudSearchEntry { Card = title, Label = title });
        ButtonHook.Register(
            headerButton,
            new Action(() =>
            {
                card.Expanded = !card.Expanded;
                card.Body.SetActive(card.Expanded);
                card.Indicator.text = card.Expanded ? "-" : "+";
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            })
        );
        return card;
    }

    public void AddButtonGrid(string id, string[] labels, Action[] clicks)
    {
        if (labels == null || clicks == null || labels.Length != clicks.Length)
            return;
        var grid = Node(content, "Actions_" + id);
        var gridLayout = grid.AddComponent<VerticalLayoutGroup>();
        gridLayout.spacing = HudTheme.ActionButtonGap;
        gridLayout.childControlWidth = true;
        gridLayout.childControlHeight = true;
        gridLayout.childForceExpandWidth = true;
        gridLayout.childForceExpandHeight = false;
        var fitter = grid.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var searchGroup = new ActionSearchGroup { Root = grid };
        actionSearchGroups.Add(searchGroup);
        for (int i = 0; i < labels.Length; i += 2)
        {
            var row = Node(grid, "Row_" + (i / 2));
            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.minHeight = HudTheme.ActionButtonHeight;
            rowElement.preferredHeight = HudTheme.ActionButtonHeight;
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = HudTheme.ActionButtonGap;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            searchGroup.Rows.Add(row);
            RegisterAction(searchGroup, labels[i], AddGridButton(row, i, labels[i], clicks[i]));
            if (i + 1 < labels.Length)
                RegisterAction(
                    searchGroup,
                    labels[i + 1],
                    AddGridButton(row, i + 1, labels[i + 1], clicks[i + 1])
                );
        }
    }

    public Button AddButton(Card card, string id, string label, Action click)
    {
        var buttonObject = Node(card.Body, "Button_" + id);
        var element = buttonObject.AddComponent<LayoutElement>();
        element.minHeight = HudTheme.ActionButtonHeight;
        element.preferredHeight = HudTheme.ActionButtonHeight;
        var image = buttonObject.AddComponent<Image>();
        image.color = HudTheme.Surface;
        HudStyler.AddPrimaryBorder(buttonObject);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = HudTheme.ActionButtonColors(HudTheme.Surface, HudTheme.Selection);
        if (click != null)
            Prefab.BindButton(button, click);
        var text = TextNode(buttonObject, "Label", label, HudTheme.BodyFontSize);
        Stretch(text.GetComponent<RectTransform>());
        text.alignment = TextAnchor.MiddleCenter;
        Register(card, label, buttonObject);
        return button;
    }

    public Text AddText(Card card, string id, string text, float height = 48f)
    {
        var row = Row(card, "Text_" + id, height);
        var label = TextNode(row, "Label", text, HudTheme.BodyFontSize);
        Stretch(label.GetComponent<RectTransform>());
        label.GetComponent<RectTransform>().offsetMin = new Vector2(6f, 0f);
        label.GetComponent<RectTransform>().offsetMax = new Vector2(-6f, 0f);
        label.alignment = TextAnchor.MiddleLeft;
        label.color = HudTheme.TextMuted;
        Register(card, text, row);
        return label;
    }

    public void AddKeybind(
        Card card,
        string id,
        string label,
        KeybindSetting setting,
        string resetLabel = "Clear"
    )
    {
        if (setting == null)
            return;
        var row = Row(card, "Keybind_" + id, HudTheme.RowHeight + 10f);
        var labelText = TextNode(row, "Label", label, HudTheme.BodyFontSize);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = new Vector2(0.34f, 1f);
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = Vector2.zero;
        labelText.alignment = TextAnchor.MiddleLeft;

        var capture = KeybindButton(row, "Capture", 0.35f, 0.82f);
        var display = capture.GetComponentInChildren<Text>(true);
        var clear = KeybindButton(row, "Clear", 0.83f, 1f);
        var clearText = clear.GetComponentInChildren<Text>(true);
        LocaleRegistry.Apply(clearText, resetLabel);
        Prefab.BindButton(
            capture,
            new Action(() =>
            {
                display.text = LocaleRegistry.Translate(KeybindStrings.CapturePrompt);
                KeybindCapture.Begin(setting, display);
            })
        );
        Prefab.BindButton(clear, new Action(setting.ResetToDefault));
        setting.Changed += value =>
        {
            if (!display.IsNullOrDestroyed())
                display.text = KeybindFormat.Friendly(value);
        };
        keybinds.Add(new KeybindBinding { Display = display, Setting = setting });
        Register(card, label, row);
    }

    public Toggle AddToggle(Card card, string id, string label, Func<bool> read, Action<bool> write)
    {
        var row = Row(card, "Toggle_" + id, HudTheme.RowHeight);
        var labelText = TextNode(row, "Label", label, HudTheme.BodyFontSize);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = new Vector2(-64f, 0f);
        labelText.alignment = TextAnchor.MiddleLeft;

        var box = Node(row, "Box");
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(1f, 0.5f);
        boxRect.anchorMax = new Vector2(1f, 0.5f);
        boxRect.pivot = new Vector2(1f, 0.5f);
        boxRect.anchoredPosition = new Vector2(-6f, 0f);
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

        var toggle = row.AddComponent<Toggle>();
        toggle.targetGraphic = boxImage;
        toggle.graphic = checkImage;
        toggle.colors = HudTheme.ButtonColors(HudTheme.ControlBox, HudTheme.SurfaceHover);
        ToggleHook.Register(
            toggle,
            value =>
            {
                if (!refreshing)
                    write?.Invoke(value);
            }
        );
        toggles.Add(new ToggleBinding { Control = toggle, Read = read });
        Register(card, label, row);
        return toggle;
    }

    public Slider AddSlider(
        Card card,
        string id,
        string label,
        string unit,
        float minimum,
        float maximum,
        bool wholeNumbers,
        Func<float> read,
        Action<float> write
    )
    {
        return AddSliderRow(
            card,
            id,
            label,
            unit,
            minimum,
            maximum,
            wholeNumbers,
            read,
            write,
            null,
            null
        );
    }

    public Slider AddToggleSlider(
        Card card,
        string id,
        string label,
        string unit,
        float minimum,
        float maximum,
        bool wholeNumbers,
        Func<bool> readEnabled,
        Action<bool> writeEnabled,
        Func<float> read,
        Action<float> write
    )
    {
        return AddSliderRow(
            card,
            id,
            label,
            unit,
            minimum,
            maximum,
            wholeNumbers,
            read,
            write,
            readEnabled,
            writeEnabled
        );
    }

    public void SetVisibleWhen(Component control, Func<bool> read)
    {
        if (read == null)
        {
            return;
        }

        GameObject row = RowOf(control);
        if (row.IsNullOrDestroyed())
        {
            return;
        }

        visibility.Add(new VisibilityBinding { Root = row, Read = read });
        if (searchItemsByRoot.TryGetValue(row, out SearchItem item))
        {
            item.Conditional = true;
        }
    }

    /// <summary>Greys out (or restores) the slider and its input box.</summary>
    public void SetInteractable(Slider control, bool on)
    {
        SliderBinding binding = FindSlider(control);
        if (binding == null)
        {
            return;
        }

        binding.Control.interactable = on;
        if (!binding.Input.IsNullOrDestroyed())
        {
            binding.Input.interactable = on;
        }
    }

    /// <summary>Changes the slider bounds without firing its write callback.</summary>
    public void SetSliderRange(Slider control, float minimum, float maximum, bool wholeNumbers)
    {
        SliderBinding binding = FindSlider(control);
        if (binding == null)
        {
            return;
        }

        refreshing = true;
        try
        {
            binding.Minimum = minimum;
            binding.Maximum = maximum;
            binding.Control.minValue = minimum;
            binding.Control.maxValue = maximum;
            binding.Control.wholeNumbers = wholeNumbers;
        }
        finally
        {
            refreshing = false;
        }
    }

    /// <summary>Sets the row label of a control to a ready text; it is not translated again.</summary>
    public void SetLabel(Component control, string text)
    {
        GameObject row = RowOf(control);
        if (row.IsNullOrDestroyed())
        {
            return;
        }

        Transform label = row.transform.Find("Label");
        Text target = label == null ? null : label.GetComponent<Text>();
        if (target.IsNullOrDestroyed())
        {
            return;
        }

        LocaleRegistry.Apply(target, null);
        target.text = text;
    }

    /// <summary>Fades the whole row of a control (or restores it).</summary>
    public void SetDimmed(Component control, bool dimmed)
    {
        GameObject row = RowOf(control);
        if (row.IsNullOrDestroyed())
        {
            return;
        }

        CanvasGroup group = row.GetComponent<CanvasGroup>();
        if (group.IsNullOrDestroyed())
        {
            group = row.AddComponent<CanvasGroup>();
        }

        group.alpha = dimmed ? DimmedAlpha : 1f;
    }

    /// <summary>Styles a card body built after the window was styled: selectable colors and font scale.</summary>
    public static void StyleCard(Card card)
    {
        if (card == null || card.Body.IsNullOrDestroyed())
        {
            return;
        }

        HudStyler.NormalizeSelectableGraphics(card.Body);
        HudStyler.ApplyFontScale(card.Body);
    }

    /// <summary>Removes every row of the card body with its bindings and search entries.</summary>
    public void ClearCard(Card card)
    {
        Transform body = card.Body.transform;
        HudStyler.ForgetFontBaselines(card.Body);
        Unhook(body);
        toggles.RemoveAll(b => IsUnder(b.Control, body));
        sliders.RemoveAll(b => IsUnder(b.Control, body));
        dropdowns.RemoveAll(b => IsUnder(b.Control, body));
        keybinds.RemoveAll(b => IsUnder(b.Display, body));
        visibility.RemoveAll(b => b.Root.IsNullOrDestroyed() || b.Root.transform.IsChildOf(body));
        foreach (SearchItem item in card.SearchItems)
        {
            searchItemsByRoot.Remove(item.Root);
            searchEntries.Remove(item.Entry);
        }
        card.SearchItems.Clear();

        for (int i = body.childCount - 1; i >= 0; i--)
        {
            GameObject child = body.GetChild(i).gameObject;
            child.SetActive(false);
            child.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(child);
        }
    }

    /// <summary>Clones the HUD input template as a themed numeric input; null without a template.</summary>
    public TMP_InputField BuildInput(GameObject parent)
    {
        if (inputTemplate.IsNullOrDestroyed())
            return null;
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
            input.textComponent.horizontalAlignment = HorizontalAlignmentOptions.Right;
            input.textComponent.verticalAlignment = VerticalAlignmentOptions.Middle;
        }
        clone.SetActive(true);
        return input;
    }

    // The direct child of a card body that holds the control, or null.
    private GameObject RowOf(Component control)
    {
        if (control.IsNullOrDestroyed())
        {
            return null;
        }

        Transform node = control.transform;
        while (node.parent != null)
        {
            if (IsCardBody(node.parent))
            {
                return node.gameObject;
            }

            node = node.parent;
        }

        return null;
    }

    private bool IsCardBody(Transform node)
    {
        foreach (Card card in cards)
        {
            if (card.Body.transform == node)
            {
                return true;
            }
        }

        return false;
    }

    // Destroy is deferred, so the controls are still alive here.
    private void Unhook(Transform body)
    {
        foreach (SliderBinding b in sliders)
        {
            if (!b.Control.IsNullOrDestroyed() && b.Control.transform.IsChildOf(body))
            {
                SliderHook.Unregister(b.Control);
            }
        }
        foreach (ToggleBinding b in toggles)
        {
            if (!b.Control.IsNullOrDestroyed() && b.Control.transform.IsChildOf(body))
            {
                ToggleHook.Unregister(b.Control);
            }
        }
    }

    private static bool IsUnder(Component control, Transform body)
    {
        return control.IsNullOrDestroyed() || control.transform.IsChildOf(body);
    }

    private Slider AddSliderRow(
        Card card,
        string id,
        string label,
        string unit,
        float minimum,
        float maximum,
        bool wholeNumbers,
        Func<float> read,
        Action<float> write,
        Func<bool> readEnabled,
        Action<bool> writeEnabled
    )
    {
        var row = Row(card, "Slider_" + id, HudTheme.SliderCardRowHeight);
        var labelText = TextNode(row, "Label", label, HudTheme.BodyFontSize);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.offsetMin = new Vector2(6f, -38f);
        labelRect.offsetMax = new Vector2(readEnabled == null ? -150f : -190f, -4f);
        labelText.alignment = TextAnchor.MiddleLeft;

        if (readEnabled != null)
        {
            var box = Node(row, "Box");
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

            var toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.colors = HudTheme.ButtonColors(HudTheme.ControlBox, HudTheme.SurfaceHover);
            ToggleHook.Register(
                toggle,
                value =>
                {
                    if (!refreshing)
                        writeEnabled?.Invoke(value);
                }
            );
            toggles.Add(new ToggleBinding { Control = toggle, Read = readEnabled });
        }

        var sliderObject = Node(row, "Control");
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
        trackRect.sizeDelta = new Vector2(-6f, HudTheme.SliderTrackHeight);
        track.AddComponent<Image>().color = HudTheme.ControlTrack;

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
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = wholeNumbers;
        slider.direction = Slider.Direction.LeftToRight;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.colors = HudTheme.ButtonColors(HudTheme.ControlHandle, HudTheme.ControlHandle);

        var input = BuildInput(row);
        if (readEnabled != null && !input.IsNullOrDestroyed())
        {
            // Toggle-slider rows consistently place the editable value first and
            // the enable checkbox at the far-right edge.
            var inputRect = input.GetComponent<RectTransform>();
            inputRect.anchoredPosition = new Vector2(-HudTheme.ToggleSize - 16f, -4f);
            inputRect.sizeDelta = new Vector2(HudTheme.SliderValueWidth - 14f, 34f);
        }
        var binding = new SliderBinding
        {
            Control = slider,
            Input = input,
            Read = read,
            Write = write,
            Minimum = minimum,
            Maximum = maximum,
            Unit = unit ?? string.Empty,
        };
        sliders.Add(binding);
        Register(card, label, row);
        SliderHook.Register(slider, value => SliderChanged(binding, value));
        if (!binding.Input.IsNullOrDestroyed())
        {
            binding.Input.onEndEdit = new TMP_InputField.SubmitEvent();
            binding.Input.onEndEdit.AddListener(
                (UnityAction<string>)(text => CommitInput(binding, text))
            );
        }
        return slider;
    }

    /// <summary>A dropdown row with its own options, cloned from the first dropdown in the hud.</summary>
    public Dropdown AddOptionsDropdown(
        Card card,
        string id,
        string label,
        IReadOnlyList<string> options,
        Func<int> read,
        Action<int> write
    )
    {
        Dropdown dropdown = AddDropdownRow(card, id, label, FindDropdownTemplate(), read, write);
        if (dropdown.IsNullOrDestroyed())
        {
            return null;
        }

        refreshing = true;
        try
        {
            SettingsBuilder.ApplyDropdownOptions(dropdown, options);
        }
        finally
        {
            refreshing = false;
        }
        return dropdown;
    }

    private Dropdown FindDropdownTemplate()
    {
        if (_dropdownTemplate.IsNullOrDestroyed())
        {
            _dropdownTemplate = _hud.GetComponentInChildren<Dropdown>(true);
        }

        return _dropdownTemplate;
    }

    private Dropdown AddDropdownRow(
        Card card,
        string id,
        string label,
        Dropdown template,
        Func<int> read,
        Action<int> write
    )
    {
        var row = Row(card, "Dropdown_" + id, HudTheme.RowHeight + 8f);
        var labelText = TextNode(row, "Label", label, HudTheme.BodyFontSize);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = new Vector2(0.38f, 1f);
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = Vector2.zero;
        labelText.alignment = TextAnchor.MiddleLeft;

        if (template.IsNullOrDestroyed())
        {
            Main.logger_instance?.Warning("HudFormPage: dropdown source missing for " + id);
            return null;
        }
        GameObject clone = UnityEngine.Object.Instantiate(
            template.gameObject,
            row.transform,
            false
        );
        clone.name = "Control";
        clone.SetActive(true);
        var dropdown = clone.GetComponent<Dropdown>();
        dropdown.onValueChanged.RemoveAllListeners();
        var rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.38f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-6f, 0f);
        rect.sizeDelta = new Vector2(-10f, 36f);
        rect.localScale = Vector3.one;
        var layout = clone.GetComponent<LayoutElement>();
        if (!layout.IsNullOrDestroyed())
            layout.ignoreLayout = true;
        var image = clone.GetComponent<Image>();
        if (!image.IsNullOrDestroyed())
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = HudTheme.Surface;
        }
        var outline = clone.GetComponent<Outline>();
        if (!outline.IsNullOrDestroyed())
            outline.enabled = false;
        HudStyler.AddPrimaryBorder(clone);
        dropdown.colors = HudTheme.ButtonColors(HudTheme.Surface, HudTheme.Selection);
        foreach (var text in clone.GetComponentsInChildren<Text>(true))
        {
            text.color = HudTheme.TextPrimary;
            text.fontSize = HudTheme.ValueFontSize;
        }
        HudStyler.ApplyDropdown(dropdown);
        Prefab.BindDropdown(
            dropdown,
            new Action<int>(_ =>
            {
                if (refreshing)
                {
                    ModSettings.Trace(
                        "HudFormPage: dropdown " + id + " change skipped while refreshing"
                    );
                    return;
                }

                // The Il2Cpp event argument is unreliable; read the control's own index.
                write?.Invoke(dropdown.value);
            })
        );
        dropdowns.Add(new DropdownBinding { Control = dropdown, Read = read });
        Register(card, label, row);
        return dropdown;
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

    public void RefreshValues()
    {
        if (root.IsNullOrDestroyed())
            return;
        Refresh();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    public void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ClearSearch();
            return;
        }

        Refresh();
        searchActive = true;
        foreach (var card in cards)
        {
            if (!card.ExpandedBeforeSearch.HasValue)
                card.ExpandedBeforeSearch = card.Expanded;
            bool cardMatch = Matches(query, card.Title, card.Title);
            bool any = cardMatch;
            foreach (var item in card.SearchItems)
            {
                bool show = cardMatch || Matches(query, item.Label, card.Title);
                if (!item.Root.IsNullOrDestroyed())
                    item.Root.SetActive(show);
                any |= show;
            }
            if (!card.Root.IsNullOrDestroyed())
                card.Root.SetActive(any);
            if (any)
            {
                card.Expanded = true;
                card.Body.SetActive(true);
                card.Indicator.text = "-";
            }
        }

        foreach (var group in actionSearchGroups)
        {
            bool any = false;
            foreach (var item in group.Items)
            {
                bool show = Matches(query, item.Label, "Actions");
                if (!item.Root.IsNullOrDestroyed())
                    item.Root.SetActive(show);
                any |= show;
            }
            foreach (var row in group.Rows)
                if (!row.IsNullOrDestroyed())
                    row.SetActive(HasActiveChild(row));
            if (!group.Root.IsNullOrDestroyed())
                group.Root.SetActive(any);
        }

        RebuildAfterSearch();
    }

    public void ClearSearch()
    {
        if (!searchActive)
            return;
        searchActive = false;
        foreach (var card in cards)
        {
            if (!card.Root.IsNullOrDestroyed())
                card.Root.SetActive(true);
            foreach (var item in card.SearchItems)
                if (!item.Root.IsNullOrDestroyed() && !item.Conditional)
                    item.Root.SetActive(true);
            if (card.ExpandedBeforeSearch.HasValue)
            {
                card.Expanded = card.ExpandedBeforeSearch.Value;
                card.ExpandedBeforeSearch = null;
                card.Body.SetActive(card.Expanded);
                card.Indicator.text = card.Expanded ? "-" : "+";
            }
        }
        foreach (var group in actionSearchGroups)
        {
            if (!group.Root.IsNullOrDestroyed())
                group.Root.SetActive(true);
            foreach (var row in group.Rows)
                if (!row.IsNullOrDestroyed())
                    row.SetActive(true);
            foreach (var item in group.Items)
                if (!item.Root.IsNullOrDestroyed())
                    item.Root.SetActive(true);
        }
        Refresh();
        RebuildAfterSearch();
    }

    private void Refresh()
    {
        refreshing = true;
        try
        {
            foreach (var binding in toggles)
                if (!binding.Control.IsNullOrDestroyed())
                    binding.Control.isOn = binding.Read?.Invoke() ?? false;
            foreach (var binding in sliders)
            {
                if (binding.Control.IsNullOrDestroyed())
                    continue;
                float value = Mathf.Clamp(
                    binding.Read?.Invoke() ?? 0f,
                    binding.Minimum,
                    binding.Maximum
                );
                binding.Control.value = value;
                SetInputText(binding, binding.Control.value);
            }
            foreach (var binding in dropdowns)
            {
                if (binding.Control.IsNullOrDestroyed())
                    continue;
                binding.Control.value = Mathf.Clamp(
                    binding.Read?.Invoke() ?? 0,
                    0,
                    Mathf.Max(0, binding.Control.options.Count - 1)
                );
                binding.Control.RefreshShownValue();
            }
            foreach (var binding in visibility)
                if (!binding.Root.IsNullOrDestroyed())
                    binding.Root.SetActive(binding.Read?.Invoke() ?? true);
            foreach (var binding in keybinds)
                if (!binding.Display.IsNullOrDestroyed() && !KeybindCapture.Active)
                    binding.Display.text = KeybindFormat.Friendly(binding.Setting.Value);
        }
        finally
        {
            refreshing = false;
        }
    }

    private SliderBinding FindSlider(Slider control)
    {
        foreach (SliderBinding binding in sliders)
        {
            if (binding.Control == control)
            {
                return binding;
            }
        }

        return null;
    }

    private void SliderChanged(SliderBinding binding, float value)
    {
        if (refreshing)
            return;
        value = Mathf.Clamp(value, binding.Minimum, binding.Maximum);
        binding.Write?.Invoke(value);
        SetInputText(binding, value);
    }

    private void CommitInput(SliderBinding binding, string text)
    {
        if (binding == null || binding.Control.IsNullOrDestroyed())
            return;
        if (TryParse(text, out float value))
            binding.Control.value = Mathf.Clamp(value, binding.Minimum, binding.Maximum);
        SetInputText(binding, binding.Control.value);
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
        scrollbarObject.AddComponent<Image>().color = HudTheme.Background;
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

    private Button KeybindButton(GameObject parent, string name, float left, float right)
    {
        var buttonObject = Node(parent, name);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(left, 0.14f);
        rect.anchorMax = new Vector2(right, 0.86f);
        rect.offsetMin = new Vector2(3f, 0f);
        rect.offsetMax = new Vector2(-3f, 0f);
        var image = buttonObject.AddComponent<Image>();
        image.color = HudTheme.Surface;
        HudStyler.AddPrimaryBorder(buttonObject);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = HudTheme.ActionButtonColors(HudTheme.Surface, HudTheme.Selection);
        var text = TextNode(buttonObject, "Label", string.Empty, HudTheme.ValueFontSize);
        Stretch(text.GetComponent<RectTransform>());
        text.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private GameObject AddGridButton(GameObject parent, int index, string label, Action click)
    {
        var buttonObject = Node(parent, "Button_" + index);
        var element = buttonObject.AddComponent<LayoutElement>();
        element.minHeight = HudTheme.ActionButtonHeight;
        element.preferredHeight = HudTheme.ActionButtonHeight;
        element.flexibleWidth = 1f;
        var image = buttonObject.AddComponent<Image>();
        image.color = HudTheme.Surface;
        HudStyler.AddPrimaryBorder(buttonObject);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = HudTheme.ActionButtonColors(HudTheme.Surface, HudTheme.Selection);
        if (click != null)
            Prefab.BindButton(button, click);
        var text = TextNode(buttonObject, "Label", label, HudTheme.BodyFontSize);
        Stretch(text.GetComponent<RectTransform>());
        text.alignment = TextAnchor.MiddleCenter;
        return buttonObject;
    }

    private void Register(Card card, string label, GameObject row)
    {
        if (card == null || row.IsNullOrDestroyed() || string.IsNullOrWhiteSpace(label))
            return;
        var entry = new HudSearchEntry { Card = card.Title, Label = label };
        var item = new SearchItem
        {
            Label = label,
            Root = row,
            Entry = entry,
        };
        card.SearchItems.Add(item);
        searchItemsByRoot[row] = item;
        searchEntries.Add(entry);
    }

    private void RegisterAction(ActionSearchGroup group, string label, GameObject button)
    {
        if (group == null || button.IsNullOrDestroyed() || string.IsNullOrWhiteSpace(label))
            return;
        group.Items.Add(new SearchItem { Label = label, Root = button });
        searchEntries.Add(new HudSearchEntry { Card = "Actions", Label = label });
    }

    private bool Matches(string query, string label, string card)
    {
        if (!HudNavigation.TryGetPage(PageId, out var section, out var page))
            return false;
        return HudSearch.Score(query, label, card, page.Label, section.Label) >= 0;
    }

    private void RebuildAfterSearch()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
    }

    private static bool HasActiveChild(GameObject parent)
    {
        for (int i = 0; i < parent.transform.childCount; i++)
            if (parent.transform.GetChild(i).gameObject.activeSelf)
                return true;
        return false;
    }

    private GameObject Row(Card card, string name, float height)
    {
        var row = Node(card.Body, name);
        var element = row.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
        return row;
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

    private static void SetInputText(SliderBinding binding, float value)
    {
        if (binding.Input.IsNullOrDestroyed() || binding.Input.isFocused)
            return;
        string number =
            Mathf.Abs(value - Mathf.Round(value)) < 0.001f
                ? Mathf.Round(value).ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        binding.Input.SetTextWithoutNotify(number + binding.Unit);
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
        HudElements.Text(parent, name, caption, font, size, HorizontalWrapMode.Wrap);

    private static void Stretch(RectTransform rect) => HudElements.Stretch(rect);
}
