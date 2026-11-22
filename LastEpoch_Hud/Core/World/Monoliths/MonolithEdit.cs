namespace LastEpoch_Hud.Scripts.Core.Monoliths;

/// <summary>One value to write; RunIndex is the index in the snapshot list.</summary>
public readonly record struct MonolithEdit(
    int RunIndex,
    int TimelineId,
    MonolithField Field,
    int Value
);
