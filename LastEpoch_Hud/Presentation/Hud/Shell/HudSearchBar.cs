using System;
using System.Collections.Generic;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

/// <summary>Search bar. Input, results and match lists are replaced/cleared in Build on HUD re-bind; Clear empties matches.</summary>
internal static class HudSearchBar
{
    private const int ResultLimit = 8;

    private sealed class ResultRow
    {
        public GameObject Root;
        public Button Button;
        public Text Label;
        public Text Breadcrumb;
        public HudSearchMatch Match;
    }

    private static readonly List<ResultRow> ResultRows = new();
    private static readonly List<HudSearchMatch> Matches = new();
    private static TMP_InputField input;
    private static GameObject resultsPanel;
    private static RectTransform resultsRect;
    private static Font font;
    private static Action<HudSearchMatch, string> activate;
    private static Sprite roundedSprite;

    public static void Build(
        GameObject header,
        GameObject window,
        GameObject hud,
        Font inheritedFont,
        Action<HudSearchMatch, string> activateMatch
    )
    {
        font = inheritedFont;
        activate = activateMatch;
        ResultRows.Clear();
        Matches.Clear();
        roundedSprite ??= CreateRoundedSprite();

        TMP_InputField template = FindInputTemplate(hud);
        if (template.IsNullOrDestroyed())
        {
            Main.logger_instance?.Warning("HudSearchBar: no TMP input template found");
            return;
        }

        var shell = Node(header, "SearchShell");
        var shellRect = shell.GetComponent<RectTransform>();
        shellRect.anchorMin = new Vector2(HudTheme.SearchAnchorX, 0.5f);
        shellRect.anchorMax = new Vector2(HudTheme.SearchAnchorX, 0.5f);
        shellRect.pivot = new Vector2(0.5f, 0.5f);
        shellRect.sizeDelta = new Vector2(HudTheme.SearchWidth, HudTheme.SearchHeight);
        var shellImage = shell.AddComponent<Image>();
        shellImage.sprite = roundedSprite;
        shellImage.type = Image.Type.Sliced;
        shellImage.color = HudTheme.Surface;
        var shellOutline = shell.AddComponent<Outline>();
        shellOutline.effectColor = HudTheme.Border;
        shellOutline.effectDistance = new Vector2(1f, -1f);
        shellOutline.useGraphicAlpha = false;
        var mask = shell.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        var clone = UnityEngine.Object.Instantiate(template.gameObject, shell.transform, false);
        clone.name = "SearchInput";
        var inputRect = clone.GetComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.offsetMin = new Vector2(12f, 3f);
        inputRect.offsetMax = new Vector2(-HudTheme.SearchButtonWidth - 10f, -3f);
        inputRect.localScale = Vector3.one;
        var layout = clone.GetComponent<LayoutElement>();
        if (!layout.IsNullOrDestroyed())
            layout.ignoreLayout = true;
        var inputImage = clone.GetComponent<Image>();
        if (!inputImage.IsNullOrDestroyed())
        {
            inputImage.sprite = null;
            inputImage.type = Image.Type.Simple;
            inputImage.color = HudTheme.Transparent;
        }
        foreach (var outline in clone.GetComponents<Outline>())
            outline.enabled = false;
        input = clone.GetComponent<TMP_InputField>();
        input.onValueChanged = new TMP_InputField.OnChangeEvent();
        input.onSubmit = new TMP_InputField.SubmitEvent();
        input.contentType = TMP_InputField.ContentType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 80;
        input.readOnly = false;
        input.interactable = true;
        input.SetTextWithoutNotify(string.Empty);
        if (!input.textViewport.IsNullOrDestroyed())
        {
            input.textViewport.anchorMin = Vector2.zero;
            input.textViewport.anchorMax = Vector2.one;
            input.textViewport.offsetMin = new Vector2(2f, 0f);
            input.textViewport.offsetMax = new Vector2(-2f, 0f);
        }
        if (!input.textComponent.IsNullOrDestroyed())
        {
            input.textComponent.color = HudTheme.TextPrimary;
            input.textComponent.fontSize = HudTheme.BodyFontSize;
            input.textComponent.horizontalAlignment = HorizontalAlignmentOptions.Left;
            input.textComponent.verticalAlignment = VerticalAlignmentOptions.Middle;
        }
        if (!input.placeholder.IsNullOrDestroyed())
        {
            input.placeholder.gameObject.SetActive(true);
            var placeholder = input.placeholder.GetComponent<TMP_Text>();
            if (!placeholder.IsNullOrDestroyed())
            {
                LocaleRegistry.Apply(placeholder, "Search settings...");
                placeholder.color = HudTheme.TextMuted;
                placeholder.fontSize = HudTheme.BodyFontSize;
            }
        }
        input.onValueChanged.AddListener((UnityAction<string>)UpdateSuggestions);
        input.onSubmit.AddListener((UnityAction<string>)Submit);
        clone.SetActive(true);

        var divider = Node(shell, "SearchDivider");
        var dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(1f, 0f);
        dividerRect.anchorMax = Vector2.one;
        dividerRect.pivot = new Vector2(1f, 0.5f);
        dividerRect.anchoredPosition = new Vector2(-HudTheme.SearchButtonWidth, 0f);
        dividerRect.sizeDelta = new Vector2(HudTheme.BorderWidth, 0f);
        var dividerImage = divider.AddComponent<Image>();
        dividerImage.color = HudTheme.Border;
        dividerImage.raycastTarget = false;

        var searchObject = Node(shell, "SearchButton");
        var searchRect = searchObject.GetComponent<RectTransform>();
        searchRect.anchorMin = new Vector2(1f, 0f);
        searchRect.anchorMax = Vector2.one;
        searchRect.pivot = new Vector2(1f, 0.5f);
        searchRect.sizeDelta = new Vector2(HudTheme.SearchButtonWidth, 0f);
        var searchImage = searchObject.AddComponent<Image>();
        searchImage.color = HudTheme.Transparent;
        var searchButton = searchObject.AddComponent<Button>();
        searchButton.targetGraphic = searchImage;
        searchButton.colors = HudTheme.ButtonColors(HudTheme.Transparent, HudTheme.Selection);
        ButtonHook.Register(searchButton, () => Submit(input?.text));
        var searchLabel = TextNode(searchObject, "Label", "Search", HudTheme.ValueFontSize);
        LocaleRegistry.Apply(searchLabel, "Search");
        Stretch(searchLabel.GetComponent<RectTransform>());
        searchLabel.alignment = TextAnchor.MiddleCenter;

        BuildResults(window);
    }

