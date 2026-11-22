namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Fixed ids and levels of one custom unique item.</summary>
public sealed class CustomUniqueSpec
{
    /// <summary>Base id value meaning "take the next free subtype id".</summary>
    public const int AllocateBaseId = -1;

    public string Name { get; init; }
    public ushort UniqueId { get; init; }
    public byte BaseType { get; init; }
    public int BaseId { get; init; }
    public bool AddsBase { get; init; }
    public int LevelRequirement { get; init; }
    public bool OverrideLevelRequirement { get; init; }
    public int EffectiveLevelForLegendaryPotential { get; init; }

    /// <summary>Lowercase '/'-path suffix of the icon in the HUD bundle.</summary>
    public string IconAsset { get; init; }

    /// <summary>Unique whose icon shows while this item's own icon is missing; 0 = none.</summary>
    public ushort IconFallbackUniqueId { get; init; }

    /// <summary>Game item whose 3D visual is borrowed; null uses the game's own lookup.</summary>
    public CustomItemVisualSource VisualSource { get; init; }
}
