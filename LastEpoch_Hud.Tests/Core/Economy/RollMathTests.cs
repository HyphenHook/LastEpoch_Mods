using LastEpoch_Hud.Scripts.Core.Economy;

namespace LastEpoch_Hud.Tests.Core.Economy;

public class RollMathTests
{
    [Fact]
    public void HighRollIsBestWhenMoreIsBetter()
    {
        Assert.Equal(1.0, RollMath.Quality(255, maxRollIsBest: true), 6);
        Assert.Equal(0.0, RollMath.Quality(0, maxRollIsBest: true), 6);
    }

    [Fact]
    public void LowRollIsBestWhenLessIsBetter()
    {
        // A drawback, a cost or a delay: the smaller roll is the better one.
        Assert.Equal(1.0, RollMath.Quality(0, maxRollIsBest: false), 6);
        Assert.Equal(0.0, RollMath.Quality(255, maxRollIsBest: false), 6);
    }

    [Fact]
    public void QualityNeverLeavesZeroToOne()
    {
        Assert.Equal(1.0, RollMath.Quality(9999, true), 6);
        Assert.Equal(0.0, RollMath.Quality(-5, true), 6);
    }

    [Fact]
    public void AffixFactorRisesWithQualityAndSpansAboutThreePointFour()
    {
        double previous = double.MinValue;
        for (int roll = 0; roll <= 255; roll += 15)
        {
            double f = RollMath.AffixFactor(RollMath.Quality(roll, true));
            Assert.True(f > previous, "a better roll must never be cheaper");
            previous = f;
        }
        double ratio = RollMath.AffixFactor(1) / RollMath.AffixFactor(0);
        Assert.InRange(ratio, 3.3, 3.5);
    }

    [Fact]
    public void BestRollsPayMoreThanMiddlingOnesByMoreThanLinear()
    {
        // Convex: moving a roll from the middle to perfect adds more than moving it from worst to the middle.
        double worst = RollMath.AffixFactor(0);
        double middle = RollMath.AffixFactor(0.5);
        double best = RollMath.AffixFactor(1);
        Assert.True(best - middle > middle - worst);
    }

    [Fact]
    public void MinimiseStatPaysMoreForTheLowRoll()
    {
        // Same stat, same tier: the item that rolled the smaller drawback must be worth more.
        double lowRoll = RollMath.AffixFactor(RollMath.Quality(10, maxRollIsBest: false));
        double highRoll = RollMath.AffixFactor(RollMath.Quality(245, maxRollIsBest: false));
        Assert.True(lowRoll > highRoll);
    }

    [Fact]
    public void ValueFallbackTreatsLargerNumberAsBetter()
    {
        Assert.True(RollMath.MaxRollIsBestByValue(5f, 20f));
        Assert.False(RollMath.MaxRollIsBestByValue(20f, 5f));
        // A penalty that gets smaller in magnitude as the roll rises: -30 at min roll, -10 at max roll.
        Assert.True(RollMath.MaxRollIsBestByValue(-30f, -10f));
    }

    [Fact]
    public void LessIsBetterStatsMakeTheLowRollTheGoodOne()
    {
        // A cost that runs 5 (min roll) to 25 (max roll): the smaller cost is the better roll.
        Assert.False(RollMath.MaxRollIsBestByValue(5f, 25f, lessIsBetter: true));
        // A delay whose range runs downward, 25 at min roll to 5 at max roll: now the HIGH roll is the better one.
        Assert.True(RollMath.MaxRollIsBestByValue(25f, 5f, lessIsBetter: true));
    }

    [Fact]
    public void NegativeNumbersAreJudgedByWhichEndIsKinder()
    {
        // A damage penalty running -30 (min roll) to -10 (max roll): -10 hurts less, so the high roll is better.
        Assert.True(RollMath.MaxRollIsBestByValue(-30f, -10f));
        // A drawback where being bigger is worse (a cost-like stat), -10 to -30: the low roll is better.
        Assert.False(RollMath.MaxRollIsBestByValue(-10f, -30f));
        // A bonus that is negative-looking but wanted smaller, e.g. -5 to -25 with lessIsBetter: more negative is better.
        Assert.True(RollMath.MaxRollIsBestByValue(-5f, -25f, lessIsBetter: true));
    }

    [Fact]
    public void UniqueFactorRisesWithQuality()
    {
        double previous = double.MinValue;
        for (int i = 0; i <= 10; i++)
        {
            double f = RollMath.UniqueFactor(i / 10.0);
            Assert.True(f > previous);
            previous = f;
        }
        Assert.InRange(RollMath.UniqueFactor(0), 0.49, 0.51);
        Assert.InRange(RollMath.UniqueFactor(1), 1.59, 1.61);
    }

