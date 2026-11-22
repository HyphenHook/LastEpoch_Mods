using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FD = LastEpoch_Hud.Scripts.ModUI.ForceDrop.ForceDropModel;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

public static partial class ForceDropBuilder
{

    static Number NumericGrid(
        GameObject parent,
        string name,
        int col,
        int row,
        int min,
        int max,
        int value,
        bool percent,
        int rowCount = 2,
        int columns = 2
    )
    {
        float width = .96f / columns,
            height = .92f / rowCount;
        var cell = Panel(
            parent,
            name,
            .02f + col * width,
            .04f + row * height,
            .02f + (col + 1) * width - .01f,
            .04f + (row + 1) * height - .01f
        );
        Label(cell, name + (percent ? " %" : ""), .025f, .60f, .97f, .97f, 12);
        var n = NumericCompact(cell, .03f, .08f, .55f, .57f, min, max, value);
        n.group = cell;
        n.mode = Button(
            cell,
            "Fixed",
            .60f,
            .08f,
            .97f,
            .57f,
            () =>
            {
                n.random = !n.random;
                RefreshModes();
            }
        );
        return n;
    }

    static GameObject Panel(
        GameObject parent,
        string name,
        float x0,
        float y0,
        float x1,
        float y1,
        bool bordered = true
    )
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        Rect(go, x0, y0, x1, y1);
        go.AddComponent<Image>().color = dark;
        if (bordered)
            HudStyler.AddPrimaryBorder(go, HudTheme.BorderWidth);
        return go;
    }

    static void Divider(GameObject parent, float x0, float y, float x1)
    {
        var divider = new GameObject("TitleDivider");
        divider.AddComponent<RectTransform>();
        divider.transform.SetParent(parent.transform, false);
        Rect(divider, x0, y, x1, y + .002f);
        var image = divider.AddComponent<Image>();
        image.color = HudTheme.CardDivider;
        image.raycastTarget = false;
    }

    static void Rect(GameObject go, float x0, float y0, float x1, float y1)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(x0, y0);
        rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static Text Label(
        GameObject parent,
        string text,
        float x0,
        float y0,
        float x1,
        float y1,
        int size = 15
    )
    {
        var go = new GameObject("Label");
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        Rect(go, x0, y0, x1, y1);
        var label = go.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.color = foreground;
        LocaleRegistry.Apply(label, text);
        label.raycastTarget = false;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    static Button Button(
        GameObject parent,
        string text,
        float x0,
        float y0,
        float x1,
        float y1,
        Action action
    )
    {
        var go = Panel(parent, "Button", x0, y0, x1, y1);
        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.targetGraphic.color = HudTheme.SelectableTint;
        button.colors = HudTheme.ActionButtonColors(HudTheme.Surface, HudTheme.Surface);
        var label = Label(go, text, .025f, .04f, .975f, .96f);
        label.alignment = TextAnchor.MiddleLeft;
        var accent = new GameObject("LEHUD_SelectedAccent");
        accent.AddComponent<RectTransform>();
        accent.transform.SetParent(go.transform, false);
        Rect(accent, 0f, 0f, .012f, 1f);
        var accentImage = accent.AddComponent<Image>();
        accentImage.color = HudTheme.Accent;
        accentImage.raycastTarget = false;
        accent.SetActive(false);
        clicks[button.GetInstanceID()] = action;
        return button;
    }

    static void StyleSelection(Button button, bool selected)
    {
        if (button.IsNullOrDestroyed())
            return;
        var image = button.GetComponent<Image>();
        if (!image.IsNullOrDestroyed())
            image.color = HudTheme.SelectableTint;
        button.colors = HudTheme.ActionButtonColors(
            selected ? HudTheme.Selection : HudTheme.Surface,
            selected ? HudTheme.Selection : HudTheme.Surface
        );
        var accent = button.transform.Find("LEHUD_SelectedAccent");
        if (!accent.IsNullOrDestroyed())
            accent.gameObject.SetActive(selected);
    }

    static void Caption(Button button, string text)
    {
        LocaleRegistry.Apply(button.GetComponentInChildren<Text>(true), text);
    }

    static string L(string text) => LocaleRegistry.Translate(text);

    static string Selected(Dropdown catalog, string fallback) =>
        catalog.value > 0 && catalog.value < catalog.options.Count
            ? catalog.options[catalog.value].text
            : fallback;

    static TMP_InputField Input(
        GameObject parent,
        string name,
        float x0,
        float y0,
        float x1,
        float y1,
        string value,
        bool numeric
    )
    {
        var go = UnityEngine.Object.Instantiate(template.gameObject, parent.transform);
        go.name = name;
        Rect(go, x0, y0, x1, y1);
        var layout = go.GetComponent<LayoutElement>();
        if (!layout.IsNullOrDestroyed())
            layout.ignoreLayout = true;
        var input = go.GetComponent<TMP_InputField>();
        input.onValueChanged.RemoveAllListeners();
        input.onEndEdit.RemoveAllListeners();
        input.enabled = true;
        input.readOnly = false;
        input.interactable = true;
        input.characterLimit = numeric ? 8 : 100;
        input.contentType = numeric
            ? TMP_InputField.ContentType.IntegerNumber
            : TMP_InputField.ContentType.Standard;
        input.SetTextWithoutNotify(value);
        input.targetGraphic.raycastTarget = true;
        var background = go.GetComponent<Image>();
        if (!background.IsNullOrDestroyed())
        {
            background.sprite = null;
            background.color = HudTheme.InputBackground;
        }
        HudStyler.AddPrimaryBorder(go, HudTheme.ControlBorderWidth);
        if (!input.placeholder.IsNullOrDestroyed())
            input.placeholder.gameObject.SetActive(false);
        input.textComponent.color = foreground;
        input.textComponent.fontSize = 15;
        input.textComponent.enableAutoSizing = true;
        input.textComponent.fontSizeMin = 10;
        input.textComponent.fontSizeMax = 15;
        input.textComponent.raycastTarget = false;
        input.textComponent.margin = Vector4.zero;
        if (!input.textViewport.IsNullOrDestroyed())
            Rect(input.textViewport.gameObject, .04f, .05f, .96f, .95f);
        Rect(input.textComponent.gameObject, 0, 0, 1, 1);
        var group = go.GetComponent<CanvasGroup>();
        if (!group.IsNullOrDestroyed())
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        go.SetActive(true);
        return input;
    }

    static Number NumericCompact(
        GameObject parent,
        float x0,
        float y0,
        float x1,
        float y1,
        int min,
        int max,
        int value
    )
    {
        var n = new Number
        {
            min = min,
            max = max,
            value = value,
            input = Input(parent, "Number", x0, y0, x1, y1, value.ToString(), true),
        };
        numbers.Add(n);
        return n;
    }

    static Number Numeric(
        GameObject parent,
        string name,
        float y,
        int min,
        int max,
        int value,
        bool percent,
        bool mode
    )
    {
        Label(parent, name + (percent ? " (%)" : ""), .03f, y - .015f, .56f, y + .055f, 14);
        var n = NumericCompact(
            parent,
            .57f,
            y - .025f,
            mode ? .77f : .96f,
            y + .055f,
            min,
            max,
            value
        );
        if (mode)
            n.mode = Button(
                parent,
                "Fixed",
                .79f,
                y - .025f,
                .97f,
                y + .055f,
                () =>
                {
                    n.random = !n.random;
                    RefreshModes();
                }
            );
        return n;
    }

    [HarmonyPatch(typeof(Button), "Press")]
    public class ButtonPress
    {
        [HarmonyPostfix]
        static void Postfix(Button __instance)
        {
            if (!__instance.interactable || !__instance.gameObject.activeInHierarchy)
                return;
            if (clicks.TryGetValue(__instance.GetInstanceID(), out var action))
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Main.logger_instance.Error("Force Drop action: " + ex.Message);
                }
        }
    }
}
