namespace LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;

/// <summary>One implicit of a custom base item.</summary>
public readonly record struct CustomBaseImplicit(
    CustomItemStat Stat,
    CustomItemTag Tags,
    CustomItemModType Type,
    byte SpecialTag,
    float Value,
    float MaxValue
);
