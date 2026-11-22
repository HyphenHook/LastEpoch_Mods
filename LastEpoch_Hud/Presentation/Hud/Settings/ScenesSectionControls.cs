using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Settings;

internal static class ScenesSectionControls
{
    public static void Bind(GameObject content)
    {
        var center = Prefab.Child(content, "Center");
        var dungeonPanel = Prefab.Child(center, "Scenes_Dungeons_Content");
        var minimapPanel = Prefab.Child(center, "Scenes_Minimap_Content");
        var dungeons = Prefab.ViewportContent(content, "Center", "Scenes_Dungeons_Content");
        var minimap = Prefab.ViewportContent(content, "Center", "Scenes_Minimap_Content");
        if (
            center.IsNullOrDestroyed()
            || dungeons.IsNullOrDestroyed()
            || minimap.IsNullOrDestroyed()
        )
            return;
        var dungeonTitle = Prefab.Child(center, "Title");
        var minimapTitle = Prefab.Child(center, "Title (1)");
        if (dungeonTitle.IsNullOrDestroyed() || minimapTitle.IsNullOrDestroyed())
            return;
        var misc = Prefab.Child(center, "Scenes_Misc_Content");
        if (misc.IsNullOrDestroyed())
        {
            misc = new GameObject("Scenes_Misc_Content");
            misc.layer = center.layer;
            misc.AddComponent<RectTransform>().SetParent(center.transform, false);
            var source = minimapPanel.GetComponent<Image>();
            if (!source.IsNullOrDestroyed())
            {
                var image = misc.AddComponent<Image>();
                image.sprite = source.sprite;
                image.type = source.type;
                image.color = source.color;
                image.raycastTarget = false;
            }
        }
        var miscViewport = Prefab.Child(misc, "Viewport");
        if (miscViewport.IsNullOrDestroyed())
            miscViewport = Node(misc, "Viewport");
        Stretch(miscViewport);
        var miscContent = Prefab.Child(miscViewport, "Content");
        if (miscContent.IsNullOrDestroyed())
            miscContent = Node(miscViewport, "Content");
        Place(miscContent, 0, 88);
        var miscTitle = Prefab.Child(center, "MiscTitle");
        if (miscTitle.IsNullOrDestroyed())
        {
            miscTitle = Object.Instantiate(dungeonTitle, center.transform, false);
            miscTitle.name = "MiscTitle";
        }
        Title(dungeonTitle, "Dungeons");
        Title(miscTitle, "Misc");
        Title(minimapTitle, "Minimap");
        var camera = Prefab.Child(content, "Camera");
        var cameraTitle = camera.IsNullOrDestroyed() ? null : Prefab.Child(camera, "Title");
        var headerStyle = cameraTitle.IsNullOrDestroyed()
            ? null
            : cameraTitle.GetComponentInChildren<Text>(true);
        if (!headerStyle.IsNullOrDestroyed())
            foreach (var title in new[] { dungeonTitle, miscTitle, minimapTitle })
            foreach (var text in title.GetComponentsInChildren<Text>(true))
            {
                text.font = headerStyle.font;
                text.fontSize = Mathf.Clamp(headerStyle.fontSize, 20, 24);
                text.fontStyle = headerStyle.fontStyle;
                text.color = headerStyle.color;
            }
        // Keep the native full-height frame, with compact sections at the top.
        const float header = 32;
        const float dungeonHeight = 108;
        const float miscHeight = 104;
        const float minimapHeight = 52;
        const float gap = 8;
        var centerRect = center.GetComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(centerRect.anchorMin.x, .010475103f);
        centerRect.pivot = new Vector2(.5f, .5f);
        centerRect.anchoredPosition = Vector2.zero;
        centerRect.sizeDelta = Vector2.zero;
        var background = Prefab.Child(center, "SectionBackground");
        if (background.IsNullOrDestroyed())
        {
            background = Node(center, "SectionBackground");
            var image = background.AddComponent<Image>();
            var source = minimapPanel.GetComponent<Image>();
            image.color = source.IsNullOrDestroyed() ? HudTheme.SurfaceRaised : source.color;
            image.raycastTarget = false;
        }
        var backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(.007f, .004f);
        backgroundRect.anchorMax = new Vector2(.993f, .996f);
        backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        background.transform.SetAsFirstSibling();
        float top = 4;
        Place(dungeonTitle, top, header);
        Place(dungeonPanel, top += header, dungeonHeight);
        Place(miscTitle, top += dungeonHeight + gap, header);
        Place(misc, top += header, miscHeight);
        Place(minimapTitle, top += miscHeight + gap, header);
        Place(minimapPanel, top + header, minimapHeight);
        FitScrollPanel(dungeonPanel);
        FitScrollPanel(minimapPanel);
        DungeonRevealControls.Bind(content, dungeons);
        // Existing key paths now resolve through the Misc viewport. Values/save keys stay the same.
        var safe = Prefab.Child(minimap, "SafeTeleport");
        if (!safe.IsNullOrDestroyed())
        {
            safe.transform.SetParent(miscContent.transform, false);
            Place(safe, 0, miscHeight);
            ModSettings.SafeTeleport.Group.ResolveAndBind(content);
        }
        else
            SafeTeleportControls.Bind(content, miscContent);
        MelonLoader.MelonCoroutines.Start(
            MatchNativeRows(center, dungeons, minimap, miscContent, dungeonTitle)
        );
    }

