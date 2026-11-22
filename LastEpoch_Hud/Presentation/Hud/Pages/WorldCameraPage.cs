using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>World &gt; Camera HUD page. State resets on HUD re-bind (Build).</summary>
internal static class WorldCameraPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;
        var card = page.AddCard("Camera", "Camera");
        page.AddButton(card, "Set", "Set", Mods.Camera.Camera_Override.Set);
        page.AddButton(card, "Reset", "Reset", Mods.Camera.Camera_Override.ResetToDefault);
        AddToggle(
            card,
            "Enable",
            "Enable Camera Override",
            () => Save_Manager.instance.data.Scenes.Camera.Enable_Mod,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_Mod = v
        );
        AddSlider(
            card,
            "ZoomMinimum",
            "Zoom Minimum",
            SceneSliderLimits.CameraZoomMinimum,
            () => Save_Manager.instance.data.Scenes.Camera.Enable_ZoomMinimum,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_ZoomMinimum = v,
            () => Save_Manager.instance.data.Scenes.Camera.ZoomMinimum,
            v => Save_Manager.instance.data.Scenes.Camera.ZoomMinimum = v
        );
        AddSlider(
            card,
            "ZoomPerScroll",
            "Zoom Per Scroll",
            SceneSliderLimits.CameraZoomPerScroll,
            () => Save_Manager.instance.data.Scenes.Camera.Enable_ZoomPerScroll,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_ZoomPerScroll = v,
            () => Save_Manager.instance.data.Scenes.Camera.ZoomPerScroll,
            v => Save_Manager.instance.data.Scenes.Camera.ZoomPerScroll = v
        );
        AddSlider(
            card,
            "ZoomSpeed",
            "Zoom Speed",
            SceneSliderLimits.CameraZoomSpeed,
            () => Save_Manager.instance.data.Scenes.Camera.Enable_ZoomSpeed,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_ZoomSpeed = v,
            () => Save_Manager.instance.data.Scenes.Camera.ZoomSpeed,
            v => Save_Manager.instance.data.Scenes.Camera.ZoomSpeed = v
        );
        AddSlider(
            card,
            "DefaultRotation",
            "Default Rotation",
            SceneSliderLimits.CameraDefaultRotation,
            () => Save_Manager.instance.data.Scenes.Camera.Enable_DefaultRotation,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_DefaultRotation = v,
            () => Save_Manager.instance.data.Scenes.Camera.DefaultRotation,
            v => Save_Manager.instance.data.Scenes.Camera.DefaultRotation = v
        );
        AddSlider(
            card,
            "OffsetMinimum",
            "Offset Minimum",
            SceneSliderLimits.CameraOffsetMinimum,
            () => Save_Manager.instance.data.Scenes.Camera.Enable_OffsetMinimum,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_OffsetMinimum = v,
            () => Save_Manager.instance.data.Scenes.Camera.OffsetMinimum,
            v => Save_Manager.instance.data.Scenes.Camera.OffsetMinimum = v
        );
        AddSlider(
            card,
            "OffsetMaximum",
            "Offset Maximum",
            SceneSliderLimits.CameraOffsetMaximum,
            () => Save_Manager.instance.data.Scenes.Camera.Enable_OffsetMaximum,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_OffsetMaximum = v,
            () => Save_Manager.instance.data.Scenes.Camera.OffsetMaximum,
            v => Save_Manager.instance.data.Scenes.Camera.OffsetMaximum = v
        );
        AddSlider(
            card,
            "AngleMinimum",
            "Angle Minimum",
            SceneSliderLimits.CameraAngleMinimum,
            () => Save_Manager.instance.data.Scenes.Camera.Enable_AngleMinimum,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_AngleMinimum = v,
            () => Save_Manager.instance.data.Scenes.Camera.AngleMinimum,
            v => Save_Manager.instance.data.Scenes.Camera.AngleMinimum = v
        );
        AddSlider(
            card,
            "AngleMaximum",
            "Angle Maximum",
            SceneSliderLimits.CameraAngleMaximum,
            () => Save_Manager.instance.data.Scenes.Camera.Enable_AngleMaximum,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_AngleMaximum = v,
            () => Save_Manager.instance.data.Scenes.Camera.AngleMaximum,
            v => Save_Manager.instance.data.Scenes.Camera.AngleMaximum = v
        );
        AddToggle(
            card,
            "LoadOnStart",
            "Load On Start",
            () => Save_Manager.instance.data.Scenes.Camera.Enable_LoadOnStart,
            v => Save_Manager.instance.data.Scenes.Camera.Enable_LoadOnStart = v
        );
    }

    public static void Show() => page?.Show();

    public static void Hide() => page?.Hide();

    public static void Refresh() => page?.RefreshValues();

    private static void AddToggle(
        HudFormPage.Card card,
        string id,
        string label,
        Func<bool> read,
        Action<bool> write
    ) =>
        page.AddToggle(
            card,
            id,
            label,
            () => HasSave() && read(),
            v =>
            {
                if (HasSave())
                    write(v);
            }
        );

    private static void AddSlider(
        HudFormPage.Card card,
        string id,
        string label,
        SliderRange range,
        Func<bool> readEnabled,
        Action<bool> writeEnabled,
        Func<float> read,
        Action<float> write
    )
    {
        page.AddToggleSlider(
            card,
            id,
            label,
            string.Empty,
            range.Min,
            range.Max,
            range.WholeNumbers,
            () => HasSave() && readEnabled(),
            v =>
            {
                if (HasSave())
                    writeEnabled(v);
            },
            () => HasSave() ? read() : range.Min,
            v =>
            {
                if (HasSave())
                    write(v);
            }
        );
    }

    private static bool HasSave() =>
        !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;
}