    public static void Clear()
    {
        Matches.Clear();
        if (!input.IsNullOrDestroyed())
            input.SetTextWithoutNotify(string.Empty);
        if (!resultsPanel.IsNullOrDestroyed())
            resultsPanel.SetActive(false);
    }

    private static void BuildResults(GameObject window)
    {
        resultsPanel = Node(window, "LEHUD_SearchResults");
        resultsRect = resultsPanel.GetComponent<RectTransform>();
        resultsRect.anchorMin = new Vector2(HudTheme.SearchAnchorX, 1f);
        resultsRect.anchorMax = new Vector2(HudTheme.SearchAnchorX, 1f);
        resultsRect.pivot = new Vector2(0.5f, 1f);
        resultsRect.anchoredPosition = new Vector2(0f, -HudTheme.HeaderHeight - 6f);
        resultsRect.sizeDelta = new Vector2(HudTheme.SearchResultsWidth, 0f);
        var image = resultsPanel.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = HudTheme.Surface;
        var outline = resultsPanel.AddComponent<Outline>();
        outline.effectColor = HudTheme.Border;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        for (int i = 0; i < ResultLimit; i++)
            ResultRows.Add(BuildResultRow(resultsPanel, i));
        resultsPanel.SetActive(false);
    }

    private static ResultRow BuildResultRow(GameObject parent, int index)
    {
        var root = Node(parent, "Result_" + index);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -6f - index * HudTheme.SearchResultHeight);
        rect.sizeDelta = new Vector2(-12f, HudTheme.SearchResultHeight - 4f);
        var image = root.AddComponent<Image>();
        image.color = HudTheme.Surface;
        var button = root.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = HudTheme.ButtonColors(HudTheme.Surface, HudTheme.Selection);

        var accent = Node(root, "Accent");
        var accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = Vector2.zero;
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.sizeDelta = new Vector2(3f, 0f);
        var accentImage = accent.AddComponent<Image>();
        accentImage.color = HudTheme.Accent;
        accentImage.raycastTarget = false;

