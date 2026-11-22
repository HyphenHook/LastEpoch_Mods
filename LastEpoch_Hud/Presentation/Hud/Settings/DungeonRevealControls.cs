using HarmonyLib;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Settings;

internal static class DungeonRevealControls
{
    const string RevealName = "Toggle_DungeonControls_Reveal";
    const string KeylessName = "Toggle_DungeonControls_Keyless";

    public static void Bind(GameObject content, GameObject unused)
    {
        var viewport = Prefab.ViewportContent(content, "Center", "Scenes_Dungeons_Content");
        if (viewport.IsNullOrDestroyed())
            viewport = Prefab.ViewportContent(content, "Center", "Scenes_Minimap_Content");
        if (viewport.IsNullOrDestroyed())
        {
            Main.logger_instance?.Warning(
                "Dungeon controls: no dungeon/minimap viewport in this HUD bundle"
            );
            return;
        }
        if (Prefab.Child(viewport, "DungeonControls") != null)
            return;
        var sample = viewport.GetComponentInChildren<Text>(true);
        if (sample.IsNullOrDestroyed())
            return;
        var style = viewport.GetComponentInChildren<Toggle>(true);
        var section = Node(viewport, "DungeonControls", 0, 0, 1, 1);
        var rect = section.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.sizeDelta = new Vector2(0, 88);
        var layout = section.AddComponent<LayoutElement>();
        layout.minHeight = 88;
        layout.preferredHeight = 88;
        layout.flexibleHeight = 0;
        Checkbox(
            section,
            RevealName,
            "Reveal Dungeon Objectives",
            sample.font,
            style,
            .56f,
            .96f,
            ModSettings.DungeonReveal.Enabled.Value
        );
        bool keyless =
            !Save_Manager.instance.IsNullOrDestroyed()
            && Save_Manager.instance.initialized
            && Save_Manager.instance.data.Scenes.Dungeons.Enable_EnterWithoutKey;
        Checkbox(
            section,
            KeylessName,
            "Enter Without Key",
            sample.font,
            style,
            .25f,
            .55f,
            keyless
        );
        var line = Node(section, "Separator", .01f, .22f, .99f, .22f);
        line.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 1);
        var image = line.AddComponent<Image>();
        image.color = HudTheme.Accent;
        image.raycastTarget = false;
        Label(
            section,
            "Description",
            sample.font,
            "Revealed objectives remain visible for this floor.",
            .03f,
            .01f,
            .97f,
            .21f
        );
        // Keep one visible entry control when the original bundle includes it.
        var old = Prefab.Child(viewport, "EnterWithoutKey");
        if (!old.IsNullOrDestroyed())
            old.SetActive(false);
        MelonLoader.MelonCoroutines.Start(PositionWhenVisible(viewport, section));
        Main.logger_instance?.Msg(
            "Dungeon controls: created in " + viewport.transform.parent.parent.name
        );
    }

    static System.Collections.IEnumerator PositionWhenVisible(
        GameObject viewport,
        GameObject section
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
        if (viewport.GetComponent<VerticalLayoutGroup>().IsNullOrDestroyed())
        {
            var parent = viewport.GetComponent<RectTransform>();
            var rect = section.GetComponent<RectTransform>();
            float bottom = parent.rect.yMax;
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
            rect.anchoredPosition = new Vector2(0, bottom - parent.rect.yMax - 8);
            parent.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(parent.rect.height, -rect.anchoredPosition.y + 88)
            );
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(viewport.GetComponent<RectTransform>());
    }

    static void Checkbox(
        GameObject section,
        string name,
        string label,
        Font font,
        Toggle style,
        float bottom,
        float top,
        bool value
    )
    {
        var row = Node(section, name, 0, bottom, 1, top);
        var toggle = row.AddComponent<Toggle>();
        var box = Node(row, "Box", 0, .5f, 0, .5f).AddComponent<Image>();
        var rect = box.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0, .5f);
        rect.sizeDelta = new Vector2(18, 18);
        box.color = HudTheme.ControlBox;
        var check = Node(box.gameObject, "Check", .2f, .2f, .8f, .8f).AddComponent<Image>();
        check.color = HudTheme.ControlCheck;
        if (!style.IsNullOrDestroyed())
        {
            var sourceBox = style.targetGraphic.IsNullOrDestroyed()
                ? null
                : style.targetGraphic.GetComponent<Image>();
            var sourceCheck = style.graphic.IsNullOrDestroyed()
                ? null
                : style.graphic.GetComponent<Image>();
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
            toggle.colors = style.colors;
            toggle.transition = style.transition;
        }
        toggle.targetGraphic = box;
        toggle.graphic = check;
        Label(row, "Label", font, label, .1f, 0, 1, 1);
        toggle.SetIsOnWithoutNotify(value);
        var separator = Node(row, "Separator", 0, 0, 1, 0);
        separator.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 1);
        var line = separator.AddComponent<Image>();
        line.color = HudTheme.Accent;
        line.raycastTarget = false;
    }

    static void Save(Toggle toggle)
    {
        if (toggle.IsNullOrDestroyed())
            return;
        if (toggle.name == RevealName)
            ModSettings.DungeonReveal.Enabled.Set(toggle.isOn);
        else if (
            toggle.name == KeylessName
            && !Save_Manager.instance.IsNullOrDestroyed()
            && Save_Manager.instance.initialized
        )
        {
            Save_Manager.instance.data.Scenes.Dungeons.Enable_EnterWithoutKey = toggle.isOn;
            Save_Manager.instance.Save();
        }
    }

    [HarmonyPatch(typeof(Toggle), "OnPointerClick")]
    static class Click
    {
        [HarmonyPostfix]
        static void Postfix(Toggle __instance) => Save(__instance);
    }

    [HarmonyPatch(typeof(Toggle), "OnSubmit")]
    static class Submit
    {
        [HarmonyPostfix]
        static void Postfix(Toggle __instance) => Save(__instance);
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
        go.layer = parent.layer;
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent.transform, false);
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
}
