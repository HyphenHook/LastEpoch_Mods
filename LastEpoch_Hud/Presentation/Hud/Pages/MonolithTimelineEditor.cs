using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.Monoliths;
using LastEpoch_Hud.Scripts.Mods.Monoliths;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>
/// World > Monoliths timeline editor built from its own controls.
/// Build resets all state, once per HUD instance; the selected timeline and its view are cached
/// here and reloaded after every select, edit, copy and page show.
/// </summary>
internal static class MonolithTimelineEditor
{
    private const float EditorHeight = 500f;

    private static readonly List<Button> _timelineButtons = new();
    private static readonly List<GameObject> _timelineAccents = new();
    private static readonly List<Text> _timelineLabels = new();
    private static readonly List<MonolithValueRow> _valueRows = new();
    private static GameObject _root;
    private static Text _selectedLabel;
    private static Text _availabilityLabel;
    private static Button _copyButton;
    private static int _selectedTimelineId;
    private static MonolithTimelineView _view;

    private static Color Gold => HudTheme.AccentBright;

    private static Color PanelColor => HudTheme.InputBackground;

    public static void Build(HudFormPage page, GameObject parent, Font font)
    {
        ResetState();
        if (parent.IsNullOrDestroyed())
        {
            return;
        }

        _root = CreateRoot(parent);
        GameObject list = CreatePanel("TimelineSelection", _root, 0f, 0.34f);
        GameObject values = CreatePanel("TimelineValues", _root, 0.34f, 0.75f);
        GameObject actions = CreatePanel("TimelineActions", _root, 0.75f, 1f);
        CreateLabel(list, font, "Timelines", 8f, 32f);
        _selectedLabel = CreateLabel(values, font, string.Empty, 8f, 52f);
        CreateLabel(actions, font, "Actions", 8f, 32f);

        BuildTimelineButtons(list, font);
        AddValueRow(page, values, font, MonolithField.BasicStability, "Normal Stability", 76f);
        AddValueRow(
            page,
            values,
            font,
            MonolithField.EmpoweredStability,
            "Empowered Stability",
            158f
        );
        AddValueRow(page, values, font, MonolithField.Corruption, "Corruption", 240f);
        AddValueRow(page, values, font, MonolithField.Gaze, "Gazes", 322f);
        _availabilityLabel = CreateLabel(values, font, string.Empty, 408f, 80f);
        _availabilityLabel.fontSize = 13;

        _copyButton = CreateButton(actions, font, "Copy to All", 46f, 40f, CopyToAll);
        CreateButton(actions, font, "Reload Values", 98f, 40f, Reload);
        Text note = CreateLabel(
            actions,
            font,
            "Edits apply immediately.\n\nCopy to All applies the visible values to existing timelines of the same difficulty.",
            158f,
            190f
        );
        note.fontSize = 13;
        note.alignment = TextAnchor.UpperLeft;

        Select(MonolithRunEditor.InitialTimelineId());
    }

    /// <summary>Loads the selected timeline's view and shows it.</summary>
    public static void Reload()
    {
        _view = MonolithRunEditor.ViewOf(_selectedTimelineId);
        foreach (MonolithValueRow row in _valueRows)
        {
            row.Show(_view);
        }

        RefreshSelection();
    }

    /// <summary>Applies the cached selection and availability state to the controls.</summary>
    public static void RefreshSelection()
    {
        if (_root.IsNullOrDestroyed())
        {
            return;
        }

        ApplySelectedLabel();
        for (int i = 0; i < _timelineButtons.Count; i++)
        {
            ApplyTimelineName(i);
            ApplyButtonState(i, MonolithTimelineCatalog.All[i].TimelineId == _selectedTimelineId);
        }

        ApplyAvailability();
    }

    internal static void Place(
        GameObject obj,
        float left,
        float right,
        float top,
        float height,
        float leftInset = 0f,
        float rightInset = 0f
    )
    {
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(left, 1f);
        rect.anchorMax = new Vector2(right, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-leftInset - rightInset, height);
        rect.anchoredPosition = new Vector2((leftInset - rightInset) * 0.5f, -top);
        rect.localScale = Vector3.one;
        LayoutElement layout = obj.GetComponent<LayoutElement>();
        if (layout.IsNullOrDestroyed())
        {
            return;
        }

        layout.ignoreLayout = true;
        layout.flexibleHeight = 0f;
    }

    private static void ResetState()
    {
        _root = null;
        _selectedLabel = null;
        _availabilityLabel = null;
        _copyButton = null;
        _timelineButtons.Clear();
        _timelineAccents.Clear();
        _timelineLabels.Clear();
        _valueRows.Clear();
        _selectedTimelineId = 0;
        _view = default;
    }

    private static void Select(int timelineId)
    {
        _selectedTimelineId = timelineId;
        Reload();
    }

    private static void Commit(MonolithField field, string text)
    {
        if (MonolithValueRules.TryParse(text, out int value))
        {
            MonolithRunEditor.SetValue(_selectedTimelineId, field, value);
        }

        Reload();
    }

    private static void CopyToAll()
    {
        MonolithRunEditor.CopyToAll(_selectedTimelineId);
        Reload();
    }

