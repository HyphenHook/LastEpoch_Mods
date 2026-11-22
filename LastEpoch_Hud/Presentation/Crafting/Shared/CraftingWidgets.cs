using System;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>Native widget copying and drawing primitives shared by crafting presentations.</summary>
internal static class CraftingWidgets
{

    internal static void CopyRect(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;
    }


    /// <summary>Removes the parts of a copied forge object that would run forge logic or reset our text.</summary>
    internal static void StripGameLogic(GameObject go)
    {
        foreach (Component c in go.GetComponentsInChildren<Component>(true))
        {
            if (c == null)
            {
                continue;
            }
            string type = c.GetIl2CppType().Name;
            if (type == "LocalizeStringEvent" || type == "CraftingMaterialsToggleButton" || type == "TransformToggler"
                || type == "ContentSizeFitter" || type == "DOTweenAnimation" || type == "DOTweenVisualManager")
            {
                Object.Destroy(c);
            }
        }
        foreach (Button b in go.GetComponentsInChildren<Button>(true))
        {
            b.onClick = new Button.ButtonClickedEvent();
        }
    }


    internal static void OnClick(GameObject go, Action action)
    {
        Button b = go.GetComponent<Button>();
        if (b == null)
        {
            return;
        }
        b.onClick = new Button.ButtonClickedEvent();
        b.onClick.AddListener(action);
    }


    internal static void SetText(GameObject go, string text)
    {
        TextMeshProUGUI t = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null)
        {
            t.text = text;
        }
    }


    internal static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.AddComponent<CanvasRenderer>();
        Image img = go.AddComponent<Image>();
        go.transform.SetParent(parent, false);
        img.color = color;
        return img;
    }


    internal static Image Part(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, float rotation = 0f)
    {
        Image img = MakeImage(parent, name, color);
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        r.localRotation = Quaternion.Euler(0, 0, rotation);
        return img;
    }
    /// <summary>Fits a child to its parent's bounds with an inset.</summary>
    internal static void Stretch(RectTransform rect, float margin)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);
    }
}
