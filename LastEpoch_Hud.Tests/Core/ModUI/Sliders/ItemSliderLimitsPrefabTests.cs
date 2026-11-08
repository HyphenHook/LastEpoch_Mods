using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;
using LastEpoch_Hud.Tests.Support;

namespace LastEpoch_Hud.Tests.Core.ModUI.Sliders;

public sealed class ItemSliderLimitsPrefabTests
{
    // User decision 2026-10-12: drop code uses whole affix counts, so the prefab's fractional slider is overridden.
    private static readonly Dictionary<string, bool> _wholeNumbersOverrides = new()
    {
        [nameof(ItemSliderLimits.DropAffixCount)] = true,
    };

    // The mod-wide affix limit (AffixLimits.MaxTotal) is higher than the prefab's old slider maximum.
    private static readonly Dictionary<string, float> _maxOverrides = new()
    {
        [nameof(ItemSliderLimits.DropAffixCount)] = AffixLimits.MaxTotal,
    };

    [Theory]
    [InlineData(
        nameof(ItemSliderLimits.DropImplicits),
        "Slider_Items_Drop_Implicits_Min",
        "Slider_Items_Drop_Implicits_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.DropForgingPotential),
        "Slider_Items_Drop_ForginPotencial_Min",
        "Slider_Items_Drop_ForginPotencial_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.DropSealTier),
        "Slider_Items_Drop_SealTier_Min",
        "Slider_Items_Drop_SealTier_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.DropSealValue),
        "Slider_Items_Drop_SealValue_Min",
        "Slider_Items_Drop_SealValue_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.DropAffixCount),
        "Slider_Items_Drop_NbAffixes_Min",
        "Slider_Items_Drop_NbAffixes_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.DropAffixTiers),
        "Slider_Items_Drop_AffixesTiers_Min",
        "Slider_Items_Drop_AffixesTiers_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.DropAffixValues),
        "Slider_Items_Drop_AffixesValues_Min",
        "Slider_Items_Drop_AffixesValues_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.DropUniqueMods),
        "Slider_Items_Drop_UniqueMods_Min",
        "Slider_Items_Drop_UniqueMods_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.DropLegendaryPotential),
        "Slider_Items_Drop_LegendaryPotencial_Min",
        "Slider_Items_Drop_LegendaryPotencial_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.DropWeaverWill),
        "Slider_Items_Drop_WeaverWill_Min",
        "Slider_Items_Drop_WeaverWill_Max"
    )]
    [InlineData(
        nameof(ItemSliderLimits.AutoShatterChance),
        "Slider_Items_Pickup_AutoShatter_Chance",
        "Slider_Items_Pickup_AutoShatter_Chance"
    )]
    [InlineData(
        nameof(ItemSliderLimits.AutoShatterAffixChance),
        "Slider_Items_Pickup_AutoShatter_AffixChance",
        "Slider_Items_Pickup_AutoShatter_AffixChance"
    )]
    [InlineData(
        nameof(ItemSliderLimits.AutoShatterQuantityChance),
        "Slider_Items_Pickup_AutoShatter_QuantityChance",
        "Slider_Items_Pickup_AutoShatter_QuantityChance"
    )]
    public void Range_MatchesPrefab(string field, string minSlider, string maxSlider)
    {
        IReadOnlyDictionary<string, SliderRange> prefab = PrefabSliders.Load(
            PrefabSliders.HudPrefabPath
        );

        Assert.True(prefab.ContainsKey(minSlider), $"Prefab slider missing: {minSlider}");
        Assert.True(prefab.ContainsKey(maxSlider), $"Prefab slider missing: {maxSlider}");
        bool whole = _wholeNumbersOverrides.GetValueOrDefault(
            field,
            prefab[minSlider].WholeNumbers
        );
        var expected = new SliderRange(
            prefab[minSlider].Min,
            _maxOverrides.GetValueOrDefault(field, prefab[maxSlider].Max),
            whole
        );
        SliderRange? actual = SliderLimitsReflection.Find(typeof(ItemSliderLimits), field);
        Assert.True(actual.HasValue, $"Limits field missing: {field}");
        Assert.Equal(expected, actual.Value);
    }

    [Theory]
    [InlineData(
        nameof(ItemSliderLimits.CraftingSlotForgingPotential),
        "Slider_Items_Craft_ForginPotencial"
    )]
    [InlineData(
        nameof(ItemSliderLimits.CraftingSlotImplicit),
        "Slider_Items_Craft_Implicit0",
        "Slider_Items_Craft_Implicit1",
        "Slider_Items_Craft_Implicit2"
    )]
    [InlineData(nameof(ItemSliderLimits.CraftingSlotSealTier), "Slider_Items_Craft_SealTier")]
    [InlineData(nameof(ItemSliderLimits.CraftingSlotSealValue), "Slider_Items_Craft_SealValue")]
    [InlineData(
        nameof(ItemSliderLimits.CraftingSlotAffixTier),
        "Slider_Items_Craft_AffixTier0",
        "Slider_Items_Craft_AffixTier1",
        "Slider_Items_Craft_AffixTier2",
        "Slider_Items_Craft_AffixTier3"
    )]
    [InlineData(
        nameof(ItemSliderLimits.CraftingSlotAffixValue),
        "Slider_Items_Craft_AffixValue0",
        "Slider_Items_Craft_AffixValue1",
        "Slider_Items_Craft_AffixValue2",
        "Slider_Items_Craft_AffixValue3"
    )]
    [InlineData(
        nameof(ItemSliderLimits.CraftingSlotUniqueMod),
        "Slider_Items_Craft_UniqueMod0",
        "Slider_Items_Craft_UniqueMod1",
        "Slider_Items_Craft_UniqueMod2",
        "Slider_Items_Craft_UniqueMod3",
        "Slider_Items_Craft_UniqueMod4",
        "Slider_Items_Craft_UniqueMod5",
        "Slider_Items_Craft_UniqueMod6",
        "Slider_Items_Craft_UniqueMod7"
    )]
    [InlineData(
        nameof(ItemSliderLimits.CraftingSlotLegendaryPotential),
        "Slider_Items_Craft_LegendaryPotencial"
    )]
    [InlineData(nameof(ItemSliderLimits.CraftingSlotWeaverWill), "Slider_Items_Craft_WeaverWill")]
    public void CraftingSlotRange_MatchesPrefab(string field, params string[] sliders)
    {
        IReadOnlyDictionary<string, SliderRange> prefab = PrefabSliders.Load(
            PrefabSliders.HudPrefabPath
        );
        SliderRange? actual = SliderLimitsReflection.Find(typeof(ItemSliderLimits), field);
        Assert.True(actual.HasValue, $"Limits field missing: {field}");

        foreach (string slider in sliders)
        {
            Assert.True(prefab.ContainsKey(slider), $"Prefab slider missing: {slider}");
            // Whole numbers is always true: ItemsCraftingSlotPage forces it, the prefab flag is false for all of them.
            var expected = new SliderRange(prefab[slider].Min, prefab[slider].Max, true);
            Assert.Equal(expected, actual.Value);
        }
    }
}
