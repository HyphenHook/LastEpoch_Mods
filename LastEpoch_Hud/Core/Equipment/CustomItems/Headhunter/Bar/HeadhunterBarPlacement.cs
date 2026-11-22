namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Bar bottom-center in screen pixels, its scale and the screen size it was computed for.</summary>
public readonly record struct HeadhunterBarPlacement(
    float X,
    float Y,
    float Scale,
    float ScreenW = 0f,
    float ScreenH = 0f
);
