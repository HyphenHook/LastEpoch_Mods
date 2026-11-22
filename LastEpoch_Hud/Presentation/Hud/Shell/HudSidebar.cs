using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

/// <summary>Builds the sidebar from HudNavigation.Sections and owns its buttons, accordion state and selection visuals; reset on HUD re-bind.</summary>
internal static class HudSidebar
{
    private static readonly Dictionary<HudPageId, NavigationButton> _pageButtons = new();
    private static readonly List<NavigationSection> _sections = new();
    private static Font _font;
    private static Action<HudPageDefinition> _activatePage;

    public static void Reset()
    {
        _pageButtons.Clear();
        _sections.Clear();
        _font = null;
        _activatePage = null;
    }

    public static void Build(GameObject menu, Font font, Action<HudPageDefinition> activatePage)
    {
        _font = font;
        _activatePage = activatePage;
        StyleMenu(menu);
        GameObject rightBorder = BuildRightBorder(menu);
        GameObject list = BuildList(menu);
        foreach (HudSectionDefinition definition in HudNavigation.Sections)
        {
            BuildSection(list, definition);
        }
        // Navigation rows span the full sidebar and otherwise cover this edge.
        rightBorder.transform.SetAsLastSibling();
    }

    public static void ExpandSection(HudSectionId sectionId, bool expanded)
    {
        foreach (NavigationSection section in _sections)
        {
            if (!section.Definition.Accordion)
            {
                continue;
            }
            bool show = section.Definition.Id == sectionId && expanded;
            if (!expanded && section.Definition.Id != sectionId)
            {
                show = section.Expanded;
            }
            section.Expanded = show;
            if (!section.Indicator.IsNullOrDestroyed())
            {
                section.Indicator.text = show ? "-" : "+";
            }
            ShowRows(section, show);
        }
    }

    public static void SetSelected(HudPageId pageId)
    {
        foreach (KeyValuePair<HudPageId, NavigationButton> pair in _pageButtons)
        {
            bool selected = pair.Key == pageId;
            pair.Value.Background.color = HudTheme.SelectableTint;
            pair.Value.Button.colors = HudTheme.ButtonColors(
                selected ? HudTheme.Selection : HudTheme.Surface,
                selected ? HudTheme.Selection : HudTheme.SurfaceHover
            );
            if (!pair.Value.Accent.IsNullOrDestroyed())
            {
                pair.Value.Accent.SetActive(selected);
            }
        }
    }

    private static void StyleMenu(GameObject menu)
    {
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = new Vector2(0f, 1f);
        menuRect.pivot = new Vector2(0f, 0.5f);
        menuRect.offsetMin = Vector2.zero;
        menuRect.offsetMax = new Vector2(HudTheme.SidebarWidth, -HudTheme.HeaderHeight);

        GameObject oldContent = Prefab.Child(menu, "Content");
        if (!oldContent.IsNullOrDestroyed())
        {
            oldContent.SetActive(false);
        }

        Image background = menu.GetComponent<Image>();
        if (background.IsNullOrDestroyed())
        {
            background = menu.AddComponent<Image>();
        }
        background.color = HudTheme.Sidebar;
    }

    private static GameObject BuildRightBorder(GameObject menu)
    {
        GameObject rightBorder = Node(menu, "LEHUD_RightBorder");
        RectTransform borderRect = rightBorder.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(1f, 0f);
        borderRect.anchorMax = Vector2.one;
        borderRect.pivot = new Vector2(1f, 0.5f);
        borderRect.anchoredPosition = Vector2.zero;
        borderRect.sizeDelta = new Vector2(HudTheme.BorderWidth, 0f);
        Image border = rightBorder.AddComponent<Image>();
        border.color = HudTheme.Border;
        border.raycastTarget = false;
        return rightBorder;
    }

    private static GameObject BuildList(GameObject menu)
    {
        GameObject list = Node(menu, "LEHUD_Navigation");
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 1f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.anchoredPosition = Vector2.zero;
        listRect.sizeDelta = Vector2.zero;
        listRect.offsetMin = new Vector2(0f, 0f);
        listRect.offsetMax = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = list.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = list.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        return list;
    }

    private static void BuildSection(GameObject list, HudSectionDefinition definition)
    {
        var section = new NavigationSection { Definition = definition };
        _sections.Add(section);
        Button sectionButton = CreateRow(
            list,
            "Section_" + definition.Id,
            definition.Label,
            HudTheme.SectionHeight,
            false
        );
        section.Indicator = AddIndicator(sectionButton.gameObject);
        AddDivider(sectionButton.gameObject, false);

        if (!definition.Accordion)
        {
            HudPageDefinition firstPage = definition.Pages[0];
            Prefab.BindButton(sectionButton, new Action(() => _activatePage(firstPage)));
            _pageButtons[firstPage.Id] = new NavigationButton
            {
                Button = sectionButton,
                Background = sectionButton.GetComponent<Image>(),
            };
            return;
        }

        Prefab.BindButton(sectionButton, new Action(() => ToggleSection(section)));
        foreach (HudPageDefinition page in definition.Pages)
        {
            BuildPageRow(list, section, page);
        }
    }

