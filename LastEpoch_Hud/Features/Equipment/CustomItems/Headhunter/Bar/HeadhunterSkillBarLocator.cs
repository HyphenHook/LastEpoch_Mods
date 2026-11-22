using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

/// <summary>Reads the screen rect of the visible skill icons.</summary>
internal static class HeadhunterSkillBarLocator
{
    private const int FirstSlot = 1;
    private const int LastSlot = 5;
    private static readonly Il2CppStructArray<Vector3> _corners = new(4);

    public static SkillBarBounds Read(out Canvas rootCanvas)
    {
        rootCanvas = null;
        SkillBarBounds bounds = SkillBarBounds.Empty;
        DList<AbilityBarIcon> icons = AbilityBarIcon.all;
        if (icons == null)
        {
            return SkillBarBounds.ScreenFallback(Screen.width, Screen.height);
        }

        for (int i = 0; i < icons.Count; i++)
        {
            bounds = IncludeIcon(bounds, icons[i], ref rootCanvas);
        }

        if (bounds.IsEmpty)
        {
            return SkillBarBounds.ScreenFallback(Screen.width, Screen.height);
        }

        return bounds;
    }

    /// <summary>Camera for screen-space conversion; null for overlay canvases.</summary>
    private static UnityEngine.Camera ScreenCamera(Canvas rootCanvas)
    {
        if (
            rootCanvas.IsNullOrDestroyed()
            || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
        )
        {
            return null;
        }

        return rootCanvas.worldCamera;
    }

    private static SkillBarBounds IncludeIcon(
        SkillBarBounds bounds,
        AbilityBarIcon barIcon,
        ref Canvas rootCanvas
    )
    {
        if (!IsVisible(barIcon))
        {
            return bounds;
        }

        Canvas canvas = barIcon.icon.canvas;
        if (rootCanvas.IsNullOrDestroyed() && !canvas.IsNullOrDestroyed())
        {
            rootCanvas = canvas.rootCanvas;
        }

        UnityEngine.Camera cam = ScreenCamera(rootCanvas);
        barIcon.icon.rectTransform.GetWorldCorners(_corners);
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int c = 0; c < 4; c++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(cam, _corners[c]);
            minX = Mathf.Min(minX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxX = Mathf.Max(maxX, point.x);
            maxY = Mathf.Max(maxY, point.y);
        }

        return bounds.IncludeRect(minX, minY, maxX, maxY, barIcon.transform.localScale.y);
    }

    private static bool IsVisible(AbilityBarIcon barIcon)
    {
        if (barIcon.IsNullOrDestroyed() || barIcon.icon.IsNullOrDestroyed())
        {
            return false;
        }

        if (barIcon.abilityNumber < FirstSlot || barIcon.abilityNumber > LastSlot)
        {
            return false;
        }

        return barIcon.gameObject.activeInHierarchy;
    }
}
