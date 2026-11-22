using LastEpoch_Hud.Scripts.Core.Jewels;

namespace LastEpoch_Hud.Tests.Core.Jewels;

[Collection("JewelAffixRulesState")]
public sealed class JewelAffixRulesTests
{
    [Theory]
    [InlineData(1, 2.5f)]
    [InlineData(2, 3.125f)]
    [InlineData(3, 3.75f)]
    [InlineData(4, 4.375f)]
    [InlineData(5, 5f)]
    [InlineData(6, 6.5f)]
    [InlineData(7, 8f)]
    public void InheritedMultipliersFollowConfirmedProgression(int tier, float expected) =>
        Assert.Equal(expected, JewelAffixRules.IdolMultiplier(tier));

    [Fact]
    public void EveryExtraDamageAndTakenAsAffixRequiresExaltedTiers()
    {
        foreach (int id in Enumerable.Range(JewelAffixRules.ExtraDamageIdStart, 7)
                     .Concat(Enumerable.Range(JewelAffixRules.TakenAsIdStart, 7)))
        {
            Assert.Equal(6, JewelAffixRules.MinimumTier(id));
            for (int tier = 1; tier <= 7; tier++)
                Assert.Equal(tier >= 6, JewelAffixRules.IsTierAllowed(id, tier));
        }
        Assert.Equal(1, JewelAffixRules.MinimumTier(JewelAffixRules.ManaLeechIdStart));
        Assert.False(JewelAffixRules.IsTierAllowed(JewelAffixRules.ManaLeechIdStart, 0));
        Assert.False(JewelAffixRules.IsTierAllowed(JewelAffixRules.ManaLeechIdStart, 8));
    }

    [Fact]
    public void ExclusiveRangesInterpolateOnlyTheFirstFiveTiers()
    {
        for (int tier = 1; tier <= 5; tier++)
            Assert.Equal(((float)tier, (float)tier), JewelAffixRules.ExclusiveRange(tier, 1, 5, 6, 8, 10, 12));
        Assert.Equal((6f, 8f), JewelAffixRules.ExclusiveRange(6, 1, 5, 6, 8, 10, 12));
        Assert.Equal((10f, 12f), JewelAffixRules.ExclusiveRange(7, 1, 5, 6, 8, 10, 12));
        Assert.Equal(.02f, JewelAffixRules.Roll(.02f, .024f, 0));
        Assert.Equal(.024f, JewelAffixRules.Roll(.02f, .024f, 255));
    }

    [Fact]
    public void SteppedRollsLandOnWholeStepsAndEveryStepIsEquallyLikely()
    {
        // 10-12% in 1% steps: 10, 11 or 12, each from a third of the rolls.
        var counts = new Dictionary<float, int>();
        for (int roll = 0; roll <= 255; roll++)
        {
            float value = JewelAffixRules.RollStepped(.10f, .12f, (byte)roll, .01f);
            counts[value] = counts.GetValueOrDefault(value) + 1;
        }
        Assert.Equal(new[] { .10f, .11f, .12f }, counts.Keys.OrderBy(x => x).Select(x => (float)Math.Round(x, 2)).ToArray());
        Assert.All(counts.Values, count => Assert.InRange(count, 85, 86));
        Assert.Equal(.10f, JewelAffixRules.RollStepped(.10f, .12f, 0, .01f));
        Assert.Equal(.12f, JewelAffixRules.RollStepped(.10f, .12f, 255, .01f), 5);
        // No step, or a range that is a single value: unchanged behaviour.
        Assert.Equal(JewelAffixRules.Roll(.02f, .024f, 100), JewelAffixRules.RollStepped(.02f, .024f, 100, 0f));
        Assert.Equal(.05f, JewelAffixRules.RollStepped(.05f, .05f, 200, .01f));
    }

    [Fact]
    public void StableIdsDoNotDependOnRegistrationOrder()
    {
        Assert.Equal(2123, JewelAffixRules.InheritedId(123));
        Assert.Equal(2001, JewelAffixRules.InheritedId(1));
        Assert.Equal(2123, JewelAffixRules.InheritedId(123));
        Assert.Throws<ArgumentOutOfRangeException>(() => JewelAffixRules.InheritedId(2000));
        Assert.True(JewelAffixRules.TakenAsIdStart + 6 <= JewelAffixRules.MaximumPackedAffixId);
    }

    [Fact]
    public void DifferentExtraDamageTypesCannotFeedEachOther()
    {
        float[] damage = { 100, 20, 0, 0, 0, 0, 0 };
        float[] fractions = { 0, .10f, .20f, 0, 0, 0, 0 };
        JewelAffixRules.ApplyExtraDamage(damage, fractions);
        Assert.Equal(100, damage[0]);
        Assert.Equal(32, damage[1]);
        Assert.Equal(24, damage[2]);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(3f)]
    [InlineData(-.5f)]
    public void LeechRateChangesDurationAndPreservesTotal(float increasedRate)
    {
        float remaining = 120, baseSeconds = 3;
        double recovered = 0;
        float duration = JewelAffixRules.LeechDuration(increasedRate);
        for (int tick = 0; tick < 100; tick++)
            recovered += JewelAffixRules.RecoverLeech(ref remaining, ref baseSeconds, duration / 100, increasedRate);
        Assert.InRange(Math.Abs(recovered - 120), 0, .0001f);
        Assert.InRange(remaining, 0, .0001f);
    }

    [Fact]
    public void ActiveLeechRespondsToRateChangesWithoutCreatingMana()
    {
        float remaining = 120, baseSeconds = 3;
        Assert.Equal(40, JewelAffixRules.RecoverLeech(ref remaining, ref baseSeconds, 1, 0));
        Assert.Equal(80, JewelAffixRules.RecoverLeech(ref remaining, ref baseSeconds, 1, 1));
        Assert.Equal(0, JewelAffixRules.RecoverLeech(ref remaining, ref baseSeconds, 1, 1));
    }
}
