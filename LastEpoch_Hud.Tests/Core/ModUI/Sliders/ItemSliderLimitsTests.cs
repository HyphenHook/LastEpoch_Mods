using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;

namespace LastEpoch_Hud.Tests.Core.ModUI.Sliders;

public sealed class ItemSliderLimitsTests
{
    private const int RangeCount = 23;

    [Fact]
    public void EveryRange_HasMinBelowMax()
    {
        SliderRange[] ranges = SliderLimitsReflection.AllRanges(typeof(ItemSliderLimits));

        Assert.Equal(RangeCount, ranges.Length);
        Assert.All(ranges, range => Assert.True(range.Min < range.Max));
    }

    [Fact]
    public void AffixCount_IsWholeNumbers() =>
        Assert.Equal(new SliderRange(0f, 12f, true), ItemSliderLimits.DropAffixCount);

    [Fact]
    public void IdolAffixCount_MatchesCodeFallback() =>
        Assert.Equal(new SliderRange(0f, 12f, true), ItemSliderLimits.DropIdolAffixCount);
}
