namespace LastEpoch_Hud.Scripts.Core.Monoliths;

/// <summary>Plain copy of one saved monolith run.</summary>
public readonly record struct MonolithRunSnapshot(
    int TimelineId,
    int Difficulty,
    int Stability,
    bool HasEchoWeb,
    int Corruption,
    int Gaze
);