    private static void BuildPageRow(
        GameObject list,
        NavigationSection section,
        HudPageDefinition page
    )
    {
        Button pageButton = CreateRow(
            list,
            "Page_" + page.Id,
            page.Label,
            HudTheme.PageHeight,
            true
        );
        GameObject row = pageButton.gameObject;
        GameObject accent = AddAccent(row);
        AddDivider(row, true);
        Prefab.BindButton(pageButton, new Action(() => _activatePage(page)));
        section.PageRows.Add(row);
        _pageButtons[page.Id] = new NavigationButton
        {
            Button = pageButton,
            Background = Prefab.Child(row, "ButtonSurface").GetComponent<Image>(),
            Accent = accent,
        };
        row.SetActive(false);
    }

    private static Button CreateRow(
        GameObject parent,
        string name,
        string caption,
        float height,
        bool child
    )
    {
        GameObject row = Node(parent, name);
        Image image = row.AddComponent<Image>();
        image.color = HudTheme.Surface;
        Button button = row.AddComponent<Button>();
        LayoutElement element = row.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;

        Image buttonSurface = image;
        if (child)
        {
            GameObject surfaceObject = Node(row, "ButtonSurface");
            RectTransform surfaceRect = surfaceObject.GetComponent<RectTransform>();
            surfaceRect.anchorMin = Vector2.zero;
            surfaceRect.anchorMax = Vector2.one;
            surfaceRect.offsetMin = new Vector2(HudTheme.NavigationIndent - 10f, 0f);
            surfaceRect.offsetMax = Vector2.zero;
            buttonSurface = surfaceObject.AddComponent<Image>();
            buttonSurface.color = HudTheme.Surface;
        }
        button.targetGraphic = buttonSurface;
        button.colors = HudTheme.ButtonColors(HudTheme.Surface, HudTheme.SurfaceHover);

        Text label = TextNode(
            row,
            "Label",
            caption,
            child ? HudTheme.PageFontSize : HudTheme.SectionFontSize
        );
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(child ? HudTheme.NavigationIndent : 18f, 0f);
        labelRect.offsetMax = new Vector2(-46f, 0f);
        label.alignment = TextAnchor.MiddleLeft;
        label.color = HudTheme.TextPrimary;
        return button;
    }

    private static Text AddIndicator(GameObject row)
    {
        Text indicator = TextNode(row, "Indicator", string.Empty, HudTheme.SectionFontSize);
        RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(1f, 0f);
        indicatorRect.anchorMax = Vector2.one;
        indicatorRect.pivot = new Vector2(1f, 0.5f);
        indicatorRect.offsetMin = new Vector2(-44f, 0f);
        indicatorRect.offsetMax = new Vector2(-14f, 0f);
        indicator.alignment = TextAnchor.MiddleCenter;
        indicator.color = HudTheme.TextPrimary;
        return indicator;
    }

    private static GameObject AddAccent(GameObject row)
    {
        GameObject gutter = Node(row, "SubmenuGutter");
        RectTransform gutterRect = gutter.GetComponent<RectTransform>();
        gutterRect.anchorMin = Vector2.zero;
        gutterRect.anchorMax = new Vector2(0f, 1f);
        gutterRect.pivot = new Vector2(0f, 0.5f);
        gutterRect.sizeDelta = new Vector2(HudTheme.BorderWidth, 0f);
        gutterRect.anchoredPosition = new Vector2(HudTheme.NavigationIndent - 12f, 0f);
        Image gutterImage = gutter.AddComponent<Image>();
        gutterImage.color = HudTheme.Border;
        gutterImage.raycastTarget = false;

        GameObject accent = Node(row, "SelectedAccent");
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = Vector2.zero;
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(4f, 0f);
        accentRect.anchoredPosition = new Vector2(HudTheme.NavigationIndent - 12f, 0f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = HudTheme.Accent;
        accentImage.raycastTarget = false;
        accent.SetActive(false);
        return accent;
    }

    private static void AddDivider(GameObject row, bool child)
    {
        GameObject divider = Node(row, "Divider");
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = Vector2.zero;
        dividerRect.anchorMax = new Vector2(1f, 0f);
        dividerRect.pivot = new Vector2(0.5f, 0f);
        dividerRect.offsetMin = new Vector2(child ? HudTheme.NavigationIndent - 10f : 0f, 0f);
        dividerRect.offsetMax = Vector2.zero;
        dividerRect.sizeDelta = new Vector2(dividerRect.sizeDelta.x, HudTheme.BorderWidth);
        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = child ? HudTheme.Border : HudTheme.AccentMuted;
        dividerImage.raycastTarget = false;
    }

    private static void ToggleSection(NavigationSection section)
    {
        if (section == null || !section.Definition.Accordion)
        {
            return;
        }
        bool expand = !section.Expanded;
        ExpandSection(section.Definition.Id, expand);
        if (expand && section.Definition.Pages.Length > 0)
        {
            _activatePage(section.Definition.Pages[0]);
        }
    }

    private static void ShowRows(NavigationSection section, bool show)
    {
        foreach (GameObject row in section.PageRows)
        {
            if (!row.IsNullOrDestroyed())
            {
                row.SetActive(show);
            }
        }
    }

    private static GameObject Node(GameObject parent, string name) =>
        HudElements.Node(parent, name);

    private static Text TextNode(GameObject parent, string name, string caption, int size) =>
        HudElements.Text(parent, name, caption, _font, size);

    private sealed class NavigationButton
    {
        public Button Button;
        public Image Background;
        public GameObject Accent;
    }

    private sealed class NavigationSection
    {
        public HudSectionDefinition Definition;
        public Text Indicator;
        public readonly List<GameObject> PageRows = new();
        public bool Expanded;
    }
}
