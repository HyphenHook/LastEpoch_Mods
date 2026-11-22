using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

// Shared renderer for single-card pages made from full-width action buttons.
internal sealed class HudActionCard : IHudSearchPage
{
    internal sealed class Definition
    {
        public string Id;
        public string Label;
        public Action Click;
    }

    private readonly GameObject root;
    private readonly Font font;
    private readonly string title;
    private readonly List<(Definition Definition, GameObject Root)> rows = new();
    private readonly List<HudSearchEntry> searchEntries = new();
    private bool searchActive;

    public HudPageId PageId { get; }
    public IReadOnlyList<HudSearchEntry> SearchEntries => searchEntries;

    private HudActionCard(
        GameObject parent,
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

        var body = Node(card, "Actions");
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(HudTheme.SliderCardPadding, HudTheme.SliderCardPadding);
        bodyRect.offsetMax = new Vector2(-HudTheme.SliderCardPadding, -100f);
        var layout = body.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = HudTheme.ActionButtonGap;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        foreach (var definition in definitions)
            AddButton(body, definition);

        root.SetActive(false);
        searchEntries.Add(new HudSearchEntry { Card = title, Label = title });
        HudSearch.Register(this);
    }

    public static HudActionCard Build(
        GameObject parent,
        Font font,
        HudPageId pageId,
        string title,
        IReadOnlyList<Definition> definitions
    )
    {
        string rootName = HudPageRoutes.SearchRoot(pageId);
        if (rootName == null || parent.IsNullOrDestroyed())
        {
            return null;
        }

        return new HudActionCard(parent, font, pageId, rootName, title, definitions);
    }

    public void Show()
    {
        if (!root.IsNullOrDestroyed())
            root.SetActive(true);
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
    }

    public void ClearSearch()
    {
        if (!searchActive)
            return;
        searchActive = false;
        foreach (var row in rows)
            if (!row.Root.IsNullOrDestroyed())
                row.Root.SetActive(true);
    }

    private void AddButton(GameObject parent, Definition definition)
    {
        var buttonObject = Node(parent, "Button_" + definition.Id);
        var element = buttonObject.AddComponent<LayoutElement>();
        element.minHeight = HudTheme.ActionButtonHeight;
        element.preferredHeight = HudTheme.ActionButtonHeight;
        element.flexibleHeight = 0f;

        var image = buttonObject.AddComponent<Image>();
        image.color = HudTheme.Surface;
        HudStyler.AddPrimaryBorder(buttonObject);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = HudTheme.ActionButtonColors(HudTheme.Surface, HudTheme.Selection);
        Prefab.BindButton(button, definition.Click);

        var label = TextNode(buttonObject, "Label", definition.Label, HudTheme.BodyFontSize);
        Stretch(label.GetComponent<RectTransform>());
        label.alignment = TextAnchor.MiddleCenter;
        label.color = HudTheme.TextPrimary;
        rows.Add((definition, buttonObject));
        searchEntries.Add(new HudSearchEntry { Card = title, Label = definition.Label });
    }

    private bool Matches(string query, string label)
    {
        if (!HudNavigation.TryGetPage(PageId, out var section, out var page))
            return false;
        return HudSearch.Score(query, label, title, page.Label, section.Label) >= 0;
    }

    private static GameObject Node(GameObject parent, string name) =>
        HudElements.Node(parent, name);

    private Text TextNode(GameObject parent, string name, string caption, int size) =>
        HudElements.Text(parent, name, caption, font, size, HorizontalWrapMode.Wrap);

    private static void Stretch(RectTransform rect) => HudElements.Stretch(rect);
}