    private static void ApplySelectedLabel()
    {
        if (_selectedLabel.IsNullOrDestroyed())
        {
            return;
        }

        if (!MonolithTimelineCatalog.TryGet(_selectedTimelineId, out MonolithTimelineOption option))
        {
            return;
        }

        _selectedLabel.text = MonolithTimelineNames.Of(option);
    }

    private static void ApplyTimelineName(int index)
    {
        Text label = _timelineLabels[index];
        if (label.IsNullOrDestroyed())
        {
            return;
        }

        label.text = MonolithTimelineNames.Of(MonolithTimelineCatalog.All[index]);
    }

    private static void ApplyButtonState(int index, bool isSelected)
    {
        Button button = _timelineButtons[index];
        if (button.IsNullOrDestroyed())
        {
            return;
        }

        Color normal = isSelected ? HudTheme.Selection : HudTheme.Surface;
        button.colors = HudTheme.ActionButtonColors(
            normal,
            isSelected ? HudTheme.Selection : HudTheme.SurfaceHover
        );
        Image image = button.GetComponent<Image>();
        if (!image.IsNullOrDestroyed())
        {
            image.color = HudTheme.SelectableTint;
        }

        if (!_timelineAccents[index].IsNullOrDestroyed())
        {
            _timelineAccents[index].SetActive(isSelected);
        }
    }

    private static void ApplyAvailability()
    {
        if (!_availabilityLabel.IsNullOrDestroyed())
        {
            Prefab.ApplyLabel(
                _availabilityLabel,
                _view.HasRun
                    ? "Only unlocked timeline values are shown."
                    : "This timeline has no saved run yet."
            );
        }

        if (!_copyButton.IsNullOrDestroyed())
        {
            _copyButton.interactable = _view.HasRun;
        }
    }

    private static GameObject CreateRoot(GameObject parent)
    {
        GameObject root = HudElements.Node(parent, "TimelineEditor");
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, EditorHeight);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.ignoreLayout = false;
        layout.minHeight = EditorHeight;
        layout.preferredHeight = EditorHeight;
        layout.flexibleHeight = 0f;
        return root;
    }

    private static void BuildTimelineButtons(GameObject list, Font font)
    {
        for (int i = 0; i < MonolithTimelineCatalog.All.Count; i++)
        {
            MonolithTimelineOption option = MonolithTimelineCatalog.All[i];
            int timelineId = option.TimelineId;
            Button button = CreateButton(
                list,
                font,
                option.Label,
                46f + (i * 44f),
                40f,
                () => Select(timelineId),
                false
            );
            _timelineButtons.Add(button);
            _timelineLabels.Add(button.GetComponentInChildren<Text>(true));
            _timelineAccents.Add(CreateSelectionAccent(button.gameObject));
        }
    }

    private static void AddValueRow(
        HudFormPage page,
        GameObject parent,
        Font font,
        MonolithField field,
        string title,
        float top
    )
    {
        _valueRows.Add(MonolithValueRow.Build(page, parent, font, field, title, top, Commit));
    }

    private static GameObject CreatePanel(string name, GameObject parent, float left, float right)
    {
        GameObject panel = HudElements.Node(parent, name);
        Place(panel, left, right, 0f, EditorHeight, 4f, 4f);
        Image image = panel.AddComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = false;
        Outline border = panel.AddComponent<Outline>();
        border.effectColor = Gold;
        border.effectDistance = new Vector2(1f, -1f);
        return panel;
    }

    private static Text CreateLabel(
        GameObject parent,
        Font font,
        string text,
        float top,
        float height,
        bool localize = true
    )
    {
        Text label = HudElements.Text(
            parent,
            "Label",
            text,
            font,
            16,
            HorizontalWrapMode.Wrap,
            localize
        );
        label.alignment = TextAnchor.MiddleLeft;
        Place(label.gameObject, 0f, 1f, top, height, 12f, 12f);
        return label;
    }

    private static Button CreateButton(
        GameObject parent,
        Font font,
        string text,
        float top,
        float height,
        Action onClick,
        bool localize = true
    )
    {
        GameObject obj = HudElements.Node(parent, "Btn_" + text.Replace(' ', '_'));
        Place(obj, 0f, 1f, top, height, 10f, 10f);
        Image image = obj.AddComponent<Image>();
        image.type = Image.Type.Simple;
        image.color = HudTheme.Surface;
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        StyleButton(button);
        Prefab.BindButton(button, onClick);
        Text label = CreateLabel(obj, font, text, 0f, height, localize);
        label.alignment = TextAnchor.MiddleCenter;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 11;
        label.resizeTextMaxSize = 16;
        return button;
    }

    private static void StyleButton(Button button)
    {
        HudStyler.AddPrimaryBorder(button.gameObject);
        button.colors = HudTheme.ActionButtonColors(HudTheme.Surface, HudTheme.Selection);
    }

    private static GameObject CreateSelectionAccent(GameObject button)
    {
        GameObject accent = HudElements.Node(button, "SelectedAccent");
        RectTransform rect = accent.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(4f, 0f);
        Image image = accent.AddComponent<Image>();
        image.color = HudTheme.Accent;
        image.raycastTarget = false;
        accent.transform.SetAsLastSibling();
        accent.SetActive(false);
        return accent;
    }
}
