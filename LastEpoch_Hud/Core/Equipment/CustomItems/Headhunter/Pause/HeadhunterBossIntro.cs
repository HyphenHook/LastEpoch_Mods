namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause;

/// <summary>One tracked boss intro.</summary>
public readonly record struct HeadhunterBossIntro(
    long Id,
    string Actor,
    float DurationSeconds,
    double StartedAt
);
