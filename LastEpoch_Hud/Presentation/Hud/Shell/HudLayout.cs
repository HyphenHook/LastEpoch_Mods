using System;
using LastEpoch_Hud.Scripts.ModUI.Pages;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Shell;

/// <summary>Owns the HUD window, header and page activation; wires sidebar, settings panel and search; state resets on HUD re-bind in Initialize.</summary>
public static class HudLayout
{
    private static GameObject boundHud;
    private static GameObject window;
    private static Font font;
    private static HudPageDefinition activePage;

    public static void Initialize(GameObject hud)
    {
        if (hud.IsNullOrDestroyed() || boundHud == hud)
            return;

        Main.logger_instance?.Msg("[Startup] HudLayout.Initialize entered.");
        boundHud = hud;
        HudSidebar.Reset();
        HudSearch.Reset();
        activePage = null;
        HudTheme.LoadPreferences();
        HudStyler.ResetFontBaselines();
        font = HudElements.FindFont(hud);

        var content = Prefab.Child(hud, "Content");
        var menu = Prefab.Child(hud, "Menu");
        if (content.IsNullOrDestroyed() || menu.IsNullOrDestroyed())
        {
            Main.logger_instance?.Warning("HudLayout: missing Content or Menu root");
            return;
        }

        Main.logger_instance?.Msg("[Startup] HudLayout building window.");
        window = BuildWindow(hud);
        menu.transform.SetParent(window.transform, false);
        content.transform.SetParent(window.transform, false);
        Main.logger_instance?.Msg("[Startup] HudLayout building header and sidebar.");
        BuildHeader(window);
        HudSidebar.Build(menu, font, page => ActivateNavigationOnly(page));
        Main.logger_instance?.Msg("[Startup] HudLayout building pages.");
        PlaceLegacyContent(content);
        HideLegacyPanels(content);
        foreach (var page in HudNavigation.Pages)
            page.Build(window, hud, font);

        var defaultPage = HudNavigation.Sections[0].Pages[0];
        HudSidebar.ExpandSection(HudNavigation.Sections[0].Id, true);
        ActivateNavigationOnly(defaultPage);
        Main.logger_instance?.Msg("[Startup] HudLayout applying styles.");
        HudStyler.NormalizeSelectableGraphics(window);
        HudStyler.ApplyFontScale(window);
        HudSettingsPanel.Refresh();
        Main.logger_instance?.Msg("HudLayout: replacement navigation initialized");
    }

    private static GameObject BuildWindow(GameObject hud)
    {
        var backdrop = Node(hud, "LEHUD_Backdrop");
        Stretch(backdrop.GetComponent<RectTransform>());
        var image = backdrop.AddComponent<Image>();
        image.color = HudTheme.Backdrop;
        image.raycastTarget = false;
        backdrop.transform.SetAsFirstSibling();

        var windowObject = Node(hud, "LEHUD_Window");
        var windowRect = windowObject.GetComponent<RectTransform>();
        windowRect.anchorMin = HudTheme.WindowAnchorMin;
        windowRect.anchorMax = HudTheme.WindowAnchorMax;
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;
        var windowImage = windowObject.AddComponent<Image>();
        windowImage.color = HudTheme.Background;
        var outline = windowObject.AddComponent<Outline>();
        outline.effectColor = HudTheme.Border;
        outline.effectDistance = new Vector2(1f, -1f);
        return windowObject;
    }

    private static void BuildHeader(GameObject hud)
    {
        var header = Node(hud, "LEHUD_Header");
        var rect = header.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, HudTheme.HeaderHeight);
        var background = header.AddComponent<Image>();
        background.color = HudTheme.Header;
        background.raycastTarget = false;

        var brand = TextNode(header, "Brand", "LE HUD MOD", HudTheme.BrandFontSize);
        var brandRect = brand.GetComponent<RectTransform>();
        brandRect.anchorMin = Vector2.zero;
        brandRect.anchorMax = Vector2.one;
        brandRect.offsetMin = new Vector2(26f, 0f);
        brandRect.offsetMax = new Vector2(-HudTheme.HeaderHeight, 0f);
        brand.alignment = TextAnchor.MiddleLeft;
        brand.color = HudTheme.TextPrimary;

