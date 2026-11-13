using LastEpoch_Hud.Scripts.Core.Economy;

namespace LastEpoch_Hud.Tests.Core.Economy;

public class RarityMathTests
{
    [Fact]
    public void RarerTiersAreRarer()
    {
        Assert.True(RarityMath.TierChance(6) < RarityMath.TierChance(5));
        Assert.True(RarityMath.TierChance(7) < RarityMath.TierChance(6));
        Assert.True(RarityMath.TierChance(8) < RarityMath.TierChance(7));
    }

    [Fact]
    public void EachStepOfLegendaryPotentialIsHarderToFind()
    {
        double previous = double.MaxValue;
        for (int lp = 0; lp <= 4; lp++)
        {
            double c = RarityMath.LegendaryPotentialChance(lp);
            Assert.True(c < previous);
            previous = c;
        }
    }

    [Fact]
    public void UniquePriceRisesWithEveryLegendaryPotential()
    {
        double previous = 0;
        for (int lp = 0; lp <= 4; lp++)
        {
            double gold = RarityMath.UniqueGold(RarityMath.LegendaryPotentialChance(lp), 1, 1);
            Assert.True(gold > previous);
            previous = gold;
        }
    }

    [Fact]
    public void FourLegendaryPotentialIsWorthManyHoursOfFarming()
    {
        double gold = RarityMath.UniqueGold(RarityMath.LegendaryPotentialChance(4), 1, 1);
        Assert.True(gold > 2_000_000, "a 4LP unique must cost millions, not thousands");
        Assert.True(gold > 20 * RarityMath.UniqueGold(RarityMath.LegendaryPotentialChance(1), 1, 1));
    }

    [Fact]
    public void ARarerUniqueCostsMore()
    {
        double common = RarityMath.UniqueGold(0.025, 1, 1);
        double scarce = RarityMath.UniqueGold(0.025, 4, 1);
        double boss = RarityMath.UniqueGold(0.025, 1, 3);
        Assert.True(scarce > common);
        Assert.True(boss > common);
    }

    [Fact]
    public void ASingleT7CostsAboutAWellRolledQuarterMillion()
    {
        double gold = RarityMath.GearAffixGold(new[] { RarityMath.TierChance(7) / 2.0 }, RarityMath.GearSearchRate);
        Assert.InRange(gold, 100_000, 500_000);
    }

    [Fact]
    public void EveryExtraTopAffixMultipliesThePrice()
    {
        double c7 = RarityMath.TierChance(7) / 2.0;
        double one = RarityMath.GearAffixGold(new[] { c7 }, RarityMath.GearSearchRate);
        double two = RarityMath.GearAffixGold(new[] { c7, c7 }, RarityMath.GearSearchRate);
        double three = RarityMath.GearAffixGold(new[] { c7, c7, c7 }, RarityMath.GearSearchRate);
        Assert.True(two > one * 20);
        Assert.True(three >= two);
    }

    [Fact]
    public void T7CostsMoreThanT6AndT8MoreThanT7()
    {
        double t6 = RarityMath.GearAffixGold(new[] { RarityMath.TierChance(6) }, RarityMath.GearSearchRate);
        double t7 = RarityMath.GearAffixGold(new[] { RarityMath.TierChance(7) }, RarityMath.GearSearchRate);
        double t8 = RarityMath.GearAffixGold(new[] { RarityMath.TierChance(8) }, RarityMath.GearSearchRate);
        Assert.True(t7 > 3 * t6);
        Assert.True(t8 > 5 * t7);
    }

    [Fact]
    public void NothingFoundIsFree()
    {
        Assert.Equal(0, RarityMath.GearAffixGold(System.Array.Empty<double>(), 500));
    }

    [Fact]
    public void NoPriceExceedsTheCeiling()
    {
        Assert.True(RarityMath.CompressGear(1e30) <= RarityMath.MaxPrice);
        Assert.True(RarityMath.CompressUnique(1e30) <= RarityMath.MaxPrice);
        Assert.Equal(50_000_000, RarityMath.MaxPrice);
    }

    [Fact]
    public void ScalesLeaveSmallPricesAloneAndNeverFall()
    {
        Assert.Equal(20_000, RarityMath.CompressGear(20_000), 3);
        Assert.InRange(RarityMath.CompressUnique(200_000), 199_000, 200_000);
        double g = -1, u = -1;
        for (double x = 1_000; x < 1e15; x *= 2)
        {
            Assert.True(RarityMath.CompressGear(x) >= g);
            Assert.True(RarityMath.CompressUnique(x) >= u);
            g = RarityMath.CompressGear(x);
            u = RarityMath.CompressUnique(x);
        }
    }

    [Fact]
    public void GearPricesFollowTheFeederScale()
    {
        double c7 = RarityMath.TierChance(7) / 2.0;
        double Price(int n) => RarityMath.CompressGear(RarityMath.GearAffixGold(System.Linq.Enumerable.Repeat(c7, n), RarityMath.GearSearchRate));
        double one = Price(1), two = Price(2), three = Price(3), four = Price(4);
        Assert.InRange(one, 80_000, 200_000);
        Assert.InRange(two, 200_000, 300_000);
        Assert.InRange(three, 1_000_000, 2_500_000);
        Assert.InRange(four, 4_000_000, 10_000_000);
        double eight = RarityMath.CompressGear(RarityMath.GearAffixGold(new[] { c7, c7, c7, RarityMath.TierChance(8) / 2.0 }, RarityMath.GearSearchRate));
        Assert.InRange(eight, 15_000_000, 50_000_000);
    }

    [Fact]
    public void ThreeAndFourLegendaryPotentialUniquesAreMillionPriced()
    {
        double lp3 = RarityMath.CompressUnique(RarityMath.UniqueGold(RarityMath.LegendaryPotentialChance(3), 1, 1));
        double lp4 = RarityMath.CompressUnique(RarityMath.UniqueGold(RarityMath.LegendaryPotentialChance(4), 1, 1));
        Assert.InRange(lp3, 800_000, 2_500_000);
        Assert.InRange(lp4, 2_500_000, 7_000_000);
    }

    [Fact]
    public void WeaverAndEnchantedIdolAffixesAreRarerThanOrdinary()
    {
        Assert.True(RarityMath.IdolAffixChance("IdolWeaver") < RarityMath.IdolAffixChance("None"));
        Assert.True(RarityMath.IdolAffixChance("IdolEnchantment") < RarityMath.IdolAffixChance("IdolWeaver"));
    }

    [Fact]
    public void HighWeaversWillIsRarer()
    {
        Assert.True(RarityMath.WeaversWillChance(30) < RarityMath.WeaversWillChance(10));
    }
}
