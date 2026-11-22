namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>One game-ready stat row. Increased and More are game fractions (0.1 = 10 %).</summary>
public readonly record struct MagebloodBuffStat(
    int StatId,
    int Tags,
    string BuffName,
    float Added,
    float Increased,
    float More
);
