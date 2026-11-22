namespace LastEpoch_Hud.Scripts.Core.ForceDrop;

/// <summary>One selectable Force Drop item, by ids, with its raw catalog label.</summary>
public readonly record struct ForceDropItemOption(
    int BaseType,
    int SubType,
    ForceDropRarity Rarity,
    int UniqueId,
    string Label
);