        var closeObject = Node(header, "Close");
        var closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-24f, 0f);
        closeRect.sizeDelta = new Vector2(48f, 48f);
        var closeImage = closeObject.AddComponent<Image>();
        closeImage.color = HudTheme.Surface;
        var outline = closeObject.AddComponent<Outline>();
        outline.effectColor = HudTheme.Border;
        outline.effectDistance = new Vector2(1f, -1f);
        var close = closeObject.AddComponent<Button>();
        close.targetGraphic = closeImage;
        close.colors = HudTheme.ButtonColors(HudTheme.Surface, HudTheme.SurfaceHover);
        Prefab.BindButton(close, new Action(Close));
        var closeText = TextNode(closeObject, "Label", "X", 25);
        Stretch(closeText.GetComponent<RectTransform>());
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = HudTheme.TextPrimary;

        var settingsObject = Node(header, "Settings");
        var settingsRect = settingsObject.GetComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(1f, 0.5f);
        settingsRect.anchorMax = new Vector2(1f, 0.5f);
        settingsRect.pivot = new Vector2(1f, 0.5f);
        settingsRect.anchoredPosition = new Vector2(-84f, 0f);
        settingsRect.sizeDelta = new Vector2(48f, 48f);
        var settingsImage = settingsObject.AddComponent<Image>();
        settingsImage.color = HudTheme.Surface;
        HudStyler.AddPrimaryBorder(settingsObject, 1f);
        var settingsButton = settingsObject.AddComponent<Button>();
        settingsButton.targetGraphic = settingsImage;
        settingsButton.colors = HudTheme.ButtonColors(HudTheme.Surface, HudTheme.Selection);
        ButtonHook.Register(settingsButton, HudSettingsPanel.Toggle);
        var settingsIcon = TextNode(settingsObject, "Label", "⚙", 27);
        Stretch(settingsIcon.GetComponent<RectTransform>());
        settingsIcon.alignment = TextAnchor.MiddleCenter;
        settingsIcon.color = HudTheme.TextPrimary;

        var line = Node(header, "Border");
        var lineRect = line.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta = new Vector2(0f, HudTheme.BorderWidth);
        var lineImage = line.AddComponent<Image>();
        lineImage.color = HudTheme.Border;
        lineImage.raycastTarget = false;

        HudSettingsPanel.Build(hud, font, ReapplySelection);
        HudSearchBar.Build(header, hud, boundHud, font, ActivateSearchMatch);
    }

    private static void PlaceLegacyContent(GameObject content)
    {
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(HudTheme.SidebarWidth, 0f);
        contentRect.offsetMax = new Vector2(0f, -HudTheme.HeaderHeight);
        var background = content.GetComponent<Image>();
        if (!background.IsNullOrDestroyed())
            background.color = HudTheme.Background;
    }

    private static void ActivateNavigationOnly(HudPageDefinition page, bool preserveSearch = false)
    {
        if (page == null)
            return;
        if (!preserveSearch)
        {
            HudSearch.ClearAll();
            HudSearchBar.Clear();
        }
        foreach (var candidate in HudNavigation.Pages)
            candidate.Hide();
        page.Show();
        HudSidebar.SetSelected(page.Id);
        activePage = page;
    }

    private static void ActivateSearchMatch(HudSearchMatch match, string query)
    {
        if (
            match == null
            || string.IsNullOrWhiteSpace(query)
            || !HudNavigation.TryGetPage(match.PageId, out var section, out var page)
        )
            return;
        HudSearch.ClearAll();
        HudSidebar.ExpandSection(section.Id, true);
        ActivateNavigationOnly(page, true);
        match.Page.ApplySearch(query);
    }

    private static void ReapplySelection()
    {
        if (activePage == null)
        {
            return;
        }
        HudSidebar.SetSelected(activePage.Id);
    }

    public static void RefreshActivePage()
    {
        activePage?.Refresh();
    }

    /// <summary>Hides every prefab panel under the legacy Content root once.</summary>
    private static void HideLegacyPanels(GameObject content)
    {
        foreach (GameObject child in Functions.GetAllChild(content))
        {
            child.SetActive(false);
        }
    }

    private static void Close()
    {
        if (Hud_Manager.mod_menu_open)
            Hud_Manager.mod_menu_open = false;
        else
            GamePauseMenu.Resume();
    }

    private static GameObject Node(GameObject parent, string name) =>
        HudElements.Node(parent, name);

    private static Text TextNode(GameObject parent, string name, string caption, int size) =>
        HudElements.Text(parent, name, caption, font, size);

    private static void Stretch(RectTransform rect) => HudElements.Stretch(rect);
}
