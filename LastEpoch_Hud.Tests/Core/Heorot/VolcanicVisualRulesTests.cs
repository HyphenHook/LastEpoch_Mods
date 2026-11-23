using LastEpoch_Hud.Scripts.Core.Volcanic;

namespace LastEpoch_Hud.Tests.Core.Heorot;

public sealed class VolcanicVisualRulesTests
{
    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(true, 2, true)]
    [InlineData(true, 4, true)]
    [InlineData(false, 0, false)]
    [InlineData(false, 1, true)]
    public void CommonAuraDoesNotRequireAnger(bool core, int buffs, bool expected) =>
        Assert.Equal(expected, VolcanicVisualRules.HasAura(core, buffs));

    [Theory]
    [InlineData(0)]
    [InlineData(.25)]
    [InlineData(.5)]
    [InlineData(.75)]
    [InlineData(1)]
    public void CrossfadeKeepsTotalOpacityInsteadOfFlashing(float blend)
    {
        var opacity = VolcanicVisualRules.RingOpacity(blend);
        Assert.Equal(.9f, opacity.Front + opacity.Back * (1 - opacity.Front), 5);
    }

    [Fact]
    public void AnimationBlendsBackToFirstFrameAcrossItsBoundary()
    {
        var frame = Enumerable.Range(0, 1000).Select(i => VolcanicVisualRules.RingAnimation(i / 100d))
            .First(f => f.Current == VolcanicVisualRules.RingFrameCount - 1);
        Assert.Equal(0, frame.Next);
        Assert.InRange(frame.Blend, 0f, 1f);
    }

    [Fact]
    public void LongerAnimationReachesAllSixtyFourFrames()
    {
        var frames = Enumerable.Range(0, 2400).Select(i => VolcanicVisualRules.RingAnimation(i / 240d).Current).Distinct();
        Assert.Equal(Enumerable.Range(0, 64), frames.OrderBy(frame => frame));
    }
}