    static System.Collections.IEnumerator MatchNativeRows(
        GameObject center,
        GameObject dungeons,
        GameObject minimap,
        GameObject misc,
        GameObject dungeonTitle
    )
    {
        while (!center.IsNullOrDestroyed() && !center.activeInHierarchy)
            yield return null;
        yield return null;
        yield return null;
        if (
            center.IsNullOrDestroyed()
            || dungeons.IsNullOrDestroyed()
            || minimap.IsNullOrDestroyed()
        )
            yield break;
        Canvas.ForceUpdateCanvases();
        var sample = minimap.GetComponentInChildren<Toggle>(true);
        if (sample.IsNullOrDestroyed() || sample.targetGraphic.IsNullOrDestroyed())
            yield break;
        var sourceText = sample.GetComponentInChildren<Text>(true);
        if (sourceText.IsNullOrDestroyed())
            yield break;
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        var sourceBox = sample.targetGraphic.GetComponent<RectTransform>();
        sourceBox.GetWorldCorners(corners);
        foreach (var toggle in dungeons.GetComponentsInChildren<Toggle>(true))
        {
            if (!toggle.gameObject.activeInHierarchy || toggle.targetGraphic.IsNullOrDestroyed())
                continue;
            var row = toggle.GetComponent<RectTransform>();
            var box = toggle.targetGraphic.GetComponent<RectTransform>();
            box.anchoredPosition = new Vector2(
                row.InverseTransformPoint(corners[0]).x - row.rect.xMin,
                0
            );
            box.sizeDelta = sourceBox.rect.size;
            var label = Prefab.Child(toggle.gameObject, "Label");
            if (!label.IsNullOrDestroyed())
            {
                var rect = label.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(box.anchoredPosition.x + box.rect.width + 3, 0);
                rect.offsetMax = Vector2.zero;
            }
        }
        foreach (var text in dungeons.GetComponentsInChildren<Text>(true))
            MatchText(text, sourceText);
        foreach (var text in misc.GetComponentsInChildren<Text>(true))
            MatchText(text, sourceText);
        foreach (var text in dungeonTitle.GetComponentsInChildren<Text>(true))
            text.alignment = TextAnchor.MiddleCenter;
    }

    static void MatchText(Text text, Text source)
    {
        text.font = source.font;
        text.fontSize = source.fontSize;
        text.fontStyle = source.fontStyle;
        text.color = source.color;
        text.alignment = TextAnchor.MiddleLeft;
    }

    static GameObject Node(GameObject parent, string name)
    {
        var node = new GameObject(name);
        node.layer = parent.layer;
        node.AddComponent<RectTransform>().SetParent(parent.transform, false);
        return node;
    }

    static void Stretch(GameObject node)
    {
        var rect = node.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void FitScrollPanel(GameObject panel)
    {
        var scroll = panel.GetComponent<ScrollRect>();
        if (scroll.IsNullOrDestroyed())
            return;
        scroll.horizontal = false;
        scroll.vertical = false;
        if (!scroll.horizontalScrollbar.IsNullOrDestroyed())
            scroll.horizontalScrollbar.gameObject.SetActive(false);
        if (!scroll.verticalScrollbar.IsNullOrDestroyed())
            scroll.verticalScrollbar.gameObject.SetActive(false);
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbar = null;
        if (!scroll.viewport.IsNullOrDestroyed())
            Stretch(scroll.viewport.gameObject);
        if (!scroll.content.IsNullOrDestroyed())
        {
            var rect = scroll.content;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, rect.sizeDelta.y);
        }
    }

    static void Place(GameObject node, float top, float height)
    {
        var rect = node.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.007f, 1);
        rect.anchorMax = new Vector2(.993f, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(0, -top);
        rect.sizeDelta = new Vector2(0, height);
    }

    static void Title(GameObject node, string caption)
    {
        foreach (var label in node.GetComponentsInChildren<Text>(true))
        {
            label.fontSize = 24;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 20;
            label.resizeTextMaxSize = 24;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 0);
            rect.offsetMax = new Vector2(-8, 0);
            LocaleRegistry.Apply(label, caption);
        }
        foreach (var label in node.GetComponentsInChildren<Il2CppTMPro.TMP_Text>(true))
        {
            label.fontSize = 24;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 24;
            LocaleRegistry.Apply(label, caption);
        }
    }
}
