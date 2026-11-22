using LastEpoch_Hud.Scripts.Core.Heorot;

namespace LastEpoch_Hud.Tests.Core.Heorot;

[Collection("HeorotValuesState")]
public sealed class HeorotRulesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, .25f)]
    [InlineData(3, .75f)]
    [InlineData(5, 1.25f)]
    [InlineData(8, 2f)] // Count all sources; do not cap Power at the player's own limit.
    public void PowerProducesAnIncreasedStatValue(float stacks, float expected) =>
        Assert.Equal(expected, HeorotRules.PowerIncreasedCold(true, stacks));

    [Theory]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidStackCountsDoNotInjectDamage(float stacks) =>
        Assert.Equal(0, HeorotRules.PowerIncreasedCold(true, stacks));

    [Fact]
    public void RemovedPowerDoesNotChangeDamage() =>
        Assert.Equal(0, HeorotRules.PowerIncreasedCold(false, 5));

    [Fact]
    public void PowerJoinsGenericColdAndEnergyIncreasesBeforeMore()
    {
        const float baseAndAddedCold = 100 + 120;
        const float genericIncreased = 2, coldIncreased = 1, energyIncreased = .75f, more = 1.5f;
        float normal = baseAndAddedCold * (1 + genericIncreased + coldIncreased + energyIncreased) * more;
        float rebuilt = baseAndAddedCold * (1 + genericIncreased + coldIncreased + energyIncreased
            + HeorotRules.PowerIncreasedCold(true, 5)) * more;
        Assert.Equal(1980f, HeorotRules.RebuiltColdPreservingScale(normal, normal, rebuilt));
        Assert.NotEqual(normal * (1 + 5 * .25f), rebuilt);
    }

    [Theory]
    [InlineData(100, 200, 125, 250)] // Native post-build x2 survives the target rebuild.
    [InlineData(100, 50, 125, 62.5f)]
    [InlineData(100, 0, 125, 0)] // Explicit outgoing suppression remains suppressed.
    [InlineData(0, 0, 25, 25)] // Increased pool previously clamped to zero can recover.
    public void NativePostBuildScalingSurvives(float built, float outgoing, float rebuilt, float expected) =>
        Assert.Equal(expected, HeorotRules.RebuiltColdPreservingScale(built, outgoing, rebuilt));

    [Theory]
    [InlineData(3, false, 3)]
    [InlineData(3, true, 5)]
    [InlineData(7, true, 9)]
    [InlineData(0, true, 0)]
    [InlineData(-1, true, -1)]
    [InlineData(int.MaxValue, true, int.MaxValue)]
    public void OnlyTheApplyingSourceGetsTwoMoreStacks(int nativeLimit, bool defence, int expected) =>
        Assert.Equal(expected, HeorotRules.ChillLimit(nativeLimit, defence));

    [Fact]
    public void SwitchingSourcesDoesNotCarryDefenceToTheNextAttacker()
    {
        Assert.Equal(5, HeorotRules.ChillLimit(3, true));
        Assert.Equal(3, HeorotRules.ChillLimit(3, false));
        Assert.Equal(3, HeorotRules.ChillLimit(3, false));
    }

    [Fact]
    public void MixedSourceReplacementAndExpiryLeaveNoResidualNativeChillEffect()
    {
        // Native collected buffs apply min(after, effectCap) - min(before, effectCap).
        // All private instances use the same effect cap, while application caps differ.
        int effectCap = HeorotRules.ChillLimit(3, true), stacks = 0, effect = 0;
        void Change(int delta)
        {
            effect += Math.Min(stacks + delta, effectCap) - Math.Min(stacks, effectCap);
            stacks += delta;
        }
        Change(3); // Ordinary source.
        Change(2); // Defence overflow.
        Assert.Equal(5, effect);
        while (stacks >= HeorotRules.ChillLimit(3, false)) Change(-1);
        Change(1); // Ordinary source replaces at its native cap.
        Assert.Equal(3, stacks);
        Assert.Equal(stacks, effect);
        while (stacks > 0) Change(-1); // Native expiration/cleanse.
        Assert.Equal(0, effect);
    }
}