    [Fact]
    public void UniqueWithBetterRollsIsWorthMore()
    {
        var mods = new[] { (Roll: 0, Min: 10f, Max: 40f, MaxBest: true), (Roll: 0, Min: 5f, Max: 25f, MaxBest: true) };
        double poor = RollMath.UniqueFactor(RollMath.UniqueQuality(Build(mods, 20, 20)));
        double good = RollMath.UniqueFactor(RollMath.UniqueQuality(Build(mods, 240, 240)));
        Assert.True(good > poor * 2, "perfect rolls on a unique should at least double a poor one");
    }

    [Fact]
    public void UniqueDrawbackIsBetterWhenSmaller()
    {
        // One damage modifier (more is better) and one drawback (less is better).
        var mods = new[] { (Roll: 0, Min: 10f, Max: 40f, MaxBest: true), (Roll: 0, Min: 5f, Max: 25f, MaxBest: false) };
        double goodDamageSmallDrawback = RollMath.UniqueQuality(Build(mods, 255, 0));
        double goodDamageBigDrawback = RollMath.UniqueQuality(Build(mods, 255, 255));
        double poorDamageSmallDrawback = RollMath.UniqueQuality(Build(mods, 0, 0));
        double poorDamageBigDrawback = RollMath.UniqueQuality(Build(mods, 0, 255));

        Assert.Equal(1.0, goodDamageSmallDrawback, 6); // best of both
        Assert.Equal(0.0, poorDamageBigDrawback, 6); // worst of both
        Assert.True(goodDamageSmallDrawback > goodDamageBigDrawback);
        Assert.True(poorDamageSmallDrawback > poorDamageBigDrawback);
        // Pricing follows quality.
        Assert.True(RollMath.UniqueFactor(goodDamageSmallDrawback) > RollMath.UniqueFactor(goodDamageBigDrawback));
    }

    [Fact]
    public void BlindAveragingOfRawRollsWouldGetTheDrawbackBackwards()
    {
        // The mistake this module exists to avoid: a high raw roll on a drawback is a BAD roll.
        var mods = new[] { (Roll: 0, Min: 5f, Max: 25f, MaxBest: false) };
        double bigDrawback = RollMath.UniqueQuality(Build(mods, 250));
        double smallDrawback = RollMath.UniqueQuality(Build(mods, 5));
        Assert.True(smallDrawback > bigDrawback);
    }

    [Fact]
    public void ModifiersThatDoNotRollAreIgnored()
    {
        var mods = new (int Roll, float ValueAtMin, float ValueAtMax, bool MaxRollIsBest)[]
        {
            (255, 10f, 40f, true), // rolls: perfect
            (0, 15f, 15f, true), // fixed value: its stored roll means nothing
        };
        Assert.Equal(1.0, RollMath.UniqueQuality(mods), 6);
    }

    [Fact]
    public void UniqueWithNothingToRollIsAverage()
    {
        var mods = new (int Roll, float ValueAtMin, float ValueAtMax, bool MaxRollIsBest)[] { (0, 7f, 7f, true) };
        Assert.Equal(0.5, RollMath.UniqueQuality(mods), 6);
        Assert.Equal(0.5, RollMath.UniqueQuality(System.Array.Empty<(int, float, float, bool)>()), 6);
    }

    [Fact]
    public void ImplicitFactorSpansPointEightToOnePointThree()
    {
        Assert.Equal(0.8, RollMath.ImplicitFactor(new double[] { 0, 0 }), 6);
        Assert.Equal(1.3, RollMath.ImplicitFactor(new double[] { 1, 1, 1 }), 6);
        Assert.Equal(1.0, RollMath.ImplicitFactor(System.Array.Empty<double>()), 6);
        Assert.Equal(1.0, RollMath.ImplicitFactor(null), 6);
    }

    [Fact]
    public void BetterImplicitsNeverCostLess()
    {
        double previous = double.MinValue;
        for (int i = 0; i <= 10; i++)
        {
            double f = RollMath.ImplicitFactor(new[] { i / 10.0 });
            Assert.True(f > previous);
            previous = f;
        }
    }

    [Fact]
    public void WholeItemPremiumNeedsThreeAffixes()
    {
        Assert.Equal(1.0, RollMath.ItemRollPremium(new[] { 1.0, 1.0 }), 6);
        Assert.Equal(1.25, RollMath.ItemRollPremium(new[] { 0.95, 0.92, 1.0 }), 6);
        Assert.Equal(0.85, RollMath.ItemRollPremium(new[] { 0.1, 0.2, 0.3, 0.0 }), 6);
        Assert.Equal(1.0, RollMath.ItemRollPremium(new[] { 0.6, 0.5, 0.7 }), 6);
    }

    private static (int Roll, float ValueAtMin, float ValueAtMax, bool MaxRollIsBest)[] Build(
        (int Roll, float Min, float Max, bool MaxBest)[] shape,
        params int[] rolls
    )
    {
        var result = new (int, float, float, bool)[shape.Length];
        for (int i = 0; i < shape.Length; i++)
        {
            result[i] = (rolls[i], shape[i].Min, shape[i].Max, shape[i].MaxBest);
        }
        return result;
    }
}
