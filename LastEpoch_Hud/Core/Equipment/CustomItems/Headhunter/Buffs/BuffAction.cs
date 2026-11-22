namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>One buff change. Increased is a game fraction (0.1 = 10 %). Tags is the game ability tag id (0 = none). More is one game more fraction (0 = none).</summary>
public readonly record struct BuffAction(
    BuffActionKind Kind,
    string BuffName,
    int StatId,
    float Added,
    float Increased,
    float DurationSeconds,
    int Stacks,
    int Tags = 0,
    float More = 0f
);
