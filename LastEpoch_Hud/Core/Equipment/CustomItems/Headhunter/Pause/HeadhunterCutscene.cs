namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>One held cutscene.</summary>
public readonly record struct HeadhunterCutscene(
    string Id,
    double DurationSeconds,
    double StartedAt
);
