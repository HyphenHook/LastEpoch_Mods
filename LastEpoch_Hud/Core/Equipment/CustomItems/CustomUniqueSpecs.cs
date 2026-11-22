using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>The custom uniques the mod adds to the game.</summary>
public static class CustomUniqueSpecs
{
    public static readonly CustomUniqueSpec Headhunter = new()
    {
        Name = "Headhunter",
        UniqueId = 500,
        BaseType = 2, // Belt
        BaseId = CustomUniqueSpec.AllocateBaseId,
        AddsBase = true,
        LevelRequirement = 40,
        OverrideLevelRequirement = true,
        EffectiveLevelForLegendaryPotential = 0,
        IconAsset = "/headhunter/texture2d/icon.png",
    };

    public static readonly CustomUniqueSpec Mjolner = new()
    {
        Name = "Mjolner",
        UniqueId = 501,
        BaseType = 7, // Mace
        BaseId = 10, // Rune hammer
        AddsBase = false,
        LevelRequirement = 78,
        OverrideLevelRequirement = false,
        EffectiveLevelForLegendaryPotential = 60,
        IconAsset = "/mjolner.png",
    };

    public static readonly CustomUniqueSpec SandsOfSilk = new()
    {
        Name = "Sands of Silk",
        UniqueId = 502,
        BaseType = 1, // Body armor
        BaseId = CustomUniqueSpec.AllocateBaseId,
        AddsBase = true,
        LevelRequirement = 16,
        OverrideLevelRequirement = true,
        EffectiveLevelForLegendaryPotential = 0,
        IconAsset = "/sandsofsilk/texture2d/icon.png",
        VisualSource = new CustomItemVisualSource { SubType = 0, UniqueId = 7 },
    };

    public static readonly CustomUniqueSpec EssentiaSanguis = new()
    {
        Name = "Essentia Sanguis",
        UniqueId = 503,
        BaseType = 4, // Gloves
        BaseId = CustomUniqueSpec.AllocateBaseId,
        AddsBase = true,
        LevelRequirement = 52,
        OverrideLevelRequirement = true,
        EffectiveLevelForLegendaryPotential = 0,
        IconAsset = "/essentiasanguis/texture2d/icon.png",
        VisualSource = new CustomItemVisualSource { SubType = 0, UniqueId = 22 },
    };

    public static readonly CustomUniqueSpec Mageblood = new()
    {
        Name = "Mageblood",
        UniqueId = 505, // 504 is taken by Temporalis (Items_Temporalis.cs)
        BaseType = 2, // Belt
        BaseId = CustomUniqueSpec.AllocateBaseId,
        AddsBase = true,
        LevelRequirement = 44,
        OverrideLevelRequirement = true,
        EffectiveLevelForLegendaryPotential = 0,
        IconAsset = "/mageblood/texture2d/icon.png",
        IconFallbackUniqueId = 500,
    };

    public static readonly IReadOnlyList<CustomUniqueSpec> All = new[]
    {
        Headhunter,
        Mjolner,
        SandsOfSilk,
        EssentiaSanguis,
        Mageblood,
    };
}
