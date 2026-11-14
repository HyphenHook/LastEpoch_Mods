using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public class ReflectorMathTests
{
    [Fact]
    public void OnlyTheFirstItemNeedsAllFourAffixes()
    {
        Assert.NotEqual("", ReflectorMath.WhyNot(3, 4));
        Assert.NotEqual("", ReflectorMath.WhyNot(4, 0));
        Assert.Equal("", ReflectorMath.WhyNot(4, 1));
        Assert.Equal("", ReflectorMath.WhyNot(4, 4));
        Assert.Equal("", ReflectorMath.WhyNot(5, 6));
    }

    [Fact]
    public void AnItemWithSixAffixesCannotTakeAnother()
    {
        Assert.NotEqual("", ReflectorMath.WhyNot(6, 4));
    }

    [Fact]
    public void TheFifthAffixIsTwentyPercentAndTheSixthTenAtAMiddleTier()
    {
        Assert.Equal(0.20, ReflectorMath.Chance(4, 4), 6);
        Assert.Equal(0.10, ReflectorMath.Chance(5, 4), 6);
        Assert.Equal(0, ReflectorMath.Chance(6, 4));
        Assert.Equal(0, ReflectorMath.Chance(3, 4));
    }

    [Fact]
    public void HigherTiersAreHarderToReflect()
    {
        double previous = 2;
        for (int tier = 1; tier <= 7; tier++)
        {
            double p = ReflectorMath.Chance(4, tier);
            Assert.True(p < previous);
            previous = p;
        }
        Assert.InRange(ReflectorMath.Chance(4, 7), 0.05, 0.10);
    }

    [Fact]
    public void LegendaryItemsNeedAnAffixEach()
    {
        Assert.NotEqual("", ReflectorMath.LegendaryWhyNot(0, 2));
        Assert.NotEqual("", ReflectorMath.LegendaryWhyNot(2, 0));
        Assert.Equal("", ReflectorMath.LegendaryWhyNot(1, 1));
        Assert.Equal("", ReflectorMath.LegendaryWhyNot(4, 1));
    }

    [Fact]
    public void MoreAffixesOnTheSecondLegendaryMakeItLikelier()
    {
        double previous = 0;
        for (int b = 1; b <= 5; b++)
        {
            double p = ReflectorMath.LegendaryChance(2, b);
            Assert.True(p > previous);
            previous = p;
        }
    }

    [Fact]
    public void AFullFirstLegendaryLowersTheChanceSignificantly()
    {
        double open = ReflectorMath.LegendaryChance(2, 1);
        double four = ReflectorMath.LegendaryChance(4, 1);
        double five = ReflectorMath.LegendaryChance(5, 1);
        Assert.True(four < open * 0.8);
        Assert.True(five < four);
        Assert.Equal(0.15, ReflectorMath.LegendaryChance(2, 1), 6);
        Assert.Equal(0.10, ReflectorMath.LegendaryChance(3, 1), 6);
        Assert.Equal(0.06, ReflectorMath.LegendaryChance(4, 1), 6);
        Assert.Equal(0.03, ReflectorMath.LegendaryChance(5, 1), 6);
        Assert.Equal(0.30, ReflectorMath.LegendaryChance(2, 6), 6);
    }

    [Fact]
    public void CostsGrowWithTierAndWithHowFullTheItemIs()
    {
        Assert.True(ReflectorMath.MemoryAmber(4, 7) > ReflectorMath.MemoryAmber(4, 3));
        Assert.True(ReflectorMath.MemoryAmber(5, 4) > ReflectorMath.MemoryAmber(4, 4));
        Assert.True(ReflectorMath.Gold(4, 6) > ReflectorMath.Gold(4, 2));
        Assert.True(ReflectorMath.Bones(5, 5) > ReflectorMath.Bones(4, 5));
        Assert.True(ReflectorMath.LegendaryMemoryAmber(4, 3) > ReflectorMath.LegendaryMemoryAmber(2, 3));
        Assert.True(ReflectorMath.LegendaryGold(2, 4) > ReflectorMath.LegendaryGold(2, 1));
    }

    [Fact]
    public void ItIsAnEndGamePrice()
    {
        Assert.True(ReflectorMath.MemoryAmber(4, 1) >= 1000);
        Assert.True(ReflectorMath.Gold(4, 1) >= 100_000);
        Assert.True(ReflectorMath.Bones(4, 1) >= 2_500);
    }
}
