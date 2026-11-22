namespace LastEpoch_Hud.Scripts.Core.ModUI.Sliders;

/// <summary>Slider limits of the World menu sliders, copied from the legacy prefab.</summary>
public static class SceneSliderLimits
{
    // Source: Slider_Scenes_Camera_ZoomMinimum
    public static readonly SliderRange CameraZoomMinimum = new(-255f, 0f, false);

    // Source: Slider_Scenes_Camera_ZoomPerScroll
    public static readonly SliderRange CameraZoomPerScroll = new(0f, 20f, false);

    // Source: Slider_Scenes_Camera_ZoomSpeed
    public static readonly SliderRange CameraZoomSpeed = new(0f, 20f, false);

    // Source: Slider_Scenes_Camera_DefaultRotation
    public static readonly SliderRange CameraDefaultRotation = new(-255f, 255f, false);

    // Source: Slider_Scenes_Camera_OffsetMinimum
    public static readonly SliderRange CameraOffsetMinimum = new(-255f, 255f, false);

    // Source: Slider_Scenes_Camera_OffsetMaximum
    public static readonly SliderRange CameraOffsetMaximum = new(-255f, 255f, false);

    // Source: Slider_Scenes_Camera_AngleMinimum
    public static readonly SliderRange CameraAngleMinimum = new(-255f, 255f, false);

    // Source: Slider_Scenes_Camera_AngleMaximum
    public static readonly SliderRange CameraAngleMaximum = new(-255f, 255f, false);

    // Monolith*: Min/Max from the prefab; whole numbers on purpose (prefab says false), as the page always did.
    // Source: Slider_Scenes_Monoliths_MaxStability
    public static readonly SliderRange MonolithMaxStability = new(0f, 255f, true);

    // Source: Slider_Scenes_Monoliths_MobsDensity
    public static readonly SliderRange MonolithMobsDensity = new(0f, 255f, true);

    // Source: Slider_Scenes_Monoliths_MobsDefeatOnStart
    public static readonly SliderRange MonolithMobsDefeat = new(0f, 255f, true);

    // Source: Slider_Scenes_Monoliths_BlessingSlots
    public static readonly SliderRange MonolithBlessingSlots = new(3f, 5f, true);
}
