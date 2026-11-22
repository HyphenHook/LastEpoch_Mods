using LastEpoch_Hud.Scripts.Core.Heorot;

namespace LastEpoch_Hud.Tests.Core.Heorot;

public sealed class HeorotCoreRulesTests
{
    [Fact]
    public void DirectTestRatesCanGuaranteeOnlyTheChosenVariant()
    {
        var rates = HeorotCoreRules.ValidateVariantChances(0, 100, 0);
        Assert.Equal(1, HeorotCoreRules.CombinedChance(rates));
        Assert.Equal(2, HeorotCoreRules.WeightedTier(0, rates));
        Assert.Equal(2, HeorotCoreRules.WeightedTier(.9999, rates));
    }

    [Fact]
    public void DisabledAndInvalidRatesCannotEnableDrops()
    {
        var rates = HeorotCoreRules.ValidateVariantChances(-3, double.NaN, double.PositiveInfinity);
        Assert.Equal(0, HeorotCoreRules.CombinedChance(rates));
    }

    [Fact]
    public void RatesAboveOneHundredKeepOneDropAndRelativeVariantWeights()
    {
        var rates = HeorotCoreRules.ValidateVariantChances(100, 100, 100);
        Assert.Equal(1, HeorotCoreRules.CombinedChance(rates));
        Assert.Equal(1, HeorotCoreRules.WeightedTier(.1, rates));
        Assert.Equal(2, HeorotCoreRules.WeightedTier(.5, rates));
        Assert.Equal(3, HeorotCoreRules.WeightedTier(.9, rates));
    }

    [Fact]
    public void EveryCombinationHasAStableDistinctUniqueId()
    {
        var ids = Enumerable.Range(1, 7).Select(HeorotCoreRules.UniqueIdFor).ToArray();
        Assert.Equal(7, ids.Distinct().Count());
        foreach (int mask in Enumerable.Range(1, 7))
            Assert.Equal(mask, HeorotCoreRules.MaskFor(HeorotCoreRules.UniqueIdFor(mask)));
        Assert.Equal(0, HeorotCoreRules.MaskFor(505));
        Assert.Equal(0, HeorotCoreRules.MaskFor(506));
        Assert.Equal(0, HeorotCoreRules.MaskFor(514));
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(1, 2, 4)]
    [InlineData(2, 0, 6)]
    [InlineData(2, 1, 5)]
    [InlineData(2, 2, 3)]
    [InlineData(3, 0, 7)]
    public void VariantsSelectExactlyThePromisedBuffs(int count, int choice, int mask) =>
        Assert.Equal(mask, HeorotCoreRules.MaskForVariant(count, choice));

    [Theory]
    [InlineData(.0, 1)]
    [InlineData(.79, 1)]
    [InlineData(.81, 2)]
    [InlineData(.975, 3)]
    [InlineData(.999, 4)]
    public void LegendaryPotentialUsesProgressivelyRarerWeights(double roll, int expected) =>
        Assert.Equal(expected, HeorotCoreRules.WeightedTier(roll, new[] { 80d, 17d, 2.8, .2 }));

    [Fact]
    public void DisabledOutcomesCannotBeSelectedAtTheTopBoundary() =>
        Assert.Equal(2, HeorotCoreRules.WeightedTier(1, new[] { 0d, 1d, 0d }));

    [Fact]
    public void InvalidWeightsCannotCreateAnUnintendedOutcome()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HeorotCoreRules.WeightedTier(.5, new[] { 0d, 0d }));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeorotCoreRules.WeightedTier(.5, new[] { 1d, -1d }));
    }

    [Theory]
    [InlineData(true, true, false, true, true, 100, "Heorot(Clone)", true)]
    [InlineData(true, true, false, true, true, 90, "Heorot(Clone)", false)]
    [InlineData(true, true, false, false, true, 100, "Heorot(Clone)", false)]
    [InlineData(true, true, false, true, false, 100, "Heorot(Clone)", false)]
    [InlineData(true, true, false, true, true, 100, "Harbinger(Clone)", false)]
    [InlineData(true, false, false, true, true, 100, "HeorotMinion", false)]
    [InlineData(true, true, true, true, true, 100, "Heorot(Clone)", false)]
    [InlineData(false, true, false, true, true, 100, "Heorot(Clone)", false)]
    public void DropsRequireTheOfflineLevel100HeorotTimelineBoss(bool offline, bool boss, bool minion,
        bool timelineBoss, bool heorotTimeline, int level, string name, bool expected) =>
        Assert.Equal(expected, HeorotCoreRules.CanDrop(offline, boss, minion, timelineBoss, heorotTimeline, level, name));
}
