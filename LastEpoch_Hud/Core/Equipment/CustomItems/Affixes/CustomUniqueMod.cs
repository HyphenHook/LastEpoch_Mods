namespace LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;

/// <summary>One affix of a custom unique.</summary>
public readonly record struct CustomUniqueMod(
    CustomItemStat Stat,
    CustomItemTag Tags,
    CustomItemModType Type,
    float Value,
    float MaxValue,
    bool CanRoll
);
