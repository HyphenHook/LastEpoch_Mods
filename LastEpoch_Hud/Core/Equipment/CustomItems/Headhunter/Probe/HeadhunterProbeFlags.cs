namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Probe;

/// <summary>Game flags that can mark a cutscene or boss intro.</summary>
public readonly record struct HeadhunterProbeFlags(
    bool Cinematic,
    bool InterruptibleCinematic,
    bool InputDisabled,
    bool Subtitles
);
