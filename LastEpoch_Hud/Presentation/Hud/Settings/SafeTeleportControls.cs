using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Settings;

internal static class SafeTeleportControls
{
    public static void Bind(GameObject content, GameObject viewport)
    {
        if (viewport.IsNullOrDestroyed() || Prefab.Child(viewport, "SafeTeleport") != null)
            return;
        var minimap = Prefab.ViewportContent(content, "Center", "Scenes_Minimap_Content");
        var styleSource = minimap.IsNullOrDestroyed() ? viewport : minimap;
        var sample = styleSource.GetComponentInChildren<Text>(true);
        if (sample.IsNullOrDestroyed())
            return;
        Font font = sample.font;
        var styleToggle = styleSource.GetComponentInChildren<Toggle>(true);
        var section = Node(viewport, "SafeTeleport", 0, 0, 1, 1);
        var rect = section.GetComponent<RectTransform>();
        var layout = section.AddComponent<LayoutElement>();
        layout.minHeight = 88;
        layout.preferredHeight = 88;
        layout.flexibleHeight = 0;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(0, 88);
        Label(section, "Title", font, "Safe Teleport", .03f, .70f, .40f, 1f);
        var toggleGo = Node(section, "Enabled", .03f, .33f, .40f, .67f);
        var toggle = toggleGo.AddComponent<Toggle>();
        var box = Node(toggleGo, "Box", 0, .2f, .045f, .8f).AddComponent<Image>();
        box.color = HudTheme.ControlBox;
        var check = Node(box.gameObject, "Check", .2f, .2f, .8f, .8f).AddComponent<Image>();
        check.color = HudTheme.ControlCheck;
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0, .5f);
        boxRect.anchorMax = new Vector2(0, .5f);
        boxRect.pivot = new Vector2(0, .5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = new Vector2(18, 18);
        if (!styleToggle.IsNullOrDestroyed())
        {
            var sourceBox = styleToggle.targetGraphic.IsNullOrDestroyed()
                ? null
                : styleToggle.targetGraphic.GetComponent<Image>();
            var sourceCheck = styleToggle.graphic.IsNullOrDestroyed()
                ? null
                : styleToggle.graphic.GetComponent<Image>();
            if (!sourceBox.IsNullOrDestroyed())
            {
                box.sprite = sourceBox.sprite;
                box.type = sourceBox.type;
                box.color = sourceBox.color;
            }
            if (!sourceCheck.IsNullOrDestroyed())
            {
                check.sprite = sourceCheck.sprite;
                check.type = sourceCheck.type;
                check.color = sourceCheck.color;
            }
            toggle.colors = styleToggle.colors;
            toggle.transition = styleToggle.transition;
        }
        toggle.targetGraphic = box;
        toggle.graphic = check;
        Label(toggleGo, "Label", font, "Enable Safe Teleport", .10f, 0, 1, 1);
        var key = Node(section, "Key", .41f, .33f, .97f, .67f);
        Label(key, "Label", font, "Teleport Key", 0, 1.09f, 1, 1.97f);
        Button(key, "Capture", font, 0, .65f);
        Button(key, "Reset", font, .68f, 1f);
        var separator = Node(section, "Separator", .01f, .29f, .99f, .29f);
        separator.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 1);
        var line = separator.AddComponent<Image>();
        line.color = HudTheme.Accent;
        line.raycastTarget = false;
        Label(
            section,
            "Description",
            font,
            "End of Time waypoint required. Bind a key or modifier + key.",
            .03f,
            .01f,
            .97f,
            .25f
        );
        ModSettings.SafeTeleport.Group.ResolveAndBind(content);
        MelonLoader.MelonCoroutines.Start(PositionWhenVisible(viewport, section, styleToggle));
    }

    static System.Collections.IEnumerator PositionWhenVisible(
        GameObject viewport,
        GameObject section,
        Toggle styleToggle
    )
    {
        while (!viewport.IsNullOrDestroyed() && !viewport.activeInHierarchy)
            yield return null;
        if (viewport.IsNullOrDestroyed() || section.IsNullOrDestroyed())
            yield break;
        yield return null;
        if (viewport.IsNullOrDestroyed() || section.IsNullOrDestroyed())
            yield break;
        Canvas.ForceUpdateCanvases();
        var rect = section.GetComponent<RectTransform>();
        if (viewport.GetComponent<VerticalLayoutGroup>().IsNullOrDestroyed())
        {
            // Older bundles may use explicit positions instead of a layout group.
            var parentRect = viewport.GetComponent<RectTransform>();
            float bottom = parentRect.rect.yMax;
            var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(
                4
            );
            for (int i = 0; i < viewport.transform.childCount; i++)
            {
                var child = viewport.transform.GetChild(i).gameObject;
                if (child == section || !child.activeSelf)
                    continue;
                var childRect = child.GetComponent<RectTransform>();
                if (childRect.IsNullOrDestroyed())
                    continue;
                childRect.GetWorldCorners(corners);
                bottom = Mathf.Min(bottom, viewport.transform.InverseTransformPoint(corners[0]).y);
            }
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, bottom - parentRect.rect.yMax - 8);
            rect.sizeDelta = new Vector2(0, 88);
            parentRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(parentRect.rect.height, -rect.anchoredPosition.y + 88)
            );
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(viewport.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();
        // Match the existing checkbox's horizontal position and size after layout.
        if (!styleToggle.IsNullOrDestroyed() && !styleToggle.targetGraphic.IsNullOrDestroyed())
        {
            var sourceRect = styleToggle.targetGraphic.GetComponent<RectTransform>();
            var enabled = Prefab.Child(section, "Enabled");
            var box = Prefab.Child(enabled, "Box").GetComponent<RectTransform>();
            var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(
                4
            );
            sourceRect.GetWorldCorners(corners);
            float left = enabled.transform.InverseTransformPoint(corners[0]).x;
            box.anchoredPosition = new Vector2(
                left - enabled.GetComponent<RectTransform>().rect.xMin,
                0
            );
            box.sizeDelta = sourceRect.rect.size;
        }
        Main.logger_instance?.Msg(
            "Safe Teleport layout: active="
                + section.activeInHierarchy
                + ", rect="
                + rect.rect
                + ", position="
                + rect.anchoredPosition
        );
    }

    static GameObject Node(
        GameObject parent,
        string name,
        float left,
        float bottom,
        float right,
        float top
    )
    {
        var go = new GameObject(name);
        // Match the HUD layer so the UI camera renders and raycasts these controls.
        go.layer = parent.layer;
        var r = go.AddComponent<RectTransform>();
        r.SetParent(parent.transform, false);
        r.anchorMin = new Vector2(left, bottom);
        r.anchorMax = new Vector2(right, top);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        return go;
    }

    static Text Label(
        GameObject parent,
        string name,
        Font font,
        string label,
        float left,
        float bottom,
        float right,
        float top
    )
    {
        var text = Node(parent, name, left, bottom, right, top).AddComponent<Text>();
        text.font = font;
        text.fontSize = 12;
        text.color = HudTheme.ControlCheck;
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        Prefab.ApplyLabel(text, label);
        return text;
    }

    static void Button(GameObject parent, string name, Font font, float left, float right)
    {
        var go = Node(parent, name, left, .05f, right, .95f);
        var image = go.AddComponent<Image>();
        image.color = HudTheme.SurfaceRaised;
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        Label(go, "Value", font, name == "Reset" ? "Clear" : "Unbound", .05f, 0, .95f, 1);
    }
}
