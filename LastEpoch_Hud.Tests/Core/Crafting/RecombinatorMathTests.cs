using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public class RecombinatorMathTests
{
    private static RecombAffix A(int id, int tier, double weight = 1, int source = 0) =>
        new() { AffixId = id, DisplayedTier = tier, Weight = weight, Source = source };

    private static Func<double> Seeded(int seed)
    {
        var r = new Random(seed);
        return r.NextDouble;
    }

    private static RecombAffix P(int id, int tier, int placement, int source = 0) =>
        new() { AffixId = id, DisplayedTier = tier, Weight = 1, Source = source, Placement = placement };

    [Fact]
    public void SelectionMustHaveOneToSixDifferentAffixes()
    {
        Assert.NotEqual("", RecombinatorMath.Validate(new List<RecombAffix>()));
        Assert.NotEqual("", RecombinatorMath.Validate(new[] { A(1, 3), A(1, 3, 1, 1) }));
        Assert.NotEqual("", RecombinatorMath.Validate(new[] { A(1, 1), A(2, 1), A(3, 1), A(4, 1), A(5, 1), A(6, 1), A(7, 1) }));
        Assert.Equal("", RecombinatorMath.Validate(new[] { A(1, 3), A(2, 4) }));
        Assert.Equal("", RecombinatorMath.Validate(new[] { A(1, 1), A(2, 1), A(3, 1), A(4, 1), A(5, 1), A(6, 1) }));
    }

    [Fact]
    public void NoMoreThanThreePrefixesOrThreeSuffixes()
    {
        var fourPrefixes = new[] { P(1, 3, 1), P(2, 3, 1), P(3, 3, 1), P(4, 3, 1) };
        var threeAndThree = new[] { P(1, 3, 1), P(2, 3, 1), P(3, 3, 1), P(4, 3, 2), P(5, 3, 2), P(6, 3, 2) };
        Assert.NotEqual("", RecombinatorMath.Validate(fourPrefixes));
        Assert.Equal("", RecombinatorMath.Validate(threeAndThree));
        Assert.Equal(0, RecombinatorMath.SuccessChance(fourPrefixes));
    }

    [Fact]
    public void OneLowTierAffixIsAlmostCertain()
    {
        Assert.InRange(RecombinatorMath.SuccessChance(new[] { A(1, 2) }), 0.9, 1.0);
    }

    [Fact]
    public void KeepingMoreAffixesIsHarder()
    {
        double one = RecombinatorMath.SuccessChance(new[] { A(1, 4) });
        double two = RecombinatorMath.SuccessChance(new[] { A(1, 4), A(2, 4) });
        double three = RecombinatorMath.SuccessChance(new[] { A(1, 4), A(2, 4), A(3, 4) });
        double four = RecombinatorMath.SuccessChance(new[] { A(1, 4), A(2, 4), A(3, 4), A(4, 4) });
        Assert.True(one > two && two > three && three > four);
    }

    [Fact]
    public void HigherTiersAreHarderToKeep()
    {
        double t3 = RecombinatorMath.SuccessChance(new[] { A(1, 3) });
        double t5 = RecombinatorMath.SuccessChance(new[] { A(1, 5) });
        double t7 = RecombinatorMath.SuccessChance(new[] { A(1, 7) });
        Assert.True(t3 > t5 && t5 > t7);
    }

    [Fact]
    public void ChanceStaysBetweenZeroAndOne()
    {
        var worst = new[] { A(1, 7), A(2, 7), A(3, 7), A(4, 7), A(5, 7), A(6, 7) };
        double p = RecombinatorMath.SuccessChance(worst);
        Assert.InRange(p, 0.0, 1.0);
        Assert.True(p < 0.01);
        Assert.Equal(0, RecombinatorMath.SuccessChance(new List<RecombAffix>()));
    }

    [Fact]
    public void SixAffixesAreStillWithinReachWhenTheyAreLowTier()
    {
        var six = Enumerable.Range(1, 6).Select(i => A(i, 3)).ToList();
        double p = RecombinatorMath.SuccessChance(six);
        Assert.InRange(p, 0.2, 0.5);
        Assert.True(p < RecombinatorMath.SuccessChance(six.Take(4).ToList()));
    }

    [Fact]
    public void KeepingMoreAffixesIsAlwaysHarderUpToSix()
    {
        double previous = 2;
        for (int n = 1; n <= 6; n++)
        {
            double p = RecombinatorMath.SuccessChance(Enumerable.Range(1, n).Select(i => A(i, 4)).ToList());
            Assert.True(p < previous);
            previous = p;
        }
    }

    [Fact]
    public void ResultCountIsTheAverageOfTheTwoItems()
    {
        var rng = Seeded(1);
        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(4, RecombinatorMath.ResultCount(4, 4, 12, rng));
            Assert.Equal(5, RecombinatorMath.ResultCount(4, 6, 12, rng));
            Assert.Equal(6, RecombinatorMath.ResultCount(6, 6, 12, rng));
            Assert.InRange(RecombinatorMath.ResultCount(3, 4, 12, rng), 3, 4);
        }
    }

    [Fact]
    public void AVariedResultUsuallyKeepsTheAverageAndSometimesDiffersByOne()
    {
        var rng = Seeded(11);
        var counts = new Dictionary<int, int>();
        for (int i = 0; i < 6000; i++)
        {
            int c = RecombinatorMath.ResultCount(4, 6, 12, rng, varied: true);
            counts[c] = counts.GetValueOrDefault(c) + 1;
        }
        Assert.InRange(counts[5], 3900, 4500); // about 70%
        Assert.InRange(counts[4], 700, 1100); // about 15%
        Assert.InRange(counts[6], 700, 1100);
        Assert.True(counts.Keys.All(k => k >= 1 && k <= RecombinatorMath.MaxAffixes));
    }

    [Fact]
    public void ResultCountNeverExceedsThePool()
    {
        var rng = Seeded(2);
        for (int i = 0; i < 500; i++)
        {
            Assert.InRange(RecombinatorMath.ResultCount(4, 4, 2, rng), 1, 2);
        }
    }

    [Fact]
    public void DrawGivesDifferentAffixesAndRespectsCount()
    {
        var pool = new[] { A(1, 3), A(2, 3), A(2, 4, 1, 1), A(3, 3), A(4, 3) };
        var drawn = RecombinatorMath.Draw(pool, 3, Seeded(3));
        Assert.Equal(3, drawn.Count);
        Assert.Equal(3, drawn.Select(a => a.AffixId).Distinct().Count());
        Assert.Equal(4, RecombinatorMath.Draw(pool, 10, Seeded(4)).Count); // only four different affixes exist
    }

    [Fact]
    public void DrawNeverGivesMoreThanThreeOfOneSide()
    {
        var pool = Enumerable.Range(1, 8).Select(i => P(i, 3, 1)).Concat(Enumerable.Range(20, 8).Select(i => P(i, 3, 2))).ToList();
        var rng = Seeded(12);
        for (int i = 0; i < 300; i++)
        {
            var drawn = RecombinatorMath.Draw(pool, 6, rng);
            Assert.Equal(6, drawn.Count);
            Assert.Equal(3, drawn.Count(a => a.Placement == 1));
            Assert.Equal(3, drawn.Count(a => a.Placement == 2));
        }
        // with two prefixes already placed, only one more prefix may come
        var more = RecombinatorMath.Draw(pool, 6, rng, prefixes: 2, suffixes: 0);
        Assert.True(more.Count(a => a.Placement == 1) <= 1);
        Assert.True(more.Count(a => a.Placement == 2) <= 3);
    }

    [Fact]
    public void CommonAffixesAreDrawnMoreOftenThanRareOnes()
    {
        var pool = new[] { A(1, 3, 100), A(2, 3, 1) };
        var rng = Seeded(5);
        int commonFirst = 0;
        for (int i = 0; i < 2000; i++)
        {
            if (RecombinatorMath.Draw(pool, 1, rng)[0].AffixId == 1)
            {
                commonFirst++;
            }
        }
        Assert.True(commonFirst > 1900);
    }

    [Fact]
    public void BaseIsPickedWithEqualChance()
    {
        var rng = Seeded(6);
        int a = 0;
        for (int i = 0; i < 4000; i++)
        {
            a += RecombinatorMath.PickBase(rng) == 0 ? 1 : 0;
        }
        Assert.InRange(a, 1800, 2200);
    }

    [Fact]
    public void AnAffixThatFitsOnlyOneBaseHalvesTheChance()
    {
        var both = new[] { A(1, 3) };
        var onlyA = new[] { new RecombAffix { AffixId = 1, DisplayedTier = 3, Weight = 1, LegalOnB = false } };
        Assert.Equal(RecombinatorMath.SuccessChance(both) / 2, RecombinatorMath.SuccessChance(onlyA), 6);
        var none = new[] { new RecombAffix { AffixId = 1, DisplayedTier = 3, Weight = 1, LegalOnA = false, LegalOnB = false } };
        Assert.Equal(0, RecombinatorMath.SuccessChance(none));
    }

    [Fact]
    public void SealsAreNotCountedAmongTheOrdinaryAffixes()
    {
        Assert.Equal(6, RecombinatorMath.Capacity(true));
        Assert.Equal(6, RecombinatorMath.Capacity(false));
    }

    [Fact]
    public void ResultCountRespectsTheBaseCapacity()
    {
        var rng = Seeded(9);
        for (int i = 0; i < 500; i++)
        {
            Assert.InRange(RecombinatorMath.ResultCount(4, 4, 8, rng, capacity: 3), 1, 3);
        }
    }

    [Fact]
    public void CostGrowsWithAffixCountAndTier()
    {
        var small = new[] { A(1, 2), A(2, 2) };
        var more = new[] { A(1, 2), A(2, 2), A(3, 2), A(4, 2) };
        var higher = new[] { A(1, 5), A(2, 5) };
        Assert.True(RecombinatorMath.Gold(more, false) > RecombinatorMath.Gold(small, false));
        Assert.True(RecombinatorMath.Gold(higher, false) > RecombinatorMath.Gold(small, false));
        Assert.True(RecombinatorMath.Embers(more, false) >= RecombinatorMath.Embers(small, false));
        Assert.True(RecombinatorMath.Embers(higher, false) > RecombinatorMath.Embers(small, false));
    }

    [Fact]
    public void ExaltedItemsCostMore()
    {
        var items = new[] { A(1, 5), A(2, 5) };
        Assert.True(RecombinatorMath.Gold(items, true) > RecombinatorMath.Gold(items, false));
        Assert.True(RecombinatorMath.Embers(items, true) >= RecombinatorMath.Embers(items, false));
    }

    [Fact]
    public void PrimordialRaisesTheChanceByOneAndAHalfButNeverPastOne()
    {
        var picks = new[] { A(1, 5), A(2, 5) };
        double plain = RecombinatorMath.SuccessChance(picks);
        double boosted = RecombinatorMath.SuccessChance(picks, 4, 4, primordial: true);
        Assert.Equal(plain * 1.5, boosted, 6);
        Assert.True(RecombinatorMath.SuccessChance(new[] { A(1, 1) }, 4, 4, true) <= 1.0);
        Assert.True(RecombinatorMath.SurvivalChance(new[] { A(1, 1) }, true) <= 1.0);
    }

    [Fact]
    public void PrimordialBonesStartAt2500AndScaleLikeGold()
    {
        var small = new[] { A(1, 2), A(2, 2) };
        var big = new[] { A(1, 6), A(2, 6), A(3, 6), A(4, 6) };
        Assert.True(RecombinatorMath.Bones(small, false) >= 2500);
        Assert.True(RecombinatorMath.Bones(big, false) > RecombinatorMath.Bones(small, false));
        Assert.True(RecombinatorMath.Bones(big, true) > RecombinatorMath.Bones(big, false));
        Assert.Equal(2500, RecombinatorMath.Bones(new List<RecombAffix>(), false));
    }

    [Fact]
    public void CostIsNeverZeroAndTheFlatFeeIgnoresTheItems()
    {
        Assert.True(RecombinatorMath.Embers(new List<RecombAffix>(), false) >= 1);
        Assert.True(RecombinatorMath.FlatGold > 0 && RecombinatorMath.FlatEmbers > 0);
    }
}
