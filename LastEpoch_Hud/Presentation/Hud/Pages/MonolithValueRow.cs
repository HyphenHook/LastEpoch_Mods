using System;
using System.Globalization;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Monoliths;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>One editable monolith value: a label and an integer input.</summary>
internal sealed class MonolithValueRow
{
    private readonly MonolithField _field;
    private readonly GameObject _root;
    private readonly TMP_InputField _input;

    private MonolithValueRow(MonolithField field, GameObject root, TMP_InputField input)
    {
        _field = field;
        _root = root;
        _input = input;
    }

    public static MonolithValueRow Build(
        HudFormPage page,
        GameObject parent,
        Font font,
        MonolithField field,
        string title,
        float top,
        Action<MonolithField, string> commit
    )
    {
        GameObject root = HudElements.Node(parent, "Row_" + field);
        MonolithTimelineEditor.Place(root, 0f, 1f, top, 72f, 12f, 12f);
        Text label = HudElements.Text(root, "Label", title, font, 14, HorizontalWrapMode.Wrap);
        label.color = HudTheme.AccentBright;
        label.alignment = TextAnchor.MiddleLeft;
        MonolithTimelineEditor.Place(label.gameObject, 0f, 1f, 0f, 26f);

        TMP_InputField input = BuildValueInput(page, root);
        MonolithValueRow row = new(field, root, input);
        row.BindCommit(commit);
        return row;
    }

    public void Show(MonolithTimelineView view)
    {
        if (_root.IsNullOrDestroyed())
        {
            return;
        }

        _root.SetActive(view.Shows(_field));
        if (_input.IsNullOrDestroyed() || _input.isFocused)
        {
            return;
        }

        _input.SetTextWithoutNotify(view.ValueOf(_field).ToString(CultureInfo.InvariantCulture));
    }

    private static TMP_InputField BuildValueInput(HudFormPage page, GameObject row)
    {
        TMP_InputField input = page.BuildInput(row);
        if (input.IsNullOrDestroyed())
        {
            Main.logger_instance?.Warning("MonolithValueRow: input template missing");
            return null;
        }

        MonolithTimelineEditor.Place(input.gameObject, 0f, 1f, 32f, 32f);
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = MonolithValueRules.CharacterLimit;
        if (!input.textComponent.IsNullOrDestroyed())
        {
            input.textComponent.fontSize = HudTheme.ValueFontSize;
        }

        return input;
    }

    private void BindCommit(Action<MonolithField, string> commit)
    {
        if (_input.IsNullOrDestroyed())
        {
            return;
        }

        MonolithField field = _field;
        _input.onEndEdit = new TMP_InputField.SubmitEvent();
        _input.onEndEdit.AddListener((UnityAction<string>)(text => commit(field, text)));
    }
}