        var label = TextNode(root, "Label", string.Empty, HudTheme.BodyFontSize);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.42f);
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 0f);
        labelRect.offsetMax = new Vector2(-12f, 0f);
        label.alignment = TextAnchor.MiddleLeft;

        var breadcrumb = TextNode(root, "Breadcrumb", string.Empty, HudTheme.ValueFontSize - 2);
        var breadcrumbRect = breadcrumb.GetComponent<RectTransform>();
        breadcrumbRect.anchorMin = Vector2.zero;
        breadcrumbRect.anchorMax = new Vector2(1f, 0.44f);
        breadcrumbRect.offsetMin = new Vector2(14f, 0f);
        breadcrumbRect.offsetMax = new Vector2(-12f, 0f);
        breadcrumb.alignment = TextAnchor.MiddleLeft;
        breadcrumb.color = HudTheme.TextMuted;

        var row = new ResultRow
        {
            Root = root,
            Button = button,
            Label = label,
            Breadcrumb = breadcrumb,
        };
        ButtonHook.Register(
            button,
            () =>
            {
                if (row.Match != null)
                    Select(row.Match);
            }
        );
        root.SetActive(false);
        return row;
    }

    private static void UpdateSuggestions(string query)
    {
        HudSearch.ClearAll();
        Matches.Clear();
        string normalized = HudSearchText.Normalize(query);
        if (normalized.Length < 2)
        {
            resultsPanel?.SetActive(false);
            return;
        }
        Matches.AddRange(HudSearch.Find(query));
        RenderResults();
    }

    private static void Submit(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            HudSearch.ClearAll();
            Clear();
            return;
        }
        Matches.Clear();
        Matches.AddRange(HudSearch.Find(query));
        if (Matches.Count > 0)
            Select(Matches[0]);
        else
            RenderResults();
    }

    private static void Select(HudSearchMatch match)
    {
        if (match == null)
            return;
        if (!resultsPanel.IsNullOrDestroyed())
            resultsPanel.SetActive(false);
        if (!input.IsNullOrDestroyed())
            input.DeactivateInputField();
        activate?.Invoke(match, input?.text?.Trim() ?? string.Empty);
    }

    private static void RenderResults()
    {
        int visible = Mathf.Min(ResultLimit, Matches.Count);
        for (int i = 0; i < ResultRows.Count; i++)
        {
            var row = ResultRows[i];
            bool show = i < visible;
            row.Root.SetActive(show);
            row.Match = show ? Matches[i] : null;
            if (!show)
                continue;
            row.Button.interactable = true;
            LocaleRegistry.Apply(row.Label, string.Empty);
            LocaleRegistry.Apply(row.Breadcrumb, string.Empty);
            row.Label.text = row.Match.Label;
            row.Breadcrumb.text = row.Match.Breadcrumb;
        }

        if (visible == 0 && ResultRows.Count > 0)
        {
            var row = ResultRows[0];
            row.Root.SetActive(true);
            row.Match = null;
            row.Button.interactable = false;
            LocaleRegistry.Apply(row.Label, "No matching settings");
            LocaleRegistry.Apply(row.Breadcrumb, "Try a broader search");
            visible = 1;
        }
        resultsRect.sizeDelta = new Vector2(
            HudTheme.SearchResultsWidth,
            visible * HudTheme.SearchResultHeight + 12f
        );
        resultsPanel.SetActive(true);
        resultsPanel.transform.SetAsLastSibling();
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

    private static Sprite CreateRoundedSprite()
    {
        const int size = 32;
        const float radius = HudTheme.SearchCornerRadius;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nearestX = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float nearestY = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float distance = Vector2.Distance(
                new Vector2(x + 0.5f, y + 0.5f),
                new Vector2(nearestX, nearestY)
            );
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.5f - distance) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "LEHUD_RoundedRect",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(10f, 10f, 10f, 10f)
        );
        sprite.name = "LEHUD_RoundedRect";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static GameObject Node(GameObject parent, string name) =>
        HudElements.Node(parent, name);

    private static Text TextNode(GameObject parent, string name, string caption, int size) =>
        HudElements.Text(parent, name, caption, font, size, localize: false);

    private static void Stretch(RectTransform rect) => HudElements.Stretch(rect);
}
