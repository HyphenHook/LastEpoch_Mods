using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Own canvas of an icon bar above the skill bar: panel, placement and tooltip.</summary>
internal sealed class BuffBarFrame
{
    private readonly string _rootName;
    private GameObject _root;
    private RectTransform _panel;
    private CanvasGroup _panelGroup;
    private Canvas _canvas;
    private Canvas _matchedSource;
    private UnityEngine.Camera _matchedCamera;
    private HeadhunterBarPlacement _placement;
    private bool _visible;
    private HeadhunterBarGrid _grid;
    private float _canvasScale = 1f;
    private HeadhunterBarTooltip _tooltip;

    public BuffBarFrame(string rootName)
    {
        _rootName = rootName;
    }

    public bool IsVisible => _visible;

    public int LayoutVersion { get; private set; }

    public HeadhunterBarGrid Grid => _grid;

    public Transform Panel => _panel;

    /// <summary>Makes sure the root exists. created is true for a new root: the owner drops its slots.</summary>
    public bool EnsureCreated(out bool created)
    {
        created = false;
        if (!_root.IsNullOrDestroyed())
        {
            return true;
        }

        if (!HeadhunterBuffBarAssets.TryLoad())
        {
            return false;
        }

        _grid = default;
        _tooltip = null;
        _placement = default;
        _matchedSource = null;
        _root = Object.Instantiate(HeadhunterBuffBarAssets.BarPrefab);
        _root.name = _rootName;
        Object.DontDestroyOnLoad(_root);
        ConfigureCanvas();
        ConfigurePanel();
        created = true;
        return true;
    }

    public void SetAlpha(float alpha)
    {
        _panelGroup.alpha = alpha;
    }

    /// <summary>Shows and places the bar. True when the grid changed: the owner places its slots.</summary>
    public bool Show(HeadhunterBarGrid grid, HeadhunterBarSettings settings)
    {
        _root.SetActive(true);
        bool changed = grid != _grid;
        if (changed)
        {
            ApplyLayout(grid);
        }

        Place(settings);
        _visible = true;
        return changed;
    }

    public void Hide()
    {
        if (!_visible)
        {
            return;
        }

        _visible = false;
        _grid = default;
        HideTooltip();
        if (!_root.IsNullOrDestroyed())
        {
            _root.SetActive(false);
        }
    }

    public int IndexAt(float x, float y)
    {
        if (!_visible)
        {
            return -1;
        }

        return HeadhunterBarGeometry.IndexAt(_placement, _canvasScale, _grid, x, y);
    }

    public void ShowTooltip(int index, string text, Font font)
    {
        if (index < 0 || index >= _grid.Count || !EnsureTooltip(font))
        {
            return;
        }

        (float sx, float sy) = HeadhunterBarGeometry.TooltipAnchor(
            _placement,
            _canvasScale,
            _grid,
            index
        );
        (float x, float y) = HeadhunterBarLayout.ToLocal(
            sx,
            sy,
            _placement.ScreenW,
            _placement.ScreenH,
            _canvasScale
        );
        _tooltip.Show(text, x, y, _placement.Scale);
    }

    public void HideTooltip()
    {
        if (_tooltip == null || _root.IsNullOrDestroyed())
        {
            return;
        }

        _tooltip.Hide();
    }

    private void ConfigureCanvas()
    {
        _root.transform.localScale = Vector3.one;
        _canvas = _root.GetComponent<Canvas>();
        GraphicRaycaster raycaster = _root.GetComponent<GraphicRaycaster>();
        if (!raycaster.IsNullOrDestroyed())
        {
            raycaster.enabled = false;
        }
    }

    private void ConfigurePanel()
    {
        GameObject panelObject = Functions.GetChild(_root, "Panel");
        _panel = panelObject.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 0.5f);
        _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0f);
        _panel.sizeDelta = new Vector2(20f, HeadhunterBarLayout.EntrySize + 4f);
        panelObject.GetComponent<GridLayoutGroup>().enabled = false;
        _panelGroup = EnsureGroup(panelObject);
    }

    private static CanvasGroup EnsureGroup(GameObject panelObject)
    {
        CanvasGroup group = panelObject.GetComponent<CanvasGroup>();
        if (!group.IsNullOrDestroyed())
        {
            return group;
        }

        return panelObject.AddComponent<CanvasGroup>();
    }

    private void ApplyLayout(HeadhunterBarGrid grid)
    {
        _grid = grid;
        _panel.sizeDelta = new Vector2(grid.Width, grid.Height);
        LayoutVersion++;
    }

    private bool EnsureTooltip(Font font)
    {
        if (_root.IsNullOrDestroyed())
        {
            return false;
        }

        if (_tooltip != null)
        {
            return true;
        }

        _tooltip = new HeadhunterBarTooltip(_root.transform, font);
        return true;
    }

    private void Place(HeadhunterBarSettings settings)
    {
        SkillBarBounds bounds = HeadhunterSkillBarLocator.Read(out Canvas source);
        MatchCanvas(source);
        if (
            !HeadhunterBarLayout.TryPlace(
                bounds,
                settings,
                HeadhunterBarLayout.EntrySize,
                _canvas.scaleFactor,
                out HeadhunterBarPlacement next
            )
        )
        {
            return;
        }

        next = next with { ScreenW = Screen.width, ScreenH = Screen.height };
        if (!HeadhunterBarLayout.ShouldMove(_placement, next, HeadhunterBarLayout.EntrySize))
        {
            return;
        }

        Move(next);
    }

    private void Move(HeadhunterBarPlacement placement)
    {
        _placement = placement;
        _canvasScale = _canvas.scaleFactor;
        LayoutVersion++;
        (float x, float y) = HeadhunterBarLayout.ToLocal(
            placement,
            placement.ScreenW,
            placement.ScreenH,
            _canvas.scaleFactor
        );
        _panel.anchoredPosition = new Vector2(x, y);
        _panel.localScale = new Vector3(placement.Scale, placement.Scale, 1f);
    }

    private void MatchCanvas(Canvas source)
    {
        if (source.IsNullOrDestroyed())
        {
            ApplyOverlayDefaults();
            return;
        }

        if (source == _matchedSource && source.worldCamera == _matchedCamera)
        {
            return;
        }

        _matchedSource = source;
        _matchedCamera = source.worldCamera;
        _canvas.renderMode = source.renderMode;
        _canvas.worldCamera = source.worldCamera;
        _canvas.planeDistance = source.planeDistance;
        _canvas.sortingLayerID = source.sortingLayerID;
        _canvas.sortingOrder = source.sortingOrder - 1;
    }

    private void ApplyOverlayDefaults()
    {
        if (_matchedSource == null && _canvas.sortingOrder == -1)
        {
            return;
        }

        _matchedSource = null;
        _matchedCamera = null;
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = -1;
    }
}
