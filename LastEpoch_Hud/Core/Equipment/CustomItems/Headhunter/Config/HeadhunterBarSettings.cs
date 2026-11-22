namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

/// <summary>Bar placement from the config: offsets in default icon rows, icon size factor, icons per row.</summary>
public readonly record struct HeadhunterBarSettings(
    float OffsetX,
    float OffsetY,
    float IconSize,
    int PerRow
);
